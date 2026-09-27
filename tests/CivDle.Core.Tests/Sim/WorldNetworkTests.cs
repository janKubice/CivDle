using CivDle.Core.Content;
using CivDle.Core.Sim;
using CivDle.Core.Tests.Support;
using CivDle.Core.World;
using Xunit;

namespace CivDle.Core.Tests.Sim;

/// <summary>
/// Co sítě umí kvůli světům galaxie (svety-design.md 4.1): zdroje, které
/// dodávají jen ve dne nebo jen v noci, přírodní zdroj z terénu (oáza),
/// vlastní tvrdý práh budovy (háj bez vody neurodí), dopad na bydlení a cíl
/// guvernéra, který k suché budově postaví zdroj.
/// </summary>
public class WorldNetworkTests
{
    private const int Water = 1;
    private const int Food = 0;

    private const int Farm = 0;
    private const int DewTrap = 1;
    private const int Mirror = 2;
    private const int Well = 3;
    private const int Grove = 4;
    private const int Hut = 5;

    private const byte Sand = 1;
    private const byte Oasis = 2;

    [Fact]
    public void ANightSourceDeliversOnlyAtNight()
    {
        var (sim, _) = World();
        int farm = Place(sim, Farm, 4, 0);
        Place(sim, DewTrap, 0, 0);

        sim.DebugSetTimeOfDay(0.5);
        sim.Tick();
        Assert.Equal(0.0, Coverage(sim, farm), 6);

        sim.DebugSetTimeOfDay(0.02);
        sim.Tick();
        Assert.Equal(1.0, Coverage(sim, farm), 6);
    }

    [Fact]
    public void ADaySourceFollowsTheSun()
    {
        var (sim, _) = World();
        int farm = Place(sim, Farm, 4, 0);
        Place(sim, Mirror, 0, 0);

        double At(double time)
        {
            sim.DebugSetTimeOfDay(time);
            sim.Tick();
            return sim.NetworkSupplyAt(Water, sim.Buildings[farm].X, sim.Buildings[farm].Y);
        }

        double noon = At(0.5);
        double morning = At(0.32);
        double night = At(0.9);

        Assert.Equal(20, noon, 6);
        Assert.InRange(morning, 1, noon - 1);
        Assert.Equal(0, night, 6);
    }

    [Fact]
    public void TheGovernorSeesTheAverageOfTheDay()
    {
        var (sim, _) = World();
        int farm = Place(sim, Farm, 4, 0);
        Place(sim, DewTrap, 0, 0); // 10 v noci na poptávku 10 = půl dne plně

        sim.DebugSetTimeOfDay(0.5);
        sim.Tick();

        Assert.Equal(0.0, Coverage(sim, farm), 6);
        Assert.Equal(0.5, sim.NetworkSteadyCoverageAt(Water, sim.Buildings[farm].X, sim.Buildings[farm].Y), 6);
    }

    [Fact]
    public void AnOasisWatersItsNeighbourhoodWithoutAnyBuilding()
    {
        var (sim, _) = World(oasisCell: true);
        int near = Place(sim, Farm, 12, 4);   // buňka vedle oázy
        int far = Place(sim, Farm, 200, 4);   // daleko za dosahem

        Assert.True(Coverage(sim, near) > 0.99, "oáza má pokrýt okolí");
        Assert.Equal(0.0, Coverage(sim, far), 6);
    }

    [Fact]
    public void ABuildingWithItsOwnThresholdStopsInsteadOfSlowingDown()
    {
        var (sim, _) = World();
        int grove = Place(sim, Grove, 4, 0);
        Place(sim, Farm, 12, 0);
        Place(sim, Well, 0, 0); // 10 vody na 20 poptávky = pokrytí 0,5

        Tick(sim, 30);
        Assert.Equal(0.5, Coverage(sim, grove), 6);
        Assert.Equal(BuildingStall.NetworkShortage, sim.Buildings[grove].Stall);

        Place(sim, Well, 4, 8); // voda dorovnána
        Tick(sim, 30);
        Assert.NotEqual(BuildingStall.NetworkShortage, sim.Buildings[grove].Stall);
    }

    [Fact]
    public void HomesWithoutWaterGrowSlowerAndAreUnhappier()
    {
        var (sim, _) = World();
        Place(sim, Hut, 0, 0);
        Place(sim, Hut, 40, 0);

        Assert.Equal(1 - 0.6, sim.NetworkGrowthMult, 6);
        Assert.Equal(0.2, sim.NetworkHappinessDrop, 6);

        Place(sim, Well, 1, 0); // první chýše má vodu, druhá je za dosahem

        Assert.Equal(1 - 0.3, sim.NetworkGrowthMult, 6);
        Assert.Equal(0.1, sim.NetworkHappinessDrop, 6);
        Assert.Equal(-0.1, sim.HappinessParts.Networks, 6);
    }

    [Fact]
    public void TheHomeworldIsNotTouchedByHousingEffects()
    {
        var content = TestData.LoadRealContent();
        var sim = new Simulation(content, new UniformTerrain(content.Biomes.IndexOf("grassland")));

        Assert.Equal(1.0, sim.NetworkGrowthMult);
        Assert.Equal(0.0, sim.NetworkHappinessDrop);
    }

