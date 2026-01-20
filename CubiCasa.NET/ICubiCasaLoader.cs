using CubiCasa.Data;

namespace CubiCasa
{
    public interface ICubiCasaLoader
    {
        // Parses a standard CubiCasa5k folder structure
        CubiCasaBuilding LoadBuilding(string folderPath);

        // Discovers and parses all buildings in a root dataset directory
        IEnumerable<CubiCasaBuilding> LoadDataset(string datasetRootPath);

        // Parses a single SVG file as a specific floor
        CubiCasaFloor LoadFloor(string svgFilePath);
    }
}
