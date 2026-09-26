using CivDle.Core.Content;
using CivDle.Rendering.Sprites;
using Xunit;

namespace CivDle.Ui.Tests;

/// <summary>
/// Malíř vzhledu z dat: každý tvar a prvek z katalogu (<see cref="BuildingLook"/>)
/// musí jít nakreslit. Katalog je v jádře a loader podle něj pouští data dovnitř —
/// tvar, který by malíř neuměl, by prošel načtením a spadl až při startu grafiky.
/// </summary>
public sealed class LookPainterTests
{
    private static readonly RgbColor Stone = new(170, 160, 150);
    private static readonly RgbColor Clay = new(160, 90, 70);
    private static readonly RgbColor Leaf = new(80, 150, 90);

    public static IEnumerable<object[]> Shapes() => BuildingLook.KnownShapes.Order().Select(s => new object[] { s });

    public static IEnumerable<object[]> Features() => BuildingLook.KnownFeatures.Order().Select(f => new object[] { f });

    [Theory]
    [MemberData(nameof(Shapes))]
    public void EveryShape_PaintsSomething(string shape)
    {
        foreach (var (w, h) in new[] { (32, 32), (64, 64), (96, 96), (32, 64) })
        {
            var canvas = new PixelCanvas(w, h);
            LookPainter.Paint(canvas, new BuildingLook(shape, Stone, Clay, Leaf, null, Array.Empty<string>()));

            Assert.True(CoveredPixels(canvas) > w * h / 20, $"{shape} {w}×{h}: skoro prázdné plátno");
        }
    }

    [Theory]
    [MemberData(nameof(Features))]
    public void EveryFeature_CanBeAddedToAHouse(string feature)
    {
        var canvas = new PixelCanvas(64, 64);
        LookPainter.Paint(canvas, new BuildingLook("house", Stone, Clay, Leaf, null, new[] { feature }));

        Assert.True(CoveredPixels(canvas) > 0);
    }

    [Fact]
    public void WindowsAreRecorded_SoTheyLightUpAtNight()
    {
        var canvas = new PixelCanvas(32, 32);
        LookPainter.Paint(canvas, new BuildingLook("house", Stone, Clay, Leaf, null, new[] { "windows" }));

        Assert.NotEmpty(canvas.Windows);
    }

    [Fact]
    public void ATallBuilding_UsesTheWholeCanvas()
    {
        // Věž přerůstá půdorys — na vysokém plátně musí jít až nahoru,
        // jinak by ve vysokém obdélníku stál malý dům a nad ním prázdno.
        var canvas = new PixelCanvas(32, 96);
        LookPainter.Paint(canvas, new BuildingLook("tower", Stone, Clay, Leaf, null, new[] { "windows" }));

        bool topRowsUsed = Enumerable.Range(0, 32).Any(y => Enumerable.Range(0, 32).Any(x => canvas.At(x, y).A > 0));
        Assert.True(topRowsUsed);
    }

    private static int CoveredPixels(PixelCanvas canvas)
    {
        int covered = 0;
        for (int y = 0; y < canvas.Height; y++)
        {
            for (int x = 0; x < canvas.Width; x++)
            {
                if (canvas.At(x, y).A > 0)
                {
                    covered++;
                }
            }
        }

        return covered;
    }
}
