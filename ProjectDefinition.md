Project Definition: CubiCasa.NET
Type: Open Source Library (NuGet)
Repository Name: CubiCasa.NET
Namespace: CubiCasa
Purpose: A pure, unopinionated .NET parser for the CubiCasa5k dataset (SVG geometry + JSON metadata).

1. Project Scope
This library is a Raw Data Loader. It faithfully represents the source data structures of CubiCasa5k in C#.

IN SCOPE: Parsing model.svg, parsing pixel_to_meter (if present), parsing COCO annotations.
OUT OF SCOPE: Coordinate normalization (centering), Unit conversion (pixels -> meters), Graph synthesis, MapGenerator integration.
2. Dependencies
.NET 9
Svg (NuGet): For parsing SVG paths and attributes.
System.Text.Json: For parsing metadata.
NetTopologySuite: For robust geometry representation (

Polygon
, Geometry).
3. Public API Contract
3.1 Data Models
namespace CubiCasa.Data
{
    public class CubiCasaBuilding
    {
        public string BuildingId { get; set; }
        public CubiCasaFloor[] Floors { get; set; }
    }
    public class CubiCasaFloor
    {
        public int FloorIndex { get; set; } // Derived from folder structure
        public string SvgPath { get; set; }
        
        // The raw SVG dimensions (pixels)
        public double WidthPixels { get; set; }
        public double HeightPixels { get; set; }
        public CubiCasaEntity[] Entities { get; set; }
    }
    public class CubiCasaEntity
    {
        public string OriginalId { get; set; } // SVG Element ID
        public CubiCasaEntityType Type { get; set; } // Enum: Wall, Window, Door, Stairs...
        
        // Geometry in RAW PIXEL COORDINATES (Y-Down, Top-Left Origin)
        public NetTopologySuite.Geometries.Geometry Geometry { get; set; }
        
        // Raw Dictionary of SVG attributes for custom data access
        public Dictionary<string, string> Attributes { get; set; }
    }
    public enum CubiCasaEntityType
    {
        Wall, Room, Icon, Door, Window, Stairs, Railing, Background, Undefined
    }
}
3.2 Service Interface
namespace CubiCasa
{
    public interface ICubiCasaLoader
    {
        // Parses a standard CubiCasa5k folder structure
        CubiCasaBuilding LoadBuilding(string folderPath);
        
        // Parses a single SVG file as a specific floor
        CubiCasaFloor LoadFloor(string svgFilePath);
    }
}
4. Implementation Guidelines
SVG Parsers: The loader must handle complex SVG Path commands (M, L, C, Z).
Y-Axis Handling: SVG uses a Top-Left origin (Y-Down). Do not flip this. Keep raw data as-is.
Resilience: The parser should log warnings on non-critical SVG errors.
Deliverable: A compiled CubiCasa.dll and Unit Tests ensuring accurate geometry extraction.
