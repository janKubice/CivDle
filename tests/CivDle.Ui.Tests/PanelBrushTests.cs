using CivDle.Screens;
using Microsoft.Xna.Framework;
using Xunit;

namespace CivDle.Ui.Tests;

/// <summary>
/// Panel, který vypadá jako panel.
///
/// <para>Celé rozhraní kreslilo panely jedním plným odstínem. Plná plocha nemá
/// povrch: nedá se z ní poznat, co je nahoře a co dole, ani kde jeden panel
/// končí a druhý začíná. HUD je přitom v každém screenshotu — je to jediná
/// věc, kterou divák uvidí i tehdy, když se na hru dívá pět vteřin.</para>
///
/// <para>Samotné kreslení potřebuje Myru a grafické zařízení, takže se testuje
/// to, co rozhoduje o vzhledu a co jde spočítat: odstíny, ze kterých se panel
/// skládá, a to, že si drží krytí. Průhledný panel by nechal scénu prosvítat
/// skrz čísla.</para>
/// </summary>
public class PanelBrushTests
{
    private static readonly Color Base = new(24, 28, 38, 235);

    [Fact]
    public void ThePanelRemembersWhatColorItCameFrom()
    {
        // Z barvy panelu se odvozují další odstíny jinde v rozhraní; kdyby se
        // ztratila, musela by se psát dvakrát.
        var brush = new PanelBrush(Base);

        Assert.Equal(Base, brush.BaseColor);
    }

    [Fact]
    public void EveryPanelColorKeepsItsOpacity()
    {
        // Průhledný panel nechá scénu prosvítat skrz čísla a ta přestanou být
        // čitelná přesně nad světlým terénem.
        foreach (var color in new[] { UiPaletteProbe.Panel, UiPaletteProbe.PanelDeep })
        {
            var brush = new PanelBrush(color);

            Assert.Equal(color.A, brush.BaseColor.A);
        }
    }

    [Fact]
    public void TheBandsCoverThePanelExactly()
    {
        // Kdyby poslední pruh nedosáhl na spodní okraj, prosvítal by pod
        // panelem proužek scény — a pod čísly v HUD zrovna.
        const int top = 40, height = 97, count = 12;

        Assert.Equal(top, PanelBrush.BandBounds(0, count, top, height).Top);
        Assert.Equal(top + height, PanelBrush.BandBounds(count - 1, count, top, height).Bottom);
    }

    [Fact]
    public void TheBandsDoNotOverlapOrLeaveGaps()
    {
        const int top = 0, height = 100, count = 12;

        for (int i = 1; i < count; i++)
        {
            Assert.Equal(
                PanelBrush.BandBounds(i - 1, count, top, height).Bottom,
                PanelBrush.BandBounds(i, count, top, height).Top);
        }
    }

    [Fact]
    public void AZeroHeightPanelProducesEmptyBands()
    {
        // Widget o nulové výšce vznikne při skládání rozvržení dřív, než se
        // spočítají velikosti.
        var (top, bottom) = PanelBrush.BandBounds(3, 12, 10, 0);

        Assert.Equal(top, bottom);
    }

    [Fact]
    public void APanelShorterThanItsBandsStillFitsInside()
    {
        // Pět pixelů na dvanáct pruhů: většina vyjde prázdná, ale žádný nesmí
        // přetéct ven — to by byl pruh nakreslený přes sousední widget.
        const int top = 7, height = 5, count = 12;

        for (int i = 0; i < count; i++)
        {
            var (from, to) = PanelBrush.BandBounds(i, count, top, height);
            Assert.InRange(from, top, top + height);
            Assert.InRange(to, top, top + height);
        }
    }
}

/// <summary>
/// Barvy panelů pro testy. <c>UiPalette</c> je <c>internal</c> a testovací
/// projekt na ni nevidí; opsané hodnoty by se rozešly, takže se čtou odrazem
/// ze sestavy hry.
/// </summary>
internal static class UiPaletteProbe
{
    public static Color Panel => Read(nameof(Panel));

    public static Color PanelDeep => Read(nameof(PanelDeep));

    private static Color Read(string name)
    {
        var type = typeof(PanelBrush).Assembly.GetType("CivDle.Screens.UiPalette")!;
        return (Color)type.GetField(name)!.GetValue(null)!;
    }
}
