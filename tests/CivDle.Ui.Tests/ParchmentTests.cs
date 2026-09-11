using CivDle.Rendering;
using Xunit;

namespace CivDle.Ui.Tests;

/// <summary>
/// Papírový vzhled při velkém oddálení.
///
/// <para>Zblízka nese obraz detail — domy, stromy, lidi. Jak se hráč oddálí,
/// detail zmizí a zbydou barevné plochy; a barevné plochy bez detailu jsou
/// přesně to, co vypadá jako tabulka. Papír tu díru zaplní a dá velkému
/// oddálení vlastní záměr místo dojmu, že se hra vzdala.</para>
///
/// <para>Testuje se náběh, protože jen ten se dá zkazit: zapnout papír
/// zblízka by zašpinilo hru, skokové přepnutí vypadá jako chyba i když je to
/// záměr.</para>
/// </summary>
public class ParchmentTests
{
    [Fact]
    public void UpCloseThereIsNoPaper()
    {
        // Zblízka je na co koukat. Papír by přes domy jen ležel jako špína.
        Assert.Equal(0f, ParchmentOverlay.StrengthAt(1.6f));
        Assert.Equal(0f, ParchmentOverlay.StrengthAt(ParchmentOverlay.FadeInZoom));
    }

    [Fact]
    public void FarOutItIsFullyPaper()
    {
        Assert.Equal(1f, ParchmentOverlay.StrengthAt(ParchmentOverlay.FullZoom));
        Assert.Equal(1f, ParchmentOverlay.StrengthAt(0.01f));
    }

    [Fact]
    public void ItComesInGradually()
    {
        // Skokové přepnutí vzhledu při jednom kroku zoomu vypadá jako chyba.
        float mid = (ParchmentOverlay.FadeInZoom + ParchmentOverlay.FullZoom) * 0.5f;
        float strength = ParchmentOverlay.StrengthAt(mid);

        Assert.InRange(strength, 0.2f, 0.8f);
    }

    [Fact]
    public void ItOnlyEverGetsStrongerAsYouZoomOut()
    {
        // Nemonotónní náběh by při plynulém oddalování papír rozblikal.
        float previous = 0f;
        for (float zoom = ParchmentOverlay.FadeInZoom; zoom > 0.05f; zoom -= 0.01f)
        {
            float now = ParchmentOverlay.StrengthAt(zoom);
            Assert.True(now >= previous - 0.0001f, $"při zoomu {zoom:0.00} papír zeslábl");
            previous = now;
        }
    }

    [Fact]
    public void PaperArrivesWhenBuildingsTurnIntoDensity()
    {
        // Obojí je týž okamžik: jednotlivé domy zmizí a nahradí je plocha.
        // Kdyby se to rozešlo, byl by mezi tím pruh zoomu, kde je krajina holá.
        Assert.Equal(CityScaleRenderer.ThresholdZoom, ParchmentOverlay.FadeInZoom);
    }
}
