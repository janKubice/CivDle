using CivDle.Screens;
using Xunit;

namespace CivDle.Ui.Tests;

/// <summary>
/// Vyhledávání ve stromu výzkumu a ve stavebním katalogu.
///
/// <para>Jádro těchhle testů je diakritika. Hra běží v pěti jazycích a hráč
/// nepíše háčky ani přehlásky — kdyby hledání vyžadovalo přesný zápis, bylo by
/// nad sto čtyřiceti technologiemi k ničemu právě těm hráčům, kterým má
/// pomoct.</para>
/// </summary>
public class SearchIndexTests
{
    [Fact]
    public void WithoutAQueryEverythingPasses()
    {
        // Prázdné políčko nesmí schovat strom — je to výchozí stav obrazovky.
        var index = new SearchIndex(new[] { "dřevo", "kámen" });

        Assert.False(index.IsFiltering);
        Assert.Equal(2, index.MatchCount);
        Assert.True(index.IsMatch(0));
        Assert.True(index.IsMatch(1));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void BlankQueriesAreNoQuery(string? query)
    {
        // Ujetý mezerník nesmí vypadat jako „nic nenalezeno".
        var index = new SearchIndex(new[] { "dřevo" });

        index.Search(query);

        Assert.False(index.IsFiltering);
        Assert.True(index.IsMatch(0));
    }

    [Fact]
    public void TypingWithoutDiacriticsStillFinds()
    {
        // Tohle je celý důvod, proč tahle třída existuje.
        var index = new SearchIndex(new[] { "Dřevěné prkno", "Kamenná zeď" });

        index.Search("drevene");

        Assert.True(index.IsFiltering);
        Assert.Equal(1, index.MatchCount);
        Assert.True(index.IsMatch(0));
        Assert.False(index.IsMatch(1));
    }

    [Fact]
    public void TypingWithDiacriticsFindsPlainText()
    {
        // A obráceně: kdo diakritiku píše, taky nesmí přijít zkrátka.
        var index = new SearchIndex(new[] { "Drevo" });

        index.Search("dřevo");

        Assert.True(index.IsMatch(0));
    }

    [Theory]
    [InlineData("Mühle", "muhle")]          // němčina
    [InlineData("Kraków", "krakow")]        // polština
    [InlineData("Fundición", "fundicion")]  // španělština
    [InlineData("Žďár", "zdar")]            // čeština, dva háčky v jednom slově
    public void EveryLanguageOfTheGameWorks(string entry, string typed)
    {
        var index = new SearchIndex(new[] { entry });

        index.Search(typed);

        Assert.True(index.IsMatch(0), $"'{typed}' nenašlo '{entry}'");
    }

    [Fact]
    public void SearchIsCaseInsensitive()
    {
        var index = new SearchIndex(new[] { "Vodní Mlýn" });

        index.Search("VODNI");

        Assert.True(index.IsMatch(0));
    }

    [Fact]
    public void SearchMatchesAnywhereInTheText()
    {
        // Hráč si pamatuje slovo z popisu, ne začátek názvu.
        var index = new SearchIndex(new[] { "pila — zpracovává klády na prkna" });

        index.Search("klády");

        Assert.True(index.IsMatch(0));
    }

    [Fact]
    public void ClearingTheQueryBringsEverythingBack()
    {
        // Bez tohohle by hráč po smazání textu koukal na prázdný strom.
        var index = new SearchIndex(new[] { "dřevo", "kámen" });
        index.Search("dřevo");
        Assert.Equal(1, index.MatchCount);

        index.Search(string.Empty);

        Assert.False(index.IsFiltering);
        Assert.Equal(2, index.MatchCount);
    }

    [Fact]
    public void NothingFoundIsAValidAnswer()
    {
        var index = new SearchIndex(new[] { "dřevo" });

        index.Search("uran");

        Assert.True(index.IsFiltering);
        Assert.Equal(0, index.MatchCount);
        Assert.Equal(-1, index.FirstMatch());
    }

    [Fact]
    public void FirstMatchPointsAtTheFirstHit()
    {
        // Podle tohohle skáče kamera na nález, takže to musí být opravdu první.
        var index = new SearchIndex(new[] { "kámen", "dřevo", "dřevěné uhlí" });

        index.Search("dřev");

        Assert.Equal(2, index.MatchCount);
        Assert.Equal(1, index.FirstMatch());
    }

    [Fact]
    public void AnEmptyEntryNeverMatches()
    {
        // Takhle se ze stromu výzkumu vyřazují uzly, které hráč ještě nemá
        // odhalené: dostanou prázdný text a hledání je nenajde. Kdyby
        // prázdný text vyhověl čemukoli, prozradilo by hledání celý strom.
        var index = new SearchIndex(new[] { string.Empty, "dřevo" });

        index.Search("e");

        Assert.False(index.IsMatch(0));
        Assert.True(index.IsMatch(1));
        Assert.Equal(1, index.MatchCount);
    }

    [Fact]
    public void AnEmptyIndexDoesNotBlowUp()
    {
        // Mod může vyprázdnit registr; obrazovka se kvůli tomu nesmí složit.
        var index = new SearchIndex(Array.Empty<string>());

        index.Search("cokoliv");

        Assert.Equal(0, index.Count);
        Assert.Equal(0, index.MatchCount);
        Assert.Equal(-1, index.FirstMatch());
    }
}
