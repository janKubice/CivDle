using CivDle.Core.Content;
using CivDle.Core.Sim;
using CivDle.Screens;
using Xunit;

namespace CivDle.Ui.Tests;

/// <summary>
/// Body plánu guvernéra v Politikách. Každá potřeba musí mít větu v každém
/// jazyce — chybějící klíč by se hráči ukázal jako holé „governor.need.power",
/// a to je přesně to místo, kde se hráč ptá „proč guvernér staví tohle?".
/// </summary>
public class GovernorPlanTextTests
{
    private static GameContent Content() =>
        new ContentLoader().LoadFrom(Path.Combine(AppContext.BaseDirectory, "data"));

    public static IEnumerable<object[]> Needs() =>
        Enum.GetValues<CityNeed>().Where(n => n != CityNeed.None).Select(n => new object[] { n });

    [Theory]
    [MemberData(nameof(Needs))]
    public void EveryNeedHasASentenceInEveryLanguage(CityNeed need)
    {
        var content = Content();
        int wood = content.Resources.IndexOf("wood");
        for (int language = 0; language < content.Languages.Count; language++)
        {
            var loc = new Localization(content.Languages, content.Languages[language].Id);

            string line = GovernorPlanText.Line(content, loc, new GovernorAgendaItem(need, 50, wood));

            Assert.NotEmpty(line);
            Assert.DoesNotContain("governor.", line);
            Assert.DoesNotContain("{0}", line);
        }
    }

    [Fact]
    public void TheResourceIsNamedWhereTheSentenceTalksAboutOne()
    {
        var content = Content();
        var loc = new Localization(content.Languages, "cs");
        int science = content.Resources.IndexOf("science");

        string line = GovernorPlanText.Line(content, loc, new GovernorAgendaItem(CityNeed.Knowledge, 55, science));

        Assert.Contains(loc[content.Resources[science].NameKey], line);
    }
}
