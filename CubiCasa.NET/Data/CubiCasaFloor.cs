namespace CubiCasa.Data
{
    public class CubiCasaFloor
    {
        public int FloorIndex { get; set; } // Derived from folder structure
        public string SvgPath { get; set; }

        // The raw SVG dimensions (pixels)
        public double WidthPixels { get; set; }
        public double HeightPixels { get; set; }

        // Conversion factor (if available)
        public double? PixelsPerMeter { get; set; }

        public CubiCasaEntity[] Entities { get; set; }
    }
}
