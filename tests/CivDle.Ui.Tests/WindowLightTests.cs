using CivDle.Rendering.Sprites;
using Microsoft.Xna.Framework;
using Xunit;

namespace CivDle.Ui.Tests;

/// <summary>
/// Rozsvícená okna musí sedět na okna ve spritu.
///
/// <para>Polohy oken se dřív losovaly z hashe kdekoli uvnitř obdélníku budovy.
/// V noci tak svítilo uprostřed střechy nebo ve zdi vedle skutečného okna — na
/// hotovém spritu je to vidět okamžitě a kazí to jinak pěknou scénu. Sprity
/// teď své okna hlásí samy.</para>
///
/// <para>Testuje se plátno, ne knihovna: registrace spritů potřebuje grafické
/// zařízení, samotné zaznamenání oken ne.</para>
/// </summary>
public class WindowLightTests
{
    private static readonly Color Glass = new(150, 205, 225);

    [Fact]
    public void ADrawnWindowIsRemembered()
    {
        var canvas = new PixelCanvas(32, 32);

        canvas.Window(9, 19, 4, 4, Glass);

        Assert.Single(canvas.Windows);
        Assert.Equal(new Rectangle(9, 19, 4, 4), canvas.Windows[0]);
    }

    [Fact]
    public void AWindowIsAlsoActuallyPainted()
    {
        // Zaznamenat okno a nenakreslit ho by znamenalo světlo visící na
        // prázdné zdi — přesně ta chyba, kterou tohle má odstranit.
        var canvas = new PixelCanvas(32, 32);

        canvas.Window(9, 19, 4, 4, Glass);

        Assert.Equal(Glass, canvas.At(10, 20));
    }

    [Fact]
    public void ASpriteWithoutWindowsReportsNone()
    {
        // Sklad, pole ani monument okna nekreslí. Dřív jim je hash vyrobil
        // taky — svítící pole je nesmysl, kterého si hráč všimne dřív než
        // čehokoli hezkého vedle.
        var canvas = new PixelCanvas(32, 32);
        canvas.FillRect(2, 2, 28, 28, new Color(120, 90, 60));

        Assert.Empty(canvas.Windows);
    }

    [Fact]
    public void EveryRememberedWindowLiesInsideTheSprite()
    {
        // Okno mimo plátno by se po přepočtu na velikost budovy objevilo vedle ní.
        var canvas = new PixelCanvas(32, 32);
        canvas.Window(9, 19, 4, 4, Glass);
        canvas.Window(20, 19, 4, 4, Glass);

        Assert.All(canvas.Windows, w =>
        {
            Assert.InRange(w.X, 0, canvas.Width - 1);
            Assert.InRange(w.Y, 0, canvas.Height - 1);
            Assert.InRange(w.Right, 1, canvas.Width);
            Assert.InRange(w.Bottom, 1, canvas.Height);
        });
    }

    [Fact]
    public void WindowsSurviveTheOutlinePass()
    {
        // Obrys se zapéká až po kresbě. Kdyby seznam oken procházel týmž
        // krokem, přišel by o ně.
        var canvas = new PixelCanvas(32, 32);
        canvas.FillRect(4, 4, 24, 24, new Color(160, 120, 90));
        canvas.Window(9, 19, 4, 4, Glass);

        canvas.Outline(0.3f);

        Assert.Single(canvas.Windows);
    }
}
