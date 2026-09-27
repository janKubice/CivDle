using CivDle.Core.Content;
using CivDle.Core.Save;
using CivDle.Core.Sim;
using CivDle.Core.Tests.Support;
using CivDle.Core.World;
using Xunit;

namespace CivDle.Core.Tests.Sim;

/// <summary>
/// Xeno (svety-design.md 4.6): flóra roste z hnízda v tepech, obalí budovu na
/// okraji (sama se vrátí), prořezávač šíření v okruhu zastaví, Strom života
/// ho nechá růst, ale nic neobalí, bariéra ho nepustí a flóra přežije save.
/// Klidný tep (★★) chce i dost zeleně kolem města — vymýcená flóra se nepočítá.
/// </summary>
public class FloraTests
{
    private const int Hut = 0;
    private const int Pruner = 1;
    private const int Tree = 2;
    private const int Barrier = 3;

    private const int Land = 1;
    private const int Nest = 2;
    private const int Bloom = 3;

    /// <summary>Tep každých 10 s, první v 10 s, obalení na 30 s.</summary>
    private const double Interval = 10;

    [Fact]
    public void TheFloraGrowsFromTheNestInPulses()
    {
        var sim = World();
        Place(sim, Hut, 30, 30); // město daleko — flóra roste volně

        Run(sim, seconds: 9);
        Assert.Equal(0, BloomTiles(sim));

        Run(sim, seconds: 2); // první tep
        int first = BloomTiles(sim);
        Assert.InRange(first, 1, 4); // hnízdo obroste sousedy

        Run(sim, seconds: 40); // další čtyři tepy
        Assert.True(BloomTiles(sim) > first + 4, $"flóra má růst dál: {first} → {BloomTiles(sim)}");
    }

    [Fact]
    public void TheSameSeedGrowsTheSameFlora()
    {
        var a = World();
        var b = World();
        Place(a, Hut, 30, 30);
        Place(b, Hut, 30, 30);

        Run(a, seconds: 61);
        Run(b, seconds: 61);

        for (int y = -8; y <= 8; y++)
        {
            for (int x = -8; x <= 8; x++)
            {
                Assert.Equal(a.BiomeAt(x, y), b.BiomeAt(x, y));
            }
        }
    }

    [Fact]
    public void AHutAtTheEdgeIsWrappedAndComesBack()
    {
        var sim = World();
        int hut = Place(sim, Hut, 1, 0); // hned vedle hnízda

        Run(sim, seconds: 11);

        Assert.Equal(DisableCause.Burial, sim.Buildings[hut].DisabledCause);
        Assert.Equal(BuildingStall.Overgrown, sim.Buildings[hut].Stall);
        Assert.Equal(Land, sim.BiomeAt(1, 0)); // pod budovou flóra neroste, jen ji obalí

        Run(sim, seconds: 35);
        // Hnízdo ji obalí znovu každý tep — ale výpadek pořád jen dočasně.
        Assert.True(sim.Buildings[hut].DisabledTicks <= Simulation.TicksPerSecond * 31);
    }

    [Fact]
    public void APrunerStopsTheSpreadAroundIt()
    {
        var sim = World();
        Place(sim, Pruner, 0, 3);
        int hut = Place(sim, Hut, 1, 0);

        Run(sim, seconds: 61);

        Assert.Equal(0, sim.Buildings[hut].DisabledTicks);
        for (int y = -2; y <= 2; y++)
        {
            for (int x = -2; x <= 2; x++)
            {
                Assert.NotEqual(Bloom, sim.BiomeAt(x, y)); // v okruhu prořezávače nic nevyrostlo
            }
        }

        Assert.True(sim.CalmHazards > 0, "tepy, které k městu dorostly a nic neobalily, jsou klidné");
    }

    [Fact]
    public void ALifeTreeLetsTheFloraGrowButNothingIsWrapped()
    {
        var sim = World();
        Place(sim, Tree, 0, 3);
        int hut = Place(sim, Hut, 1, 0);

        Run(sim, seconds: 61);

        Assert.Equal(0, sim.Buildings[hut].DisabledTicks);
        Assert.True(BloomTiles(sim) > 4, "u Stromu života flóra dál roste");
    }

    [Fact]
    public void APrunedCityIsNotCalmWhenTheWorldMustStayGreen()
    {
        var sim = World(calmGreenShare: 0.5);
        Place(sim, Pruner, 0, 3);
        Place(sim, Hut, 1, 0);

        Run(sim, seconds: 61);

        Assert.True(sim.HazardsWeathered > 0, "tepy k městu dorostly");
        Assert.Equal(0, sim.CalmHazards); // nic neobalily, ale zeleně kolem města skoro není
    }

    [Fact]
    public void ALifeTreeKeepsTheCityCalmAndGreen()
    {
        var sim = World(calmGreenShare: 0.0005); // ~3 dlaždice flóry z 6 561
        Place(sim, Tree, 0, 3);
        Place(sim, Hut, 1, 0);

        Run(sim, seconds: 61);

        Assert.True(sim.CalmHazards > 0, "u Stromu života flóra roste a nic neobalí — klidné tepy se počítají");
    }

