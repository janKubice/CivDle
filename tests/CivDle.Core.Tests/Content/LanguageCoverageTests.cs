using CivDle.Core.Tests.Support;
using Xunit;

namespace CivDle.Core.Tests.Content;

/// <summary>
/// Jazyky ve skutečných datech. Hlídá se to, co by hráč poznal až ve hře:
/// základní jazyk musí být úplný, ostatní smí být rozpracované — a žádný
/// nesmí obsahovat prázdný řetězec, protože ten se v UI projeví jako díra.
/// </summary>
public class LanguageCoverageTests
{
    [Fact]
    public void TheGameOffersMoreThanTwoLanguages()
    {
        Assert.True(TestData.LoadRealContent().Languages.Count >= 4);
    }

    [Fact]
    public void TheBaseLanguageIsComplete()
    {
        // Základní jazyk je záchranná síť všech ostatních — v něm díra být nesmí.
        var content = TestData.LoadRealContent();

        Assert.True(content.Languages[0].IsComplete);
    }

    [Fact]
    public void EveryShippedLanguageIsFullyTranslated()
    {
        // Částečný překlad hra unese (doplní se ze základního jazyka), ale to je
        // pojistka pro rozpracovaný jazyk — ne stav, ve kterém se hra vydává.
        // Půl obrazovky česky a půl anglicky vypadá jako chyba, protože to chyba je.
        var content = TestData.LoadRealContent();

        foreach (var language in content.Languages.All)
        {
            Assert.True(
                language.IsComplete,
                $"Jazyk '{language.Id}' je přeložený jen z {language.Coverage:P0} — dopřelož ho, "
                + "nebo ho z data/lang vyřaď.");
        }
    }

    [Fact]
    public void EveryLanguageCanAnswerEveryKey()
    {
        // Po doplnění ze základního jazyka musí mít každý jazyk všechny klíče,
        // jinak by hra místo textu ukázala ~klíč~.
        var content = TestData.LoadRealContent();
        var reference = content.Languages[0];

        foreach (var language in content.Languages.All)
        {
            foreach (string key in reference.Strings.Keys)
            {
                Assert.True(
                    language.Strings.ContainsKey(key),
                    $"Jazyk '{language.Id}' neumí odpovědět na '{key}'.");
            }
        }
    }

    [Fact]
    public void NoLanguageHasEmptyText()
    {
        var content = TestData.LoadRealContent();

        foreach (var language in content.Languages.All)
        {
            foreach (var pair in language.Strings)
            {
                Assert.False(
                    string.IsNullOrWhiteSpace(pair.Value),
                    $"Jazyk '{language.Id}': klíč '{pair.Key}' je prázdný.");
            }
        }
    }

    [Fact]
    public void PartialLanguagesReportHowFarTheyGot()
    {
        // Menu z pokrytí píše procento, ať hráč ví, do čeho jde.
        var content = TestData.LoadRealContent();

        foreach (var language in content.Languages.All)
        {
            Assert.InRange(language.Coverage, 0.0, 1.0);
        }
    }

    [Fact]
    public void EveryLanguageNamesItselfInItsOwnTongue()
    {
        // „German" v seznamu je k ničemu tomu, kdo hledá „Deutsch".
        var content = TestData.LoadRealContent();

        Assert.All(content.Languages.All, l => Assert.False(string.IsNullOrWhiteSpace(l.NativeName)));
        Assert.Equal(
            content.Languages.Count,
            content.Languages.All.Select(l => l.NativeName).Distinct().Count());
    }

    [Fact]
    public void TranslationsKeepThePlaceholders()
    {
        // Chybějící {0} v překladu znamená, že hráč místo čísla nedostane nic —
        // a nikdo si toho nevšimne, dokud si daný jazyk nezapne.
        var content = TestData.LoadRealContent();
        var reference = content.Languages[0];

        foreach (var language in content.Languages.All.Skip(1))
        {
            foreach (var pair in reference.Strings)
            {
                for (int slot = 0; slot < 4; slot++)
                {
                    string placeholder = "{" + slot + "}";
                    if (!pair.Value.Contains(placeholder, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    Assert.True(
                        language.Strings[pair.Key].Contains(placeholder, StringComparison.Ordinal),
                        $"Jazyk '{language.Id}': '{pair.Key}' přišel o {placeholder}.");
                }
            }
        }
    }
}
