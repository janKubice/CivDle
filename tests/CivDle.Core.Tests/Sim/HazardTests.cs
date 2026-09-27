using CivDle.Core.Content;
using CivDle.Core.Save;
using CivDle.Core.Sim;
using CivDle.Core.Tests.Support;
using CivDle.Core.World;
using Xunit;

namespace CivDle.Core.Tests.Sim;

/// <summary>
/// Přírodní jevy (svety-design.md 7.3) — písečná bouře na Duně. Hlídá se, co
/// slibuje návrh: rozvrh je funkce seedu, bouře varuje předem, zasype jen, co
/// stojí v pásu a nechrání ho větrolam, zasypané se samo vrátí a načtená hra
/// potká tutéž bouři.
/// </summary>
public class HazardTests
{
    private const int Hut = 0;
    private const int Fence = 1;
    private const int Well = 2;

    [Fact]
    public void TheScheduleDependsOnlyOnTheSeed()
    {
        var a = World(seed: 7, jitter: 0.5);
        var b = World(seed: 7, jitter: 0.5);
        var c = World(seed: 8, jitter: 0.5);

        var phasesA = new List<HazardPhase>();
        var phasesC = new List<HazardPhase>();
        for (int i = 0; i < 6_000; i++)
        {
            a.Tick();
            b.Tick();
            c.Tick();
            Assert.Equal(a.CurrentHazard, b.CurrentHazard);
            phasesA.Add(a.CurrentHazard.Phase);
            phasesC.Add(c.CurrentHazard.Phase);
        }

        Assert.Contains(HazardPhase.Active, phasesA);
        Assert.NotEqual(phasesA, phasesC); // jiný seed, jiné bouře
    }

    [Fact]
    public void NothingHappensBeforeTheFirstStorm()
    {
        var sim = World();
        Run(sim, seconds: 4); // první bouře za 10 s, varuje 5 s předem

        Assert.Equal(HazardPhase.Calm, sim.CurrentHazard.Phase);
    }

    [Fact]
    public void TheStormWarnsFirstAndTheSkyTurns()
    {
        var sim = World();
        Place(sim, Hut, 0, 0);
        Run(sim, seconds: 7);

        var view = sim.CurrentHazard;
        Assert.Equal(HazardPhase.Warning, view.Phase);
        Assert.InRange(view.SecondsToStart, 0.5, 5);
        Assert.Equal(sim.Content.Weather.IndexOf("sandstorm"), sim.CurrentWeatherIndex);
        Assert.Contains(Drain(sim), n => n.Kind == NotificationKind.Hazard && n.TitleKey == "hazard.sandstorm.warning");
    }

    [Fact]
    public void AStormBuriesWhatStandsInTheOpenAndTheFenceProtects()
    {
        var sim = World();
        int sheltered = Place(sim, Hut, 0, 0);
        Place(sim, Fence, 2, 0);
        int exposed = Place(sim, Hut, 60, 0);
        for (int i = 0; i < 4; i++)
        {
            Place(sim, Hut, 30 + i * 2, 20);
        }

        Run(sim, seconds: 32); // bouře 10–30 s přejde přes všechno

        Assert.Equal(0, sim.Buildings[sheltered].DisabledTicks);
        Assert.True(sim.Buildings[exposed].DisabledTicks > 0, "nechráněná chýše má být zasypaná");
        Assert.Equal(DisableCause.Burial, sim.Buildings[exposed].DisabledCause);
        Assert.Equal(BuildingStall.Buried, sim.Buildings[exposed].Stall);
        Assert.Equal(1, sim.HazardsWeathered);
        Assert.Equal(0, sim.CalmHazards);
        Assert.Contains(Drain(sim), n => n.TitleKey == "hazard.sandstorm.passed" && n.SubjectKey == "hazard.buried");

        Run(sim, seconds: 40); // zasypání trvá 30 s — lidé ji vyhrabou

        Assert.Equal(0, sim.Buildings[exposed].DisabledTicks);
        Assert.NotEqual(BuildingStall.Buried, sim.Buildings[exposed].Stall);
    }

    [Fact]
    public void AStormThatBuriesNothingCountsAsCalm()
    {
        var sim = World();
        for (int i = 0; i < 5; i++)
        {
            Place(sim, Hut, i * 2, 0);
        }

        Place(sim, Fence, 4, 2);
        Run(sim, seconds: 32);

        Assert.Equal(1, sim.HazardsWeathered);
        Assert.Equal(1, sim.CalmHazards);
        Assert.Equal(1, sim.EvaluateMetric(MetricKind.CalmHazards, -1));
    }

    [Fact]
    public void AStormOverAnEmptyDesertDoesNotCount()
    {
        var sim = World();
        Place(sim, Hut, 0, 0); // jedna chýše není město

        Run(sim, seconds: 32);

        Assert.Equal(0, sim.HazardsWeathered);
        Assert.Equal(0, sim.CalmHazards);
    }

    [Fact]
    public void ABuriedWellStopsGivingWater()
    {
        var sim = World();
        int well = Place(sim, Well, 0, 0);
        for (int i = 0; i < 5; i++)
        {
            Place(sim, Hut, 40 + i * 2, 0);
        }

        Run(sim, seconds: 32);

        Assert.True(sim.Buildings[well].DisabledTicks > 0);
        Assert.Equal(0.0, sim.NetworkSupplyAt(1, 0, 0), 6);
    }

