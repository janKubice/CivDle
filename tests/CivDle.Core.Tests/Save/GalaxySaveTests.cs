using CivDle.Core.Content;
using CivDle.Core.Galaxy;
using CivDle.Core.Save;
using CivDle.Core.Sim;
using CivDle.Core.Tests.Support;
using CivDle.Core.World;
using Xunit;

namespace CivDle.Core.Tests.Save;

/// <summary>
/// Save jako kontejner galaxie (svety-design.md 7.7): galaxie přežije uložení,
/// starý save se načte jako galaxie s jedinou Domovinou a poškozená galaxie
/// se radši nenačte, než aby hra přišla o kolonie.
/// </summary>
public class GalaxySaveTests
{
    [Fact]
    public void AGalaxySurvivesTheRoundTrip()
    {
        var content = TestData.LoadRealContent();
        var sim = NewHome(content);
        var galaxy = GalaxyState.NewWithHome(sim);
        galaxy.GateOpened = true;
        galaxy.Ship = new ColonyShipState("frost") { StageIndex = 2 };
        galaxy.Ship.Invested["steel"] = 1234.5;
        var dune = galaxy.Add(new WorldRecord("dune", 99)
        {
            FoundedAtSeconds = 10,
            LeftAtSeconds = 250,
            LandingX = 12,
            LandingY = -7,
            Snapshot = new byte[] { 1, 2, 3, 4, 5 },
            Summary = new WorldSummary(
                new[] { "glass", "spice" }, new[] { 10.0, 2.0 }, new[] { 0.5, -0.1 }, new[] { 500.0, 80.0 }, 120, 200, 0.3),
        });
        dune.Stars.Add("dune_pop");
        dune.PendingDelta["glass"] = -40;

        var loaded = RoundTrip(sim, galaxy, content).Galaxy!;

        Assert.True(loaded.GateOpened);
        Assert.Equal(WorldScope.HomeId, loaded.ActiveWorldId);
        Assert.Equal(galaxy.ActiveEnteredAtSeconds, loaded.ActiveEnteredAtSeconds);
        Assert.Equal("frost", loaded.Ship!.TargetWorldId);
        Assert.Equal(2, loaded.Ship.StageIndex);
        Assert.Equal(1234.5, loaded.Ship.Invested["steel"]);

        var loadedDune = loaded.Records["dune"];
        Assert.Equal(99, loadedDune.Seed);
        Assert.Equal(250, loadedDune.LeftAtSeconds);
        Assert.Equal((12, -7), (loadedDune.LandingX, loadedDune.LandingY));
        Assert.Equal(new byte[] { 1, 2, 3, 4, 5 }, loadedDune.Snapshot);
        Assert.Equal(new[] { "dune_pop" }, loadedDune.Stars);
        Assert.Equal(-40, loadedDune.PendingDelta["glass"]);
        Assert.Equal(new[] { "glass", "spice" }, loadedDune.Summary!.ResourceIds);
        Assert.Equal(-0.1, loadedDune.Summary.FlowOf("spice"));
        Assert.Equal(200, loadedDune.Summary.Housing);
        Assert.Null(loaded.Records[WorldScope.HomeId].Snapshot); // aktivní svět žije v těle savu
    }

    [Fact]
    public void ASaveWithoutAGalaxyLoadsWithoutOne()
    {
        var content = TestData.LoadRealContent();
        var sim = NewHome(content);

        var loaded = RoundTrip(sim, null, content);

        Assert.Null(loaded.Galaxy); // volající založí galaxii s jedinou Domovinou
        Assert.Equal(sim.TickCount, loaded.Simulation.TickCount);
    }

    [Fact]
    public void AFormat16SaveStillLoads()
    {
        var content = TestData.LoadRealContent();
        var sim = NewHome(content);
        sim.AddResource(0, 33);
        var stream = new MemoryStream();
        new SaveGameSerializer().Write(stream, sim, Metadata());

        var old = SaveCompatibilityTests.DowngradeToV16(stream.ToArray());
        var loaded = new SaveGameSerializer().Read(new MemoryStream(old), GalaxyContent.HomeOnly(content));

        Assert.Equal(sim.GetResource(0), loaded.Simulation.GetResource(0), 6);
        Assert.True(loaded.Simulation.Content.World.IsHome);
        Assert.Null(loaded.Galaxy);
    }

    [Fact]
    public void AColonySaveNeedsItsWorldsContent()
    {
        // Save, jehož aktivní svět je kolonie, se bez dat kolonie nenačte —
        // a řekne proč, místo aby postavil Duně terén Domoviny.
        var home = TestData.LoadRealContent();
        var dune = home.WithWorld(WorldProfile.Home with { Id = "dune" });
        var sim = new Simulation(dune, new UniformTerrain(dune.Biomes.IndexOf("grassland")), 3);
        var stream = new MemoryStream();
        new SaveGameSerializer().Write(stream, sim, Metadata());
        stream.Position = 0;

        var ex = Assert.Throws<SaveLoadException>(
            () => new SaveGameSerializer().Read(stream, GalaxyContent.HomeOnly(home)));

        Assert.Contains("dune", ex.Message);
    }

    [Fact]
    public void ACorruptGalaxyFailsInsteadOfLosingTheColonies()
    {
        var content = TestData.LoadRealContent();
        var sim = NewHome(content);
        var stream = new MemoryStream();
        new SaveGameSerializer().Write(stream, sim, Metadata());

        var broken = SaveCompatibilityTests.WithSection(stream.ToArray(), "galaxy", w =>
        {
            w.Write(1);
            w.Write("home");
            w.Write(12345); // useknuté uprostřed
        });

        Assert.Throws<SaveLoadException>(
            () => new SaveGameSerializer().Read(new MemoryStream(broken), GalaxyContent.HomeOnly(content)));
    }

    [Fact]
    public void TheStoreSavesAndLoadsTheGalaxy()
    {
        var content = TestData.LoadRealContent();
        var sim = NewHome(content);
        var galaxy = GalaxyState.NewWithHome(sim);
        galaxy.GateOpened = true;
        string dir = Path.Combine(Path.GetTempPath(), "civdle-galaxy-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new SaveStore(Path.Combine(dir, "save.civdle"));
            Assert.True(store.TrySave(sim, Metadata(), galaxy));

            var loaded = store.TryLoad(GalaxyContent.HomeOnly(content), out string? error);

            Assert.Null(error);
            Assert.True(loaded!.Galaxy!.GateOpened);
            Assert.NotNull(store.TryLoad(content, out _)); // stará cesta pro nástroje funguje dál
        }
        finally
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, true);
            }
        }
    }

    private static LoadedSave RoundTrip(Simulation sim, GalaxyState? galaxy, GameContent content)
    {
        var stream = new MemoryStream();
        new SaveGameSerializer().Write(stream, sim, Metadata(), galaxy);
        stream.Position = 0;
        return new SaveGameSerializer().Read(stream, GalaxyContent.HomeOnly(content));
    }

    private static Simulation NewHome(GameContent content)
    {
        var preset = content.WorldGen.Presets[content.WorldGen.DefaultPresetIndex];
        var sim = new Simulation(content, new ProceduralTerrain(content.Biomes, preset, 42), 42);
        for (int i = 0; i < 30; i++)
        {
            sim.Tick();
        }

        return sim;
    }

    private static SaveMetadata Metadata()
    {
        return new SaveMetadata(42, "medium", TestData.LoadRealContent().WorldGen.Presets[TestData.LoadRealContent().WorldGen.DefaultPresetIndex].Id, DateTime.UtcNow);
    }
}
