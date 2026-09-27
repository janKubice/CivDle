using CivDle.Core.Content;
using CivDle.Core.Save;
using CivDle.Core.Sim;
using CivDle.Core.Tests.Support;
using CivDle.Core.World;
using Xunit;

namespace CivDle.Core.Tests.Sim;

/// <summary>
/// Výheň (svety-design.md 4.4): láva vyteče z průduchu, teče z kopce, zalije
/// budovy v cestě (vrátí se samy), hráz ji zastaví, kanál odvede, ztuhne
/// v novou zem a guvernér zahradí dráhu dřív, než láva přijde.
/// </summary>
public class EruptionTests
{
    private const int Hut = 0;
    private const int Wall = 1;
    private const int Channel = 2;

    private const int Water = 0;
    private const int Land = 1;
    private const int Vent = 2;
    private const int Crust = 3;

    [Fact]
    public void TheLavaFlowsDownhillFromTheVent()
    {
        var sim = World();
        Place(sim, Hut, 12, 6);

        var path = sim.PredictedLavaPath;

        Assert.Equal((0, 0), (TileKey.X(path[0]), TileKey.Y(path[0])));
        Assert.True(path.Count > 20, $"láva má téct údolím daleko, dráha má {path.Count} dlaždic");
        for (int i = 1; i < path.Count; i++)
        {
            Assert.Equal(i, TileKey.X(path[i])); // z kopce, dnem údolí
            Assert.Equal(0, TileKey.Y(path[i]));
        }
    }

    [Fact]
    public void ABuildingInThePathIsScorchedTheLavaSetsAndTheBuildingComesBack()
    {
        var sim = World();
        int burnt = Place(sim, Hut, 8, 0);
        int spared = Place(sim, Hut, 8, 6);

        Run(sim, seconds: 32); // erupce 10–30 s

        Assert.Equal(DisableCause.Lava, sim.Buildings[burnt].DisabledCause);
        Assert.True(sim.Buildings[burnt].DisabledTicks > 0);
        Assert.Equal(BuildingStall.Scorched, sim.Buildings[burnt].Stall);
        Assert.Equal(0, sim.Buildings[spared].DisabledTicks);
        Assert.Equal(Crust, sim.BiomeAt(5, 0));      // dráha ztuhla v novou zem
        Assert.Equal(Vent, sim.BiomeAt(0, 0));       // průduch zůstane průduchem
        Assert.True(sim.LavaLandTiles > 20);
        Assert.Equal(sim.LavaLandTiles, sim.EvaluateMetric(MetricKind.LavaLand, -1));
        Assert.Equal(0, sim.CalmHazards);

        Run(sim, seconds: 62);
        Assert.Equal(0, sim.Buildings[burnt].DisabledTicks);
    }

    [Fact]
    public void AWallAcrossTheValleyStopsTheLava()
    {
        var sim = World();
        for (int y = -3; y <= 3; y++)
        {
            Place(sim, Wall, 6, y);
        }

        int hut = Place(sim, Hut, 10, 0);

        var path = sim.PredictedLavaPath;
        Assert.All(path, tile => Assert.True(TileKey.X(tile) < 6, "láva hrází neprojde"));

        Run(sim, seconds: 32);
        Assert.Equal(0, sim.Buildings[hut].DisabledTicks);
        Assert.Equal(1, sim.CalmHazards); // erupce, která nic nevyřadila
    }

    [Fact]
    public void AChannelLeadsTheLavaAwayAndItStopsAtTheEnd()
    {
        var sim = World();
        for (int y = 1; y <= 8; y++)
        {
            Place(sim, Channel, 1, y);
        }

        int hut = Place(sim, Hut, 8, 0);

        var path = sim.PredictedLavaPath;
        long last = path[^1];
        Assert.Equal((1, 8), (TileKey.X(last), TileKey.Y(last)));

        Run(sim, seconds: 32);
        Assert.Equal(0, sim.Buildings[hut].DisabledTicks);
    }

