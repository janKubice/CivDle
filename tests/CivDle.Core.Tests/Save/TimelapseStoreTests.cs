using CivDle.Core.Content;
using CivDle.Core.Save;
using CivDle.Core.Sim;
using Xunit;

namespace CivDle.Core.Tests.Save;

/// <summary>
/// Sbírka časosběrů: suvenýr, ke kterému se nedá vrátit, není suvenýr.
///
/// <para>Testuje se hlavně věrnost (soubor musí vrátit tytéž snímky i barvy)
/// a odolnost — poškozený exemplář má zmizet ze seznamu, ne shodit menu.</para>
/// </summary>
public sealed class TimelapseStoreTests : IDisposable
{
    private readonly string _tempDir;
    private readonly TimelapseStore _store;

    public TimelapseStoreTests()
    {
        _tempDir = Path.Combine(AppContext.BaseDirectory, "tmp-timelapse-tests", Guid.NewGuid().ToString("N"));
        _store = new TimelapseStore(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    /// <summary>Kronika s pár snímky a dvěma barvami — dost na ověření věrnosti.</summary>
    private static CityHistory SampleHistory()
    {
        var history = new CityHistory(maxFrames: 16);
        byte wood = (byte)(history.PaletteIndexOf(new RgbColor(140, 90, 40)) + 1);
        byte stone = (byte)(history.PaletteIndexOf(new RgbColor(120, 120, 128)) + 1);

        var cells = new byte[CityHistory.CellBytes];
        cells[32 * CityHistory.GridSize + 32] = wood;
        history.Add(new HistoryFrame(100, 10, 1, 0, 0.9, 12, 1.5, 1), cells);

        cells[32 * CityHistory.GridSize + 33] = stone;
        history.Add(new HistoryFrame(200, 25, 2, 1, 0.8, 20, 3.0, 1), cells);
        return history;
    }

    [Fact]
    public void ASavedTimelapseComesBackIdentical()
    {
        var history = SampleHistory();

        string? path = _store.TrySave(history, seed: 777, sizeId: "medium", presetId: "continents");
        Assert.NotNull(path);

        var loaded = _store.TryLoad(path!);
        Assert.NotNull(loaded);
        Assert.Equal(777, loaded!.Seed);
        Assert.Equal("continents", loaded.PresetId);
        Assert.Equal(history.Count, loaded.History.Count);
        Assert.Equal(history.FrameAt(1), loaded.History.FrameAt(1));
        Assert.Equal(history.Palette, loaded.History.Palette);
        Assert.Equal(history.ColorAt(1, 33, 32), loaded.History.ColorAt(1, 33, 32));
    }

    [Fact]
    public void ASingleFrameIsNotWorthSaving()
    {
        // Z jednoho snímku není co přehrávat — soubor by byl jen matoucí
        // položka v seznamu.
        var history = new CityHistory(maxFrames: 4);
        history.Add(new HistoryFrame(1, 1, 1, -1), new byte[CityHistory.CellBytes]);

        Assert.Null(_store.TrySave(history, 1, "s", "p"));
        Assert.Empty(_store.ListFiles());
    }

    [Fact]
    public void GarbageFilesAreSkippedNotFatal()
    {
        Directory.CreateDirectory(_tempDir);
        File.WriteAllBytes(Path.Combine(_tempDir, "rozbite.civtl"), new byte[] { 1, 2, 3, 4, 5 });

        Assert.Null(_store.TryLoad(Path.Combine(_tempDir, "rozbite.civtl")));
    }

    [Fact]
    public void TheListIsNewestFirst()
    {
        var history = SampleHistory();
        Directory.CreateDirectory(_tempDir);

        // Jména souborů nesou čas — řazení podle jména sestupně = nejnovější první.
        File.WriteAllText(Path.Combine(_tempDir, "20260101-000000.civtl"), "x");
        File.WriteAllText(Path.Combine(_tempDir, "20260601-000000.civtl"), "x");
        _ = history;

        var files = _store.ListFiles();
        Assert.Equal(2, files.Count);
        Assert.Contains("20260601", files[0]);
    }

    [Fact]
    public void AMissingFolderMeansAnEmptyCollection()
    {
        Assert.Empty(new TimelapseStore(Path.Combine(_tempDir, "neexistuje")).ListFiles());
    }
}
