using CivDle.Core.Content;
using CivDle.Core.Sim;
using CivDle.Core.Tests.Support;
using CivDle.Core.World;
using Xunit;

namespace CivDle.Core.Tests.Sim;

/// <summary>
/// Nebesa (svety-design.md 4.5): svět bez země. Loď přiveze kus paluby,
/// v oblacích nestojí nic (ani silnice), guvernér palubu rozšiřuje, když mu
/// dochází místo, hromosvody dodávají jen v bouřkovém pásu a pás budovy
/// zasahuje bleskem, ne zasypáním.
/// </summary>
public class SkyTests
{
    private const int Lander = 0;
    private const int Hut = 1;
    private const int Harvester = 2;
    private const int Workshop = 3;

    private const int Cloud = 1;
    private const int Deck = 2;

    private const int Fiber = 0;
    private const int Charge = 1; // síť, kterou napájí hromosvod (v testu místo proudu)

    [Fact]
    public void TheShipBringsADeckAndTheModuleLandsOnIt()
    {
        var sim = World();

        Assert.Equal(PlacementResult.Ok, sim.Land(0, 0));

        // Modul 2×2 na (0, 0), střed (1, 1), paluba ±3.
        Assert.Equal(Deck, sim.BiomeAt(-2, -2));
        Assert.Equal(Deck, sim.BiomeAt(4, 4));
        Assert.Equal(Cloud, sim.BiomeAt(5, 1));
        Assert.Equal(0, sim.TerraformedTiles); // palubu z lodi nepočítá hvězda hráče
        Assert.Equal(Lander, sim.Buildings[0].DefIndex);
    }

    [Fact]
    public void NothingStandsOnTheClouds()
    {
        var sim = World();
        sim.Land(0, 0);

        Assert.Equal(PlacementResult.WrongBiome, sim.CanBuildRoad(8, 0));
        Assert.Equal(PlacementResult.Ok, sim.CanBuildRoad(-2, 3));
        Assert.Equal(PlacementResult.WrongBiome, sim.CanPlace(Hut, 8, 0));
        Assert.Equal(PlacementResult.Ok, sim.CanPlace(Hut, -2, 3));
    }

    [Fact]
    public void TheGovernorLaysDeckWhenTheCityRunsOutOfRoom()
    {
        var sim = World(governor: true);
        sim.Land(0, 0);
        int before = DeckTiles(sim);

        Run(sim, seconds: 180);

        Assert.True(DeckTiles(sim) > before + 20, $"paluba měla růst: {before} → {DeckTiles(sim)}");
        Assert.True(sim.TerraformedTiles > 0);
        Assert.True(sim.Buildings.Length > 12, $"město mělo růst, má {sim.Buildings.Length} budov");
        Assert.All(sim.Buildings.ToArray(), b => Assert.Equal(Deck, sim.BiomeAt(b.X, b.Y)));
    }

    [Fact]
    public void AHarvesterOnlyGivesInAStormBand()
    {
        var sim = World(storm: true);
        sim.Land(0, 0);
        Place(sim, Harvester, -2, -2);
        Place(sim, Workshop, 3, 3);

        Run(sim, seconds: 5);
        Assert.False(sim.StormActive);
        Assert.Equal(0.0, sim.NetworkCoverageAt(Charge, 3, 3), 6);

        Run(sim, seconds: 10); // pás 10–30 s
        Assert.True(sim.StormActive);
        Assert.True(sim.NetworkCoverageAt(Charge, 3, 3) > 0.9);

        Run(sim, seconds: 20);
        Assert.False(sim.StormActive);
        Assert.Equal(0.0, sim.NetworkCoverageAt(Charge, 3, 3), 6);
    }

    [Fact]
    public void TheStormBandStrikesInsteadOfBurying()
    {
        var sim = World(storm: true);
        sim.Land(0, 0);
        int hut = Place(sim, Hut, -2, 3);

        Run(sim, seconds: 32);

        Assert.Equal(DisableCause.Burial, sim.Buildings[hut].DisabledCause); // v savu týž výpadek
        Assert.Equal(BuildingStall.Struck, sim.Buildings[hut].Stall);
    }