    [Fact]
    public void SpareCapacityCountsWhatConsumersReallyUse()
    {
        // Volný výkon je pohled guvernéra: kolik háj ještě utáhne. Dvě studny
        // u jednoho háje nesmí hlásit nulu jen proto, že každá vidí celou jeho
        // poptávku — jinak guvernér staví studnu za studnou.
        var (sim, _) = World();
        Place(sim, Well, 0, 0);
        Assert.Equal(10.0, sim.NetworkSteadySpareAt(Water, 0, 0), 6); // nikdo nic nechce

        Place(sim, Grove, 1, 0);
        Assert.Equal(0.0, sim.NetworkSteadySpareAt(Water, 0, 0), 6);  // háj vzal všechno

        Place(sim, Well, 2, 0);
        Assert.Equal(10.0, sim.NetworkSteadySpareAt(Water, 0, 0), 6); // druhá studna je volná
        Assert.Equal(0.0, sim.NetworkSteadySpareAt(Water, 40, 0), 6); // mimo dosah nic
    }

    [Fact]
    public void TheGovernorDigsAWellNextToADryGrove()
    {
        var (sim, _) = World(governor: true);
        Place(sim, Grove, 0, 0);

        for (int i = 0; i < 400 && !sim.Buildings.ToArray().Any(b => b.DefIndex == Well); i++)
        {
            sim.Tick();
        }

        var well = sim.Buildings.ToArray().FirstOrDefault(b => b.DefIndex == Well);
        Assert.Equal(Well, well.DefIndex);
        Assert.True(Math.Abs(well.X) + Math.Abs(well.Y) <= 16, $"studna má stát u háje, stojí na {well.X},{well.Y}");
        Assert.True(sim.NetworkSteadyCoverageAt(Water, 0, 0) > 0.5);
    }

    // ----- svět -----

    private static (Simulation Sim, GameContent Content) World(bool oasisCell = false, bool governor = false)
    {
        var biomes = new[] { TestContent.WaterBiome(), TestContent.LandBiome("sand"), TestContent.LandBiome("oasis") };
        var resources = new[] { new Resource("food", new RgbColor(200, 180, 60), 0, BaseStorage: 1_000_000) };
        var land = new[] { false, true, true };

        BuildingDef Def(string id, Recipe? recipe, NetworkUse use, SupplyTime time = SupplyTime.Always, int housing = 0) => new(
            id, "test", new RgbColor(1, 1, 1), 1, 1,
            WorkerSlots: 0, HousingCapacity: housing,
            BuildCost: Array.Empty<ResourceAmount>(),
            Recipe: recipe, AllowedBiomes: land, StorageBonus: Array.Empty<ResourceAmount>(),
            AutoBuild: governor && id == "well", Buildable: true, UpgradesToIndex: -1,
            UpgradeCost: Array.Empty<ResourceAmount>(), PowerSupply: 0, PowerDemand: 0,
            NetworksOrNull: new[] { use }, SupplyTime: time);

        var grow = new Recipe(Array.Empty<ResourceAmount>(), new[] { new ResourceAmount(Food, 1) }, 10);
        var buildings = new[]
        {
            Def("farm", grow, new NetworkUse(Water, 0, 10, 0)),
            Def("dew_trap", null, new NetworkUse(Water, 10, 0, 0), SupplyTime.Night),
            Def("mirror", null, new NetworkUse(Water, 20, 0, 0), SupplyTime.Day),
            Def("well", null, new NetworkUse(Water, 10, 0, 0)),
            Def("grove", grow, new NetworkUse(Water, 0, 10, 0, CutoffBelow: 0.8)),
            Def("hut", null, new NetworkUse(Water, 0, 1, 0), housing: 5),
        };

        var gameplay = TestContent.DefaultGameplay with
        {
            FoodPerPersonPerSecond = 0,
            PopulationGrowthPerSecond = 0,
            HappinessOrNull = new HappinessConfig(
                IntervalTicks: 1, BaseHappiness: 0.5, ServiceWeight: 0.5, OvercrowdingPenalty: 0,
                PeoplePerServicePoint: 10, GrowthFloor: 0.2, FreePopulation: 0),
        };
        if (governor)
        {
            gameplay = gameplay with
            {
                AutoBuild = new AutoBuildConfig(IntervalTicks: 5, SearchRadius: 6, PopulationHeadroom: 2),
                GovernorOrNull = new GovernorConfig(
                    true, StorageGoalConfig.Off, SupplyGoalConfig.Off, SupplyGoalConfig.Off,
                    LandscapeGoalConfig.Off, new PowerGoalConfig(0.9)),
            };
        }

        var content = TestContent.Build(biomes, 1, resources, buildings, gameplay).WithNetworks(new[]
        {
            new NetworkTypeDef("water", 2, NetworkShortage.Slowdown, 0, new RgbColor(60, 160, 230),
                new[] { new TerrainSource(Oasis, 0.5) }, new NetworkHousing(0.6, 0.2)),
        });

        ITerrain terrain = oasisCell ? new OasisTerrain() : new UniformTerrain(Sand);
        return (new Simulation(content, terrain), content);
    }

    /// <summary>Písek a jedna buňka 8×8 oázy u počátku (8 dlaždic × 8 = 32 vody na buňku).</summary>
    private sealed class OasisTerrain : ITerrain
    {
        public byte BiomeAt(int x, int y) => x is >= 0 and < 8 && y is >= 0 and < 8 ? Oasis : Sand;
    }

    private static int Place(Simulation sim, int defIndex, int x, int y)
    {
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuildingFree(defIndex, x, y));
        sim.DebugCompleteConstruction();
        return sim.Buildings.Length - 1;
    }

    private static double Coverage(Simulation sim, int building) =>
        sim.NetworkCoverageAt(Water, sim.Buildings[building].X, sim.Buildings[building].Y);

    private static void Tick(Simulation sim, int ticks)
    {
        for (int i = 0; i < ticks; i++)
        {
            sim.Tick();
        }
    }
}
