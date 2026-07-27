using CivDle.Core.Config;
using Xunit;

namespace CivDle.Core.Tests.Config;

/// <summary>
/// Přístupnostní volby musí přežít restart a nesmí jít nastavit tak, aby se UI
/// rozbilo — soubor s nastavením je textový a hráči do něj sahají.
/// </summary>
public class AccessibilitySettingsTests
{
    [Fact]
    public void Defaults_AreTheUnchangedExperience()
    {
        var settings = new GameSettings();

        Assert.Equal(1.0f, settings.SafeUiScale);
        Assert.False(settings.ReduceMotion);
        Assert.False(settings.ColorCues);
    }

    [Theory]
    [InlineData(0.1f, GameSettings.MinUiScale)]
    [InlineData(0f, GameSettings.MinUiScale)]
    [InlineData(-3f, GameSettings.MinUiScale)]
    [InlineData(99f, GameSettings.MaxUiScale)]
    public void RidiculousUiScale_IsClampedToSomethingUsable(float stored, float expected)
    {
        var settings = new GameSettings { UiScale = stored };

        Assert.Equal(expected, settings.SafeUiScale);
    }

    [Fact]
    public void UsableUiScale_IsKeptAsIs()
    {
        Assert.Equal(1.3f, new GameSettings { UiScale = 1.3f }.SafeUiScale);
    }

    [Fact]
    public void Roundtrip_KeepsAccessibilityChoices()
    {
        string path = Path.Combine(Path.GetTempPath(), $"civdle-a11y-{Guid.NewGuid():N}.json");
        try
        {
            var store = new SettingsStore(path);
            store.Save(new GameSettings { UiScale = 1.45f, ReduceMotion = true, ColorCues = true });

            var loaded = new SettingsStore(path).Load();

            Assert.Equal(1.45f, loaded.SafeUiScale);
            Assert.True(loaded.ReduceMotion);
            Assert.True(loaded.ColorCues);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
