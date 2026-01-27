using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using CubiCasa.Data;
using NetTopologySuite.Geometries;

namespace CubiCasa
{
    /// <summary>
    /// Native C# loader for CubiCasa5k dataset. Zero-config, no external scripts required.
    /// </summary>
    public class CubiCasaLoader : ICubiCasaLoader
    {
        public static async Task<List<CubiCasaBuilding>> LoadLayoutsAsync(string path = null, int? maxItems = null, Action<string> logger = null)
        {
            if (string.IsNullOrEmpty(path))
            {
                await DataManager.EnsureDataAsync(logger);
                path = DataManager.GetDataPath();
            }

            var loader = new CubiCasaLoader();
            var buildings = loader.LoadDataset(path);

            if (maxItems.HasValue)
            {
                buildings = buildings.Take(maxItems.Value);
            }

            return buildings.ToList();
        }

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

             var uniqueBuildingPaths = new HashSet<string>();

             var svgFiles = Directory.EnumerateFiles(datasetRootPath, "model.svg", SearchOption.AllDirectories);

             foreach (var svgFile in svgFiles)
             {
                 var dir = new DirectoryInfo(Path.GetDirectoryName(svgFile));

                 DirectoryInfo buildingDir;
                 if (dir.Name.StartsWith("F", StringComparison.OrdinalIgnoreCase) && dir.Name.Length > 1 && char.IsDigit(dir.Name[1]))
                 {
                     buildingDir = dir.Parent;
                 }
                 else
                 {
                     buildingDir = dir;
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
            var doc = XDocument.Load(svgFilePath);
            var root = doc.Root;

            // Default ViewBox parsing
            double width = 0, height = 0;
            if (root != null)
            {
                var viewBox = root.Attribute("viewBox")?.Value;
                if (viewBox != null)
                {
                    var parts = viewBox.Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length == 4)
                    {
                        if (double.TryParse(parts[2], out double w) && double.TryParse(parts[3], out double h))
                        {
                            width = w;
                            height = h;
                        }
                    }
                }
                else
                {
                     // Fallback to width/height attributes
                     double.TryParse(root.Attribute("width")?.Value, out width);
                     double.TryParse(root.Attribute("height")?.Value, out height);
                }
            }

            var floor = new CubiCasaFloor
            {
                SvgPath = svgFilePath,
                WidthPixels = width,
                HeightPixels = height,
                Entities = ParseEntities(doc).ToArray()
            };

            // Check for pixel_to_meter.txt
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

        private IEnumerable<CubiCasaEntity> ParseEntities(XDocument doc)
        {
            if (doc.Root == null) yield break;

            var ns = doc.Root.Name.Namespace;

            // Find all paths (handle with and without namespace if necessary, but typically strict)
            // If the root has a default namespace, all descendants inherit it.

            foreach (var element in doc.Descendants())
            {
                if (element.Name.LocalName != "path") continue;

                var parent = element.Parent;
                var type = CubiCasaEntityType.Undefined;
                var originalId = parent?.Attribute("id")?.Value ?? element.Attribute("id")?.Value;

                if (parent != null)
                {
                    type = ParseType(parent.Attribute("id")?.Value);
                }

                if (type == CubiCasaEntityType.Undefined)
                {
                    type = ParseType(element.Attribute("id")?.Value);
                }

                if (type != CubiCasaEntityType.Undefined)
                {
                    var d = element.Attribute("d")?.Value;
                    if (!string.IsNullOrEmpty(d))
                    {
                        var geometry = ParsePathData(d);
                        if (geometry != null)
                        {
                             var attributes = element.Attributes()
                                 .ToDictionary(a => a.Name.LocalName, a => a.Value);

                             yield return new CubiCasaEntity
                             {
                                 OriginalId = originalId,
                                 Type = type,
                                 Geometry = geometry,
                                 Attributes = attributes
                             };
                        }
                    }
                }
            }
        }

        private CubiCasaEntityType ParseType(string? id)
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

        private Geometry? ParsePathData(string d)
        {
            var coordinates = new List<Coordinate>();
            ReadOnlySpan<char> span = d.AsSpan();
            int i = 0;

            while (i < span.Length)
            {
                // Skip separators
                while (i < span.Length && (span[i] == ' ' || span[i] == ','))
                {
                    i++;
                }
                if (i >= span.Length) break;

                // Check for command
                char c = span[i];
                if (IsCommand(c))
                {
                    char cmd = c;
                    i++; // Consume command

                    if (cmd == 'M' || cmd == 'L')
                    {
                        if (TryReadCoordinate(span, ref i, out double x) && TryReadCoordinate(span, ref i, out double y))
                        {
                            coordinates.Add(new Coordinate(x, y));
                        }
                    }
                    else if (cmd == 'Z' || cmd == 'z')
                    {
                        if (coordinates.Count > 0)
                        {
                            coordinates.Add(coordinates[0]);
                        }
                    }
                }
                else
                {
                    // Unexpected token or part of previous command we didn't handle?
                    SkipToken(span, ref i);
                }
            }

            if (coordinates.Count < 4) return null;

            try
            {
                var factory = new GeometryFactory();
                return factory.CreatePolygon(coordinates.ToArray());
            }
            catch
            {
                return null;
            }
        }

        private static bool IsCommand(char c)
        {
            return c == 'M' || c == 'L' || c == 'Z' || c == 'z';
        }

        private static bool TryReadCoordinate(ReadOnlySpan<char> span, ref int i, out double val)
        {
            val = 0;
            // Skip whitespace
            while (i < span.Length && (span[i] == ' ' || span[i] == ',')) i++;
            if (i >= span.Length) return false;

            // Check if we hit a command
            if (IsCommand(span[i])) return false;

            // Find end of number token
            int start = i;
            while (i < span.Length && span[i] != ' ' && span[i] != ',' && !IsCommand(span[i]))
            {
                i++;
            }

            return double.TryParse(span.Slice(start, i - start), out val);
        }

        private static void SkipToken(ReadOnlySpan<char> span, ref int i)
        {
            while (i < span.Length && span[i] != ' ' && span[i] != ',' && !IsCommand(span[i]))
            {
                i++;
            }
        }
    }
}
