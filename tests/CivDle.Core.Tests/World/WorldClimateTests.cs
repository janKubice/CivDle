using CivDle.Core.Content;
using CivDle.Core.Tests.Support;
using CivDle.Core.World;
using Xunit;

namespace CivDle.Core.Tests.World;

/// <summary>
/// Klima světů galaxie (svety-design.md 7.4): preset světa umí posunout
/// teplotu a vlhkost celého světa a položit přes terén záplaty zvláštních
/// biomů (solné pláně, zvodně, horké prameny). Domovina bez těch polí musí
/// zůstat dlaždici po dlaždici stejná.
/// </summary>
public sealed class WorldClimateTests
{
    private const int Span = 240;

    [Fact]
    public void AHotDryWorld_IsHotterAndDrierEverywhere()
    {
        var content = TestData.LoadRealContent();
        var home = content.WorldGen.Presets[0];
        var desert = home with { TemperatureShift = 0.3f, MoistureShift = -0.3f };
        var a = new ProceduralTerrain(content.Biomes, home, 99);
        var b = new ProceduralTerrain(content.Biomes, desert, 99);

        double hotter = 0;
        int samples = 0;
        for (int y = -Span; y < Span; y += 6)
        {
            for (int x = -Span; x < Span; x += 6)
            {
                Assert.True(b.TemperatureAt(x, y) >= a.TemperatureAt(x, y));
                Assert.True(b.MoistureAt(x, y) <= a.MoistureAt(x, y));
                Assert.InRange(b.TemperatureAt(x, y), 0f, 1f);
                Assert.InRange(b.MoistureAt(x, y), 0f, 1f);
                hotter += b.TemperatureAt(x, y) - a.TemperatureAt(x, y);
                samples++;
            }
        }

        Assert.True(hotter / samples > 0.15, "posun o 0,3 se má na průměru projevit");
    }

    [Fact]
    public void APatch_LiesOnlyOnItsBiomesAndReallyAppears()
    {
        var content = TestData.LoadRealContent();
        var home = content.WorldGen.Presets[0];
        int patchBiome = LandBiome(content, home, out int under);
        var on = new bool[content.Biomes.Count];
        on[under] = true;
        var patched = home with
        {
            PatchesOrNull = new[] { new BiomePatch(patchBiome, on, new NoiseSpec(0.05f, 3, 0.5f, 2f), 0.55f) },
        };
        var a = new ProceduralTerrain(content.Biomes, home, 7);
        var b = new ProceduralTerrain(content.Biomes, patched, 7);

        int changed = 0;
        for (int y = -Span; y < Span; y += 3)
        {
            for (int x = -Span; x < Span; x += 3)
            {
                byte before = a.BiomeAt(x, y);
                byte after = b.BiomeAt(x, y);
                if (before != after)
                {
                    Assert.Equal(under, before);
                    Assert.Equal(patchBiome, after);
                    changed++;
                }
            }
        }

        Assert.True(changed > 20, $"záplata se má na mapě opravdu objevit (změněno {changed} dlaždic)");
    }

    [Fact]
    public void Patches_DependOnlyOnTheSeed()
    {
        var content = TestData.LoadRealContent();
        var home = content.WorldGen.Presets[0];
        int patchBiome = LandBiome(content, home, out int under);
        var on = new bool[content.Biomes.Count];
        on[under] = true;
        var patched = home with
        {
            PatchesOrNull = new[] { new BiomePatch(patchBiome, on, new NoiseSpec(0.05f, 3, 0.5f, 2f), 0.5f) },
        };
        var a = new ProceduralTerrain(content.Biomes, patched, 31);
        var b = new ProceduralTerrain(content.Biomes, patched, 31);

        for (int y = -60; y < 60; y += 4)
        {
            for (int x = -60; x < 60; x += 4)
            {
                Assert.Equal(a.BiomeAt(x, y), b.BiomeAt(x, y));
            }
        }
    }

    /// <summary>
    /// Nejčastější biom souše kolem středu a jiný biom souše pro záplatu —
    /// test tak nezávisí na tom, jak se biomy v datech zrovna jmenují.
    /// </summary>
    private static int LandBiome(GameContent content, TerrainPreset preset, out int under)
    {
        var terrain = new ProceduralTerrain(content.Biomes, preset, 7);
        var counts = new int[content.Biomes.Count];
        for (int y = -Span; y < Span; y += 6)
        {
            for (int x = -Span; x < Span; x += 6)
            {
                counts[terrain.BiomeAt(x, y)]++;
            }
        }

        under = -1;
        for (int i = 0; i < counts.Length; i++)
        {
            if (!content.Biomes[i].IsWater && (under < 0 || counts[i] > counts[under]))
            {
                under = i;
            }
        }

        for (int i = 0; i < content.Biomes.Count; i++)
        {
            if (i != under && !content.Biomes[i].IsWater)
            {
                return i;
            }
        }

        throw new InvalidOperationException("data nemají dva biomy souše");
    }
}
