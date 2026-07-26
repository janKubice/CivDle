using CivDle.Core.Content;
using CivDle.Core.Sim;
using CivDle.Core.Tests.Support;
using CivDle.Core.World;
using Xunit;

namespace CivDle.Core.Tests.Sim;

/// <summary>
/// Spokojenost města — vrstva, kde stavění není zadarmo. Testuje se, že vytváří
/// skutečné ROZHODNUTÍ (služby stojí údržbu a bez nich se roste pomaleji) a přitom
/// zůstává měkká: nikdo neumírá, nic se neboří, jen se zpomalí růst.
/// </summary>
public sealed class HappinessTests
{
    private static readonly HappinessConfig Config = new(
        IntervalTicks: 1,
        BaseHappiness: 0.5,
        ServiceWeight: 0.5,
        OvercrowdingPenalty: 0.25,
        PeoplePerServicePoint: 10,
        GrowthFloor: 0.2,
        FreePopulation: 0);

    [Fact]
    public void WithoutTheConfig_HappinessStaysPerfect()
    {
        var sim = new Simulation(TestContent.Build(), new UniformTerrain(1));
        for (int i = 0; i < 50; i++)
        {
            sim.Tick();
        }

        Assert.Equal(1.0, sim.Happiness, 6);
        Assert.Equal(1.0, sim.HappinessGrowthFactor, 6);
    }

    [Fact]
    public void ServiceBuilding_RaisesHappiness()
    {
        var sim = NewSim();
        sim.Tick();
        double withoutServices = sim.Happiness;

        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuilding(ServiceIndex, 3, 3));
        sim.Tick();

        Assert.True(sim.Happiness > withoutServices,
            $"Služby spokojenost nezvedly ({withoutServices:0.00} → {sim.Happiness:0.00}).");
    }

    /// <summary>Bez zaplacené údržby budova neslouží — to je ta opakovaná cena.</summary>
    [Fact]
    public void ServiceBuilding_WithoutUpkeep_DoesNotServe()
    {
        var sim = NewSim(startingResources: 0);
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuildingFree(ServiceIndex, 3, 3));
        sim.Tick();
        double unpaid = sim.Happiness;

        sim.AddResource(0, 500); // teď je z čeho platit
        sim.Tick();

        Assert.True(sim.Happiness > unpaid,
            $"Zaplacená údržba se neprojevila ({unpaid:0.00} → {sim.Happiness:0.00}).");
    }

    [Fact]
    public void Upkeep_ActuallyCostsResources()
    {
        var sim = NewSim(startingResources: 500);
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuildingFree(ServiceIndex, 3, 3));
        double before = sim.GetResource(0);

        sim.Tick();

        Assert.True(sim.GetResource(0) < before, "Údržba se nestrhla — služby by byly zadarmo.");
    }

    [Fact]
    public void LowHappiness_SlowsGrowthButNeverKillsAnyone()
    {
        var sim = NewSim();
        sim.Tick();

        Assert.True(sim.HappinessGrowthFactor < 1.0, "Nespokojené město má růst pomaleji.");
        Assert.True(sim.HappinessGrowthFactor >= Config.GrowthFloor, "Růst nesmí spadnout pod podlahu.");

        double before = sim.Population;
        for (int i = 0; i < 100; i++)
        {
            sim.Tick();
        }

        Assert.True(sim.Population >= before, "Nespokojenost nesmí ubírat obyvatele.");
    }

    /// <summary>Malá vesnice si vystačí sama — jinak by hra trestala od první minuty.</summary>
    [Fact]
    public void SmallSettlement_IsNotPunishedForMissingServices()
    {
        var content = HappyContent(Config with { FreePopulation = 1000 }, startingResources: 100);
        var sim = new Simulation(content, new UniformTerrain(1));
        sim.Tick();

        Assert.Equal(Config.BaseHappiness + Config.ServiceWeight - Crowding(sim) * Config.OvercrowdingPenalty,
            sim.Happiness, 6);
    }

    [Fact]
    public void RealContent_HasServiceBuildingsToBuild()
    {
        var content = TestData.LoadRealContent();
        Assert.True(content.Gameplay.Happiness.IsEnabled, "Ostrý obsah má mít spokojenost zapnutou.");
        Assert.Contains(content.Buildings.All, b => b.Services > 0);
        Assert.Contains(content.Buildings.All, b => b.Upkeep.Count > 0);
    }

    private static double Crowding(Simulation sim) =>
        sim.HousingCapacity <= 0 ? 1.0 : Math.Clamp(sim.Population / sim.HousingCapacity, 0.0, 1.0);

    /// <summary>Index budovy se službou v testovém obsahu.</summary>
    private const int ServiceIndex = 1;

    private static Simulation NewSim(int startingResources = 500) =>
        new(HappyContent(Config, startingResources), new UniformTerrain(1));

    /// <summary>Obsah: obyčejný dům (index 0) a služba s údržbou (index 1).</summary>
    private static GameContent HappyContent(HappinessConfig happiness, int startingResources)
    {
        var gameplay = TestContent.DefaultGameplay;
        return TestContent.Build(
            resources: new[] { new Resource("food", new RgbColor(200, 160, 60), startingResources, 100000) },
            buildings: new[]
            {
                TestContent.SimpleBuilding("house", biomeCount: 2, housing: 10),
                TestContent.Service("market", serviceValue: 2, upkeepResource: 0, upkeepAmount: 1),
            },
            gameplay: gameplay with { HappinessOrNull = happiness });
    }
}
