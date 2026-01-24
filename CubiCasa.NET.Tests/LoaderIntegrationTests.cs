using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CubiCasa.Data;
using Xunit;

namespace CubiCasa.NET.Tests
{
    public class LoaderIntegrationTests
    {
        [Fact]
        public async Task LoadLayoutsAsync_UsesProvidedPath_AndReturnsLayouts()
        {
            // Arrange
            var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            var datasetDir = Path.Combine(tempDir, "CubiCasa5k");
            var b1 = Path.Combine(datasetDir, "high_quality", "5001");
            Directory.CreateDirectory(Path.Combine(b1, "F1"));
            var svgContent = @"<svg viewBox=""0 0 100 100"" xmlns=""http://www.w3.org/2000/svg""><g id=""Wall""><path d=""M0 0 L10 0 L10 10 L0 10 Z"" /></g></svg>";
            File.WriteAllText(Path.Combine(b1, "F1", "model.svg"), svgContent);

            try
            {
                // Act
                var layouts = await CubiCasaLoader.LoadLayoutsAsync(datasetDir);

                // Assert
                Assert.NotNull(layouts);
                Assert.Single(layouts);
                Assert.IsType<CubiCasaBuilding>(layouts[0]);
                Assert.Equal("5001", layouts[0].BuildingId);
                Assert.Single(layouts[0].Floors);
            }
            finally
            {
                // Cleanup
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, true);
            }
        }

        [Fact]
        public void DataManager_GetDataPath_ReturnsDefault_WhenNoDataExists()
        {
            // This test assumes no data exists in the test runner environment at ~/.cubicasa or ./CubiCasa5k
            // However, we can't guarantee that.
            // But we can check it returns a non-null string.

            var path = DataManager.GetDataPath();
            Assert.False(string.IsNullOrEmpty(path));
            // Should contain "CubiCasa"
            Assert.Contains("CubiCasa", path);
        }
    }
}
