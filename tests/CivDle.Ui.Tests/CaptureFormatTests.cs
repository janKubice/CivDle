using CivDle.Capture;
using CivDle.Core.Config;
using Xunit;

namespace CivDle.Ui.Tests;

/// <summary>
/// Rozměry a měřítko fotky a videa.
///
/// <para>Fotka měla pevných 1600×900 a proužek s čísly natvrdo. Testuje se to,
/// co z volitelného rozlišení dělá vylepšení a ne past: že se při vyšším
/// rozlišení nezmění <b>záběr</b> (jen přibudou pixely), že proužek roste
/// s obrázkem a že se dá vypnout.</para>
/// </summary>
public sealed class CaptureFormatTests
{
    [Fact]
    public void EveryResolutionIsSixteenByNine()
    {
        // Karta se sdílí do příspěvků a nahrává na Steam; jiný poměr by se
        // všude ořízl.
        foreach (var resolution in Enum.GetValues<CaptureResolution>())
        {
            var (width, height) = ShareCardOptions.SizeOf(resolution);
            Assert.Equal(16.0 / 9.0, width / (double)height, 3);
        }
    }

    [Fact]
    public void HigherStepsAreActuallyBigger()
    {
        var (hdWidth, _) = ShareCardOptions.SizeOf(CaptureResolution.Hd1080);
        var (qhdWidth, _) = ShareCardOptions.SizeOf(CaptureResolution.Qhd1440);
        var (uhdWidth, _) = ShareCardOptions.SizeOf(CaptureResolution.Uhd4K);

        Assert.True(hdWidth < qhdWidth);
        Assert.True(qhdWidth < uhdWidth);
    }

    [Fact]
    public void TheStripGrowsWithTheImage()
    {
        // Pevná výška proužku by ve 4K byla proužek o výšce vlasu.
        var hd = ShareCardOptions.For(CaptureResolution.Hd1080, withStrip: true, fullDetail: false);
        var uhd = ShareCardOptions.For(CaptureResolution.Uhd4K, withStrip: true, fullDetail: false);

        Assert.True(uhd.StripHeight > hd.StripHeight);
        Assert.Equal(
            hd.StripHeight / (double)hd.Height,
            uhd.StripHeight / (double)uhd.Height,
            2);
    }

    [Fact]
    public void WithoutTheStripTheSceneFillsTheWholeImage()
    {
        var options = ShareCardOptions.For(CaptureResolution.Uhd4K, withStrip: false, fullDetail: false);

        Assert.Equal(0, options.StripHeight);
        Assert.Equal(options.Height, options.SceneHeight);
    }

    [Fact]
    public void MorePixelsMeansTheSameFramingNotMoreWorld()
    {
        // Tohle je jádro celé věci. Kdyby se zoom nepřepočítal, byla by fotka
        // ve 4K „víc světa" v témž měřítku, ne ostřejší obrázek téhož záběru.
        const float sourceZoom = 2f;
        const int windowHeight = 1080;

        var hd = ShareCardOptions.For(CaptureResolution.Hd1080, withStrip: false, fullDetail: false);
        var uhd = ShareCardOptions.For(CaptureResolution.Uhd4K, withStrip: false, fullDetail: false);

        // Výška světa ve výřezu = výška v pixelech / zoom. Ta musí zůstat stejná.
        double hdWorld = hd.SceneHeight / hd.ZoomFor(sourceZoom, windowHeight);
        double uhdWorld = uhd.SceneHeight / uhd.ZoomFor(sourceZoom, windowHeight);

        Assert.Equal(hdWorld, uhdWorld, 3);
    }

    [Fact]
    public void AtTheWindowSizeTheZoomIsUnchanged()
    {
        // Fotka ve stejném rozlišení jako okno má být tím, co je na obrazovce.
        var options = ShareCardOptions.For(CaptureResolution.Hd1080, withStrip: false, fullDetail: false);

        Assert.Equal(1.75f, options.ZoomFor(1.75f, 1080), 4);
    }

    [Fact]
    public void ScaleFollowsTheImageHeight()
    {
        var uhd = ShareCardOptions.For(CaptureResolution.Uhd4K, withStrip: true, fullDetail: false);

        Assert.Equal(2160f / ShareCardOptions.ReferenceHeight, uhd.Scale, 4);
    }

    [Fact]
    public void ADegenerateWindowDoesNotDivideByZero()
    {
        // Minimalizované okno hlásí nulovou výšku; fotka z něj nemá být pád.
        var options = ShareCardOptions.For(CaptureResolution.Qhd1440, withStrip: true, fullDetail: false);

        Assert.True(options.ZoomFor(1.5f, 0) > 0f);
    }
}
