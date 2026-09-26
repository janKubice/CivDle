using CivDle.Core.Content;
using CivDle.Screens;
using Xunit;

namespace CivDle.Ui.Tests;

/// <summary>
/// Texty výzev: pravidla a odměny musí být vidět před startem, ve všech
/// jazycích. Pravidlo bez popisu hráč pozná až tím, že mu půl hodiny nic
/// nejde; odměna bez jména není důvod výzvu hrát.
/// </summary>
public sealed class ChallengeTextTests
{
    [Fact]
    public void EveryRule_HasADescriptionInEveryLanguage()
    {
        var content = LoadContent();
        for (int language = 0; language < content.Languages.Count; language++)
        {
            var loc = new Localization(content.Languages, content.Languages[language].Id);
            foreach (var rule in Enum.GetValues<ScenarioRule>())
            {
                Assert.DoesNotContain("~", loc[ScenariosScreen.RuleKey(rule)]);
            }
        }
    }

    [Fact]
    public void EveryChallenge_NamesItsRewardAndTheMasterRewardExists()
    {
        var content = LoadContent();
        var loc = new Localization(content.Languages, content.Languages[0].Id);

        foreach (var scenario in content.Scenarios.Scenarios)
        {
            string reward = ChallengeRewardNames.ForKey(content, loc, ChallengeRewards.KeyOf(scenario.Id));
            Assert.False(string.IsNullOrWhiteSpace(reward), $"výzva '{scenario.Id}' nemá odměnu");
            Assert.DoesNotContain("~", reward);
        }

        var all = content.Scenarios.Scenarios.Select(s => s.Id).ToList();
        string last = ChallengeRewardNames.Of(content, loc, all[^1], all);
        string master = ChallengeRewardNames.ForKey(
            content, loc, ChallengeRewards.KeyOf(ChallengeRewards.AllChallengesId));
        Assert.Contains(master, last); // poslední výzva odemkne i Síň výzev
    }

    private static GameContent LoadContent() =>
        new ContentLoader().LoadFrom(Path.Combine(AppContext.BaseDirectory, "data"));
}
