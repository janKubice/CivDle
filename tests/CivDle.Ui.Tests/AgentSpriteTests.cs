using System.Reflection;
using CivDle.Rendering.Sprites;
using Xunit;

namespace CivDle.Ui.Tests;

/// <summary>
/// Kresby chodců, vozíků a rybářů.
///
/// <para>Zvířata dostala stín zapečený ve spritu, takže stojí na zemi. Chodec
/// vedle nich se ale vznášel — stín u něj nikdy nebyl, přestože o něm v kódu
/// stál komentář. Rozdíl je vidět hned, jakmile jdou srnec a člověk přes tutéž
/// louku.</para>
///
/// <para>Testuje se i to, že kresba <b>nepřeteče plátno</b>. To je zákeřná
/// třída chyby: <see cref="PixelCanvas"/> pixely za okrajem tiše zahodí, takže
/// nic nespadne a nic nekřičí — jen rybář stojí na pahýlech, protože se mu
/// nohy kreslily o dva řádky níž, než jak bylo plátno vysoké.</para>
/// </summary>
public class AgentSpriteTests
{
    [Theory]
    [InlineData("Person", 12, 12)]
    [InlineData("Fisherman", 12, 15)]
    [InlineData("Cart", 18, 16)]
    public void AnAgentCastsAShadow(string method, int width, int height)
    {
        var canvas = Paint(method, width, height);

        Assert.True(
            HasShadowRow(canvas),
            $"{method} nemá u paty stín — vedle zvířat se bude vznášet");
    }

    [Theory]
    [InlineData("Person", 12, 12)]
    [InlineData("Fisherman", 12, 15)]
    [InlineData("Cart", 18, 16)]
    public void AnAgentStandsOnTheGroundNotAboveIt(string method, int width, int height)
    {
        // Kotva agenta je dole uprostřed, takže spodek plátna JE zem. Prázdné
        // řádky dole znamenají, že se postava vznáší nad svým vlastním bodem.
        var canvas = Paint(method, width, height);

        Assert.True(
            RowHasContent(canvas, height - 1) || RowHasContent(canvas, height - 2),
            $"{method} má dole na plátně prázdno — vznáší se nad zemí");
    }

    [Fact]
    public void TheFishermanHasLegs()
    {
        // Nohy se kreslily na řádky 12-13 na plátně vysokém dvanáct řádků
        // (tedy 0-11). PixelCanvas je tiše zahodil — žádná chyba, žádný pád,
        // jen rybář bez nohou. Test hlídá, že se zase neztratí.
        var canvas = Paint("Fisherman", 12, 15);

        Assert.True(
            RowHasContent(canvas, 12) && RowHasContent(canvas, 13),
            "rybáři se zase ořízly nohy o okraj plátna");
    }

    [Fact]
    public void TheShadowIsSeeThrough()
    {
        // Plný stín pod drobnou postavou vypadá jako díra v zemi, ne jako stín.
        var canvas = Paint("Person", 12, 12);

        for (int x = 0; x < canvas.Width; x++)
        {
            var pixel = canvas.At(x, canvas.Height - 1);
            if (pixel.A > 0)
            {
                Assert.InRange(pixel.A, 1, 120);
            }
        }
    }

    /// <summary>Má řádek aspoň jeden průsvitný tmavý pixel, tedy stín?</summary>
    private static bool HasShadowRow(PixelCanvas canvas)
    {
        for (int y = canvas.Height - 1; y >= canvas.Height - 3 && y >= 0; y--)
        {
            for (int x = 0; x < canvas.Width; x++)
            {
                var pixel = canvas.At(x, y);
                if (pixel.A is > 0 and < 160 && pixel.R < 90 && pixel.G < 90 && pixel.B < 90)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool RowHasContent(PixelCanvas canvas, int y)
    {
        for (int x = 0; x < canvas.Width; x++)
        {
            if (canvas.At(x, y).A > 0)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Zavolá tutéž privátní kresbu, jakou používá knihovna. Sprity se generují
    /// kódem, takže se jinak než reflexí bez grafické karty vyzkoušet nedají —
    /// stejný postup používá <c>FlatSpriteTests</c>.
    /// </summary>
    private static PixelCanvas Paint(string method, int width, int height)
    {
        var draw = typeof(SpriteLibrary).GetMethod(
            method, BindingFlags.NonPublic | BindingFlags.Static);

        Assert.True(draw is not null, $"kresba {method} v SpriteLibrary není");

        var canvas = new PixelCanvas(width, height);
        draw!.Invoke(null, new object[] { canvas });
        return canvas;
    }
}
