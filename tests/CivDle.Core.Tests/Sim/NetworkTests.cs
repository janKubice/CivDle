using CivDle.Core.Content;
using CivDle.Core.Sim;
using CivDle.Core.Tests.Support;
using CivDle.Core.World;
using Xunit;

namespace CivDle.Core.Tests.Sim;

/// <summary>
/// Obecné sítě (svety-design.md 7.2): elektřina, voda, teplo, vztlak jedním
/// systémem.
///
/// <para>Nejdůležitější je regrese: elektřina přes obecnou síť musí dát na
/// skutečném městě <b>bit po bitu</b> totéž co původní rozvod — hráč Domoviny
/// nesmí poznat, že se pod ním vyměnil systém. Ostatní testy hlídají, co nového
/// sítě umí: relé a dva druhy nedostatku.</para>
/// </summary>
public class NetworkTests
{
    private const int Water = 1;
    private const int Heat = 2;

    [Fact]
    public void PowerThroughTheNetworkMatchesTheOldGridExactly()
    {
        var content = TestData.LoadRealContent();
        var sim = new Simulation(content, new UniformTerrain(content.Biomes.IndexOf("grassland")));
        sim.SkipTutorial();
        for (int i = 0; i < content.Techs.Count; i++)
        {
            sim.DebugGrantTech(i);
        }

        sim.DebugFillStorages();
        sim.SetAutoUpgradeLevel(0);
        sim.SetAutoMerge(false);

        // Město s poruchou: spotřebiče a elektrárny rozházené náhodně (ale ze
        // seedu), některé elektrárny bez paliva, některé v dosahu dvou.
        var consumers = content.Buildings.All
            .Select((def, index) => (def, index))
            .Where(b => b.def.PowerDemand > 0 && b.def.Buildable && !b.def.IsProject)
            .Select(b => b.index).ToArray();
        var plants = content.Buildings.All
            .Select((def, index) => (def, index))
            .Where(b => b.def.PowerSupply > 0 && b.def.Buildable)
            .Select(b => b.index).ToArray();
        Assert.NotEmpty(consumers);
        Assert.NotEmpty(plants);

        int house = content.Buildings.IndexOf("house");
        for (int i = 0; i < 40; i++)
        {
            sim.TryPlaceBuildingFree(house, -60 + (i % 20) * 2, -60 - (i / 20) * 2);
        }

        var random = new Random(20260926);
        for (int i = 0; i < 90; i++)
        {
            sim.TryPlaceBuildingFree(consumers[random.Next(consumers.Length)], random.Next(-40, 160), random.Next(-40, 160));
        }

        for (int i = 0; i < 9; i++)
        {
            sim.TryPlaceBuildingFree(plants[random.Next(plants.Length)], random.Next(-40, 160), random.Next(-40, 160));
        }

        sim.DebugCompleteConstruction();
        for (int i = 0; i < 300; i++)
        {
            sim.Tick();
        }

        var legacy = new LegacyPowerGrid();
        legacy.Rebuild(sim.Buildings, content);

        int compared = 0;
        for (int y = -80; y < 200; y += 3)
        {
            for (int x = -80; x < 200; x += 3)
            {
                Assert.Equal(legacy.CoverageAt(x, y), sim.PowerAt(x, y));
                Assert.Equal(legacy.SupplyAt(x, y), sim.PowerSupplyAt(x, y));
                compared++;
            }
        }

        // Aby test nebyl zelený jen proto, že je všude tma nebo všude plno.
        var buildings = sim.Buildings;
        int lit = 0;
        int starved = 0;
        for (int i = 0; i < buildings.Length; i++)
        {
            double coverage = sim.PowerAt(buildings[i].X, buildings[i].Y);
            Assert.Equal(legacy.CoverageAt(buildings[i].X, buildings[i].Y), coverage);
            if (content.Buildings[buildings[i].DefIndex].PowerDemand > 0)
            {
                if (coverage > 0.999)
                {
                    lit++;
                }
                else
                {
                    starved++;
                }
            }
        }

        Assert.True(compared > 5000);
        Assert.True(lit > 0, "někde má svítit");
        Assert.True(starved > 0, "někde má být tma nebo nedostatek");
    }

    [Fact]
    public void AWaterSourceCoversItsRangeOnly()
    {
        var (sim, _) = World();
        int near = Place(sim, Farm, 4, 0);
        int far = Place(sim, Farm, 60, 0);
        Place(sim, Well, 0, 0);

        Assert.Equal(1.0, Coverage(sim, Water, near), 6);
        Assert.Equal(0.0, Coverage(sim, Water, far), 6);
    }

    [Fact]
    public void ARelayCarriesTheNetworkFurther()
    {
        var (sim, _) = World();
        int far = Place(sim, Farm, 40, 0); // 5 buněk od zdroje — dosah vody jsou 2
        Place(sim, Well, 0, 0);
        Assert.Equal(0.0, Coverage(sim, Water, far), 6);

        Place(sim, Cistern, 16, 0); // buňka 2 — na kraji dosahu, relé 3
        Assert.Equal(1.0, Coverage(sim, Water, far), 6);
    }

    [Fact]
    public void ARelayWithoutASourceCarriesNothing()
    {
        var (sim, _) = World();
        int farm = Place(sim, Farm, 8, 0);
        Place(sim, Cistern, 0, 0);

        Assert.Equal(0.0, Coverage(sim, Water, farm), 6);
    }

