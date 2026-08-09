using CivDle.Screens;
using Microsoft.Xna.Framework;
using Xunit;

namespace CivDle.Ui.Tests;

/// <summary>
/// Barvy rozhraní: jedna akcentní, zbytek do šedi podle jasu.
///
/// <para>Obrazovky vznikaly postupně a nasbíraly přes sto devadesát různých
/// barev — mimo jiné patnáct skoro stejných zlatých. Testuje se to, co z
/// úklidu dělá vylepšení: že šedá škála má opravdu <b>rozeznatelné</b> stupně
/// (jinak zmizí hierarchie textu), že akcent zůstal jeden, a hlavně že zelená
/// a červená u cen nezšedly — to je informace, ne ozdoba.</para>
/// </summary>
public sealed class UiPaletteTests
{
    [Fact]
    public void TheGreyScaleGoesFromBrightToFaint()
    {
        // Hierarchie textu stojí a padá s tím, že jsou stupně od sebe poznat.
        Assert.True(Luminance(UiPalette.TextBright) > Luminance(UiPalette.Text));
        Assert.True(Luminance(UiPalette.Text) > Luminance(UiPalette.TextDim));
        Assert.True(Luminance(UiPalette.TextDim) > Luminance(UiPalette.TextFaint));
    }

    [Fact]
    public void NeighbouringGreysAreTellableApart()
    {
        // Dva stupně o pět jednotek vedle sebe jsou k ničemu — to už je jeden.
        Assert.True(Luminance(UiPalette.TextBright) - Luminance(UiPalette.Text) > 25f);
        Assert.True(Luminance(UiPalette.Text) - Luminance(UiPalette.TextDim) > 25f);
        Assert.True(Luminance(UiPalette.TextDim) - Luminance(UiPalette.TextFaint) > 25f);
    }

    [Fact]
    public void TheTextScaleReallyIsGrey()
    {
        // Kdyby měl „šedý" text nádech, vrátila by se tím barevná nesourodost
        // zadními vrátky.
        AssertNeutral(UiPalette.TextBright);
        AssertNeutral(UiPalette.Text);
        AssertNeutral(UiPalette.TextDim);
        AssertNeutral(UiPalette.TextFaint);
    }

    [Fact]
    public void TheAccentIsTheOnlyColourThatIsNotGreyOrMeaning()
    {
        Assert.True(Saturation(UiPalette.Accent) > 60,
            $"Akcent {UiPalette.Accent} je moc vybledlý na to, aby něco zvýraznil.");
    }

    [Fact]
    public void TheAccentIsColdSoItNeverBlendsIntoTheWorld()
    {
        // Svět je teplý (hlína, dřevo, noční okna). Teplý akcent by se v něm
        // ztratil a rozhraní by přestalo být oddělitelné od scény.
        Assert.True(UiPalette.Accent.B > UiPalette.Accent.R);
        Assert.True(UiPalette.AccentDim.B > UiPalette.AccentDim.R);
    }

    [Fact]
    public void TheDimAccentIsTheSameColourJustQuieter()
    {
        Assert.True(Luminance(UiPalette.AccentDim) < Luminance(UiPalette.Accent));
        Assert.True(UiPalette.AccentDim.B > UiPalette.AccentDim.G);
        Assert.True(UiPalette.Accent.B > UiPalette.Accent.G);
    }

    [Fact]
    public void MeaningKeepsItsColour()
    {
        // Vědomá výjimka z šedi: zelenou „mám na to" a červenou „nemám" čte
        // hráč periferně u každé ceny. Kdyby zšedly, musel by číst čísla.
        Assert.True(UiPalette.Good.G > UiPalette.Good.R, "Zelená není zelená.");
        Assert.True(UiPalette.Bad.R > UiPalette.Bad.G, "Červená není červená.");
        Assert.True(UiPalette.Warn.R > UiPalette.Warn.B, "Výstražná není teplá.");
    }

    [Fact]
    public void GoodAndBadAreNotConfusableForColourBlindPlayers()
    {
        // Zelená a červená o stejném jasu jsou pro část hráčů táž barva — a je
        // to zrovna dvojice u cen, kterou hráč čte koutkem oka. Jas je to
        // jediné, co jim zbývá, takže musí být odstupňovaný celý trojlístek.
        Assert.True(Luminance(UiPalette.Good) - Luminance(UiPalette.Warn) > 15f,
            "Zelená a výstražná mají skoro stejný jas.");
        Assert.True(Luminance(UiPalette.Warn) - Luminance(UiPalette.Bad) > 15f,
            "Výstražná a červená mají skoro stejný jas.");
    }

    [Fact]
    public void PanelsAreDarkEnoughToReadTextOn()
    {
        Assert.True(Luminance(UiPalette.Panel) < 70f);
        Assert.True(Luminance(UiPalette.PanelDeep) < Luminance(UiPalette.Panel));
        Assert.True(UiPalette.Panel.A > 200, "Panel prosvítá — text na něm nebude čitelný.");
    }

    [Fact]
    public void ColouredPanelsStayBackgroundsNotSignals()
    {
        // Barevná výplň má napovědět náladu, ne přebít text, který na ní leží.
        Assert.True(Luminance(UiPalette.PanelAccent) < 100f);
        Assert.True(Luminance(UiPalette.PanelGood) < 100f);
        Assert.True(Luminance(UiPalette.PanelBad) < 100f);
    }

    [Fact]
    public void TonesFollowTheBrightnessTheyAreAskedFor()
    {
        Assert.True(Luminance(UiPalette.Tone(220)) > Luminance(UiPalette.Tone(120)));
        Assert.True(Luminance(UiPalette.Tone(120)) > Luminance(UiPalette.Tone(40)));
    }

    [Fact]
    public void TonesClampInsteadOfWrappingAround()
    {
        // Přetečení bajtu by z nejsvětlejšího odstínu udělalo černou.
        Assert.Equal(UiPalette.Tone(255), UiPalette.Tone(9000));
        Assert.Equal(UiPalette.Tone(0), UiPalette.Tone(-40));
    }

    private static void AssertNeutral(Color color) =>
        Assert.True(Saturation(color) <= 16,
            $"{color} není šedá (rozptyl složek {Saturation(color)}).");

    private static float Luminance(Color color) => (color.R + color.G + color.B) / 3f;

    private static int Saturation(Color color) =>
        Math.Max(color.R, Math.Max(color.G, color.B)) - Math.Min(color.R, Math.Min(color.G, color.B));
}
