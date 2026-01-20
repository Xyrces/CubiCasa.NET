using System;
using System.IO;
using System.Linq;
using CubiCasa.Data;
using Xunit;

namespace CubiCasa.NET.Tests
{
    public class CubiCasaLoaderTests
    {
        [Fact]
        public void LoadFloor_ParsesSimpleSvg_Correctly()
        {
            // Arrange
            var svgContent = @"<svg viewBox=""0 0 800 600"" width=""800"" height=""600"" xmlns=""http://www.w3.org/2000/svg"">
  <g id=""Wall"">
    <path d=""M10 10 L100 10 L100 100 L10 100 Z"" />
  </g>
  <g id=""Room"">
    <path d=""M200 200 L300 200 L300 300 L200 300 Z"" />
  </g>
</svg>";
            var tempFile = Path.GetTempFileName();
            File.WriteAllText(tempFile, svgContent);

            var loader = new CubiCasaLoader();

            // Act
            var floor = loader.LoadFloor(tempFile);

            // Assert
            Assert.Equal(800, floor.WidthPixels);
            Assert.Equal(600, floor.HeightPixels);
            Assert.NotNull(floor.Entities);
            Assert.Equal(2, floor.Entities.Length);

            var wall = floor.Entities.FirstOrDefault(e => e.Type == CubiCasaEntityType.Wall);
            Assert.NotNull(wall);
            Assert.Equal("Wall", wall.OriginalId);
            // 90 * 90 = 8100
            Assert.Equal(8100, wall.Geometry.Area);
            Assert.Equal(new NetTopologySuite.Geometries.Coordinate(10, 10), wall.Geometry.Coordinates[0]);

            var room = floor.Entities.FirstOrDefault(e => e.Type == CubiCasaEntityType.Room);
            Assert.NotNull(room);
            Assert.Equal("Room", room.OriginalId);
            // 100 * 100 = 10000
            Assert.Equal(10000, room.Geometry.Area);

            // Cleanup
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }

        [Fact]
        public void LoadFloor_ParsesAllEntityTypes()
        {
            var svgContent = @"<svg viewBox=""0 0 100 100"" xmlns=""http://www.w3.org/2000/svg"">
  <g id=""Window""><path d=""M0 0 L10 0 L10 10 L0 10 Z"" /></g>
  <g id=""Door""><path d=""M0 0 L10 0 L10 10 L0 10 Z"" /></g>
  <g id=""Stairs""><path d=""M0 0 L10 0 L10 10 L0 10 Z"" /></g>
  <g id=""Railing""><path d=""M0 0 L10 0 L10 10 L0 10 Z"" /></g>
  <g id=""Icon""><path d=""M0 0 L10 0 L10 10 L0 10 Z"" /></g>
</svg>";
            var tempFile = Path.GetTempFileName();
            File.WriteAllText(tempFile, svgContent);

            var loader = new CubiCasaLoader();
            var floor = loader.LoadFloor(tempFile);

            Assert.Contains(floor.Entities, e => e.Type == CubiCasaEntityType.Window);
            Assert.Contains(floor.Entities, e => e.Type == CubiCasaEntityType.Door);
            Assert.Contains(floor.Entities, e => e.Type == CubiCasaEntityType.Stairs);
            Assert.Contains(floor.Entities, e => e.Type == CubiCasaEntityType.Railing);
            Assert.Contains(floor.Entities, e => e.Type == CubiCasaEntityType.Icon);

            if (File.Exists(tempFile)) File.Delete(tempFile);
        }

        [Fact]
        public void LoadBuilding_ParsesStructure_Correctly()
        {
            // Arrange
            var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            var buildingDir = Path.Combine(tempDir, "Building1");
            var f1Dir = Path.Combine(buildingDir, "F1");
            var f2Dir = Path.Combine(buildingDir, "F2");

            Directory.CreateDirectory(f1Dir);
            Directory.CreateDirectory(f2Dir);

            var svgContent = @"<svg viewBox=""0 0 100 100"" xmlns=""http://www.w3.org/2000/svg""><g id=""Wall""><path d=""M0 0 L10 0 L10 10 L0 10 Z"" /></g></svg>";
            File.WriteAllText(Path.Combine(f1Dir, "model.svg"), svgContent);
            File.WriteAllText(Path.Combine(f2Dir, "model.svg"), svgContent);

            File.WriteAllText(Path.Combine(f1Dir, "pixel_to_meter.txt"), "0.02");

            var loader = new CubiCasaLoader();

            // Act
            var building = loader.LoadBuilding(buildingDir);

            // Assert
            Assert.Equal("Building1", building.BuildingId);
            Assert.Equal(2, building.Floors.Length);

            var f1 = building.Floors.First(f => f.FloorIndex == 1);
            Assert.Equal(0.02, f1.PixelsPerMeter);

            var f2 = building.Floors.First(f => f.FloorIndex == 2);
            Assert.Null(f2.PixelsPerMeter);

            // Cleanup
            Directory.Delete(tempDir, true);
        }

        [Fact]
        public void LoadDataset_DiscoversBuildings_Correctly()
        {
            // Arrange
            var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            var datasetDir = Path.Combine(tempDir, "CubiCasa5k");

            // Structure:
            // CubiCasa5k/high_quality/1001/F1/model.svg
            // CubiCasa5k/high_quality/1002/model.svg (flat structure case)
            // CubiCasa5k/colorful/2001/F1/model.svg
            // CubiCasa5k/colorful/2001/F2/model.svg

            var b1 = Path.Combine(datasetDir, "high_quality", "1001");
            Directory.CreateDirectory(Path.Combine(b1, "F1"));

            var b2 = Path.Combine(datasetDir, "high_quality", "1002");
            Directory.CreateDirectory(b2);

            var b3 = Path.Combine(datasetDir, "colorful", "2001");
            Directory.CreateDirectory(Path.Combine(b3, "F1"));
            Directory.CreateDirectory(Path.Combine(b3, "F2"));

            var svgContent = @"<svg viewBox=""0 0 100 100"" xmlns=""http://www.w3.org/2000/svg""><g id=""Wall""></g></svg>";

            File.WriteAllText(Path.Combine(b1, "F1", "model.svg"), svgContent);
            File.WriteAllText(Path.Combine(b2, "model.svg"), svgContent);
            File.WriteAllText(Path.Combine(b3, "F1", "model.svg"), svgContent);
            File.WriteAllText(Path.Combine(b3, "F2", "model.svg"), svgContent);

            var loader = new CubiCasaLoader();

            // Act
            var buildings = loader.LoadDataset(datasetDir).ToList();

            // Assert
            Assert.Equal(3, buildings.Count);
            Assert.Contains(buildings, b => b.BuildingId == "1001");
            Assert.Contains(buildings, b => b.BuildingId == "1002");
            Assert.Contains(buildings, b => b.BuildingId == "2001");

            var building2001 = buildings.First(b => b.BuildingId == "2001");
            Assert.Equal(2, building2001.Floors.Length);

            // Cleanup
            Directory.Delete(tempDir, true);
        }
    }
}
