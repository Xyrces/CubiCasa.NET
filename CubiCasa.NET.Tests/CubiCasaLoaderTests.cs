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
            Assert.True(wall.Geometry.Area > 0);

            var room = floor.Entities.FirstOrDefault(e => e.Type == CubiCasaEntityType.Room);
            Assert.NotNull(room);
            Assert.Equal("Room", room.OriginalId);
            Assert.True(room.Geometry.Area > 0);

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
    }
}
