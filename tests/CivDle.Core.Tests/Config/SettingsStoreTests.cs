using CivDle.Core.Config;
using Xunit;

namespace CivDle.Core.Tests.Config;

public class SettingsStoreTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _filePath;

    public SettingsStoreTests()
    {
        _tempDir = Path.Combine(AppContext.BaseDirectory, "tmp-settings-tests", Guid.NewGuid().ToString("N"));
        _filePath = Path.Combine(_tempDir, "settings.json");
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    [Fact]
    public void Load_MissingFile_ReturnsDefaults()
    {
        var store = new SettingsStore(_filePath);

        var settings = store.Load();

        Assert.Equal(new GameSettings(), settings);
    }

    [Fact]
    public void SaveAndLoad_RoundTripsAllValues()
    {
        var store = new SettingsStore(_filePath);
        var settings = new GameSettings
        {
            Language = "en",
            ResolutionWidth = 1920,
            ResolutionHeight = 1080,
            WindowMode = WindowMode.Borderless,
            VSync = false,
        };

        store.Save(settings);
        var loaded = store.Load();

        Assert.Equal(settings, loaded);
    }

    [Fact]
    public void Load_CorruptedFile_ReturnsDefaults()
    {
        Directory.CreateDirectory(_tempDir);
        File.WriteAllText(_filePath, "{ rozbité json ");
        var store = new SettingsStore(_filePath);

        // Rozbitá uživatelská nastavení nesmí shodit hru — na rozdíl od herních dat.
        Assert.Equal(new GameSettings(), store.Load());
    }

    [Fact]
    public void Load_NonsenseResolution_IsSanitized()
    {
        Directory.CreateDirectory(_tempDir);
        File.WriteAllText(_filePath, """{ "resolutionWidth": -5, "resolutionHeight": 99999 }""");
        var store = new SettingsStore(_filePath);

        var settings = store.Load();

        Assert.Equal(new GameSettings().ResolutionWidth, settings.ResolutionWidth);
        Assert.Equal(new GameSettings().ResolutionHeight, settings.ResolutionHeight);
    }

    [Fact]
    public void Load_KeepsAChosenDetailLevel()
    {
        Directory.CreateDirectory(_tempDir);
        File.WriteAllText(_filePath, """{ "detail": "Performance" }""");

        Assert.Equal(DetailQuality.Performance, new SettingsStore(_filePath).Load().Detail);
    }

    [Fact]
    public void Load_UnknownDetailLevel_FallsBackToDefault()
    {
        // Číslo mimo výčet (starší nebo ručně upravený soubor) by prošlo jako
        // platná hodnota a render by z něj počítal nesmyslné prahy.
        Directory.CreateDirectory(_tempDir);
        File.WriteAllText(_filePath, """{ "detail": 99 }""");

        Assert.Equal(new GameSettings().Detail, new SettingsStore(_filePath).Load().Detail);
    }
}
