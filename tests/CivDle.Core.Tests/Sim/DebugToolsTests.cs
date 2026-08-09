using CivDle.Core.Content;
using CivDle.Core.Sim;
using CivDle.Core.Tests.Support;
using CivDle.Core.World;
using Xunit;

namespace CivDle.Core.Tests.Sim;

/// <summary>
/// Ladicí páky pro cheat menu.
///
/// <para>Nejsou to hračky: pozdní hra se bez nich dá vyzkoušet jen tak, že se
/// hraje několik hodin, a natočit se nedá vůbec. Testují se proto, že sahají
/// do stavu simulace a chyba v nich vypadá jako chyba hry.</para>
/// </summary>
public class DebugToolsTests
{
    private static readonly Resource[] Wood =
    {
        new("wood", new RgbColor(120, 90, 60), StartAmount: 10, BaseStorage: 500),
    };

    private static readonly PrestigeConfig EarlyAscension =
        new(new GoalCondition(MetricKind.Population, -1, 5), MetricKind.Population, -1, 5);

    private static readonly AscensionTierDef[] Tiers =
    {
        new("village", 0, 100, Array.Empty<int>()),
        new("city", 1, 10_000, Array.Empty<int>()),
    };

    private static GameContent Content()
    {
        var house = new BuildingDef(
            "house", "housing", new RgbColor(180, 100, 60), 1, 1,
            WorkerSlots: 0, HousingCapacity: 50,
            BuildCost: new[] { new ResourceAmount(0, 1) },
            Recipe: null,
            AllowedBiomes: new[] { false, true },
            StorageBonus: Array.Empty<ResourceAmount>(),
            AutoBuild: true, Buildable: true,
            UpgradesToIndex: -1, UpgradeCost: Array.Empty<ResourceAmount>(),
            PowerSupply: 0, PowerDemand: 0);

        return TestContent.Build(
            biomes: new[] { TestContent.WaterBiome(), TestContent.LandBiome("grass") },
            resources: Wood,
            buildings: new[] { house },
            prestige: EarlyAscension,
            ascensionTiers: Tiers);
    }

    private static Simulation World() => new(Content(), new UniformTerrain((byte)1));

    [Fact]
    public void FillingStoragesTopsEveryResourceToItsCap()
    {
        var sim = World();

        sim.DebugFillStorages();

        for (int i = 0; i < sim.ResourceCount; i++)
        {
            Assert.Equal(sim.GetStorageCap(i), sim.GetResource(i), 6);
        }
    }

    [Fact]
    public void GrantingAscensionLevelsRaisesTheScaleCap()
    {
        // Tohle je ta páka, kvůli které to existuje: strop měřítka je jinak
        // hodiny hraní daleko.
        var sim = World();
        double before = sim.PopulationCap;

        sim.DebugGrantAscensionLevels(1);

        Assert.Equal(1, sim.AscensionLevel);
        Assert.True(sim.PopulationCap > before);
    }

    [Fact]
    public void GrantingNothingChangesNothing()
    {
        var sim = World();

        sim.DebugGrantAscensionLevels(0);
        sim.DebugGrantLegacyPoints(-5);
        sim.DebugAddPopulation(-100);

        Assert.Equal(0, sim.AscensionLevel);
        Assert.Equal(0, sim.LegacyPoints);
        Assert.True(sim.Population > 0);
    }

    [Fact]
    public void LegacyPointsCanBeGrantedForTesting()
    {
        var sim = World();

        sim.DebugGrantLegacyPoints(250);

        Assert.Equal(250, sim.LegacyPoints);
    }

    [Fact]
    public void AddedPopulationNeverExceedsWhatTheWorldAllows()
    {
        var sim = World();

        sim.DebugAddPopulation(1_000_000);

        Assert.True(sim.Population <= sim.PopulationCap + 0.001);
        Assert.True(sim.Population <= sim.HousingCapacity + 0.001);
    }

    [Fact]
    public void TheBuildBoostSpeedsThingsUpAndThenWearsOff()
    {
        // Tempo se projeví nejdřív na intervalu (staví se častěji) a teprve
        // po jeho dosednutí na počtu staveb — proto se kouká na interval.
        var sim = World();
        int plainInterval = sim.AutoBuildInterval;

        sim.DebugBoostAutoBuild(50, seconds: 1);
        Assert.True(sim.DebugBuildBoostActive);
        Assert.True(sim.AutoBuildInterval < plainInterval,
            $"boost se na tempu neprojevil: interval {sim.AutoBuildInterval} vs {plainInterval}");

        for (int i = 0; i < (int)Simulation.TicksPerSecond + 2; i++)
        {
            sim.Tick();
        }

        Assert.False(sim.DebugBuildBoostActive);
        Assert.Equal(plainInterval, sim.AutoBuildInterval);
    }

    [Fact]
    public void TheBoostCannotSlowTheGameDown()
    {
        // Násobič pod jedničkou by z ladicí páky udělal brzdu.
        var sim = World();
        int plainInterval = sim.AutoBuildInterval;

        sim.DebugBoostAutoBuild(0.01, seconds: 10);

        Assert.Equal(plainInterval, sim.AutoBuildInterval);
    }
}