    [Fact]
    public void ABarrierHoldsTheFloraBack()
    {
        var sim = World();
        for (int y = -14; y <= 14; y++)
        {
            Place(sim, Barrier, 3, y);
        }

        Place(sim, Hut, 30, 30);
        Run(sim, seconds: 81); // osm tepů: kolem konce bariéry to flóra nestihne

        for (int y = -6; y <= 6; y++)
        {
            for (int x = 4; x <= 10; x++)
            {
                Assert.NotEqual(Bloom, sim.BiomeAt(x, y));
            }
        }

        Assert.Equal(Bloom, sim.BiomeAt(2, 0)); // před bariérou flóra je
    }

    [Fact]
    public void TheFloraAndAWrappedHutSurviveASave()
    {
        var sim = World();
        int hut = Place(sim, Hut, 1, 0);
        Run(sim, seconds: 31);

        var reloaded = Reload(sim);

        Assert.Equal(BloomTiles(sim), BloomTiles(reloaded));
        Assert.Equal(DisableCause.Burial, reloaded.Buildings[hut].DisabledCause);
        Assert.True(reloaded.Buildings[hut].DisabledTicks > 0);
    }

    // ----- svět -----

    private static int BloomTiles(Simulation sim)
    {
        int count = 0;
        for (int y = -20; y <= 20; y++)
        {
            for (int x = -20; x <= 20; x++)
            {
                count += sim.BiomeAt(x, y) == Bloom ? 1 : 0;
            }
        }

        return count;
    }

    /// <summary>Souš, hnízdo v (0, 0).</summary>
    private sealed class NestTerrain : ITerrain
    {
        public byte BiomeAt(int x, int y) => x == 0 && y == 0 ? (byte)Nest : (byte)Land;
    }

    private static Simulation World(double calmGreenShare = 0)
    {
        var biomes = new[]
        {
            TestContent.WaterBiome(), TestContent.LandBiome("moss"), TestContent.LandBiome("nest"), TestContent.LandBiome("bloom"),
        };
        var resources = new[] { new Resource("food", new RgbColor(200, 180, 60), 0, BaseStorage: 1_000) };
        var ground = new[] { false, true, false, true };

        BuildingDef Def(string id, FloraRole role = FloraRole.None, Shelter[]? shelters = null) => new(
            id, "test", new RgbColor(1, 1, 1), 1, 1,
            WorkerSlots: 0, HousingCapacity: 2, BuildCost: Array.Empty<ResourceAmount>(),
            Recipe: null, AllowedBiomes: ground, StorageBonus: Array.Empty<ResourceAmount>(),
            AutoBuild: false, Buildable: true, UpgradesToIndex: -1,
            UpgradeCost: Array.Empty<ResourceAmount>(), PowerSupply: 0, PowerDemand: 0,
            SheltersOrNull: shelters, FloraRole: role);

        var buildings = new[]
        {
            Def("hut"),
            Def("pruner", FloraRole.Pruner, new[] { new Shelter(0, 4) }),
            Def("life_tree", FloraRole.None, new[] { new Shelter(0, 5) }),
            Def("spore_barrier", FloraRole.Barrier),
        };

        var spreadOn = new[] { false, true, false, false };
        var gameplay = TestContent.DefaultGameplay with { FoodPerPersonPerSecond = 0, PopulationGrowthPerSecond = 0 };
        var content = TestContent.Build(biomes, 1, resources, buildings, gameplay)
            .WithHazards(new HazardCatalog(new[]
            {
                new HazardDef("flora", HazardBehavior.FloraSpread, null, Flora: new FloraRule(
                    FirstAfterSeconds: Interval, IntervalSeconds: Interval, WarningSeconds: 2, NestBiomeIndex: Nest,
                    BloomBiomeIndex: Bloom, SpreadOn: spreadOn, SpreadChance: 0.6, ActiveRadius: 40, WrapSeconds: 30,
                    MinBuildings: 1, CalmGreenShare: calmGreenShare)),
            }));
        return new Simulation(content, new NestTerrain(), seed: 3);
    }

    private static int Place(Simulation sim, int defIndex, int x, int y)
    {
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuildingFree(defIndex, x, y));
        sim.DebugCompleteConstruction();
        return sim.Buildings.Length - 1;
    }

    private static Simulation Reload(Simulation sim)
    {
        var serializer = new SaveGameSerializer();
        using var stream = new MemoryStream();
        serializer.Write(stream, sim, new SaveMetadata(sim.Seed, "s", "test", DateTime.UtcNow));
        stream.Position = 0;
        return serializer.Read(stream, sim.Content).Simulation;
    }

    private static void Run(Simulation sim, double seconds)
    {
        long ticks = (long)(seconds * Simulation.TicksPerSecond);
        for (long i = 0; i < ticks; i++)
        {
            sim.Tick();
        }
    }
}
