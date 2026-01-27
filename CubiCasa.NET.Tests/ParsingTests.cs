using System;
using System.IO;
using System.Linq;
using CubiCasa.Data;
using NetTopologySuite.Geometries;
using Xunit;

namespace CubiCasa.NET.Tests
{
    public class ParsingTests
    {
        private CubiCasaLoader _loader = new CubiCasaLoader();

        private CubiCasaFloor CreateFloorWithSvg(string pathData)
        {
            var svgContent = $@"<svg viewBox=""0 0 100 100"" xmlns=""http://www.w3.org/2000/svg"">
  <g id=""Wall"">
    <path d=""{pathData}"" />
  </g>
</svg>";
            var tempFile = Path.GetTempFileName();
            File.WriteAllText(tempFile, svgContent);

            try
            {
                return _loader.LoadFloor(tempFile);
            }
            finally
            {
                if (File.Exists(tempFile)) File.Delete(tempFile);
            }
        }

        [Fact]
        public void Parse_StandardPath_Correct()
        {
            var floor = CreateFloorWithSvg("M 0 0 L 10 0 L 10 10 L 0 10 Z");
            var wall = floor.Entities.First();
            var coords = wall.Geometry.Coordinates;

            Assert.Equal(new Coordinate(0, 0), coords[0]);
            Assert.Equal(new Coordinate(10, 0), coords[1]);
            Assert.Equal(new Coordinate(10, 10), coords[2]);
            Assert.Equal(new Coordinate(0, 10), coords[3]);
            Assert.Equal(new Coordinate(0, 0), coords[4]); // Closed
        }

        [Fact]
        public void Parse_AttachedCommands_Correct()
        {
            var floor = CreateFloorWithSvg("M0,0L10,0L10,10L0,10Z");
            var wall = floor.Entities.First();
            var coords = wall.Geometry.Coordinates;

            Assert.Equal(new Coordinate(0, 0), coords[0]);
            Assert.Equal(new Coordinate(10, 0), coords[1]);
            Assert.Equal(new Coordinate(10, 10), coords[2]);
            Assert.Equal(new Coordinate(0, 10), coords[3]);
            Assert.Equal(new Coordinate(0, 0), coords[4]);
        }

        [Fact]
        public void Parse_ExtraWhitespace_Correct()
        {
            // Must have at least 3 points (4 coords with closure) to be valid
            var floor = CreateFloorWithSvg("  M   0   0   L   10   0   L 10 10 Z  ");
            var wall = floor.Entities.First();
            var coords = wall.Geometry.Coordinates;

            Assert.Equal(new Coordinate(0, 0), coords[0]);
            Assert.Equal(new Coordinate(10, 0), coords[1]);
            Assert.Equal(new Coordinate(10, 10), coords[2]);
            Assert.Equal(new Coordinate(0, 0), coords[3]); // Closed
        }

        [Fact]
        public void Parse_InvalidTokens_Skipped()
        {
            // "foo" should be skipped.
            // M 0 0 -> (0,0)
            // L 10 foo -> L sees 10, then fails to read Y (foo). Returns false. L skipped.
            // Loop continues at foo. foo skipped.
            // L 10 10 -> (10,10)
            // Z -> close
            // So we get (0,0), (10,10), (0,0)
            var floor = CreateFloorWithSvg("M 0 0 L 10 foo L 10 10 Z");

            // Wait, if < 4 coordinates, it returns null and Entity is not added.
            // (0,0), (10,10), (0,0) is 3 coordinates. But Polygon needs 4 (start==end).
            // Actually, Coordinates.Count < 4 check is in ParsePathData.
            // (0,0), (10,10), (0,0) has 3 coords. So it returns null.

            Assert.Empty(floor.Entities);
        }

        [Fact]
        public void Parse_InvalidTokens_Recovers()
        {
             // M 0 0 -> (0,0)
             // L 10 0 -> (10,0)
             // L 10 foo -> Skipped
             // L 10 10 -> (10,10)
             // L 0 10 -> (0,10)
             // Z
             // Result: (0,0), (10,0), (10,10), (0,10), (0,0) -> 5 coords. Valid.
             var floor = CreateFloorWithSvg("M 0 0 L 10 0 L 10 foo L 10 10 L 0 10 Z");

             Assert.Single(floor.Entities);
             var wall = floor.Entities.First();
             var coords = wall.Geometry.Coordinates;

             Assert.Equal(5, coords.Length);
             Assert.Equal(new Coordinate(10, 0), coords[1]);
             Assert.Equal(new Coordinate(10, 10), coords[2]);
        }

        [Fact]
        public void Parse_UnknownCommands_Skipped()
        {
            // C is unknown.
            // M 0 0 -> (0,0)
            // C 5 5 -> C skipped, 5 skipped, 5 skipped (because not M/L).
            // L 10 10 -> (10,10)
            // L 0 10 -> (0,10)
            // Z
             var floor = CreateFloorWithSvg("M 0 0 C 5 5 L 10 10 L 0 10 Z");

             Assert.Single(floor.Entities);
             var coords = floor.Entities.First().Geometry.Coordinates;

             // (0,0), (10,10), (0,10), (0,0)
             Assert.Equal(4, coords.Length);
             Assert.Equal(new Coordinate(0, 0), coords[0]);
             Assert.Equal(new Coordinate(10, 10), coords[1]);
        }

        [Fact]
        public void Parse_MixedSeparators_Correct()
        {
            var floor = CreateFloorWithSvg("M 0,0 L 10,0 L 10,10 Z");
            Assert.Single(floor.Entities);
            var coords = floor.Entities.First().Geometry.Coordinates;
            Assert.Equal(new Coordinate(10, 0), coords[1]);
        }

        [Fact]
        public void Parse_LowerCaseZ_Supported()
        {
             var floor = CreateFloorWithSvg("M 0 0 L 10 0 L 10 10 z");
             Assert.Single(floor.Entities);
             // Should be closed
             var coords = floor.Entities.First().Geometry.Coordinates;
             Assert.Equal(coords[0], coords.Last());
        }
    }
}
