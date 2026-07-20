using CivDle.Core.Tests.Support;
using CivDle.Core.World;
using CivDle.Core.WorldGen;
using Xunit;

namespace CivDle.Core.Tests.World;

/// <summary>
/// Nekonečný procedurální terén: deterministický (seed + preset), funguje i pro
/// záporné souřadnice a shoduje se s materializovanou mapou z <see cref="MapGenerator"/>.
/// </summary>
public class ProceduralTerrainTests
{
    [Fact]
    public void SameSeedAndPreset_SameBiomeEverywhere()
    {
        var content = TestData.LoadRealContent();
        var preset = content.WorldGen.Presets[0];
        var a = new ProceduralTerrain(content.Biomes, preset, 12345);
        var b = new ProceduralTerrain(content.Biomes, preset, 12345);

        for (int y = -40; y < 40; y += 7)
        {
            for (int x = -40; x < 40; x += 5)
            {
                Assert.Equal(a.BiomeAt(x, y), b.BiomeAt(x, y));
            }
        }
    }

    [Fact]
    public void DifferentSeed_DiffersSomewhere()
    {
        var content = TestData.LoadRealContent();
        var preset = content.WorldGen.Presets[0];
        var a = new ProceduralTerrain(content.Biomes, preset, 1);
        var b = new ProceduralTerrain(content.Biomes, preset, 2);

        bool anyDiff = false;
        for (int x = 0; x < 200 && !anyDiff; x++)
        {
            anyDiff = a.BiomeAt(x, x) != b.BiomeAt(x, x);
        }

        Assert.True(anyDiff, "Různé seedy musí dát jinou mapu.");
    }

    [Fact]
    public void NegativeCoordinates_ReturnValidBiomes()
    {
        var content = TestData.LoadRealContent();
        var terrain = new ProceduralTerrain(content.Biomes, content.WorldGen.Presets[0], 42);

        foreach (var (x, y) in new[] { (-1, -1), (-1000, -1000), (-5, 12345), (int.MinValue / 2, 7) })
        {
            Assert.InRange(terrain.BiomeAt(x, y), (byte)0, (byte)(content.Biomes.Count - 1));
        }
    }

    [Fact]
    public void MatchesMaterializedMap()
    {
        var content = TestData.LoadRealContent();
        var preset = content.WorldGen.Presets.Single(p => p.Id == "continents");
        var terrain = new ProceduralTerrain(content.Biomes, preset, 999);
        var map = new MapGenerator().Generate(content, new WorldGenRequest(999, 40, 40, preset));

        for (int y = 0; y < map.Height; y++)
        {
            for (int x = 0; x < map.Width; x++)
            {
                Assert.Equal(map.BiomeAt(x, y), terrain.BiomeAt(x, y));
            }
        }
    }
}
