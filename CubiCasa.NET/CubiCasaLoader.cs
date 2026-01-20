using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CubiCasa.Data;
using NetTopologySuite.Geometries;
using Svg;
using Svg.Pathing;

namespace CubiCasa
{
    public class CubiCasaLoader : ICubiCasaLoader
    {
        public CubiCasaBuilding LoadBuilding(string folderPath)
        {
            if (!Directory.Exists(folderPath))
                throw new DirectoryNotFoundException($"Directory not found: {folderPath}");

            var buildingId = new DirectoryInfo(folderPath).Name;
            var floors = new List<CubiCasaFloor>();

            // Recursively search for model.svg files
            var svgFiles = Directory.GetFiles(folderPath, "model.svg", SearchOption.AllDirectories);

            foreach (var svgFile in svgFiles)
            {
                var floor = LoadFloor(svgFile);

                // Infer floor index from folder name if possible (e.g. F1, F2)
                // This is a heuristic based on observed dataset structure
                var parentDir = Directory.GetParent(svgFile)?.Name;
                if (parentDir != null && parentDir.StartsWith("F", StringComparison.OrdinalIgnoreCase) && int.TryParse(parentDir.Substring(1), out int index))
                {
                    floor.FloorIndex = index;
                }

                floors.Add(floor);
            }

            return new CubiCasaBuilding
            {
                BuildingId = buildingId,
                Floors = floors.OrderBy(f => f.FloorIndex).ToArray()
            };
        }

        public CubiCasaFloor LoadFloor(string svgFilePath)
        {
            var svgDocument = SvgDocument.Open(svgFilePath);
            var floor = new CubiCasaFloor
            {
                SvgPath = svgFilePath,
                WidthPixels = svgDocument.ViewBox.Width,
                HeightPixels = svgDocument.ViewBox.Height,
                Entities = ParseEntities(svgDocument).ToArray()
            };

            // Check for pixel_to_meter.txt in the same directory
            var dir = Path.GetDirectoryName(svgFilePath);
            if (dir != null)
            {
                var pixelToMeterPath = Path.Combine(dir, "pixel_to_meter.txt");
                if (File.Exists(pixelToMeterPath))
                {
                    var content = File.ReadAllText(pixelToMeterPath).Trim();
                    if (double.TryParse(content, out double ppm))
                    {
                        floor.PixelsPerMeter = ppm;
                    }
                }
            }

            return floor;
        }

        private IEnumerable<CubiCasaEntity> ParseEntities(SvgDocument doc)
        {
            // Iterate through all groups and paths
            // We assume grouping by ID or class determines the type
            // This is a simplified implementation based on the test case

            foreach (var element in doc.Descendants())
            {
                if (element is SvgPath path)
                {
                    // Find parent group to determine type
                    var parent = path.Parent;
                    var type = CubiCasaEntityType.Undefined;
                    var originalId = parent?.ID ?? path.ID;

                    if (parent != null)
                    {
                        type = ParseType(parent.ID);
                    }

                    if (type == CubiCasaEntityType.Undefined)
                    {
                        type = ParseType(path.ID);
                    }

                    if (type != CubiCasaEntityType.Undefined)
                    {
                         yield return new CubiCasaEntity
                         {
                             OriginalId = originalId,
                             Type = type,
                             Geometry = ConvertPathToGeometry(path),
                             Attributes = element.CustomAttributes
                         };
                    }
                }
            }
        }

        private CubiCasaEntityType ParseType(string id)
        {
            if (string.IsNullOrEmpty(id)) return CubiCasaEntityType.Undefined;

            if (id.Contains("Wall", StringComparison.OrdinalIgnoreCase)) return CubiCasaEntityType.Wall;
            if (id.Contains("Room", StringComparison.OrdinalIgnoreCase)) return CubiCasaEntityType.Room;
            if (id.Contains("Window", StringComparison.OrdinalIgnoreCase)) return CubiCasaEntityType.Window;
            if (id.Contains("Door", StringComparison.OrdinalIgnoreCase)) return CubiCasaEntityType.Door;
            if (id.Contains("Stairs", StringComparison.OrdinalIgnoreCase)) return CubiCasaEntityType.Stairs;
            if (id.Contains("Railing", StringComparison.OrdinalIgnoreCase)) return CubiCasaEntityType.Railing;
            if (id.Contains("Icon", StringComparison.OrdinalIgnoreCase)) return CubiCasaEntityType.Icon;

            return CubiCasaEntityType.Undefined;
        }

        private Geometry ConvertPathToGeometry(SvgPath path)
        {
            // Simplified conversion. Real implementation needs to handle all SVG commands.
            // SvgPath has a PathData property which is a list of SvgPathSegments.

            var coordinates = new List<Coordinate>();
            var startPoint = new Coordinate(0, 0);
            var currentPoint = new Coordinate(0, 0);

            foreach (var segment in path.PathData)
            {
                // This is a naive implementation and only supports MoveTo and LineTo for the test.
                // A robust implementation would need to approximate curves.

                if (segment is SvgMoveToSegment moveTo)
                {
                    currentPoint = new Coordinate(moveTo.Start.X, moveTo.Start.Y);
                    if (coordinates.Count == 0) coordinates.Add(currentPoint); // Initial move
                }
                else if (segment is SvgLineSegment line)
                {
                    currentPoint = new Coordinate(line.End.X, line.End.Y);
                    coordinates.Add(currentPoint);
                }
                else if (segment is SvgClosePathSegment)
                {
                    coordinates.Add(coordinates[0]); // Close the loop
                }
                // TODO: Handle curves (Cubic, Quadratic, Arc) by flattening/linearizing them.
            }

            if (coordinates.Count < 4) return null; // Not a valid polygon

            var factory = new GeometryFactory();
            return factory.CreatePolygon(coordinates.ToArray());
        }
    }
}
