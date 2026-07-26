using CivDle.Core.Tests.Support;
using CivDle.Core.World;
using Xunit;
using Xunit.Abstractions;

namespace CivDle.Core.Tests.World;

/// <summary>
/// Řeky v procedurálním terénu: vznikají z „hřebene" šumu, takže jsou spojité,
/// ale pořád jen čistá funkce souřadnic (nekonečná mapa je neukládá). Musí být
/// vidět, ale nesmí zaplavit souš.
/// </summary>
public class RiverTests
{
    private readonly ITestOutputHelper _output;

    public RiverTests(ITestOutputHelper output) => _output = output;

    private static ProceduralTerrain RealTerrain(long seed = 42)
    {
        var content = TestData.LoadRealContent();
        var preset = content.WorldGen.Presets.Single(p => p.Id == "continents");
        return new ProceduralTerrain(content.Biomes, preset, seed);
    }

    [Fact]
    public void Rivers_ExistButDoNotFloodTheLand()
    {
        var terrain = RealTerrain();
        int land = 0, river = 0;
        for (int y = -150; y < 150; y++)
        {
            for (int x = -150; x < 150; x++)
            {
                if (terrain.ElevationAt(x, y) < terrain.Preset.SeaLevel)
                {
                    continue;
                }

                land++;
                if (terrain.IsRiver(x, y))
                {
                    river++;
                }
            }
        }

        double share = river / (double)land;
        _output.WriteLine($"souš {land}, řeka {river} ({share:P2})");

        Assert.True(river > 0, "na mapě musí být řeky vidět");
        Assert.True(share is > 0.002 and < 0.12, $"řeky mají krajinu protkat, ne zaplavit — je jich {share:P2}");
    }

    [Fact]
    public void River_IsPureFunctionOfCoordinates()
    {
        // Terén se po načtení savu rekonstruuje ze seedu — řeky musí vyjít stejně.
        var a = RealTerrain();
        var b = RealTerrain();
        for (int i = 0; i < 200; i++)
        {
            int x = i * 7 - 300, y = i * 13 - 200;
            Assert.Equal(a.IsRiver(x, y), b.IsRiver(x, y));
            Assert.Equal(a.BiomeAt(x, y), b.BiomeAt(x, y));
        }
    }

    [Fact]
    public void RiverTiles_AreWaterBiome()
    {
        // Řeka je voda uprostřed souše → nedá se na ní stavět (přirozená překážka).
        var content = TestData.LoadRealContent();
        var preset = content.WorldGen.Presets.Single(p => p.Id == "continents");
        var terrain = new ProceduralTerrain(content.Biomes, preset, 42);

        int checkedTiles = 0;
        for (int y = -120; y < 120 && checkedTiles < 25; y++)
        {
            for (int x = -120; x < 120 && checkedTiles < 25; x++)
            {
                if (terrain.ElevationAt(x, y) >= preset.SeaLevel && terrain.IsRiver(x, y))
                {
                    Assert.True(content.Biomes[terrain.BiomeAt(x, y)].IsWater,
                        $"dlaždice s řekou ({x},{y}) musí být vodní biom");
                    checkedTiles++;
                }
            }
        }

        Assert.True(checkedTiles > 0, "test nenašel žádnou říční dlaždici ke kontrole");
    }

    [Fact]
    public void Rivers_StayOutOfHighMountains()
    {
        var terrain = RealTerrain();
        var preset = terrain.Preset;
        for (int y = -200; y < 200; y++)
        {
            for (int x = -200; x < 200; x++)
            {
                if (!terrain.IsRiver(x, y))
                {
                    continue;
                }

                float land = (terrain.ElevationAt(x, y) - preset.SeaLevel) / (1f - preset.SeaLevel);
                Assert.True(land <= preset.RiverMaxElevation, $"řeka na ({x},{y}) je nad povoleným stropem výšky");
            }
        }
    }
}