    [Fact]
    public void TheGovernorDamsThePathBeforeTheLavaComes()
    {
        var sim = World(governor: true);
        int hut = Place(sim, Hut, 12, 0);

        Run(sim, seconds: 8); // do erupce zbývají dvě sekundy

        Assert.Contains(sim.Buildings.ToArray(), b => b.DefIndex == Wall && b.Y == 0 && b.X is > 0 and < 12);
        Assert.DoesNotContain(sim.PredictedLavaPath, tile => TileKey.X(tile) == 12 && TileKey.Y(tile) == 0);

        Run(sim, seconds: 24);
        Assert.Equal(0, sim.Buildings[hut].DisabledTicks);
    }

    [Fact]
    public void NewLandAndScorchedBuildingsSurviveASave()
    {
        var sim = World();
        int burnt = Place(sim, Hut, 8, 0);
        Run(sim, seconds: 32);

        var reloaded = Reload(sim);

        Assert.Equal(sim.LavaLandTiles, reloaded.LavaLandTiles);
        Assert.Equal(DisableCause.Lava, reloaded.Buildings[burnt].DisabledCause);
        Assert.True(reloaded.Buildings[burnt].DisabledTicks > 0);
        Assert.Equal(Crust, reloaded.BiomeAt(5, 0));
    }

    // ----- svět -----

    /// <summary>
    /// Průduch v (0, 0), odtud údolí z kopce podél osy x (svahy do stran
    /// stoupají), od x = 30 moře.
    /// </summary>
    private sealed class SlopeTerrain : ITerrain
    {
        public byte BiomeAt(int x, int y) =>
            x == 0 && y == 0 ? (byte)Vent : x >= 30 ? (byte)Water : (byte)Land;

        public float ElevationAt(int x, int y) => 0.8f - 0.01f * Math.Clamp(x, -10, 40) + 0.005f * Math.Abs(y);
    }

    private static Simulation World(bool governor = false)
    {
        var biomes = new[]
        {
            TestContent.WaterBiome(), TestContent.LandBiome("ash"), TestContent.LandBiome("vent"), TestContent.LandBiome("basalt"),
        };
        var resources = new[] { new Resource("food", new RgbColor(200, 180, 60), 0, BaseStorage: 1_000) };
        var ground = new[] { false, true, false, true };

        BuildingDef Def(string id, LavaRole role = LavaRole.None) => new(
            id, "test", new RgbColor(1, 1, 1), 1, 1,
            WorkerSlots: 0, HousingCapacity: role == LavaRole.None ? 2 : 0, BuildCost: Array.Empty<ResourceAmount>(),
            Recipe: null, AllowedBiomes: ground, StorageBonus: Array.Empty<ResourceAmount>(),
            AutoBuild: false, Buildable: true, UpgradesToIndex: -1,
            UpgradeCost: Array.Empty<ResourceAmount>(), PowerSupply: 0, PowerDemand: 0, LavaRole: role);

        var buildings = new[] { Def("hut"), Def("lava_wall", LavaRole.Wall), Def("lava_channel", LavaRole.Channel) };

        var gameplay = TestContent.DefaultGameplay with { FoodPerPersonPerSecond = 0, PopulationGrowthPerSecond = 0 };
        if (governor)
        {
            gameplay = gameplay with
            {
                AutoBuild = new AutoBuildConfig(IntervalTicks: 5, SearchRadius: 6, PopulationHeadroom: 2),
                GovernorOrNull = new GovernorConfig(
                    true, StorageGoalConfig.Off, SupplyGoalConfig.Off, SupplyGoalConfig.Off,
                    LandscapeGoalConfig.Off, PowerGoalConfig.Off),
            };
        }

        var content = TestContent.Build(biomes, 1, resources, buildings, gameplay)
            .WithHazards(new HazardCatalog(new[]
            {
                new HazardDef("eruption", HazardBehavior.Eruptions, null, Eruption: new EruptionRule(
                    FirstAfterSeconds: 10, IntervalSeconds: 300, IntervalJitter: 0, WarningSeconds: 5, FlowSeconds: 20,
                    FlowLength: 40, LavaSeconds: 60, VentBiomeIndex: Vent, CrustBiomeIndex: Crust, SearchRadius: 40,
                    WeatherIndex: -1, SolarDim: 1, MinBuildings: 1)),
            }));
        return new Simulation(content, new SlopeTerrain(), seed: 3);
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