    [Fact]
    public void TheSunDimsDuringTheStorm()
    {
        var sim = World();
        Assert.Equal(1.0, sim.SolarDim);

        Run(sim, seconds: 15);

        Assert.Equal(HazardPhase.Active, sim.CurrentHazard.Phase);
        Assert.Equal(0.25, sim.SolarDim, 6);
    }

    [Fact]
    public void ASaveInTheMiddleOfTheStormEndsTheSameWay()
    {
        var straight = World();
        var saved = World();
        foreach (var sim in new[] { straight, saved })
        {
            Place(sim, Hut, 0, 0);
            Place(sim, Hut, 60, 0);
            Place(sim, Fence, 2, 0);
            for (int i = 0; i < 4; i++)
            {
                Place(sim, Hut, -40, i * 2);
            }
        }

        Run(straight, seconds: 18);
        Run(saved, seconds: 18);
        var reloaded = Reload(saved);

        Run(straight, seconds: 20);
        Run(reloaded, seconds: 20);

        Assert.Equal(straight.HazardsWeathered, reloaded.HazardsWeathered);
        Assert.Equal(straight.CalmHazards, reloaded.CalmHazards);
        for (int i = 0; i < straight.Buildings.Length; i++)
        {
            Assert.Equal(straight.Buildings[i].DisabledTicks > 0, reloaded.Buildings[i].DisabledTicks > 0);
            Assert.Equal(straight.Buildings[i].DisabledCause, reloaded.Buildings[i].DisabledCause);
        }
    }

    [Fact]
    public void TheGovernorFencesWhatTheStormBuried()
    {
        var sim = World(governor: true);
        for (int i = 0; i < 5; i++)
        {
            Place(sim, Hut, i * 2, 0);
        }

        Run(sim, seconds: 32); // bouře vše zasype, guvernér teprve pak ví, kde chybí ochrana
        Run(sim, seconds: 10);

        var fence = sim.Buildings.ToArray().FirstOrDefault(b => b.DefIndex == Fence);
        Assert.Equal(Fence, fence.DefIndex);
        Assert.True(Math.Abs(fence.X - 4) <= 10 && Math.Abs(fence.Y) <= 10, $"větrolam má stát u chýší, stojí na {fence.X},{fence.Y}");
    }

    [Fact]
    public void TheHomeworldHasNoHazards()
    {
        var content = TestData.LoadRealContent();
        var sim = new Simulation(content, new UniformTerrain(content.Biomes.IndexOf("grassland")));

        Assert.Equal(0, content.Hazards.Count);
        Assert.Equal(HazardPhase.Calm, sim.CurrentHazard.Phase);
        Assert.Equal(1.0, sim.SolarDim);
    }

    // ----- svět -----

    private static Simulation World(long seed = 1, double jitter = 0, bool governor = false)
    {
        var biomes = new[] { TestContent.WaterBiome(), TestContent.LandBiome("sand") };
        var resources = new[] { new Resource("food", new RgbColor(200, 180, 60), 0, BaseStorage: 1_000) };
        var land = new[] { false, true };

        BuildingDef Def(string id, NetworkUse[]? networks = null, Shelter[]? shelters = null) => new(
            id, "test", new RgbColor(1, 1, 1), 1, 1,
            WorkerSlots: 0, HousingCapacity: id == "hut" ? 2 : 0,
            BuildCost: Array.Empty<ResourceAmount>(),
            Recipe: null, AllowedBiomes: land, StorageBonus: Array.Empty<ResourceAmount>(),
            AutoBuild: false, Buildable: true, UpgradesToIndex: -1,
            UpgradeCost: Array.Empty<ResourceAmount>(), PowerSupply: 0, PowerDemand: 0,
            NetworksOrNull: networks, SheltersOrNull: shelters);

        var buildings = new[]
        {
            Def("hut", new[] { new NetworkUse(1, 0, 1, 0) }),
            Def("fence", shelters: new[] { new Shelter(0, 6) }),
            Def("well", new[] { new NetworkUse(1, 10, 0, 0) }),
        };

        var weather = new[]
        {
            new WeatherDef("sandstorm", new[] { false, true }, true, 0.85, 45, 0, new RgbColor(190, 150, 80), 0.4, "sand"),
        };

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

        var content = TestContent.Build(biomes, 1, resources, buildings, gameplay, weather: weather)
            .WithNetworks(new[] { new NetworkTypeDef("water", 2, NetworkShortage.Slowdown, 0, new RgbColor(60, 160, 230)) })
            .WithHazards(new HazardCatalog(new[]
            {
                new HazardDef("sandstorm", HazardBehavior.WeatherBurial, new BurialRule(
                    FirstAfterSeconds: 10, IntervalSeconds: 200, IntervalJitter: jitter, WarningSeconds: 5,
                    SweepSeconds: 20, BandTiles: 30, BurySeconds: 30, WeatherIndex: 0, SolarDim: 0.25, MinBuildings: 5)),
            }));

        return new Simulation(content, new UniformTerrain(1), seed);
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

    private static List<GameNotification> Drain(Simulation sim)
    {
        var notes = new List<GameNotification>();
        while (sim.TryDequeueNotification(out var note))
        {
            notes.Add(note);
        }

        return notes;
    }

    private static Simulation Reload(Simulation sim)
    {
        var serializer = new SaveGameSerializer();
        using var stream = new MemoryStream();
        serializer.Write(stream, sim, new SaveMetadata(sim.Seed, "s", "test", DateTime.UtcNow));
        stream.Position = 0;
        return serializer.Read(stream, sim.Content).Simulation;
    }
}
