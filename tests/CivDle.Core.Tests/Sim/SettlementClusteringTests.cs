using CivDle.Core.Content;
using CivDle.Core.Sim;
using CivDle.Core.Tests.Support;
using CivDle.Core.World;
using Xunit;

namespace CivDle.Core.Tests.Sim;

/// <summary>
/// Sídla přes prostorovou mřížku musí vyjít <b>přesně</b> stejně jako prosté
/// porovnání každé budovy s každou.
///
/// <para>Proč: mřížka vznikla kvůli výkonu (u 24 000 budov stál přepočet
/// skoro dvě sekundy a dohánění offline času trvalo hodiny). Rychlejší
/// algoritmus, který by občas spojil nebo rozpojil jiná sídla, by změnil
/// jména měst a bonusy — a to nesmí. Test staví náhodná města s budovami
/// různých půdorysů, i v záporných souřadnicích (buňky mřížky tam dělí
/// jinak), a porovná výsledek s referencí napsanou tím nejhloupějším
/// způsobem.</para>
/// </summary>
public class SettlementClusteringTests
{
    private const int ClusterDistance = 3;
    private const int MinBuildings = 3;

    private static GameContent Content()
    {
        var biomes = new[] { TestContent.WaterBiome(), TestContent.LandBiome("grass") };
        var resources = new[] { new Resource("wood", new RgbColor(1, 1, 1), StartAmount: 0, BaseStorage: 1000) };
        var buildings = new[]
        {
            TestContent.SimpleBuilding("hut", 2),
            TestContent.SimpleBuilding("hall", 2) with { FootprintWidth = 2, FootprintHeight = 2 },
            TestContent.SimpleBuilding("row", 2) with { FootprintWidth = 3, FootprintHeight = 1 },
            TestContent.SimpleBuilding("hub", 2) with { FootprintWidth = 4, FootprintHeight = 4 },
        };

        var gameplay = TestContent.DefaultGameplay with
        {
            Settlements = new SettlementConfig(MinBuildings, ClusterDistance, UpdateIntervalTicks: 1),
        };

        return TestContent.Build(biomes, 1, resources, buildings, gameplay);
    }

    [Theory]
    [InlineData(1, 60, 25)]
    [InlineData(2, 200, 40)]
    [InlineData(3, 500, 60)]
    [InlineData(4, 900, 140)]
    public void TheGridFindsExactlyTheSettlementsOfTheSlowWay(int seed, int count, int spread)
    {
        var content = Content();
        var sim = new Simulation(content, new UniformTerrain(1), seed);
        var rng = new Random(seed);
        for (int i = 0; i < count * 4 && sim.Buildings.Length < count; i++)
        {
            sim.TryPlaceBuildingFree(rng.Next(content.Buildings.Count), rng.Next(-spread, spread), rng.Next(-spread, spread));
        }

        sim.Tick();

        var expected = Reference(content, sim.Buildings.ToArray());
        var actual = sim.Settlements.Select(s => (s.CenterX, s.CenterY, s.BuildingCount)).ToList();

        Assert.True(expected.Count > 1, "test má smysl jen s víc sídly");
        Assert.Equal(expected, actual);
    }

    /// <summary>Reference: každá s každou, těžiště sčítané vzestupně podle indexu.</summary>
    private static List<(float X, float Y, int Count)> Reference(GameContent content, BuildingInstance[] buildings)
    {
        int n = buildings.Length;
        var parent = Enumerable.Range(0, n).ToArray();
        int Find(int i)
        {
            while (parent[i] != i)
            {
                i = parent[i];
            }

            return i;
        }

        for (int i = 0; i < n; i++)
        {
            for (int j = i + 1; j < n; j++)
            {
                if (Gap(content, buildings[i], buildings[j]) <= ClusterDistance)
                {
                    int a = Find(i), b = Find(j);
                    if (a != b)
                    {
                        parent[Math.Max(a, b)] = Math.Min(a, b);
                    }
                }
            }
        }

        var result = new List<(float, float, int)>();
        for (int root = 0; root < n; root++)
        {
            if (Find(root) != root)
            {
                continue;
            }

            int count = 0;
            float sumX = 0f, sumY = 0f;
            for (int i = 0; i < n; i++)
            {
                if (Find(i) != root)
                {
                    continue;
                }

                var def = content.Buildings[buildings[i].DefIndex];
                count++;
                sumX += buildings[i].X + def.FootprintWidth * 0.5f;
                sumY += buildings[i].Y + def.FootprintHeight * 0.5f;
            }

            if (count >= MinBuildings)
            {
                result.Add((sumX / count, sumY / count, count));
            }
        }

        return result;
    }

    private static int Gap(GameContent content, BuildingInstance a, BuildingInstance b)
    {
        var defA = content.Buildings[a.DefIndex];
        var defB = content.Buildings[b.DefIndex];
        int gapX = Math.Max(0, Math.Max(a.X - (b.X + defB.FootprintWidth - 1), b.X - (a.X + defA.FootprintWidth - 1)) - 1);
        int gapY = Math.Max(0, Math.Max(a.Y - (b.Y + defB.FootprintHeight - 1), b.Y - (a.Y + defA.FootprintHeight - 1)) - 1);
        return Math.Max(gapX, gapY);
    }
}
