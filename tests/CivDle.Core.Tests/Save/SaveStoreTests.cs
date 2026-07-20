using CivDle.Core.Save;
using CivDle.Core.Sim;
using CivDle.Core.Tests.Support;
using CivDle.Core.World;
using Xunit;

namespace CivDle.Core.Tests.Save;

public class SaveStoreTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _filePath;

    public SaveStoreTests()
    {
        _tempDir = Path.Combine(AppContext.BaseDirectory, "tmp-save-tests", Guid.NewGuid().ToString("N"));
        _filePath = Path.Combine(_tempDir, "save.civdle");
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    [Fact]
    public void HasSave_FalseBeforeFirstSave_TrueAfter()
    {
        var content = TestData.LoadRealContent();
        var store = new SaveStore(_filePath);
        var sim = new Simulation(content, GrassMap(content));

        Assert.False(store.HasSave);
        Assert.True(store.TrySave(sim, Meta()));
        Assert.True(store.HasSave);
    }

    [Fact]
    public void SaveAndLoad_RoundTripsThroughFile()
    {
        var content = TestData.LoadRealContent();
        var store = new SaveStore(_filePath);
        var sim = new Simulation(content, GrassMap(content));
        sim.TryPlaceBuilding(content.Buildings.IndexOf("house"), 3, 3);
        for (int i = 0; i < 10; i++)
        {
            sim.Tick();
        }

        Assert.True(store.TrySave(sim, Meta()));
        var loaded = store.TryLoad(content, out var error);

        Assert.Null(error);
        Assert.NotNull(loaded);
        Assert.Equal(sim.TickCount, loaded.Simulation.TickCount);
        Assert.Equal(1, loaded.Simulation.Buildings.Length);
        Assert.Equal(Meta(), loaded.Metadata);
    }

    [Fact]
    public void TryLoad_MissingFile_ReturnsNullWithoutError()
    {
        var content = TestData.LoadRealContent();
        var store = new SaveStore(_filePath);

        Assert.Null(store.TryLoad(content, out var error));
        Assert.Null(error);
    }

    [Fact]
    public void TryLoad_CorruptedFile_ReturnsNullWithError()
    {
        var content = TestData.LoadRealContent();
        Directory.CreateDirectory(_tempDir);
        File.WriteAllText(_filePath, "tohle není save");
        var store = new SaveStore(_filePath);

        // Poškozený save nesmí shodit hru — vrací null + důvod.
        Assert.Null(store.TryLoad(content, out var error));
        Assert.NotNull(error);
    }

    private static ITerrain GrassMap(CivDle.Core.Content.GameContent content) =>
        new UniformTerrain(content.Biomes.IndexOf("grassland"));

    private static SaveMetadata Meta() => new(
        Seed: 7, SizeId: "small", PresetId: "continents",
        SavedAtUtc: new DateTime(2026, 7, 20, 8, 30, 0, DateTimeKind.Utc));
}
