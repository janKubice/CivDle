using CivDle.Core.Content;
using CivDle.Core.World;
using Xunit;

namespace CivDle.Core.Tests.World;

/// <summary>
/// Klimatická vrstva: teplota podle zeměpisné šířky + šum + ochlazení výškou.
/// Testuje se to, co hráč pozná okem — mapa má pásma a je pestrá, ne pořád tráva.
/// </summary>
public sealed class ClimateTests
{
    /// <summary>Kolik dlaždic se vzorkuje při měření pestrosti (čtverec strany N).</summary>
    private const int SampleSpan = 400;

    [Fact]
    public void RealWorld_ContainsManyDifferentBiomes()
    {
        var (content, terrain) = RealTerrain();

        var seen = new HashSet<byte>();
        for (int y = -SampleSpan; y < SampleSpan; y += 4)
        {
            for (int x = -SampleSpan; x < SampleSpan; x += 4)
            {
                seen.Add(terrain.BiomeAt(x, y));
            }
        }

        // Původní mapa uměla 8 biomů a působila jednotvárně; s klimatem se jich
        // na jednom světě potká podstatně víc.
        Assert.True(seen.Count >= 12,
            $"Mapa je nudná — na vzorku je jen {seen.Count} biomů z {content.Biomes.Count}.");
    }

    [Fact]
    public void NoSingleBiome_CoversMostOfTheMap()
    {
        var (content, terrain) = RealTerrain();

        var counts = new int[content.Biomes.Count];
        int total = 0;
        for (int y = -SampleSpan; y < SampleSpan; y += 4)
        {
            for (int x = -SampleSpan; x < SampleSpan; x += 4)
            {
                counts[terrain.BiomeAt(x, y)]++;
                total++;
            }
        }

        for (int i = 0; i < counts.Length; i++)
        {
            double share = counts[i] / (double)total;
            Assert.True(share < 0.35,
                $"Biom '{content.Biomes[i].Id}' zabírá {share:P0} mapy — krajina je jednotvárná.");
        }
    }

    /// <summary>
    /// Žádný biom nesmí být mrtvý obsah — když se definuje, musí na některém
    /// presetu opravdu vzniknout. Vzorek zabírá celý klimatický cyklus, jinak by
    /// polární biomy „chyběly" jen proto, že se na ně nedohlédlo.
    ///
    /// <para>Výjimka jsou biomy s <c>natural: false</c> — ty vznikají jen tam,
    /// kam je něco přepíše (kráter po meteoritu, zaplavené pobřeží). Mrtvý obsah
    /// to není, jen se na ně nedá narazit při generování.</para>
    /// </summary>
    [Fact]
    public void EveryBiome_ActuallyOccursSomewhere()
    {
        var content = LoadContent();
        var seen = new HashSet<byte>();
        foreach (var preset in content.WorldGen.Presets)
        {
            var terrain = new ProceduralTerrain(content.Biomes, preset, seed: 12345);
            for (int y = -1400; y < 1400; y += 5)
            {
                for (int x = -700; x < 700; x += 5)
                {
                    seen.Add(terrain.BiomeAt(x, y));
                }
            }
        }

        for (byte i = 0; i < content.Biomes.Count; i++)
        {
            if (!content.Biomes[i].IsNaturallyGenerated)
            {
                continue;
            }

            Assert.True(seen.Contains(i),
                $"Biom '{content.Biomes[i].Id}' nevznikne na žádném presetu — mrtvý obsah.");
        }
    }

    /// <summary>Pásma musí být poznat: u pólu je chladněji než na rovníku.</summary>
    [Fact]
    public void PolarBand_IsColderThanEquator()
    {
        var (content, terrain) = RealTerrain();
        float band = content.WorldGen.Presets[0].TemperatureBandTiles;

        double equator = AverageTemperature(terrain, 0);
        double pole = AverageTemperature(terrain, (int)(band / 2));

        Assert.True(equator > pole + 0.25,
            $"Klimatická pásma nejsou znát: rovník {equator:0.00} vs. pól {pole:0.00}.");
    }

    /// <summary>
    /// Hory chladnou s výškou — sníh v tropech dává smysl jen díky tomu. Porovnává
    /// se STEJNÁ dlaždice se zapnutým a vypnutým ochlazením, aby do výsledku
    /// nemluvila zeměpisná šířka.
    /// </summary>
    [Fact]
    public void Altitude_CoolsTheAir()
    {
        var content = LoadContent();
        var preset = content.WorldGen.Presets[0];
        var withLapse = new ProceduralTerrain(content.Biomes, preset, seed: 12345);
        var without = new ProceduralTerrain(content.Biomes, preset with { TemperatureLapse = 0f }, seed: 12345);

        int peakX = 0, peakY = 0;
        float peak = 0f;
        for (int y = -200; y < 200; y += 3)
        {
            for (int x = -200; x < 200; x += 3)
            {
                Assert.True(withLapse.TemperatureAt(x, y) <= without.TemperatureAt(x, y) + 1e-6f,
                    "Ochlazení výškou nesmí teplotu zvyšovat.");

                float elevation = withLapse.ElevationAt(x, y);
                if (elevation > peak)
                {
                    (peak, peakX, peakY) = (elevation, x, y);
                }
            }
        }

        // Na nejvyšším místě vzorku musí být rozdíl znát, ne jen „teplotu nezvýší".
        Assert.True(peak > preset.SeaLevel, "Vzorek nezachytil jedinou souš.");
        Assert.True(withLapse.TemperatureAt(peakX, peakY) < without.TemperatureAt(peakX, peakY) - 0.05f,
            $"Vrchol [{peakX},{peakY}] (výška {peak:0.00}) není znatelně chladnější než stejné místo bez ochlazení.");
    }

    /// <summary>Řeka je sladká voda, ne korálový útes — ten patří do teplého moře.</summary>
    [Fact]
    public void Rivers_UseTheDesignatedFreshWaterBiome()
    {
        var (content, terrain) = RealTerrain();

        for (int y = -SampleSpan; y < SampleSpan; y += 3)
        {
            for (int x = -SampleSpan; x < SampleSpan; x += 3)
            {
                if (terrain.IsRiver(x, y) && terrain.ElevationAt(x, y) >= content.WorldGen.Presets[0].SeaLevel)
                {
                    Assert.Equal("shallow_water", content.Biomes[terrain.BiomeAt(x, y)].Id);
                    return;
                }
            }
        }

        Assert.Fail("Na vzorku nebyla jediná řeka.");
    }

    private static double AverageTemperature(ProceduralTerrain terrain, int centerY)
    {
        double sum = 0;
        int count = 0;
        for (int y = centerY - 40; y <= centerY + 40; y += 4)
        {
            for (int x = -200; x <= 200; x += 4)
            {
                sum += terrain.TemperatureAt(x, y);
                count++;
            }
        }

        return sum / count;
    }

    private static (GameContent Content, ProceduralTerrain Terrain) RealTerrain()
    {
        var content = LoadContent();
        return (content, new ProceduralTerrain(content.Biomes, content.WorldGen.Presets[0], seed: 12345));
    }

    private static GameContent LoadContent() =>
        new ContentLoader().LoadFrom(Path.Combine(AppContext.BaseDirectory, "data"));
}
