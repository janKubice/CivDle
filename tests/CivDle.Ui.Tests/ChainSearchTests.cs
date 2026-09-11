using CivDle.Core.Content;
using CivDle.Screens;
using Xunit;

namespace CivDle.Ui.Tests;

/// <summary>
/// Hledání v přehledu výrobních řetězců.
///
/// <para>Obrazovku hráč otevírá právě ve chvíli, kdy <b>ví</b>, co potřebuje:
/// dojdou mu prkna a chce vědět, kdo je dělá. Čtyřicet surovin se ale očima
/// prochází dlouho, takže je rozhodující, aby psaní opravdu filtrovalo — a aby
/// šlo hledat i podle budovy, protože jméno suroviny hráč často nezná.</para>
///
/// <para>Testuje se rejstřík, ne widgety: vlastní <see cref="SearchIndex"/> je
/// to jediné, co o filtrování rozhoduje, a jde ověřit bez okna.</para>
/// </summary>
public class ChainSearchTests
{
    [Fact]
    public void SearchingByResourceNameFindsIt()
    {
        var content = LoadContent();
        var loc = Loc(content);
        var index = BuildIndex(content, loc, out var resources);

        int planks = resources.IndexOf("planks");
        index.Search(loc[content.Resources[planks].NameKey]);

        Assert.True(index.IsFiltering);
        Assert.True(index.IsMatch(planks));
    }

    [Fact]
    public void SearchingByTheBuildingThatMakesItFindsItToo()
    {
        // Tohle je ta polovina, kvůli které se rejstřík skládá z obojího:
        // hráč ví, že prkna dělá pila, ale nemusí vědět, že se tomu říká prkna.
        var content = LoadContent();
        var loc = Loc(content);
        var chains = new ProductionChains(content);
        var index = BuildIndex(content, loc, out var resources);

        int planks = resources.IndexOf("planks");
        int sawmill = chains.ProducersOf(planks)[0].BuildingIndex;

        index.Search(loc[content.Buildings[sawmill].NameKey]);

        Assert.True(index.IsMatch(planks), "surovina se nenašla podle budovy, která ji vyrábí");
    }

    [Fact]
    public void ANonsenseQueryMatchesNothing()
    {
        var content = LoadContent();
        var index = BuildIndex(content, Loc(content), out _);

        index.Search("qwertzuiop");

        Assert.True(index.IsFiltering);
        Assert.Equal(0, index.MatchCount);
    }

    [Fact]
    public void AnEmptyQueryShowsEverythingAgain()
    {
        // Smazané políčko musí seznam vrátit celý — jinak zůstane hráč viset
        // ve filtru, který už nevidí.
        var content = LoadContent();
        var index = BuildIndex(content, Loc(content), out _);

        index.Search("kámen");
        index.Search(string.Empty);

        Assert.False(index.IsFiltering);
    }

    /// <summary>Rejstřík přesně tak, jak si ho staví obrazovka: surovina + budovy, které ji dělají.</summary>
    private static SearchIndex BuildIndex(
        GameContent content, Localization loc, out DefRegistry<Resource> resources)
    {
        resources = content.Resources;
        var chains = new ProductionChains(content);
        var entries = new string[resources.Count];
        for (int i = 0; i < resources.Count; i++)
        {
            var text = new System.Text.StringBuilder(loc[resources[i].NameKey]);
            foreach (var step in chains.ProducersOf(i))
            {
                text.Append(' ').Append(loc[content.Buildings[step.BuildingIndex].NameKey]);
            }

            entries[i] = text.ToString();
        }

        return new SearchIndex(entries);
    }

    private static Localization Loc(GameContent content) =>
        new(content.Languages, content.Languages[0].Id);

    private static GameContent LoadContent() =>
        new ContentLoader().LoadFrom(Path.Combine(AppContext.BaseDirectory, "data"));
}
