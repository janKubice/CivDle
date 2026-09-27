using CivDle.Core.Content;
using CivDle.Core.Save;
using CivDle.Core.Sim;
using CivDle.Core.Tests.Support;
using CivDle.Core.World;
using Xunit;

namespace CivDle.Core.Tests.Sim;

/// <summary>
/// Souostroví (svety-design.md 4.3): příliv zaplavuje přílivové mělčiny podle
/// jejich výšky, budovy bez kůlů na nich vypadnou a s odlivem se vrátí,
/// chrám přílivu drží mělčinu suchou, příboj bije jen pobřeží a přístaviště
/// trajektu napojí ostrov bez silnice.
/// </summary>
public class TideTests
{
    private const int Hut = 0;
    private const int StiltHut = 1;
    private const int Temple = 2;
    private const int FerryDock = 3;

    private const int Water = 0;
    private const int Land = 1;
    private const int Flat = 2;

    /// <summary>Celý cyklus příliv–odliv (herní sekundy).</summary>
    private const double Period = 100;

    [Fact]
    public void TheTideRisesAndEbbsWithTime()
    {
        var rule = new TideRule(Period, Flat, -0.05, 0.05);

        Assert.Equal(0.0, rule.LevelAt(0), 9);          // hra začíná odlivem
        Assert.Equal(1.0, rule.LevelAt(Period / 2), 9); // nejvyšší příliv
        Assert.Equal(0.0, rule.LevelAt(Period), 9);
        Assert.True(rule.IsRising(10));
        Assert.False(rule.IsRising(60));
    }

    [Fact]
    public void LowerFlatsFloodFirst()
    {
        var sim = World();
        Run(sim, seconds: 25); // hladina 0,5

        Assert.True(sim.IsFloodedAt(2, 5), "nízko položená mělčina má být pod vodou");
        Assert.False(sim.IsFloodedAt(8, 5), "vysoko položená mělčina má být ještě suchá");
        Assert.False(sim.IsFloodedAt(15, 5), "souš příliv nezaplaví");
        Assert.Equal(1.0, sim.TideHeightAt(15, 5));
    }

    [Fact]
    public void AHutOnTheFlatFloodsAndComesBackButStiltsStand()
    {
        var sim = World();
        int hut = Place(sim, Hut, 2, 5);
        int stilts = Place(sim, StiltHut, 3, 5);
        int dry = Place(sim, Hut, 15, 5);

        Run(sim, seconds: 30);
        Assert.Equal(BuildingStall.Flooded, sim.Buildings[hut].Stall);
        Assert.NotEqual(BuildingStall.Flooded, sim.Buildings[stilts].Stall);
        Assert.NotEqual(BuildingStall.Flooded, sim.Buildings[dry].Stall);

        Run(sim, seconds: 65); // zase odliv
        Assert.Equal(0, sim.Buildings[hut].DisabledTicks);
        Assert.NotEqual(BuildingStall.Flooded, sim.Buildings[hut].Stall);
    }

    [Fact]
    public void TheTempleKeepsTheFlatsAroundItDry()
    {
        var sim = World();
        Place(sim, Temple, 3, 8);
        int hut = Place(sim, Hut, 2, 6);

        Run(sim, seconds: 50); // nejvyšší příliv

        Assert.False(sim.IsFloodedAt(2, 6));
        Assert.NotEqual(BuildingStall.Flooded, sim.Buildings[hut].Stall);
        Assert.True(sim.IsFloodedAt(2, 30), "mimo dosah chrámu je mělčina pod vodou");
    }

    [Fact]
    public void AFloodIsNotSavedAsDamage()
    {
        // Příliv je funkce času: po načtení ho příští kontrola (do sekundy)
        // obnoví sama. Kdyby se uložil jako výpadek, vrátil by se jako
        // poškození útokem.
        var sim = World();
        int hut = Place(sim, Hut, 2, 5);
        Run(sim, seconds: 30);
        Assert.Equal(BuildingStall.Flooded, sim.Buildings[hut].Stall);

        var reloaded = Reload(sim);

        Assert.Equal(0, reloaded.Buildings[hut].DisabledTicks);
    }

    [Fact]
    public void TheGovernorKeepsHutsWithoutStiltsOffTheFlats()
    {
        // Lidé potřebují bydlení; město stojí na kraji souše u mělčiny. Chýše
        // bez kůlů smí guvernér postavit jen na souš, na mělčinu jen kůlové.
        var sim = World(governor: true);
        Place(sim, Hut, 10, 5);

        Run(sim, seconds: 120);

        var built = sim.Buildings.ToArray().Skip(1).ToList();
        Assert.True(built.Count >= 5, $"guvernér postavil jen {built.Count} domů");
        Assert.DoesNotContain(built, b => b.DefIndex == Hut && b.X < 10);
    }

