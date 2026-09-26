using CivDle.Core.Content;
using CivDle.Core.Sim;
using CivDle.Core.Tests.Support;
using CivDle.Core.World;
using Xunit;

namespace CivDle.Core.Tests.Sim;

/// <summary>
/// Vysušení jezera (endgame.md, B3): projekt, jehož každý stupeň vysuší pás
/// břehu. Hlídá se, že stupeň vezme <b>právě jeden</b> pás (ne celé jezero
/// naráz), že je výsledek deterministický a že vysušená zem je zem i pro
/// stavbu.
/// </summary>
public class LakeDrainTests
{
    private const int Water = 0;
    private const int Land = 1;
    private const int Marsh = 2;

    /// <summary>Souš pro x ≤ 2, jezero na východ od ní.</summary>
    private sealed class ShoreTerrain : ITerrain
    {
        public byte BiomeAt(int x, int y) => (byte)(x <= 2 ? Land : Water);
    }

    [Fact]
    public void EachStageDrainsOneBandOfShore()
    {
        var sim = NewSim(out _);
        int pumps = Place(sim);

        sim.TryInvestInProject(pumps);

        // První pás: voda, která sousedila se souší (x = 3), v dosahu.
        Assert.Equal(Marsh, sim.BiomeAt(3, 0));
        Assert.Equal(Water, sim.BiomeAt(4, 0));

        sim.TryInvestInProject(pumps);

        Assert.Equal(Marsh, sim.BiomeAt(4, 0));
        Assert.Equal(Water, sim.BiomeAt(5, 0));
    }

    [Fact]
    public void DrainingIsDeterministic()
    {
        var a = NewSim(out _);
        var b = NewSim(out _);
        int pa = Place(a);
        int pb = Place(b);
        a.TryInvestInProject(pa);
        b.TryInvestInProject(pb);

        for (int y = -8; y <= 8; y++)
        {
            for (int x = -2; x <= 10; x++)
            {
                Assert.Equal(a.BiomeAt(x, y), b.BiomeAt(x, y));
            }
        }

        Assert.Equal(a.TerraformedTiles, b.TerraformedTiles);
    }

    [Fact]
    public void DrainedShoreCanBeBuiltOn()
    {
        var sim = NewSim(out var content);
        int pumps = Place(sim);
        Assert.Equal(PlacementResult.WrongBiome, sim.CanPlace(0, 3, 0));

        sim.TryInvestInProject(pumps);

        Assert.Equal(PlacementResult.Ok, sim.CanPlace(0, 3, 0));
    }

    [Fact]
    public void TheRealLakeDrainIsABandProject()
    {
        var content = TestData.LoadRealContent();
        var def = content.Buildings[content.Buildings.IndexOf("lake_drain")];

        Assert.True(def.IsProject);
        Assert.Equal(ProjectRule.DrainBand, def.ProjectOrNull!.OnStage);
        Assert.False(content.Biomes[def.ProjectOrNull.EffectBiomeIndex].IsWater);
        Assert.True(def.ProjectOrNull.Stages.Count >= 8, "vysušení je bezedný odběr — stupňů má být dost");
    }

    private static Simulation NewSim(out GameContent content)
    {
        var biomes = new[] { TestContent.WaterBiome(), TestContent.LandBiome("grass"), TestContent.LandBiome("marsh") };
        var resources = new[] { new Resource("stone", new RgbColor(1, 1, 1), 10_000, BaseStorage: 100_000) };
        var project = new ProjectRule(
            new[]
            {
                new ProjectStage(new[] { new ResourceAmount(0, 100) }),
                new ProjectStage(new[] { new ResourceAmount(0, 100) }),
                new ProjectStage(new[] { new ResourceAmount(0, 100) }),
            },
            OnComplete: null, OnStage: ProjectRule.DrainBand, EffectRadius: 4, EffectBiomeIndex: Marsh);

        var hut = TestContent.SimpleBuilding("hut", biomes.Length) with
        {
            AllowedBiomes = new[] { false, true, true },
        };
        var pumps = TestContent.SimpleBuilding("pumps", biomes.Length) with
        {
            FootprintWidth = 2,
            FootprintHeight = 2,
            BuildCost = Array.Empty<ResourceAmount>(),
            AllowedBiomes = new[] { false, true, true },
            BuildTicks = project.TotalUnits,
            ProjectOrNull = project,
        };

        var gameplay = TestContent.DefaultGameplay with { FoodPerPersonPerSecond = 0, PopulationGrowthPerSecond = 0 };
        content = TestContent.Build(biomes, 1, resources, new[] { hut, pumps }, gameplay);
        return new Simulation(content, new ShoreTerrain(), 1);
    }

    /// <summary>Čerpadla na břeh: 2×2 na x = 1…2, y = −1…0.</summary>
    private static int Place(Simulation sim)
    {
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuilding(1, 1, -1));
        return sim.Buildings.Length - 1;
    }
}
