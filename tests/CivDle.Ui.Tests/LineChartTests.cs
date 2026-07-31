using CivDle.Screens;
using Microsoft.Xna.Framework;
using Xunit;

namespace CivDle.Ui.Tests;

/// <summary>
/// Geometrie grafu. Testuje se to, co by na obrazovce bylo vidět jako
/// nesmysl: osa Y musí začínat na nule (jinak malé kolísání vypadá jako
/// dramatický propad), konstantní řada nesmí dělit nulou, a body musí
/// zůstat uvnitř rámečku.
///
/// <para>Běží headless — kreslení se netestuje, jen matematika za ním.</para>
/// </summary>
public sealed class LineChartTests
{
    private static readonly Rectangle Bounds = new(100, 50, 200, 100);

    [Fact]
    public void TheAxisAlwaysStartsAtZero()
    {
        // Kdyby se osa přizpůsobovala minimu, vypadala by řada 1000→1010
        // jako raketový růst.
        var (min, max) = LineChart.RangeOf(new double[] { 1000, 1005, 1010 });

        Assert.Equal(0, min);
        Assert.Equal(1010, max);
    }

    [Fact]
    public void AFlatZeroSeriesDoesNotDivideByZero()
    {
        var (min, max) = LineChart.RangeOf(new double[] { 0, 0, 0 });

        Assert.Equal(0, min);
        Assert.True(max > 0);
    }

    [Fact]
    public void AnEmptySeriesHasAUsableRange()
    {
        var (min, max) = LineChart.RangeOf(Array.Empty<double>());

        Assert.Equal(0, min);
        Assert.True(max > 0);
    }

    [Fact]
    public void PointsStayInsideTheFrame()
    {
        var values = new double[] { 0, 5, 10, 3, 7 };
        var (min, max) = LineChart.RangeOf(values);

        for (int i = 0; i < values.Length; i++)
        {
            var point = LineChart.PointAt(values, i, Bounds, min, max);

            Assert.InRange(point.X, Bounds.Left, Bounds.Right);
            Assert.InRange(point.Y, Bounds.Top, Bounds.Bottom);
        }
    }

    [Fact]
    public void TheSeriesSpansTheWholeWidth()
    {
        var values = new double[] { 1, 2, 3, 4 };
        var (min, max) = LineChart.RangeOf(values);

        Assert.Equal(Bounds.Left, LineChart.PointAt(values, 0, Bounds, min, max).X, 3);
        Assert.Equal(Bounds.Right, LineChart.PointAt(values, values.Length - 1, Bounds, min, max).X, 3);
    }

    [Fact]
    public void BiggerValuesSitHigher()
    {
        // Y roste dolů — větší hodnota musí mít MENŠÍ Y, jinak je graf vzhůru nohama.
        var values = new double[] { 1, 9 };
        var (min, max) = LineChart.RangeOf(values);

        Assert.True(
            LineChart.PointAt(values, 1, Bounds, min, max).Y < LineChart.PointAt(values, 0, Bounds, min, max).Y);
    }

    [Fact]
    public void ASingleValueSitsAtTheLeftEdge()
    {
        var values = new double[] { 42 };
        var (min, max) = LineChart.RangeOf(values);

        Assert.Equal(Bounds.Left, LineChart.PointAt(values, 0, Bounds, min, max).X, 3);
    }

    [Fact]
    public void ValuesOutsideTheRangeAreClampedNotDrawnOffScreen()
    {
        // Ochrana proti řadě, kde by hodnota přerostla spočítané maximum.
        var values = new double[] { 5 };

        var point = LineChart.PointAt(values, 0, Bounds, 0, 1);

        Assert.InRange(point.Y, Bounds.Top, Bounds.Bottom);
    }
}
