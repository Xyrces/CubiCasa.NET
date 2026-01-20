using CubiCasa.Data;

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
