using CivDle.Core.Content;
using CivDle.Core.Sim;
using CivDle.Core.Tests.Support;
using CivDle.Core.World;
using Xunit;

namespace CivDle.Core.Tests.Sim;

/// <summary>
/// Vylepšení Vzestupu pro pravidlo světa (svety-design.md 2.6): „sklářští
/// mistři" zrychlí jen sklo, „úsporné zavlažování" sníží poptávku po vodě,
/// „hlubší studny" prodlouží dosah a „noční rosa" přidá zdrojům. Proud
/// Domoviny se jimi řídit nesmí.
/// </summary>
public class WorldUpgradeTests
{
    private const int Food = 0;
    private const int Glass = 1;
    private const int Water = 1;

    private const int Farm = 0;
    private const int Well = 1;

    private static readonly PrestigeConfig CheapAscension =
        new(new GoalCondition(MetricKind.Population, -1, 5), MetricKind.Population, -1, 5);

    [Fact]
    public void ATargetedUpgradeSpeedsUpOnlyItsResource()
    {
        var sim = World(new PrestigeUpgradeDef("glass_masters", "production_mult", 0.1, 1, Array.Empty<int>(), 5, TargetResourceIndex: Glass));
        sim.DebugGrantPrestigePoints(100);

        Assert.Equal(PlacementResult.Ok, sim.TryBuyUpgrade(0));
        Assert.Equal(PlacementResult.Ok, sim.TryBuyUpgrade(0));

        Assert.Equal(1.1 * 1.1, sim.ResourceProductionMult(Glass), 6);
        Assert.Equal(1.0, sim.ResourceProductionMult(Food), 6);
        Assert.Equal(1.0, sim.Bonuses.ProductionMult, 6);
    }

    [Fact]
    public void LessDemandMeansBetterCoverage()
    {
        var sim = World(new PrestigeUpgradeDef("frugal", "network_demand", 1.0, 1, Array.Empty<int>(), 1));
        int farm = Place(sim, Farm, 12, 0);
        Place(sim, Farm, 4, 0);
        Place(sim, Well, 0, 0); // 10 vody na 20 poptávky

        Assert.Equal(0.5, Coverage(sim, farm), 6);
        sim.DebugGrantPrestigePoints(100);
        Assert.Equal(PlacementResult.Ok, sim.TryBuyUpgrade(0)); // poptávka ÷ 2

        Assert.Equal(1.0, Coverage(sim, farm), 6);
    }

    [Fact]
    public void MoreSupplyAndLongerReach()
    {
        var sim = World(
            new PrestigeUpgradeDef("dew", "network_supply", 1.0, 1, Array.Empty<int>(), 1),
            new PrestigeUpgradeDef("deep", "network_range", 1, 1, Array.Empty<int>(), 1));
        int near = Place(sim, Farm, 12, 0);
        Place(sim, Farm, 4, 0);
        int far = Place(sim, Farm, 24, 0); // buňka 3 — dosah vody jsou 2
        Place(sim, Well, 0, 0);
        Assert.Equal(0.0, Coverage(sim, far), 6);

        sim.DebugGrantPrestigePoints(100);
        Assert.Equal(PlacementResult.Ok, sim.TryBuyUpgrade(0)); // výkon ×2
        Assert.Equal(1.0, Coverage(sim, near), 6);

        Assert.Equal(PlacementResult.Ok, sim.TryBuyUpgrade(1)); // dosah +1 buňka
        Assert.True(Coverage(sim, far) > 0, "hlubší studna má dosáhnout o buňku dál");
    }

    [Fact]
    public void WorldUpgradesNeverTouchPower()
    {
        var content = TestData.LoadRealContent();
        var sim = new Simulation(content, new UniformTerrain(content.Biomes.IndexOf("grassland")));

        Assert.Equal(1.0, sim.Bonuses.NetworkSupplyMult);
        Assert.Equal(1.0, sim.Bonuses.NetworkDemandMult);
        Assert.Equal(0.0, sim.Bonuses.NetworkRangeBonus);
        Assert.Equal(1.0, sim.Bonuses.HazardResistance);
    }

    private static Simulation World(params PrestigeUpgradeDef[] upgrades)
    {
        var biomes = new[] { TestContent.WaterBiome(), TestContent.LandBiome("sand") };
        var resources = new[]
        {
            new Resource("food", new RgbColor(200, 180, 60), 0, BaseStorage: 1_000),
            new Resource("glass", new RgbColor(150, 210, 220), 0, BaseStorage: 1_000),
        };
        var land = new[] { false, true };

        BuildingDef Def(string id, NetworkUse use) => new(
            id, "test", new RgbColor(1, 1, 1), 1, 1,
            WorkerSlots: 0, HousingCapacity: 0, BuildCost: Array.Empty<ResourceAmount>(),
            Recipe: null, AllowedBiomes: land, StorageBonus: Array.Empty<ResourceAmount>(),
            AutoBuild: false, Buildable: true, UpgradesToIndex: -1,
            UpgradeCost: Array.Empty<ResourceAmount>(), PowerSupply: 0, PowerDemand: 0,
            NetworksOrNull: new[] { use });

        var buildings = new[]
        {
            Def("farm", new NetworkUse(Water, 0, 10, 0)),
            Def("well", new NetworkUse(Water, 10, 0, 0)),
        };

        var gameplay = TestContent.DefaultGameplay with { FoodPerPersonPerSecond = 0, PopulationGrowthPerSecond = 0 };
        var content = TestContent.Build(biomes, 1, resources, buildings, gameplay, prestige: CheapAscension, prestigeUpgrades: upgrades)
            .WithNetworks(new[] { new NetworkTypeDef("water", 2, NetworkShortage.Slowdown, 0, new RgbColor(60, 160, 230)) });
        return new Simulation(content, new UniformTerrain(1));
    }

    private static int Place(Simulation sim, int defIndex, int x, int y)
    {
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuildingFree(defIndex, x, y));
        sim.DebugCompleteConstruction();
        return sim.Buildings.Length - 1;
    }

    private static double Coverage(Simulation sim, int building) =>
        sim.NetworkCoverageAt(Water, sim.Buildings[building].X, sim.Buildings[building].Y);
}