    [Fact]
    public void ASoftShortageSlowsProductionLikeMissingPower()
    {
        var (full, _) = World();
        Place(full, Farm, 4, 0);
        Place(full, Well, 0, 0);

        var (half, _) = World();
        Place(half, Farm, 4, 0);
        Place(half, Farm, 12, 0);
        Place(half, Well, 0, 0); // jedna studna na dvě pole = půl vody pro každé

        int farmIndex = 0; // první postavená budova v obou světech
        Assert.Equal(0.5, Coverage(half, Water, farmIndex), 6);

        double fullFood = Produce(full);
        double halfFood = Produce(half);

        // Dvě pole na půl vody urodí zhruba jako jedno na plnou.
        Assert.True(fullFood > 0);
        Assert.InRange(halfFood, fullFood * 0.8, fullFood * 1.2);
    }

    [Fact]
    public void AHardShortageCutsTheBuildingOutAndItComesBackOnItsOwn()
    {
        var (sim, _) = World();
        int greenhouse = Place(sim, Greenhouse, 4, 0);
        Tick(sim, 30);

        Assert.Equal(BuildingStall.NetworkShortage, sim.Buildings[greenhouse].Stall);
        double before = sim.GetResource(Food);
        Tick(sim, 100);
        Assert.Equal(before, sim.GetResource(Food), 6); // zamrzlá nevyrábí

        Place(sim, Stove, 0, 0);
        Tick(sim, 30);

        Assert.NotEqual(BuildingStall.NetworkShortage, sim.Buildings[greenhouse].Stall);
        Tick(sim, 100);
        Assert.True(sim.GetResource(Food) > before, "rozmrzlý skleník má zase vyrábět");
    }

    [Fact]
    public void ASourceThatStallsStopsFeedingTheNetwork()
    {
        // Kotel, který sám zamrzne (chce teplo, které nemá), nesmí dál hřát —
        // jinak by se dal postavit perpetuum mobile z jednoho kotle.
        var (sim, _) = World();
        int greenhouse = Place(sim, Greenhouse, 4, 0);
        Place(sim, ColdStove, 0, 0);
        Tick(sim, 30);

        Assert.Equal(BuildingStall.NetworkShortage, sim.Buildings[greenhouse].Stall);
    }

    // ----- svět -----

    private const int Food = 0;
    private const int Farm = 0;
    private const int Well = 1;
    private const int Cistern = 2;
    private const int Greenhouse = 3;
    private const int Stove = 4;
    private const int ColdStove = 5;

    private static (Simulation Sim, GameContent Content) World()
    {
        var biomes = new[] { TestContent.WaterBiome(), TestContent.LandBiome("sand") };
        var resources = new[] { new Resource("food", new RgbColor(200, 180, 60), 0, BaseStorage: 1_000_000) };
        var mask = new[] { false, true };

        BuildingDef Def(string id, Recipe? recipe, params NetworkUse[] networks) => new(
            id, "test", new RgbColor(1, 1, 1), 1, 1,
            WorkerSlots: 0, HousingCapacity: 0,
            BuildCost: Array.Empty<ResourceAmount>(),
            Recipe: recipe, AllowedBiomes: mask, StorageBonus: Array.Empty<ResourceAmount>(),
            AutoBuild: false, Buildable: true, UpgradesToIndex: -1,
            UpgradeCost: Array.Empty<ResourceAmount>(), PowerSupply: 0, PowerDemand: 0,
            NetworksOrNull: networks);

        var grow = new Recipe(Array.Empty<ResourceAmount>(), new[] { new ResourceAmount(Food, 1) }, 10);
        var buildings = new[]
        {
            Def("farm", grow, new NetworkUse(Water, 0, 10, 0)),
            Def("well", null, new NetworkUse(Water, 10, 0, 0)),
            Def("cistern", null, new NetworkUse(Water, 0, 0, 3)),
            Def("greenhouse", grow, new NetworkUse(Heat, 0, 10, 0)),
            Def("stove", null, new NetworkUse(Heat, 20, 0, 0)),
            Def("cold_stove", null, new NetworkUse(Heat, 20, 100, 0)),
        };

        var gameplay = TestContent.DefaultGameplay with { FoodPerPersonPerSecond = 0, PopulationGrowthPerSecond = 0 };
        var content = TestContent.Build(biomes, 1, resources, buildings, gameplay).WithNetworks(new[]
        {
            new NetworkTypeDef("water", 2, NetworkShortage.Slowdown, 0, new RgbColor(60, 160, 230)),
            new NetworkTypeDef("heat", 2, NetworkShortage.Cutoff, 0.5, new RgbColor(240, 140, 60)),
        });

        return (new Simulation(content, new UniformTerrain(1)), content);
    }

    private static int Place(Simulation sim, int defIndex, int x, int y)
    {
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuildingFree(defIndex, x, y));
        sim.DebugCompleteConstruction();
        return sim.Buildings.Length - 1;
    }

    private static double Coverage(Simulation sim, int network, int building) =>
        sim.NetworkCoverageAt(network, sim.Buildings[building].X, sim.Buildings[building].Y);

    private static void Tick(Simulation sim, int ticks)
    {
        for (int i = 0; i < ticks; i++)
        {
            sim.Tick();
        }
    }

    private static double Produce(Simulation sim)
    {
        double before = sim.GetResource(Food);
        Tick(sim, 400);
        return sim.GetResource(Food) - before;
    }
}
