using CivDle.Core.Content;
using CivDle.Core.Sim;
using CivDle.Core.Tests.Support;
using CivDle.Core.World;
using Xunit;

namespace CivDle.Core.Tests.Sim;

/// <summary>
/// Nástroje jako živá surovina: vyrobené nástroje se prací ohladí, takže mají
/// trvalý odbyt místo aby se hromadily do stropu skladu.
///
/// <para>Vrstva je čistě bonusová — bez nástrojů se hraje jako dřív, jen bez
/// bonusu. Testy proto hlídají obojí: že bonus funguje, i že jeho absence
/// není trest.</para>
/// </summary>
public class ToolsTests
{
    private const int Wood = 0;
    private const int Tools = 1;

    private static GameContent Content(ToolsConfig? tools = null, double startTools = 100)
    {
        var biomes = new[]
        {
            TestContent.WaterBiome(),
            TestContent.LandBiome("grass") with { ClickYield = new ClickYield(Wood, 10) },
        };
        var resources = new[]
        {
            new Resource("wood", new RgbColor(1, 1, 1), StartAmount: 100, BaseStorage: 1_000_000),
            new Resource("tools", new RgbColor(2, 2, 2), StartAmount: startTools, BaseStorage: 1_000_000),
        };

        var camp = new BuildingDef(
            "camp", "production", new RgbColor(100, 100, 100), 1, 1,
            WorkerSlots: 2, HousingCapacity: 0,
            BuildCost: Array.Empty<ResourceAmount>(),
            Recipe: new Recipe(
                Inputs: Array.Empty<ResourceAmount>(),
                Outputs: new[] { new ResourceAmount(Wood, 10) },
                TimeTicks: 1),
            AllowedBiomes: new[] { false, true },
            StorageBonus: Array.Empty<ResourceAmount>(),
            AutoBuild: false, Buildable: true, UpgradesToIndex: -1,
            UpgradeCost: Array.Empty<ResourceAmount>(), PowerSupply: 0, PowerDemand: 0);

        var gameplay = TestContent.DefaultGameplay with
        {
            FoodPerPersonPerSecond = 0,
            PopulationGrowthPerSecond = 0,
            ToolsOrNull = tools ?? new ToolsConfig(
                ResourceIndex: Tools, PerPerson: 0.5, WearPerWorkerPerSecond: 0.1,
                ProductionBonus: 0.5, HarvestBonus: 1.0),
        };

        return TestContent.Build(biomes, 1, resources, new[] { camp }, gameplay);
    }

    private static Simulation NewSim(ToolsConfig? tools = null, double startTools = 100) =>
        new(Content(tools, startTools), new UniformTerrain(1));

    [Fact]
    public void FullyEquippedCity_ProducesMoreThanAnEmptyToolshed()
    {
        var equipped = NewSim();
        Assert.Equal(PlacementResult.Ok, equipped.TryPlaceBuildingFree(0, 0, 0));

        var bare = NewSim(startTools: 0);
        Assert.Equal(PlacementResult.Ok, bare.TryPlaceBuildingFree(0, 0, 0));

        double withTools = ProducedOver(equipped, Wood, 10);
        double without = ProducedOver(bare, Wood, 10);

        Assert.True(withTools > without, $"vybavení lidé mají vyrábět víc ({withTools} vs {without})");
    }

    [Fact]
    public void WorkingWearsToolsOut()
    {
        var sim = NewSim();
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuildingFree(0, 0, 0));
        double before = sim.GetResource(Tools);

        for (int i = 0; i < 20; i++)
        {
            sim.Tick();
        }

        Assert.True(sim.GetResource(Tools) < before, "prací se nástroje mají ohladit");
        Assert.True(sim.EmployedWorkers > 0);
    }

    [Fact]
    public void CityWithNobodyAtWork_WearsNothing()
    {
        // Bez budov není kdo by nástroje ohmatal — a hra si je nemá brát jen tak.
        var sim = NewSim();
        double before = sim.GetResource(Tools);

        for (int i = 0; i < 20; i++)
        {
            sim.Tick();
        }

        Assert.Equal(before, sim.GetResource(Tools), 6);
        Assert.Equal(0, sim.EmployedWorkers);
    }

    [Fact]
    public void ToolsNeverGoNegative()
    {
        // Došlé nástroje jsou stejná dohoda jako došlé jídlo: bonus zmizí,
        // ale nikdo nedluží a nic se neboří.
        var sim = NewSim(startTools: 0.5);
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuildingFree(0, 0, 0));

        for (int i = 0; i < 200; i++)
        {
            sim.Tick();
        }

        Assert.Equal(0, sim.GetResource(Tools), 6);
        Assert.Equal(0, sim.ToolCoverage, 6);
        Assert.Equal(1.0, sim.ToolProductionMult, 6);
    }

    [Fact]
    public void Coverage_StopsAtFull()
    {
        // Deset tisíc nástrojů pro pět lidí není desetinásobný bonus.
        var sim = NewSim(startTools: 10_000);

        Assert.Equal(1.0, sim.ToolCoverage, 6);
        Assert.Equal(1.5, sim.ToolProductionMult, 6);
    }

    [Fact]
    public void BiggerCity_NeedsMoreToolsForTheSameCoverage()
    {
        // Jádro celé mechaniky: co bylo pro vesnici jedna dílna, je pro
        // velkoměsto celá výrobní větev.
        var tools = new ToolsConfig(Tools, PerPerson: 0.5, WearPerWorkerPerSecond: 0.1, ProductionBonus: 0.5, HarvestBonus: 1.0);

        Assert.Equal(1.0, tools.Coverage(tools: 10, population: 20), 6);
        Assert.Equal(0.5, tools.Coverage(tools: 10, population: 40), 6);
        Assert.Equal(0.2, tools.Coverage(tools: 10, population: 100), 6);
    }

    [Fact]
    public void ToolsRaiseHandGathering()
    {
        var equipped = NewSim();
        Assert.True(equipped.TryHarvest(5, 5, out _, out int withTools));

        var bare = NewSim(startTools: 0);
        Assert.True(bare.TryHarvest(5, 5, out _, out int without));

        Assert.True(withTools > without, $"s nástroji má sběr nést víc ({withTools} vs {without})");
    }

    [Fact]
    public void DisabledTools_ChangeNothing()
    {
        var sim = NewSim(ToolsConfig.Disabled);
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuildingFree(0, 0, 0));
        double before = sim.GetResource(Tools);

        for (int i = 0; i < 20; i++)
        {
            sim.Tick();
        }

        Assert.Equal(before, sim.GetResource(Tools), 6);
        Assert.Equal(1.0, sim.ToolProductionMult, 6);
        Assert.Equal(1.0, sim.ToolHarvestMult, 6);
    }

    [Fact]
    public void RealContent_GivesToolsAPermanentPurpose()
    {
        var tools = TestData.LoadRealContent().Gameplay.Tools;

        Assert.True(tools.IsEnabled, "nástroje mají být ve skutečných datech živá surovina");
        Assert.True(tools.WearPerWorkerPerSecond > 0, "bez opotřebení by se vyrobily jednou a platily navždy");
        Assert.True(tools.ProductionBonus > 0 || tools.HarvestBonus > 0, "opotřebení bez bonusu by byla jen daň");
    }

    private static double ProducedOver(Simulation sim, int resource, int ticks)
    {
        double before = sim.GetResource(resource);
        for (int i = 0; i < ticks; i++)
        {
            sim.Tick();
        }

        return sim.GetResource(resource) - before;
    }
}
