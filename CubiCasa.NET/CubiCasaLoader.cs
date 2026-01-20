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

        public IEnumerable<CubiCasaBuilding> LoadDataset(string datasetRootPath)
        {
             if (!Directory.Exists(datasetRootPath))
                throw new DirectoryNotFoundException($"Dataset directory not found: {datasetRootPath}");

             // The dataset is typically structured as:
             // root/
             //   high_quality/
             //     1234/
             //       F1/model.svg
             //   colorful/
             //     5678/
             //       ...
             // OR just flat list of building folders.
             //
             // Strategy: Find all folders that contain subfolders like "F1", "F2", OR find all folders that contain "model.svg" but group them by building.

             // A safer approach: Find all 'model.svg' files, get their building root (parent of parent usually, or the folder containing F* folders).
             // But `LoadBuilding` expects a "Building Folder".

             // Let's iterate top-level directories and then subdirectories.
             // If a directory contains 'model.svg' directly or in subfolders, it might be a building.
             // However, to avoid duplicates (loading F1 as a building and Building1 as a building), we need to be careful.

             // Assumption: A "Building" is the folder that contains "F1", "F2", etc.
             // OR if single floor, it might be the folder containing "model.svg".

             // We will iterate recursively. If we find a "model.svg", we trace up to find the "Floor" folder (if naming convention F*) or we assume the parent is the floor.
             // But we want to return unique Buildings.

             var uniqueBuildingPaths = new HashSet<string>();

             var svgFiles = Directory.EnumerateFiles(datasetRootPath, "model.svg", SearchOption.AllDirectories);

             foreach (var svgFile in svgFiles)
             {
                 var dir = new DirectoryInfo(Path.GetDirectoryName(svgFile));

                 // Heuristic: If folder name is "F[0-9]+", the building is the parent.
                 // If not, maybe the building is this folder itself (single floor).

                 DirectoryInfo buildingDir;
                 if (System.Text.RegularExpressions.Regex.IsMatch(dir.Name, @"^F\d+$", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                 {
                     buildingDir = dir.Parent;
                 }
                 else
                 {
                     // If the folder is not F1, F2, maybe it's just the building folder directly containing model.svg?
                     // Or maybe it's some other structure.
                     // For now, let's treat the parent of model.svg as the Floor, and its parent as the Building, UNLESS the parent doesn't look like a floor.

                     // Actually, LoadBuilding(path) expects the path to be the root of the building.
                     // It recursively searches for model.svg inside.

                     // If we pass "Building1" to LoadBuilding, it finds "Building1/F1/model.svg".
                     // So we want to identify "Building1".

                     // If we are at ".../Building1/F1/model.svg", dir is ".../Building1/F1". buildingDir is ".../Building1".
                     // If we are at ".../Building2/model.svg" (flat), dir is ".../Building2". buildingDir is ".../Building2"?
                     // No, LoadBuilding searches recursively. So if we pass ".../Building2", it finds "model.svg" inside.

                     // So:
                     // 1. Get directory of model.svg.
                     // 2. If directory name starts with 'F' and digit, go up one level.
                     // 3. Else, use that directory.

                     if (dir.Name.StartsWith("F", StringComparison.OrdinalIgnoreCase) && dir.Name.Length > 1 && char.IsDigit(dir.Name[1]))
                     {
                         buildingDir = dir.Parent;
                     }
                     else
                     {
                         buildingDir = dir;
                     }
                 }

                 if (buildingDir != null)
                 {
                     uniqueBuildingPaths.Add(buildingDir.FullName);
                 }
             }

             foreach (var path in uniqueBuildingPaths)
             {
                 yield return LoadBuilding(path);
             }
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
                    // For MoveTo, End contains the coordinates to move to.
                    // Start is the end of the previous segment (or 0,0).
                    currentPoint = new Coordinate(moveTo.End.X, moveTo.End.Y);
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
