using CivDle.Screens;
using Microsoft.Xna.Framework;
using Xunit;

namespace CivDle.Ui.Tests;

/// <summary>
/// Kruhová nabídka na ovladači.
///
/// <para>Testuje se výběr výseče — „kam palec ukazuje" je rozhodnutí
/// o ovládání, ne o kreslení, a bez ovladače se dá ověřit celé. To ostatní
/// (jak to vypadá) test stejně neposoudí.</para>
/// </summary>
public class RadialMenuTests
{
    [Fact]
    public void ARestingStickPicksNothing()
    {
        // Páčka na Decku driftuje. Kdyby se lehká výchylka počítala jako volba,
        // nabídka by pustila nástroj, o který nikdo nestál.
        Assert.Equal(-1, RadialMenu.PickIndex(Vector2.Zero, 6));
        Assert.Equal(-1, RadialMenu.PickIndex(new Vector2(0.1f, 0.1f), 6));
    }

    [Fact]
    public void UpIsTheFirstItem()
    {
        Assert.Equal(0, RadialMenu.PickIndex(new Vector2(0, 1f), 6));
    }

    [Theory]
    [InlineData(0f, 1f, 0)]     // nahoru
    [InlineData(1f, 0f, 1)]     // doprava
    [InlineData(0f, -1f, 2)]    // dolů
    [InlineData(-1f, 0f, 3)]    // doleva
    public void TheStickPicksTheSliceItPointsAt(float x, float y, int expected)
    {
        // Čtyři položky schválně: světové strany pak padnou přesně doprostřed
        // výsečí. U šesti leží „doleva" na hranici dvou a která z nich vyhraje
        // je libovolné rozhodnutí, ne vlastnost, kterou má cenu testovat.
        Assert.Equal(expected, RadialMenu.PickIndex(new Vector2(x, y), 4));
    }

    [Fact]
    public void EveryDirectionLandsSomewhereValid()
    {
        // Žádný úhel nesmí spadnout mimo rozsah — ani přesně na hranici výsečí.
        for (int degrees = 0; degrees < 360; degrees++)
        {
            double radians = degrees * Math.PI / 180.0;
            var stick = new Vector2((float)Math.Sin(radians), (float)Math.Cos(radians));

            int picked = RadialMenu.PickIndex(stick, 6);

            Assert.InRange(picked, 0, 5);
        }
    }

    [Fact]
    public void AnEmptyMenuPicksNothing()
    {
        Assert.Equal(-1, RadialMenu.PickIndex(new Vector2(0, 1f), 0));
    }

    [Fact]
    public void EverySliceIsReachable()
    {
        // Kdyby některá výseč nešla trefit, byla by tam položka, ke které se
        // hráč nedostane.
        var reached = new HashSet<int>();
        for (int degrees = 0; degrees < 360; degrees++)
        {
            double radians = degrees * Math.PI / 180.0;
            reached.Add(RadialMenu.PickIndex(
                new Vector2((float)Math.Sin(radians), (float)Math.Cos(radians)), 8));
        }

        Assert.Equal(8, reached.Count);
    }

    [Fact]
    public void ANewMenuIsClosed()
    {
        var menu = new RadialMenu();

        Assert.False(menu.IsOpen);
        Assert.Equal(0, menu.Count);
        Assert.Equal(-1, menu.Selected);
    }

    [Fact]
    public void ItRemembersItsItems()
    {
        var menu = new RadialMenu();
        menu.SetItems(new[]
        {
            new RadialItem(null, "první", () => { }),
            new RadialItem(null, "druhá", () => { }),
        });

        Assert.Equal(2, menu.Count);
    }
}
