using CivDle.Rendering.Sprites;
using Microsoft.Xna.Framework;
using Xunit;

namespace CivDle.Ui.Tests;

/// <summary>
/// Společná paleta hry.
///
/// <para>Sprity vznikaly postupně a každý si nesl vlastní odstíny — pár set
/// různých hnědých, které se lišily o jednotky. Oko to nečte jako bohatství,
/// ale jako špínu. Testuje se proto to, co ze srovnání dělá vylepšení a ne
/// škodu: že je paleta malá, že má rampy (jinak se nedá stínovat), že drží
/// odstín a hlavně že nesahá na průhlednost.</para>
/// </summary>
public sealed class GamePaletteTests
{
    [Fact]
    public void ThePaletteIsSmallEnoughToHoldTogether()
    {
        // Nad ~32 barvami přestává být paleta paletou.
        Assert.InRange(GamePalette.Count, 16, 32);
    }

    [Fact]
    public void EveryColourInThePaletteIsDistinct()
    {
        var seen = new HashSet<uint>();
        for (int i = 0; i < GamePalette.Count; i++)
        {
            Assert.True(seen.Add(GamePalette.At(i).PackedValue),
                $"Barva {i} je v paletě dvakrát.");
        }
    }

    [Fact]
    public void SnappingIsIdempotent()
    {
        // Barva z palety se nesmí posunout jinam — jinak by druhé srovnání
        // (třeba u modem dodaného spritu) kresbu rozjelo.
        for (int i = 0; i < GamePalette.Count; i++)
        {
            var color = GamePalette.At(i);
            Assert.Equal(color, GamePalette.Snap(color));
        }
    }

    [Fact]
    public void SnappingNeverTouchesTransparency()
    {
        // Průhlednost je tvar, ne barva. Kdyby na ni snap sáhl, rozpadly by se
        // měkké okraje všech spritů naráz.
        foreach (byte alpha in new byte[] { 1, 40, 128, 200, 255 })
        {
            var snapped = GamePalette.Snap(new Color(200, 30, 90) * (alpha / 255f));
            Assert.Equal(alpha, snapped.A);
        }
    }

    [Fact]
    public void FullyTransparentPixelsAreLeftAlone()
    {
        var invisible = new Color(0, 0, 0, 0);

        Assert.Equal(invisible, GamePalette.Snap(invisible));
    }

    [Fact]
    public void SnappingKeepsTheHue()
    {
        // Zelený strom nesmí po srovnání zhnědnout — to je ta nejhorší
        // varianta: „sjednotil jsem paletu" a hra vypadá jinak, než měla.
        AssertHueKept(new Color(60, 150, 70));   // listí
        AssertHueKept(new Color(40, 90, 160));   // voda
        AssertHueKept(new Color(180, 60, 50));   // střecha
        AssertHueKept(new Color(230, 200, 90));  // světlo
    }

    [Fact]
    public void SnappingKeepsLightAndDarkApart()
    {
        // Rampa je celý smysl palety: bez ní se nedá stínovat.
        var dark = GamePalette.Snap(new Color(30, 60, 32));
        var light = GamePalette.Snap(new Color(150, 200, 110));

        Assert.True(Luminance(light) > Luminance(dark) + 60f,
            $"Světlá a tmavá zeleň spadly k sobě: {light} vs {dark}.");
    }

    [Fact]
    public void ThePaletteSpansFromNearBlackToNearWhite()
    {
        float darkest = float.MaxValue;
        float brightest = float.MinValue;
        for (int i = 0; i < GamePalette.Count; i++)
        {
            float luminance = Luminance(GamePalette.At(i));
            darkest = MathF.Min(darkest, luminance);
            brightest = MathF.Max(brightest, luminance);
        }

        Assert.True(darkest < 45f, $"Paleta nemá dost tmavou barvu ({darkest:F0}).");
        Assert.True(brightest > 200f, $"Paleta nemá dost světlou barvu ({brightest:F0}).");
    }

    [Fact]
    public void NothingTheGameDrawsEverSnapsFarAway()
    {
        // Kdyby některá barva neměla v paletě rozumného souseda, byla by na
        // spritu vidět jako skvrna.
        //
        // Plně sytá čistá modř nebo purpur (rohy RGB krychle) se sem
        // nepočítají: od těch je každá malá paleta z principu daleko a hra je
        // nekreslí — sprity žijí v tlumených, mírně zašedlých odstínech.
        for (int r = 0; r <= 255; r += 17)
        {
            for (int g = 0; g <= 255; g += 17)
            {
                for (int b = 0; b <= 255; b += 17)
                {
                    var original = new Color(r, g, b);
                    if (Math.Max(r, Math.Max(g, b)) - Math.Min(r, Math.Min(g, b)) > 180)
                    {
                        continue;
                    }

                    var snapped = GamePalette.Snap(original);
                    Assert.True(Distance(original, snapped) < 150f,
                        $"{original} skočila až na {snapped}.");
                }
            }
        }
    }

    private static void AssertHueKept(Color original)
    {
        var snapped = GamePalette.Snap(original);
        Assert.Equal(Dominant(original), Dominant(snapped));
    }

    /// <summary>Který kanál v barvě převládá (hrubá náhrada odstínu).</summary>
    private static char Dominant(Color color)
    {
        if (color.R >= color.G && color.R >= color.B)
        {
            return 'R';
        }

        return color.G >= color.B ? 'G' : 'B';
    }

    private static float Luminance(Color color) => (color.R + color.G + color.B) / 3f;

    private static float Distance(Color a, Color b) => MathF.Sqrt(
        (a.R - b.R) * (a.R - b.R) + (a.G - b.G) * (a.G - b.G) + (a.B - b.B) * (a.B - b.B));
}
