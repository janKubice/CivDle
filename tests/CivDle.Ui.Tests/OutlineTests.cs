using CivDle.Rendering.Sprites;
using Microsoft.Xna.Framework;
using Xunit;

namespace CivDle.Ui.Tests;

/// <summary>
/// Obtažení siluety.
///
/// <para>Budova ležela na terénu bez hranice. Střecha o podobném jasu jako
/// tráva pod ní splynula a z bloku domů byla skvrna — oko nemělo za co chytit
/// tvar. Tmavší okraj je nejstarší trik pixel artu a dělá přesně tohle:
/// odlepí objekt od pozadí, ať je pozadí jakékoli.</para>
///
/// <para>Testuje se, co by obrys zkazil, kdyby se to udělalo naivně: že se
/// nešahá dovnitř kresby, že se obrys nelepí na stíny a že obrázek nemění
/// velikost.</para>
/// </summary>
public class OutlineTests
{
    private static readonly Color Body = new(180, 140, 100);

    [Fact]
    public void TheRimGetsDarkerThanTheInside()
    {
        var canvas = new PixelCanvas(8, 8);
        canvas.FillRect(2, 2, 4, 4, Body);

        canvas.Outline(0.3f);

        Assert.True(Luma(canvas.At(2, 2)) < Luma(canvas.At(3, 3)),
            "kraj kresby není tmavší než vnitřek");
    }

    [Fact]
    public void TheInsideIsLeftAlone()
    {
        // Obrys má tvar zvýraznit, ne celou budovu ztmavit.
        var canvas = new PixelCanvas(9, 9);
        canvas.FillRect(2, 2, 5, 5, Body);

        canvas.Outline(0.3f);

        Assert.Equal(Body, canvas.At(4, 4));
    }

    [Fact]
    public void EmptySpaceStaysEmpty()
    {
        // Obtahuje se zevnitř. Prstenec ven by obrázek zvětšil a sprite by
        // přerostl svůj půdorys.
        var canvas = new PixelCanvas(8, 8);
        canvas.FillRect(3, 3, 2, 2, Body);

        canvas.Outline(0.5f);

        Assert.Equal(0, canvas.At(0, 0).A);
        Assert.Equal(0, canvas.At(2, 3).A);
    }

    [Fact]
    public void ShadowsAreNotOutlined()
    {
        // Stín pod stromem je taky kresba. Obtáhnout ho by znamenalo tmavý
        // prstenec kolem stínu — přesně ten nálepkový dojem, proti kterému
        // obrys je.
        var shadow = new Color(0, 0, 0, 78);
        var canvas = new PixelCanvas(8, 8);
        canvas.FillRect(2, 2, 4, 4, shadow);

        var before = canvas.At(2, 2);
        canvas.Outline(0.5f);

        Assert.Equal(before, canvas.At(2, 2));
    }

    [Fact]
    public void ADrawingThatFillsTheCanvasStillGetsAnEdge()
    {
        // Dvě budovy vedle sebe by jinak splynuly v jednu plochu — a přesně
        // to dělalo z bloku domů skvrnu.
        var canvas = new PixelCanvas(8, 8);
        canvas.FillRect(0, 0, 8, 8, Body);

        canvas.Outline(0.3f);

        Assert.True(Luma(canvas.At(0, 0)) < Luma(canvas.At(4, 4)),
            "kresba přes celé plátno nedostala lem");
    }

    [Fact]
    public void ZeroStrengthChangesNothing()
    {
        var canvas = new PixelCanvas(8, 8);
        canvas.FillRect(2, 2, 4, 4, Body);

        canvas.Outline(0f);

        Assert.Equal(Body, canvas.At(2, 2));
    }

    [Fact]
    public void TheOutlineFollowsTheShapeNotItsBoundingBox()
    {
        // Budovy nejsou obdélníky — mají zasunuté vchody, přístavky a nárožní
        // věže. Kdyby se obtahoval jen opsaný obdélník, ztratil by se přesně
        // ten tvar, kvůli kterému se budovy od sebe poznají.
        var canvas = new PixelCanvas(10, 10);
        canvas.FillRect(2, 2, 6, 3, Body); // vodorovné rameno
        canvas.FillRect(2, 5, 3, 3, Body); // svislé rameno — vznikne L

        canvas.Outline(0.3f);

        // Vnitřní roh L: pixel u zářezu je kraj, i když leží uprostřed
        // opsaného obdélníku.
        Assert.True(Luma(canvas.At(5, 5)) < Luma(canvas.At(3, 3)),
            "vnitřní roh nedostal lem — obtahuje se opsaný obdélník");
    }

    private static float Luma(Color c) => 0.299f * c.R + 0.587f * c.G + 0.114f * c.B;
}