    [Fact]
    public void AFilledPresetIsOneBiomeWithItsPatches()
    {
        var biomes = new[] { TestContent.WaterBiome(), TestContent.LandBiome("cloud"), TestContent.LandBiome("storm_cloud") };
        var registry = new BiomeRegistry(biomes);
        var preset = new TerrainPreset("sky", 0.4f, 1, TestContent.Noise, TestContent.Noise,
            PatchesOrNull: new[] { new BiomePatch(2, new[] { false, true, false }, new NoiseSpec(4f, 2, 0.5f, 2f), 0.7f) },
            FillBiomeIndex: 1);
        var terrain = new ProceduralTerrain(registry, preset, seed: 5);

        int patched = 0;
        for (int y = -60; y < 60; y++)
        {
            for (int x = -60; x < 60; x++)
            {
                byte biome = terrain.BiomeAt(x, y);
                Assert.NotEqual(0, biome); // žádné moře
                patched += biome == 2 ? 1 : 0;
            }
        }

        Assert.InRange(patched, 1, 14_399); // záplaty leží přes výplň, ale nezakryjí všechno
    }

    // ----- svět -----

    private static int DeckTiles(Simulation sim)
    {
        int count = 0;
        for (int y = -40; y <= 40; y++)
        {
            for (int x = -40; x <= 40; x++)
            {
                count += sim.BiomeAt(x, y) == Deck ? 1 : 0;
            }
        }

        return count;
    }

    private static Simulation World(bool governor = false, bool storm = false)
    {
        var sky = TestContent.LandBiome("cloud_sea") with { Void = true };
        var biomes = new[] { TestContent.WaterBiome(), sky, TestContent.LandBiome("sky_deck") };
        var resources = new[]
        {
            new Resource("fiber", new RgbColor(40, 40, 40), 5_000, BaseStorage: 10_000),
            new Resource("food", new RgbColor(200, 180, 60), 1_000, BaseStorage: 10_000),
        };
        var deckOnly = new[] { false, false, true };

        BuildingDef Def(string id, int size, int housing, bool autoBuild, NetworkUse? use = null,
            SupplyTime time = SupplyTime.Always, Recipe? recipe = null) => new(
            id, "test", new RgbColor(1, 1, 1), size, size,
            WorkerSlots: 0, HousingCapacity: housing, BuildCost: new[] { new ResourceAmount(Fiber, 1) },
            Recipe: recipe, AllowedBiomes: deckOnly, StorageBonus: Array.Empty<ResourceAmount>(),
            AutoBuild: autoBuild, Buildable: true, UpgradesToIndex: -1,
            UpgradeCost: Array.Empty<ResourceAmount>(), PowerSupply: 0, PowerDemand: 0,
            NetworksOrNull: use is { } network ? new[] { network } : null, SupplyTime: time);

        var buildings = new[]
        {
            Def("lander", 2, 30, false) with { Buildable = false },
            Def("hut", 1, 4, governor),
            Def("harvester", 1, 0, false, new NetworkUse(Charge, 10, 0, 0), SupplyTime.Storm),
            Def("workshop", 1, 0, false, new NetworkUse(Charge, 0, 5, 0)),
        };

        var terraform = new[]
        {
            new TerraformDef("lay_deck", Deck, new[] { Cloud }, new[] { new ResourceAmount(Fiber, 1) }, -1),
        };

        var gameplay = TestContent.DefaultGameplay with { FoodPerPersonPerSecond = 0, PopulationGrowthPerSecond = 0 };
        if (governor)
        {
            gameplay = gameplay with
            {
                StartingPopulation = 200,
                AutoBuild = new AutoBuildConfig(IntervalTicks: 5, SearchRadius: 6, PopulationHeadroom: 2),
                GovernorOrNull = new GovernorConfig(
                    true, StorageGoalConfig.Off, SupplyGoalConfig.Off, SupplyGoalConfig.Off,
                    LandscapeGoalConfig.Off, PowerGoalConfig.Off),
            };
        }

        var content = TestContent.Build(biomes, 2, resources, buildings, gameplay, terraform: terraform)
            .WithNetworks(new[] { new NetworkTypeDef("charge", 2, NetworkShortage.Slowdown, 0, new RgbColor(200, 180, 255)) })
            .WithWorld(WorldProfile.Home with { Id = "gas_giant", LandingModuleIndex = Lander, Platform = new PlatformDef(Deck, 0, 3) });

        if (storm)
        {
            content = content.WithHazards(new HazardCatalog(new[]
            {
                new HazardDef("storm_band", HazardBehavior.WeatherBurial, new BurialRule(
                    FirstAfterSeconds: 10, IntervalSeconds: 500, IntervalJitter: 0, WarningSeconds: 5,
                    SweepSeconds: 20, BandTiles: 400, BurySeconds: 30, WeatherIndex: -1, SolarDim: 1, MinBuildings: 1,
                    Look: BurialLook.Storm)),
            }));
        }

        return new Simulation(content, new UniformTerrain(Cloud), seed: 3);
    }

    private static int Place(Simulation sim, int defIndex, int x, int y)
    {
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuildingFree(defIndex, x, y));
        sim.DebugCompleteConstruction();
        return sim.Buildings.Length - 1;
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
