using CivDle.Core.Content;
using CivDle.Core.Sim;
using CivDle.Screens;
using Xunit;

namespace CivDle.Ui.Tests;

/// <summary>
/// Hláška „sem to nejde" musí říct i to, kam to tedy jde.
///
/// <para>Bez toho hráč zkouší dlaždici po dlaždici a nakonec budovu vzdá —
/// a vypadá to úplně stejně jako rozbitá hra. Testuje se proto ta jediná věc,
/// na které to stojí: že se ve větě opravdu objeví konkrétní biom nebo stupeň
/// sídla, ne jen obecné „špatný terén".</para>
/// </summary>
public class PlacementMessageTests
{
    [Fact]
    public void WrongGroundNamesTheGroundThatWouldWork()
    {
        var content = LoadContent();
        var loc = new Localization(content.Languages, content.Languages[0].Id);

        // Podmořská farma je nejostřejší případ: na souši ji hráč zkouší
        // postavit skoro jistě, protože z ikony to nepozná.
        var def = content.Buildings[content.Buildings.IndexOf("kelp_farm")];
        string text = PlacementMessage.Describe(content, loc, def, PlacementResult.WrongBiome);

        string anyAllowed = FirstAllowedBiomeName(content, loc, def);
        Assert.Contains(anyAllowed, text);
        Assert.DoesNotContain("~", text);
    }

    [Fact]
    public void ASettlementThatIsTooSmallSaysHowBigItMustBe()
    {
        var content = LoadContent();
        var loc = new Localization(content.Languages, content.Languages[0].Id);

        var def = content.Buildings[content.Buildings.IndexOf("spaceport")];
        Assert.True(def.NeedsSettlementRank, "kosmodrom má chtít stupeň sídla — jinak test nic neověří");

        string text = PlacementMessage.Describe(content, loc, def, PlacementResult.SettlementTooSmall);

        Assert.Contains(loc[content.SettlementRanks.Ranks[def.MinSettlementRank].NameKey], text);
        Assert.DoesNotContain("~", text);
    }

    [Fact]
    public void EveryRefusalHasWordsInEveryLanguage()
    {
        // Nepřeložený klíč se v téhle hlášce pozná až ve chvíli, kdy hráč něco
        // staví špatně — tedy skoro nikdy při testování a pokaždé při hraní.
        var content = LoadContent();
        var results = Enum.GetValues<PlacementResult>();

        for (int language = 0; language < content.Languages.Count; language++)
        {
            var loc = new Localization(content.Languages, content.Languages[language].Id);
            for (int i = 0; i < content.Buildings.Count; i++)
            {
                foreach (var result in results)
                {
                    string text = PlacementMessage.Describe(content, loc, content.Buildings[i], result);
                    Assert.False(string.IsNullOrWhiteSpace(text));
                    Assert.DoesNotContain("~", text);
                }
            }
        }
    }

    private static string FirstAllowedBiomeName(GameContent content, Localization loc, BuildingDef def)
    {
        for (int i = 0; i < content.Biomes.Count; i++)
        {
            if (def.IsBiomeAllowed(i))
            {
                return loc[content.Biomes[i].NameKey];
            }
        }

        Assert.Fail("budova nesmí stát nikde");
        return string.Empty;
    }

    private static GameContent LoadContent() =>
        new ContentLoader().LoadFrom(Path.Combine(AppContext.BaseDirectory, "data"));
}
