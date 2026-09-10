using CivDle.Core.Content;
using CivDle.Core.Sim;
using CivDle.Screens;
using Xunit;

namespace CivDle.Ui.Tests;

/// <summary>
/// Co je v šabloně a kolik stojí.
///
/// <para>V seznamu šablon dřív stál jen rozměr — „5×4, 7 budov". To hráči
/// neřekne, jestli je „Blok 2" obytná ulice nebo řada pil, ani jestli na ni
/// zrovna má. Musel ji položit a dívat se, co vznikne.</para>
/// </summary>
public class TemplateSummaryTests
{
    [Fact]
    public void ContentsListTheBuildingsMostCommonFirst()
    {
        var content = LoadContent();
        var loc = Loc(content);
        var template = Template(("house", 3), ("warehouse", 1));

        string text = TemplateSummary.Contents(content, loc, template);

        Assert.StartsWith("3× " + Name(content, loc, "house"), text);
        Assert.Contains("1× " + Name(content, loc, "warehouse"), text);
    }

    [Fact]
    public void TheCostAddsUpAcrossEveryBuilding()
    {
        var content = LoadContent();
        var loc = Loc(content);
        int house = content.Buildings.IndexOf("house");
        var cost = content.Buildings[house].BuildCost[0];

        string text = TemplateSummary.Cost(content, loc, Template(("house", 4)));

        // Čtyři chalupy stojí čtyřnásobek jedné — součet, ne cena za kus.
        Assert.Contains((cost.Amount * 4).ToString(), text);
        Assert.Contains(loc[content.Resources[cost.ResourceIndex].NameKey], text);
    }

    [Fact]
    public void AnEmptyTemplateSaysNothingRatherThanLying()
    {
        var content = LoadContent();
        var loc = Loc(content);
        var empty = BuildTemplate.Empty;

        Assert.Equal(string.Empty, TemplateSummary.Contents(content, loc, empty));
        Assert.Equal(string.Empty, TemplateSummary.Cost(content, loc, empty));
    }

    [Fact]
    public void ABuildingTheDataNoLongerHasStillShowsUp()
    {
        // Šablona přežije změnu dat i mody. Kdyby zmizelá budova ze soupisu
        // tiše vypadla, nesedělo by to s tím, co se opravdu postaví.
        var content = LoadContent();
        var loc = Loc(content);

        string text = TemplateSummary.Contents(content, loc, Template(("neexistujici_budova", 2)));

        Assert.Contains("neexistujici_budova", text);
    }

    private static BuildTemplate Template(params (string Id, int Count)[] parts)
    {
        var list = new List<TemplatePart>();
        int x = 0;
        foreach (var (id, count) in parts)
        {
            for (int i = 0; i < count; i++)
            {
                list.Add(new TemplatePart(id, x++, 0));
            }
        }

        return new BuildTemplate("test", list, Array.Empty<(int, int)>());
    }

    private static string Name(GameContent content, Localization loc, string id) =>
        loc[content.Buildings[content.Buildings.IndexOf(id)].NameKey];

    private static Localization Loc(GameContent content) =>
        new(content.Languages, content.Languages[0].Id);

    private static GameContent LoadContent() =>
        new ContentLoader().LoadFrom(Path.Combine(AppContext.BaseDirectory, "data"));
}
