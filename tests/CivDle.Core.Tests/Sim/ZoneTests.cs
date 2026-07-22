using CivDle.Core.Content;
using CivDle.Core.Sim;
using CivDle.Core.Tests.Support;
using CivDle.Core.World;
using Xunit;

namespace CivDle.Core.Tests.Sim;

/// <summary>
/// Zóny (automatizace, stupeň 3): hráč označí obdélník daného typu a
/// <see cref="ZoneFillSystem"/> ho sám zaplňuje budovami — v hranicích zóny,
/// za normální cenu (dojdou suroviny → přestane), max jedna budova za interval.
/// </summary>
public class ZoneTests
{
    private static ITerrain Grass() => new UniformTerrain(1);

    /// <summary>Obsah: 1×1 buildable budova „hut" (cena 1 dřevo, bez výroby) a typ zóny,
    /// který se jí zaplňuje. Auto-stavba na interval 1, ať se plnění projeví hned.</summary>
    private static GameContent ZoneContent(int startWood = 1000)
    {
        var biomes = new[] { TestContent.WaterBiome(), TestContent.LandBiome("grass") };
        var resources = new[] { new Resource("wood", new RgbColor(140, 90, 40), StartAmount: startWood, BaseStorage: 5000) };
        var hut = TestContent.SimpleBuilding("hut", biomes.Length); // 1×1, buildable, cena 1 dřevo, ne autoBuild
        var zoneTypes = new[] { new ZoneTypeDef("res", new RgbColor(0, 0, 200), new[] { 0 }) };

        // Populace i jídlo zmražené → do plnění zón nezasahuje růst; auto-stavba jede každý tik.
        var gameplay = TestContent.DefaultGameplay with
        {
            AutoBuild = new AutoBuildConfig(IntervalTicks: 1, SearchRadius: 6, PopulationHeadroom: 2),
            PopulationGrowthPerSecond = 0,
            FoodPerPersonPerSecond = 0,
        };
        return TestContent.Build(biomes, 1, resources, new[] { hut }, gameplay, zoneTypes: zoneTypes);
    }

    private static void RunTicks(Simulation sim, int ticks)
    {
        for (int i = 0; i < ticks; i++)
        {
            sim.Tick();
        }
    }

    [Fact]
    public void ZoneFill_PlacesBuildingsInsideZone()
    {
        var sim = new Simulation(ZoneContent(), Grass());
        Assert.True(sim.AddZone(0, 2, 2, 3, 3)); // 3×3 obytná zóna v (2,2)

        RunTicks(sim, 200);

        Assert.True(sim.Buildings.Length > 0, "zóna se má sama zaplňovat budovami");
        foreach (var b in sim.Buildings)
        {
            Assert.InRange(b.X, 2, 4); // uvnitř [2..4] × [2..4]
            Assert.InRange(b.Y, 2, 4);
        }
    }

    [Fact]
    public void ZoneFill_DoesNothingWithoutZones()
    {
        var sim = new Simulation(ZoneContent(), Grass());
        RunTicks(sim, 100);
        Assert.Empty(sim.Buildings.ToArray()); // bez zóny (a bez autoBuild budov) nic nevznikne
    }

    [Fact]
    public void ZoneFill_StopsWhenResourcesRunOut()
    {
        // Dřevo jen na 2 budovy (cena 1 každá), zóna pojme víc → zaplní se právě 2.
        var sim = new Simulation(ZoneContent(startWood: 2), Grass());
        Assert.True(sim.AddZone(0, 2, 2, 4, 4)); // 16 dlaždic

        RunTicks(sim, 100);

        Assert.Equal(2, sim.Buildings.Length);
    }

    [Fact]
    public void AddZone_RejectsBadTypeAndClampsSize()
    {
        var sim = new Simulation(ZoneContent(), Grass());

        Assert.False(sim.AddZone(5, 0, 0, 2, 2)); // typ mimo rozsah (existuje jen index 0)
        Assert.False(sim.AddZone(0, 0, 0, 0, 2)); // nulová šířka
        Assert.True(sim.AddZone(0, 0, 0, 1000, 1000)); // rozměr se ořízne
        Assert.Equal(Simulation.MaxZoneDimension, sim.Zones[0].Width);
        Assert.Equal(Simulation.MaxZoneDimension, sim.Zones[0].Height);
    }

    [Fact]
    public void RemoveZoneAt_RemovesContainingZone()
    {
        var sim = new Simulation(ZoneContent(), Grass());
        Assert.True(sim.AddZone(0, 2, 2, 3, 3));

        Assert.True(sim.RemoveZoneAt(3, 3)); // dlaždice uvnitř
        Assert.Empty(sim.Zones);
        Assert.False(sim.RemoveZoneAt(3, 3)); // už není co smazat
    }
}
