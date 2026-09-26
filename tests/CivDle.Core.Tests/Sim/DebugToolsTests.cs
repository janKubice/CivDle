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
    [Fact]
    public void AMillionOfEverythingFitsBecauseTheStoragesGrow()
    {
        // Bez zvětšení skladů by „+1 milion“ skončil na stropu 500 a tlačítko
        // by vypadalo, že nic nedělá.
        var sim = World();

        sim.DebugGrantEveryResource(1_000_000);

        Assert.True(sim.GetResource(0) >= 1_000_000, $"dřeva je {sim.GetResource(0)}");
        Assert.True(sim.GetStorageCap(0) >= 1_000_000);
    }

    [Fact]
    public void EmptyingStoragesLeavesNothing()
    {
        var sim = World();
        sim.DebugFillStorages();

        sim.DebugEmptyStorages();

        Assert.Equal(0, sim.GetResource(0));
    }

    [Fact]
    public void MaxingUpgradesBuysEveryLevelOfBothLayers()
    {
        var content = TestData.LoadRealContent();
        var sim = new Simulation(content, new UniformTerrain((byte)1));

        sim.DebugMaxPrestigeUpgrades();
        sim.DebugMaxLegacyUpgrades();

        for (int i = 0; i < content.PrestigeUpgrades.Count; i++)
        {
            Assert.True(sim.IsUpgradeMaxed(i), content.PrestigeUpgrades[i].Id);
        }

        for (int i = 0; i < content.LegacyUpgrades.Count; i++)
        {
            Assert.True(sim.IsLegacyUpgradeMaxed(i), content.LegacyUpgrades[i].Id);
        }

        Assert.True(sim.Bonuses.ProductionMult > 1.0, "vykoupené upgrady se nepromítly do bonusů");
    }

    [Fact]
    public void DeepeningTheLegacyRaisesItsThreshold()
    {
        var sim = new Simulation(TestData.LoadRealContent(), new UniformTerrain((byte)1));
        long before = sim.LegacyRequirement();

        sim.DebugDeepenLegacy(3);

        Assert.Equal(3, sim.LegacyDepth);
        Assert.True(sim.LegacyRequirement() > before);
    }

    [Fact]
    public void TheClockJumpsToTheAskedTimeOfDayWithoutTouchingTicks()
    {
        // Posouvá se kalendář, ne tiky: tiky řídí intervaly systémů a konce
        // efektů, a ty se přetočením hodin hýbat nemají.
        var sim = new Simulation(TestData.LoadRealContent(), new UniformTerrain((byte)1));
        long ticks = sim.TickCount;

        sim.DebugSetTimeOfDay(0.80);
        Assert.Equal(0.80, sim.TimeOfDay01, 6);

        sim.DebugSetTimeOfDay(0.25); // dozadu se nejde, jde se na zítřejší ráno
        Assert.Equal(0.25, sim.TimeOfDay01, 6);
        Assert.True(sim.DayNumber >= 2);
        Assert.Equal(ticks, sim.TickCount);
    }

    [Fact]
    public void SkippingASeasonLandsInTheNextOne()
    {
        var content = TestData.LoadRealContent();
        Assert.True(content.Seasons.IsEnabled, "skutečná data mají mít roční období");
        var sim = new Simulation(content, new UniformTerrain((byte)1));
        int first = sim.CurrentSeasonIndex;

        sim.DebugAdvanceSeason();

        Assert.Equal((first + 1) % content.Seasons.Seasons.Count, sim.CurrentSeasonIndex);
    }

    [Fact]
    public void APreparedSceneSurvivesSavingAndLoading()
    {
        // Zimní soumrak připravený na natáčení se po načtení nesmí vrátit na
        // jarní poledne a milion prken se nesmí oříznout na běžný sklad.
        var content = TestData.LoadRealContent();
        var preset = content.WorldGen.Presets.Single(p => p.Id == "continents");
        var sim = new Simulation(content, new ProceduralTerrain(content.Biomes, preset, 42), 42);
        sim.DebugAdvanceSeason();
        sim.DebugSetTimeOfDay(0.78);
        sim.DebugGrantEveryResource(1_000_000);

        using var stream = new MemoryStream();
        var metadata = new CivDle.Core.Save.SaveMetadata(42, "medium", "continents", DateTime.UtcNow);
        new CivDle.Core.Save.SaveGameSerializer().Write(stream, sim, metadata);
        stream.Position = 0;
        var (loaded, _) = new CivDle.Core.Save.SaveGameSerializer().Read(stream, content);

        Assert.Equal(sim.CurrentSeasonIndex, loaded.CurrentSeasonIndex);
        Assert.Equal(sim.TimeOfDay01, loaded.TimeOfDay01, 9);
        int planks = content.Resources.IndexOf("planks");
        Assert.Equal(sim.GetResource(planks), loaded.GetResource(planks));
    }
}
