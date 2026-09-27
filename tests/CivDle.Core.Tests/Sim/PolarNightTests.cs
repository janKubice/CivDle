using CivDle.Core.Content;
using CivDle.Core.Save;
using CivDle.Core.Sim;
using CivDle.Core.Tests.Support;
using CivDle.Core.World;
using Xunit;

namespace CivDle.Core.Tests.Sim;

/// <summary>
/// Polární noc na Mrazu (svety-design.md 4.2): krátký den, v zimě víc tepla
/// a ★★ za zimu, ve které nic nezamrzlo.
/// </summary>
public class PolarNightTests
{
    private const int Heat = 1;
    private const int Stove = 0;
    private const int Shed = 1;

    /// <summary>Den trvá 100 tiků, období jeden den: léto, polární noc, léto…</summary>
    private const int TicksPerDay = 100;

    [Theory]
    [InlineData(0.0)]
    [InlineData(0.2)]
    [InlineData(0.5)]
    [InlineData(0.9)]
    public void ANormalDayKeepsTheClock(double clock)
    {
        Assert.Equal(clock, Simulation.SolarTime(clock, SeasonDef.NormalDaylight), 9);
    }

    [Fact]
    public void APolarDayIsShortAndKeepsNoonAndMidnight()
    {
        const double daylight = 0.15;

        Assert.Equal(0.0, Simulation.SolarTime(0.0, daylight), 9);
        Assert.Equal(0.5, Simulation.SolarTime(0.5, daylight), 9);
        Assert.Equal(0.25, Simulation.SolarTime(0.5 - daylight / 2, daylight), 9); // východ
        Assert.Equal(0.75, Simulation.SolarTime(0.5 + daylight / 2, daylight), 9); // západ

        // Dopoledne, kdy by normálně svítilo, je v polární noci ještě tma.
        double morning = Simulation.SolarTime(0.3, daylight);
        Assert.True(morning < 0.25 || morning > 0.75, $"v 0,3 má být noc, sluneční čas {morning}");

        // Čas běží dál jedním směrem — žádný skok zpátky během dne.
        double previous = Simulation.SolarTime(0.0, daylight);
        for (int i = 1; i < 1000; i++)
        {
            double solar = Simulation.SolarTime(i / 1000.0, daylight);
            double step = solar - previous;
            Assert.True(step > 0 || step < -0.9, $"sluneční čas couvl v {i / 1000.0}");
            previous = solar;
        }
    }

    [Fact]
    public void AWinterWithoutFrostCountsForTheStar()
    {
        // Pec 100 na deset kůln po 2 (v zimě 3): teplo stačí i v polární noci.
        var sim = Town(stoveSupply: 100);

        RunUntilDay(sim, 3); // léto, polární noc, a zase léto

        Assert.Equal(1, sim.WarmWinters);
        Assert.Equal(1, sim.EvaluateMetric(MetricKind.WarmWinters, 0));
    }

    [Fact]
    public void WinterWantsMoreHeatAndAFrozenNightDoesNotCount()
    {
        // Pec 12: v létě stačí na 60 % (nad prahem 50 %), v zimě chtějí
        // kůlny o polovinu víc tepla — 40 % — a zamrznou.
        var sim = Town(stoveSupply: 12);

        Run(sim, TicksPerDay / 2); // první den je léto
        Assert.DoesNotContain(sim.Buildings.ToArray(), b => b.Stall == BuildingStall.NetworkShortage);

        RunUntilDay(sim, 2); // polární noc
        Assert.Contains(sim.Buildings.ToArray(), b => b.Stall == BuildingStall.NetworkShortage);

        RunUntilDay(sim, 3);
        Assert.Equal(0, sim.WarmWinters);
    }

    [Fact]
    public void TheCountOfWarmWintersSurvivesASave()
    {
        // I svět bez přírodních jevů (sekce jevů nese i polární noci).
        var sim = Town(stoveSupply: 100);
        RunUntilDay(sim, 3);
        Run(sim, TicksPerDay); // uprostřed další polární noci

        var serializer = new SaveGameSerializer();
        using var stream = new MemoryStream();
        serializer.Write(stream, sim, new SaveMetadata(sim.Seed, "s", "test", DateTime.UtcNow));
        stream.Position = 0;
        var reloaded = serializer.Read(stream, sim.Content).Simulation;

        Assert.Equal(1, reloaded.WarmWinters);
        RunUntilDay(reloaded, 5);
        Assert.Equal(2, reloaded.WarmWinters);
    }

    private static Simulation Town(int stoveSupply)
    {
        var resources = new[] { new Resource("food", new RgbColor(200, 180, 60), 0, BaseStorage: 1000) };
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
            Def("stove", new NetworkUse(Heat, stoveSupply, 0, 0)),
            Def("shed", new NetworkUse(Heat, 0, 2, 0)),
        };

        var gameplay = TestContent.DefaultGameplay with
        {
            FoodPerPersonPerSecond = 0,
            PopulationGrowthPerSecond = 0,
            DayNight = TestContent.DefaultGameplay.DayNight with
            {
                DayLengthSeconds = TicksPerDay / Simulation.TicksPerSecond,
                StartTimeOfDay = 0,
            },
        };

        var seasons = new SeasonCalendar(
            new[]
            {
                Season("summer", daylight: 0.8, heatDemand: 1.0),
                Season("polar_night", daylight: 0.15, heatDemand: 1.5),
            },
            DaysPerSeason: 1,
            FuelResourceIndex: -1);

        var content = TestContent.Build(
                biomes: new[] { TestContent.WaterBiome(), TestContent.LandBiome("tundra") },
                resources: resources, buildings: buildings, gameplay: gameplay, seasons: seasons)
            .WithNetworks(new[] { new NetworkTypeDef("heat", 2, NetworkShortage.Cutoff, 0.5, new RgbColor(240, 140, 60)) });
        var sim = new Simulation(content, new UniformTerrain(1), seed: 3);

        // Všechno v jedné buňce sítě (8×8): pec a deset kůln kolem.
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuildingFree(Stove, 2, 2));
        for (int i = 0; i < 10; i++)
        {
            Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuildingFree(Shed, 1 + i % 5, 4 + i / 5));
        }

        sim.DebugCompleteConstruction();
        return sim;
    }

    private static SeasonDef Season(string id, double daylight, double heatDemand) => new(
        id, new RgbColor(255, 255, 255), TintAlpha: 0,
        FoodProductionMult: 1, HarvestMult: 1, GrowthMult: 1,
        FuelPerPersonPerSecond: 0, ColdGrowthMult: 1,
        Daylight: daylight, NetworkDemandOrNull: new[] { 1.0, heatDemand });

    private static void RunUntilDay(Simulation sim, long day)
    {
        for (int i = 0; i < TicksPerDay * 10 && sim.DayNumber < day; i++)
        {
            sim.Tick();
        }

        Assert.Equal(day, sim.DayNumber);
        Run(sim, 2); // přepočet sítě a počítadla proběhne na začátku dne
    }

    private static void Run(Simulation sim, int ticks)
    {
        for (int i = 0; i < ticks; i++)
        {
            sim.Tick();
        }
    }
}
