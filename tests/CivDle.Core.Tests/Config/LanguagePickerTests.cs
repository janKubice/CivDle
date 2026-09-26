using System.Globalization;
using CivDle.Core.Config;
using Xunit;

namespace CivDle.Core.Tests.Config;

/// <summary>
/// Jazyk při prvním spuštění. Dřív byla natvrdo čeština — kdo hru stáhl
/// v Německu, viděl první obrazovku v jazyce, kterému nerozumí.
/// </summary>
public class LanguagePickerTests
{
    private static readonly string[] Available = { "cs", "en", "de", "pl", "es" };

    [Theory]
    [InlineData("cs-CZ", "cs")]
    [InlineData("de-AT", "de")] // přes nadřazenou kulturu
    [InlineData("pl-PL", "pl")]
    [InlineData("es-MX", "es")]
    [InlineData("en-GB", "en")]
    public void ASupportedSystemLanguageWins(string culture, string expected)
    {
        Assert.Equal(expected, LanguagePicker.Pick(Available, new CultureInfo(culture)));
    }

    [Theory]
    [InlineData("fr-FR")]
    [InlineData("ja-JP")]
    public void AnUnsupportedLanguageFallsBackToEnglish_NotToTheDevelopersOwn(string culture)
    {
        Assert.Equal("en", LanguagePicker.Pick(Available, new CultureInfo(culture)));
    }

    [Fact]
    public void TheInvariantCultureFallsBackToEnglish()
    {
        // Linux bez nastaveného locale hlásí invariantní kulturu.
        Assert.Equal("en", LanguagePicker.Pick(Available, CultureInfo.InvariantCulture));
    }

    [Fact]
    public void WithoutEnglishItTakesTheFirstAvailable()
    {
        // Mod, který angličtinu vyřadil, nesmí nechat hru bez jazyka.
        Assert.Equal("cs", LanguagePicker.Pick(new[] { "cs", "pl" }, new CultureInfo("fr-FR")));
    }

    [Fact]
    public void TheStoreKnowsWhetherThisIsTheFirstRun()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "tmp-language-tests", Guid.NewGuid().ToString("N"), "settings.json");
        var store = new SettingsStore(path);
        try
        {
            Assert.False(store.Exists);
            store.Save(new GameSettings());
            Assert.True(store.Exists);
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(path)!, recursive: true);
        }
    }
}
