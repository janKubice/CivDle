using CivDle.Core.Content;
using CivDle.Screens;
using Xunit;

namespace CivDle.Ui.Tests;

/// <summary>
/// Popisek budovy do bubliny u kurzoru se skládá z DAT, ne z ručně psaných textů.
/// Testuje se to, na co hráč spoléhá: každá stavitelná budova má co říct a v
/// popisku nezůstane nepřeložený klíč.
/// </summary>
public sealed class BuildingSummaryTests
{
    [Fact]
    public void EveryBuilding_HasANonEmptySummaryInEveryLanguage()
    {
        var content = LoadContent();
        for (int language = 0; language < content.Languages.Count; language++)
        {
            var loc = new Localization(content.Languages, content.Languages[language].Id);
            for (int i = 0; i < content.Buildings.Count; i++)
            {
                string text = BuildingSummary.Describe(content, loc, content.Buildings[i]);
                Assert.False(string.IsNullOrWhiteSpace(text));
                Assert.DoesNotContain("~", text); // Localization značí chybějící klíč vlnovkami
            }
        }
    }

    [Fact]
    public void ProducingBuilding_MentionsWhatItMakes()
    {
        var content = LoadContent();
        var loc = new Localization(content.Languages, content.Languages[0].Id);

        int index = FirstProducer(content);
        var def = content.Buildings[index];
        string text = BuildingSummary.Describe(content, loc, def);

        string output = loc[content.Resources[def.Recipe!.Outputs[0].ResourceIndex].NameKey];
        Assert.Contains(output, text);
    }

    /// <summary>Budova bez omezení biomů nemá výčtem biomů plýtvat místem v bublině.</summary>
    [Fact]
    public void UnrestrictedBuilding_DoesNotListEveryBiome()
    {
        var content = LoadContent();
        var loc = new Localization(content.Languages, content.Languages[0].Id);

        for (int i = 0; i < content.Buildings.Count; i++)
        {
            var def = content.Buildings[i];
            bool everywhere = true;
            for (int b = 0; b < content.Biomes.Count && everywhere; b++)
            {
                everywhere = def.IsBiomeAllowed(b);
            }

            if (everywhere)
            {
                Assert.DoesNotContain(loc["tip.build.biomes"].Split('{')[0].Trim(),
                    BuildingSummary.Describe(content, loc, def));
                return;
            }
        }
    }

    private static int FirstProducer(GameContent content)
    {
        for (int i = 0; i < content.Buildings.Count; i++)
        {
            if (content.Buildings[i].Recipe is { Outputs.Count: > 0 })
            {
                return i;
            }
        }

        throw new InvalidOperationException("Herní obsah nemá jedinou vyrábějící budovu.");
    }

    private static GameContent LoadContent() =>
        new ContentLoader().LoadFrom(Path.Combine(AppContext.BaseDirectory, "data"));
}
