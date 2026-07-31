using CivDle.Core.Content;
using CivDle.Core.Sim;
using CivDle.Core.World;
using CivDle.Rendering;
using CivDle.Rendering.Effects;
using Xunit;

namespace CivDle.Ui.Tests;

/// <summary>
/// Doprava po silnicích: kulisa, která má město rozhýbat — a nesmí přitom
/// sáhnout do simulace ani zaplavit rám.
///
/// <para>Testuje se to, co by se v běžném hraní projevilo až jako chyba:
/// bez silnic nesmí nic vyjet, provoz má růst s městem (ale mít strop),
/// při oddálení má zmizet, a hlavně — vozidla nikdy nesmí nic změnit
/// v simulaci (render → sim, ne obráceně).</para>
///
/// <para>Běží headless: <c>Update</c> na grafiku nesahá, kreslení se netestuje.</para>
/// </summary>
public sealed class TrafficSystemTests
{
    private static Camera2D NewCamera()
    {
        var camera = new Camera2D();
        camera.SetViewport(1920, 1080);
        camera.Position = new Microsoft.Xna.Framework.Vector2(0, 0);
        return camera;
    }

    private static Simulation RoadWorld(out GameContent content, int length = 40, double population = 2000)
    {
        content = new ContentLoader().LoadFrom(Path.Combine(AppContext.BaseDirectory, "data"));
        var sim = new Simulation(content, new UniformTerrain((byte)content.Biomes.IndexOf("grassland")));

        // Rovná ulice napříč výřezem kamery, ať je kde jezdit.
        for (int x = -length / 2; x < length / 2; x++)
        {
            sim.AddRoadTileForTest(x, 0);
        }

        sim.SetPopulationForTest(population);
        return sim;
    }

    private static void Run(TrafficSystem traffic, Camera2D camera, Simulation sim, int frames, float dt = 1f / 60f)
    {
        for (int i = 0; i < frames; i++)
        {
            traffic.Update(dt, camera, sim);
        }
    }

    [Fact]
    public void WithoutRoadsNothingDrives()
    {
        var content = new ContentLoader().LoadFrom(Path.Combine(AppContext.BaseDirectory, "data"));
        var sim = new Simulation(content, new UniformTerrain((byte)content.Biomes.IndexOf("grassland")));
        sim.SetPopulationForTest(5000);
        var traffic = new TrafficSystem(content);

        Run(traffic, NewCamera(), sim, 240);

        Assert.Equal(0, traffic.ActiveCount);
    }

    [Fact]
    public void RoadsAndPeopleProduceTraffic()
    {
        var sim = RoadWorld(out var content);
        var traffic = new TrafficSystem(content);

        Run(traffic, NewCamera(), sim, 600);

        Assert.True(traffic.ActiveCount > 0, "Po ulici plného města by mělo něco jezdit.");
    }

    [Fact]
    public void AVillageHasLessTrafficThanACity()
    {
        // Provoz je nejlevnější způsob, jak dát velikost města najevo.
        var village = RoadWorld(out var content, population: 80);
        var city = RoadWorld(out _, population: 4000);

        var villageTraffic = new TrafficSystem(content);
        var cityTraffic = new TrafficSystem(content);
        Run(villageTraffic, NewCamera(), village, 900);
        Run(cityTraffic, NewCamera(), city, 900);

        Assert.True(cityTraffic.ActiveCount > villageTraffic.ActiveCount,
            $"Velkoměsto ({cityTraffic.ActiveCount}) nemá hustší provoz než vesnice ({villageTraffic.ActiveCount}).");
    }

    [Fact]
    public void TrafficHasACeiling()
    {
        // Bez stropu by miliónové město vykreslovalo auta místo hry.
        var sim = RoadWorld(out var content, population: 10_000_000);
        var traffic = new TrafficSystem(content);

        Run(traffic, NewCamera(), sim, 1800);

        Assert.InRange(traffic.ActiveCount, 1, 40);
    }

    [Fact]
    public void ZoomingOutClearsTheRoads()
    {
        // Z výšky je vozidlo pixel — počítat ho je zbytečná práce.
        var sim = RoadWorld(out var content);
        var traffic = new TrafficSystem(content);
        var camera = NewCamera();
        Run(traffic, camera, sim, 600);
        Assert.True(traffic.ActiveCount > 0);

        while (camera.Zoom > 0.3f)
        {
            camera.ZoomAt(new Microsoft.Xna.Framework.Vector2(960, 540), 0.8f);
        }

        traffic.Update(1f / 60f, camera, sim);

        Assert.Equal(0, traffic.ActiveCount);
    }

    [Fact]
    public void TrafficNeverTouchesTheSimulation()
    {
        // Tohle je ta hranice, kvůli které je doprava v renderu: kulisa nesmí
        // hnout ani surovinou, ani budovou, ani tikem.
        var sim = RoadWorld(out var content);
        var traffic = new TrafficSystem(content);

        long ticksBefore = sim.TickCount;
        int buildingsBefore = sim.Buildings.Length;
        int roadsBefore = sim.RoadTiles.Count;
        double woodBefore = sim.GetResource(content.Resources.IndexOf("wood"));

        Run(traffic, NewCamera(), sim, 900);

        Assert.Equal(ticksBefore, sim.TickCount);
        Assert.Equal(buildingsBefore, sim.Buildings.Length);
        Assert.Equal(roadsBefore, sim.RoadTiles.Count);
        Assert.Equal(woodBefore, sim.GetResource(content.Resources.IndexOf("wood")), 6);
    }

    [Fact]
    public void ADeadEndDoesNotStallTheSystem()
    {
        // Jedna osamocená dlaždice: vozidlo nemá kudy vyjet a systém to musí
        // přežít bez zacyklení i bez pádu.
        var content = new ContentLoader().LoadFrom(Path.Combine(AppContext.BaseDirectory, "data"));
        var sim = new Simulation(content, new UniformTerrain((byte)content.Biomes.IndexOf("grassland")));
        sim.SetPopulationForTest(5000);
        sim.AddRoadTileForTest(0, 0);
        sim.AddRoadTileForTest(30, 30);
        sim.AddRoadTileForTest(-30, -30);
        var traffic = new TrafficSystem(content);

        Run(traffic, NewCamera(), sim, 600);

        Assert.Equal(0, traffic.ActiveCount);
    }
}
