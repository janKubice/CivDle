using CivDle.Core.Content;
using CivDle.Core.Tests.Support;
using Xunit;

namespace CivDle.Core.Tests.Content;

public class LocalizationTests
{
    [Fact]
    public void RealLanguages_ContainCzechAndEnglish()
    {
        var content = TestData.LoadRealContent();

        Assert.True(content.Languages.TryIndexOf("cs", out _));
        Assert.True(content.Languages.TryIndexOf("en", out _));
    }

    [Fact]
    public void EveryLanguageCarriesTheSameKeys()
    {
        // Chybějící klíč v jednom jazyce se ve hře projeví až tím, že tam na
        // hráče vykoukne holé „demo.finale.title" — a to nikdo netestuje ručně
        // v pěti jazycích. Čeština je měřítko, protože se do ní píše první.
        var content = TestData.LoadRealContent();
        var reference = content.Languages[content.Languages.IndexOf("cs")];

        for (int i = 0; i < content.Languages.Count; i++)
        {
            var language = content.Languages[i];
            var missing = reference.Strings.Keys.Where(k => !language.Strings.ContainsKey(k)).ToList();

            Assert.True(
                missing.Count == 0,
                $"jazyk '{language.Id}' nemá {missing.Count} klíčů, např.: "
                + string.Join(", ", missing.Take(5)));
        }
    }

    [Fact]
    public void NoLanguageInventsKeysTheOthersDoNotHave()
    {
        // Opačný směr: klíč navíc znamená, že se někde překládá něco, co už
        // v kódu není — mrtvý řádek, který při další úpravě mate.
        var content = TestData.LoadRealContent();
        var reference = content.Languages[content.Languages.IndexOf("cs")];

        for (int i = 0; i < content.Languages.Count; i++)
        {
            var language = content.Languages[i];
            var extra = language.Strings.Keys.Where(k => !reference.Strings.ContainsKey(k)).ToList();

            Assert.True(
                extra.Count == 0,
                $"jazyk '{language.Id}' má {extra.Count} klíčů navíc, např.: "
                + string.Join(", ", extra.Take(5)));
        }
    }

    [Fact]
    public void Indexer_ReturnsTranslationForCurrentLanguage()
    {
        var content = TestData.LoadRealContent();
        var loc = new Localization(content.Languages, "en");

        Assert.Equal("New Game", loc["menu.newGame"]);

        loc.SetLanguage("cs");

        Assert.Equal("Nová hra", loc["menu.newGame"]);
    }

    [Fact]
    public void ContentNames_AreTranslated()
    {
        var content = TestData.LoadRealContent();
        var loc = new Localization(content.Languages, "cs");
        var grassland = content.Biomes[content.Biomes.IndexOf("grassland")];

        Assert.Equal("Louka", loc[grassland.NameKey]);

        loc.SetLanguage("en");

        Assert.Equal("Grassland", loc[grassland.NameKey]);
    }

    [Fact]
    public void UnknownInitialLanguage_FallsBackToFirst()
    {
        var content = TestData.LoadRealContent();

        var loc = new Localization(content.Languages, "klingon");

        Assert.Equal(content.Languages[0].Id, loc.Current.Id);
    }

    [Fact]
    public void MissingKey_ReturnsHighlightedKey()
    {
        var content = TestData.LoadRealContent();
        var loc = new Localization(content.Languages);

        Assert.Equal("~neexistujici.klic~", loc["neexistujici.klic"]);
    }

    [Fact]
    public void SetLanguage_FiresEventOnlyOnChange()
    {
        var content = TestData.LoadRealContent();
        var loc = new Localization(content.Languages, "cs");
        int fired = 0;
        loc.LanguageChanged += () => fired++;

        loc.SetLanguage("cs");
        Assert.Equal(0, fired);

        loc.SetLanguage("en");
        Assert.Equal(1, fired);
    }

    [Fact]
    public void Format_InsertsArguments()
    {
        var content = TestData.LoadRealContent();
        var loc = new Localization(content.Languages, "cs");

        Assert.Equal("Seed: 42", loc.Format("hud.seed", 42));
    }
}
