using CivDle.Core.Content;
using CivDle.Screens;
using Xunit;

namespace CivDle.Ui.Tests;

/// <summary>
/// Značkování řádků bubliny.
///
/// <para>Barva v bublině stojí na neviditelné značce na začátku řádku. Kdyby
/// se značka někde nechtěně dostala k hráči, viděl by v popisku smetí; kdyby
/// se ztratila, přišel by o barvu. Obojí se tady hlídá.</para>
/// </summary>
public class TipLineTests
{
    /// <summary>Řídicí znak, kterým značka začíná — v textu pro hráče nesmí být nikdy.</summary>
    private const char Marker = '\u0001';

    [Theory]
    [InlineData(TipKind.Cost)]
    [InlineData(TipKind.Produces)]
    [InlineData(TipKind.Consumes)]
    [InlineData(TipKind.People)]
    [InlineData(TipKind.Storage)]
    [InlineData(TipKind.Power)]
    [InlineData(TipKind.Limit)]
    [InlineData(TipKind.Hint)]
    public void TaggingAndSplittingAreExactOpposites(TipKind kind)
    {
        string tagged = TipLine.Tag(kind, "Cena: 120 prken");

        var found = TipLine.Split(tagged, out string text);

        Assert.Equal(kind, found);
        Assert.Equal("Cena: 120 prken", text);
    }

    [Fact]
    public void AnUntaggedLineIsPlainAndUnchanged()
    {
        // Starý kód, mody a cizí texty značku nemají. Nesmí se rozbít.
        var kind = TipLine.Split("obyčejná věta", out string text);

        Assert.Equal(TipKind.Plain, kind);
        Assert.Equal("obyčejná věta", text);
    }

    [Fact]
    public void EveryKindHasItsOwnColour()
    {
        // Dvě stejné barvy by znamenaly dvě kategorie, které hráč od sebe
        // nerozezná — a celé třídění by tím ztratilo smysl.
        var colors = Enum.GetValues<TipKind>().Select(TipLine.ColorFor).ToList();

        Assert.Equal(colors.Count, colors.Distinct().Count());
    }

    [Fact]
    public void TheDescriptionNeverShowsTheMarkerToThePlayer()
    {
        // Tohle je ta chyba, které se bojím nejvíc: značka je řídicí znak,
        // takže by v textu nebyla vidět, ale rozhodila by šířku i hledání.
        var content = LoadContent();
        var loc = new Localization(content.Languages, content.Languages[0].Id);

        for (int i = 0; i < content.Buildings.Count; i++)
        {
            string stripped = TipLine.Strip(BuildingSummary.Describe(content, loc, content.Buildings[i]));

            Assert.DoesNotContain(Marker, stripped);
        }
    }

    [Fact]
    public void TheRealDescriptionIsActuallyTagged()
    {
        // Kdyby se značkování někde ztratilo, bublina by zbělala a nikdo by si
        // toho nevšiml — barva chybí tiše.
        var content = LoadContent();
        var loc = new Localization(content.Languages, content.Languages[0].Id);
        var def = content.Buildings[content.Buildings.IndexOf("house")];

        string[] lines = BuildingSummary.Describe(content, loc, def).Split('\n');

        Assert.Contains(lines, line => TipLine.Split(line, out _) == TipKind.Cost);
    }

    private static GameContent LoadContent() =>
        new ContentLoader().LoadFrom(Path.Combine(AppContext.BaseDirectory, "data"));
}
