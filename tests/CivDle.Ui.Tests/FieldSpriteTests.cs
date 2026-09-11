using CivDle.Rendering.Sprites;
using Microsoft.Xna.Framework;
using Xunit;

namespace CivDle.Ui.Tests;

/// <summary>
/// Kresba pole.
///
/// <para>Pole zabírají největší souvislou plochu ze všech budov, takže jsou to
/// ony, kdo rozhoduje, jestli krajina vypadá jako krajina nebo jako tabulka.
/// Testuje se přesně to, co ten rozdíl dělá: že plocha <b>není jednolitá</b>,
/// že kolem dokola zůstává hlína a že řádky nekončí všechny na stejném pixelu.</para>
///
/// <para>Kreslí se do <see cref="PixelCanvas"/>, který je jen pole barev —
/// grafická karta k tomu není potřeba.</para>
/// </summary>
public class FieldSpriteTests
{
    private static readonly Color Soil = new(140, 106, 58);
    private static readonly Color Crop = new(146, 166, 78);

    private static PixelCanvas Field(bool vertical = true, int rowStep = 5, int seed = 3)
    {
        var canvas = new PixelCanvas(32, 32);
        FieldSprite.Draw(canvas, Soil, Crop, rowStep, vertical, seed);
        return canvas;
    }

    [Fact]
    public void TheSurfaceIsNotOneFlatColor()
    {
        // Tohle je celý problém v jednom tvrzení: dokud bylo pole pár odstínů,
        // vypadalo jako obarvený čtverec.
        var canvas = Field();

        var seen = new HashSet<uint>();
        for (int y = 0; y < 32; y++)
        {
            for (int x = 0; x < 32; x++)
            {
                seen.Add(canvas.At(x, y).PackedValue);
            }
        }

        Assert.True(seen.Count >= 5, $"pole má jen {seen.Count} odstínů — pořád je to plocha");
    }

    [Fact]
    public void EachFurrowHasALitAndAShadedSide()
    {
        // Bok brázdy je to, co z plochy dělá vlnitý povrch. Kdyby měly řádky
        // jednu barvu, byl by výsledek zase jen pruhovaný čtverec.
        var canvas = Field(vertical: true, rowStep: 5);

        int y = 16;
        int at = FieldSprite.Headland; // první řádek
        var lit = canvas.At(at, y);
        var body = canvas.At(at + 1, y);
        var shaded = canvas.At(at + 3, y);

        Assert.True(Luma(lit) > Luma(body), "bok ke slunci není světlejší než tělo řádku");
        Assert.True(Luma(shaded) < Luma(body), "odvrácený bok není tmavší než tělo řádku");
    }

    [Fact]
    public void ABareEdgeRunsAllTheWayAround()
    {
        // Úvrať drží dvě sousední pole oddělená. Bez ní se slijí v jednu plochu
        // a mřížka je zpátky.
        var canvas = Field();

        for (int i = 0; i < 32; i++)
        {
            Assert.False(IsCrop(canvas.At(i, 0)), $"porost sahá až na horní okraj v x={i}");
            Assert.False(IsCrop(canvas.At(i, 31)), $"porost sahá až na dolní okraj v x={i}");
            Assert.False(IsCrop(canvas.At(0, i)), $"porost sahá až na levý okraj v y={i}");
            Assert.False(IsCrop(canvas.At(31, i)), $"porost sahá až na pravý okraj v y={i}");
        }
    }

    [Fact]
    public void TheFieldDrawsNoDarkFrameOfItsOwn()
    {
        // Siluetu obtahuje knihovna spritů všem budovám stejně. Vlastní rámeček
        // by se s ní sečetl a dvě sousední pole by měla mezi sebou čtyři pixely
        // tmy — tedy přesně tu mřížku, kvůli které tahle kresba vznikla.
        var canvas = Field();

        Assert.Equal(canvas.At(1, 1), canvas.At(0, 0));
    }

    [Fact]
    public void RowsDoNotAllEndOnTheSamePixel()
    {
        // Pravítkem uříznutý porost je to, co z pole dělá ikonu.
        var canvas = Field(vertical: true, rowStep: 5);

        var tops = new List<int>();
        for (int x = FieldSprite.Headland; x < 32 - FieldSprite.Headland; x++)
        {
            for (int y = 0; y < 32; y++)
            {
                if (IsCrop(canvas.At(x, y)))
                {
                    tops.Add(y);
                    break;
                }
            }
        }

        Assert.True(tops.Count > 0, "v poli nevyrostlo vůbec nic");
        Assert.True(new HashSet<int>(tops).Count > 1, "všechny řádky začínají na témž pixelu");
    }

    [Fact]
    public void TheSameFieldIsDrawnTheSameWayTwice()
    {
        // Nepravidelnosti jsou z hashe, ne z náhody: jinak by pole při každém
        // spuštění hry vypadalo jinak.
        var a = Field();
        var b = Field();

        for (int y = 0; y < 32; y++)
        {
            for (int x = 0; x < 32; x++)
            {
                Assert.Equal(a.At(x, y), b.At(x, y));
            }
        }
    }

    [Fact]
    public void PloughingDirectionActuallyTurnsTheRows()
    {
        // Kdyby směr orby nic neudělal, měly by všechny kultury tutéž kresbu
        // a pole vedle pole by zase splynulo.
        var down = Field(vertical: true, rowStep: 5);
        var across = Field(vertical: false, rowStep: 5);

        int different = 0;
        for (int y = 0; y < 32; y++)
        {
            for (int x = 0; x < 32; x++)
            {
                if (down.At(x, y) != across.At(x, y))
                {
                    different++;
                }
            }
        }

        Assert.True(different > 200, $"orba napříč se od orby podél liší jen v {different} pixelech");
    }

    [Fact]
    public void ATinyCanvasStaysBareInsteadOfCrashing()
    {
        // Ikonky do UI jsou menší než dlaždice. Pole se na ně nevejde, ale
        // spadnout kvůli tomu nesmí.
        var canvas = new PixelCanvas(6, 6);
        FieldSprite.Draw(canvas, Soil, Crop, rowStep: 5, vertical: true, seed: 1);

        Assert.NotEqual(Color.Transparent, canvas.At(3, 3));
    }

    private static bool IsCrop(Color color) => color.G > color.R;

    private static float Luma(Color color) => 0.299f * color.R + 0.587f * color.G + 0.114f * color.B;
}