    [Fact]
    public void TheSurfOnlyHitsTheCoast()
    {
        var sim = World(surf: true);
        int coast = Place(sim, Hut, 20, 0);   // pod ní (y = −1) je moře
        int inland = Place(sim, Hut, 20, 12);
        for (int i = 0; i < 4; i++)
        {
            Place(sim, Hut, 30 + i * 2, 12);
        }

        Run(sim, seconds: 32); // příboj 10–30 s přes celou mapu

        Assert.Equal(DisableCause.Burial, sim.Buildings[coast].DisabledCause);
        Assert.True(sim.Buildings[coast].DisabledTicks > 0, "pobřežní chýši má příboj zasáhnout");
        Assert.Equal(0, sim.Buildings[inland].DisabledTicks);
    }

    [Fact]
    public void AFerryDockConnectsTheIslandWithoutARoad()
    {
        var sim = World();
        Assert.Equal(PlacementResult.Ok, sim.TryBuildRoad(40, 5)); // silnice jinde ve městě — mechanika běží
        int island = Place(sim, Hut, 20, 20);
        Assert.False(sim.IsBuildingConnected(island));

        Place(sim, FerryDock, 22, 22);

        Assert.True(sim.IsBuildingConnected(island), "přístaviště má ostrov napojit");
    }

    // ----- svět -----

    /// <summary>
    /// Moře nad y = 0, mělčina v pásu x 0–9 (výška roste s x: 0,0 … 0,9),
    /// souš od x = 10.
    /// </summary>
    private sealed class ShoreTerrain : ITerrain
    {
        public byte BiomeAt(int x, int y) => y < 0 ? (byte)Water : x is >= 0 and < 10 ? (byte)Flat : (byte)Land;

        public float ElevationAt(int x, int y) => y < 0 ? 0.3f : 0.45f + 0.01f * Math.Clamp(x, 0, 20);
    }

    private static Simulation World(bool surf = false, bool governor = false)
    {
        var biomes = new[] { TestContent.WaterBiome(), TestContent.LandBiome("sand"), TestContent.LandBiome("tidal_flat") };
        var resources = new[] { new Resource("food", new RgbColor(200, 180, 60), 0, BaseStorage: 1_000) };
        var anywhere = new[] { false, true, true };

        BuildingDef Def(string id, int footprint = 1, bool stilted = false, Shelter[]? shelters = null, int ferry = 0,
            bool autoBuild = false) => new(
            id, "test", new RgbColor(1, 1, 1), footprint, footprint,
            WorkerSlots: 0, HousingCapacity: 2, BuildCost: Array.Empty<ResourceAmount>(),
            Recipe: null, AllowedBiomes: anywhere, StorageBonus: Array.Empty<ResourceAmount>(),
            AutoBuild: autoBuild, Buildable: true, UpgradesToIndex: -1,
            UpgradeCost: Array.Empty<ResourceAmount>(), PowerSupply: 0, PowerDemand: 0,
            SheltersOrNull: shelters, Stilted: stilted, FerryReach: ferry);

        var buildings = new[]
        {
            Def("hut", autoBuild: governor),
            Def("stilt_hut", stilted: true, autoBuild: governor),
            Def("tide_temple", shelters: new[] { new Shelter(0, 5) }),
            Def("ferry_dock", ferry: 6),
        };

        var hazards = new List<HazardDef>
        {
            new("tide", HazardBehavior.Tides, null, new TideRule(Period, Flat, -0.05, 0.05)),
        };

        if (surf)
        {
            hazards.Add(new HazardDef("surf", HazardBehavior.WeatherBurial, new BurialRule(
                FirstAfterSeconds: 10, IntervalSeconds: 500, IntervalJitter: 0, WarningSeconds: 5,
                SweepSeconds: 20, BandTiles: 400, BurySeconds: 30, WeatherIndex: -1, SolarDim: 1, MinBuildings: 1,
                CoastTiles: 2)));
        }

        var gameplay = TestContent.DefaultGameplay with { FoodPerPersonPerSecond = 0, PopulationGrowthPerSecond = 0 };
        if (governor)
        {
            gameplay = gameplay with
            {
                StartingPopulation = 60,
                AutoBuild = new AutoBuildConfig(IntervalTicks: 5, SearchRadius: 6, PopulationHeadroom: 2),
                GovernorOrNull = new GovernorConfig(
                    true, StorageGoalConfig.Off, SupplyGoalConfig.Off, SupplyGoalConfig.Off,
                    LandscapeGoalConfig.Off, PowerGoalConfig.Off),
            };
        }

        var content = TestContent.Build(biomes, 1, resources, buildings, gameplay)
            .WithHazards(new HazardCatalog(hazards));
        return new Simulation(content, new ShoreTerrain(), seed: 3);
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
