using CivDle.Core.Content;
using CivDle.Core.Sim;
using CivDle.Screens;
using Xunit;

namespace CivDle.Ui.Tests;

/// <summary>
/// Řádek „co guvernér dělá". Dřív guvernér uvízl potichu; teď to musí umět
/// říct pro každý stav, v každém jazyce — a u uvíznutí i poradit.
/// </summary>
public class GovernorStatusTextTests
{
    public static IEnumerable<object[]> States()
    {
        yield return new object[] { new GovernorStatus(GovernorActivity.Building, GovernorBlocker.None, 0, -1) };
        yield return new object[] { new GovernorStatus(GovernorActivity.Saving, GovernorBlocker.None, 0, 0) };
        yield return new object[] { new GovernorStatus(GovernorActivity.Saving, GovernorBlocker.None, 0, -1) };
        yield return new object[] { new GovernorStatus(GovernorActivity.Gathering, GovernorBlocker.None, 0, 0) };
        foreach (var blocker in Enum.GetValues<GovernorBlocker>().Where(b => b != GovernorBlocker.None))
        {
            yield return new object[] { new GovernorStatus(GovernorActivity.Stuck, blocker, 0, 0) };
        }
    }

    [Theory]
    [MemberData(nameof(States))]
    public void EveryStateSaysSomethingAndAdvises(GovernorStatus status)
    {
        var content = Content();
        for (int language = 0; language < content.Languages.Count; language++)
        {
            var loc = new Localization(content.Languages, content.Languages[language].Id);

            string line = GovernorStatusText.Line(content, loc, status);
            string hint = GovernorStatusText.Hint(loc, status);

            Assert.NotEmpty(line);
            Assert.NotEmpty(hint);
            Assert.DoesNotContain("governor.", line);
            Assert.DoesNotContain("governor.", hint);
        }
    }

    [Fact]
    public void AnIdleGovernorStaysQuiet()
    {
        // Řádek, který pořád hlásí „nic", by se naučil hráč přehlížet —
        // a přehlédl by i to, až guvernér opravdu uvízne.
        var content = Content();
        var loc = new Localization(content.Languages, "cs");

        Assert.Empty(GovernorStatusText.Line(content, loc, GovernorStatus.Idle));
        Assert.False(GovernorStatusText.NeedsPlayer(GovernorStatus.Idle));
    }

    [Fact]
    public void SavingNamesTheBuildingAndTheMissingMaterial()
    {
        var content = Content();
        var loc = new Localization(content.Languages, "cs");
        int house = content.Buildings.IndexOf("house");
        int wood = content.Resources.IndexOf("wood");

        string line = GovernorStatusText.Line(
            content, loc, new GovernorStatus(GovernorActivity.Saving, GovernorBlocker.None, house, wood));

        Assert.Contains(loc[content.Buildings[house].NameKey], line);
        Assert.Contains(loc[content.Resources[wood].NameKey], line);
    }

    [Fact]
    public void WaitingForPeopleIsNotAnAlarm()
    {
        // Lidé přibudou sami — varovná barva by hráče jen zbytečně strašila.
        Assert.False(GovernorStatusText.NeedsPlayer(
            new GovernorStatus(GovernorActivity.Stuck, GovernorBlocker.NeedsPeople, -1, -1)));
        Assert.True(GovernorStatusText.NeedsPlayer(
            new GovernorStatus(GovernorActivity.Stuck, GovernorBlocker.NoProducer, -1, 0)));
    }

    private static GameContent Content() =>
        new ContentLoader().LoadFrom(Path.Combine(AppContext.BaseDirectory, "data"));
}
