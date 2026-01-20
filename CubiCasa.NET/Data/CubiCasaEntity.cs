using System.Collections.Generic;

namespace CubiCasa.Data
{
    public class CubiCasaEntity
    {
        public string OriginalId { get; set; } // SVG Element ID
        public CubiCasaEntityType Type { get; set; } // Enum: Wall, Window, Door, Stairs...

        // Geometry in RAW PIXEL COORDINATES (Y-Down, Top-Left Origin)
        public NetTopologySuite.Geometries.Geometry Geometry { get; set; }

        // Raw Dictionary of SVG attributes for custom data access
        public Dictionary<string, string> Attributes { get; set; }
    }
}
