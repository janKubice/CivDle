using CivDle.Rendering;
using Microsoft.Xna.Framework;
using Xunit;

namespace CivDle.Ui.Tests;

/// <summary>
/// Geometrie stínu.
///
/// <para>První verze kreslila dva plné obdélníky — kopii budovy posunutou
/// stranou a rámeček kolem paty. Na obrazovce z toho byly tvrdé tmavé krabice,
/// které se u shluků slévaly do špinavých ploch. Testy proto hlídají
/// vlastnosti, kterými se nová verze od té staré liší: skvrna je <b>plochá</b>,
/// leží <b>u paty</b> a <b>nepřerůstá</b> budovu.</para>
/// </summary>
public sealed class SceneLightTests
{
    private static readonly Rectangle House = new(100, 100, 16, 16);

    [Fact]
    public void ShadowFallsDownAndToTheRight()
    {
        // Jeden směr světla pro celou scénu je to, co dělá z nálepek místo.
        var shadow = SceneLight.ShadowRect(House, footprintTiles: 1);

        Assert.True(shadow.Center.X > House.Center.X);
        Assert.True(shadow.Center.Y > House.Center.Y);
    }

    [Fact]
    public void BiggerBuildingsCastFurther()
    {
        var small = SceneLight.ShadowRect(House, footprintTiles: 1);
        var large = SceneLight.ShadowRect(House, footprintTiles: 9);

        Assert.True(large.Center.X > small.Center.X);
    }

    [Fact]
    public void EvenAMegastructureStaysWithinReason()
    {
        // Bez stropu by osmidlaždicová stavba odhodila stín přes půl ulice.
        Assert.Equal(SceneLight.MaxLength, SceneLight.LengthFor(10_000));
    }

    [Fact]
    public void TheShadowIsFlatNotACircle()
    {
        // Kruh pod domem vypadá v top-down pohledu jako díra.
        var shadow = SceneLight.ShadowRect(House, footprintTiles: 1);

        Assert.True(shadow.Height < shadow.Width * 0.7f);
    }

    [Fact]
    public void TheShadowSitsAtTheFootNotUnderTheMiddle()
    {
        // Kdyby seděl na středu budovy, vypadala by, že se nad ním vznáší.
        var shadow = SceneLight.ShadowRect(House, footprintTiles: 1);

        Assert.True(shadow.Center.Y >= House.Bottom - shadow.Height);
    }

    [Fact]
    public void TheShadowDoesNotDwarfTheBuilding()
    {
        // Stín větší než objekt čte oko jako druhý objekt — přesně ta chyba,
        // kvůli které první verze vypadala jako flek na trávě.
        var shadow = SceneLight.ShadowRect(House, footprintTiles: 1);

        Assert.True(shadow.Width <= House.Width * 1.5f);
        Assert.True(shadow.Height <= House.Height);
    }

    [Fact]
    public void TheShadowIsNeverDegenerate()
    {
        // Nulový nebo záporný rozměr by SpriteBatch nenakreslil vůbec.
        foreach (int size in new[] { 1, 2, 4, 16, 64, 256 })
        {
            var shadow = SceneLight.ShadowRect(new Rectangle(0, 0, size, size), 1);

            Assert.True(shadow.Width > 0);
            Assert.True(shadow.Height > 0);
        }
    }

    [Fact]
    public void ItIsDiscreetNotADarkPatch()
    {
        Assert.InRange(SceneLight.ShadowAlpha, 0.15f, 0.45f);
    }

    [Fact]
    public void ShadowsCanBeTurnedOff()
    {
        // Vypnutí je legitimní volba, ne degradace: stín je výrazný zásah do
        // vzhledu a ne každému sedí.
        try
        {
            SceneLight.Apply(false);
            Assert.False(SceneLight.Enabled);
        }
        finally
        {
            SceneLight.Apply(true);
        }
    }
}
