using CivDle.Rendering;
using Microsoft.Xna.Framework;
using Xunit;

namespace CivDle.Ui.Tests;

/// <summary>
/// Jedno světlo pro celou scénu.
///
/// <para>Testuje se ta vlastnost, kvůli které to vzniklo: <b>všechno</b> vrhá
/// stín stejným směrem. Kdyby se směr někde spočítal opačně, vypadala by ta
/// budova jako vystřižená z jiného obrázku — a to je přesně ta chyba, které si
/// člověk na screenshotu všimne, ale v kódu ne.</para>
/// </summary>
public sealed class SceneLightTests
{
    [Fact]
    public void ShadowsAlwaysFallTheSameWay()
    {
        // Doprava dolů, ať je objekt jakkoli velký.
        for (int tiles = 1; tiles <= 64; tiles++)
        {
            var offset = SceneLight.OffsetFor(tiles);
            Assert.True(offset.X > 0, $"Stín objektu o {tiles} dlaždicích nejde doprava.");
            Assert.True(offset.Y > 0, $"Stín objektu o {tiles} dlaždicích nejde dolů.");
        }
    }

    [Fact]
    public void BiggerBuildingsCastLongerShadows()
    {
        Assert.True(SceneLight.LengthFor(9) > SceneLight.LengthFor(1));
        Assert.True(SceneLight.LengthFor(4) > SceneLight.LengthFor(1));
    }

    [Fact]
    public void EvenAMegastructureShadowStaysOnItsOwnStreet()
    {
        // Bez stropu by stín osmidlaždicové stavby přeletěl půl obrazovky
        // a zakryl ulici, na které stojí.
        Assert.Equal(SceneLight.MaxLength, SceneLight.LengthFor(10_000));
        Assert.True(SceneLight.LengthFor(64) <= SceneLight.MaxLength);
    }

    [Fact]
    public void TheSmallestShadowIsStillVisible()
    {
        Assert.True(SceneLight.LengthFor(1) >= SceneLight.MinLength);
        Assert.True(SceneLight.LengthFor(0) >= SceneLight.MinLength); // půdorys 0 je nesmysl, ne pád
    }

    [Fact]
    public void TheShadowSitsBesideTheBuildingNotOnTopOfIt()
    {
        var bounds = new Rectangle(100, 100, 32, 32);

        var shadow = SceneLight.ShadowRect(bounds, 4);

        Assert.True(shadow.X > bounds.X);
        Assert.True(shadow.Y > bounds.Y);
        Assert.True(shadow.Height > 0);
    }

    [Fact]
    public void ContactDarkeningHugsTheBuildingOnAllSides()
    {
        var bounds = new Rectangle(0, 0, 16, 16);

        var contact = SceneLight.ContactRect(bounds);

        Assert.True(contact.X < bounds.X);
        Assert.True(contact.Y < bounds.Y);
        Assert.True(contact.Right > bounds.Right);
        Assert.True(contact.Bottom > bounds.Bottom);
    }

    [Fact]
    public void ShadowsAreASuggestionNotAHole()
    {
        // Silný stín z pixelové hry udělá dírkovaný ementál. Sytost je věc,
        // která se ladí, takže má mít hlídaný strop.
        Assert.InRange(SceneLight.ShadowAlpha, 0.05f, 0.4f);
        Assert.InRange(SceneLight.ContactAlpha, 0.02f, 0.25f);
        Assert.True(SceneLight.ContactAlpha < SceneLight.ShadowAlpha);
    }

    [Fact]
    public void ShadowsAreBluishNotBlack()
    {
        // Černý stín působí jako vyříznutá díra; modravý jako denní světlo.
        var shadow = SceneLight.ShadowColor;

        Assert.True(shadow.B > shadow.R, "Stín není namodralý.");
        Assert.True(shadow.R + shadow.G + shadow.B > 0, "Stín je úplně černý.");
    }
}
