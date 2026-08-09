using CivDle.Rendering;
using Xunit;

namespace CivDle.Ui.Tests;

/// <summary>
/// Třpyt na hladině.
///
/// <para>Hloubka a pěna se pečou do chunku, ale pohyb ne — a je to právě on,
/// kdo z modré plochy udělá vodu. Testuje se to, co by na hladině bylo vidět
/// jako chyba: pravidelné pruhy putující přes celou mapu, nebo naopak hladina,
/// na které se nikdy nic nestane.</para>
/// </summary>
public sealed class WaterShimmerTests
{
    [Fact]
    public void ShimmerStaysInRange()
    {
        for (float t = 0; t < 30f; t += 0.31f)
        {
            for (int x = -20; x <= 20; x += 3)
            {
                Assert.InRange(WaterRenderer.Shimmer(x, x / 2, t), 0f, 1f);
            }
        }
    }

    [Fact]
    public void TheSameTileAtTheSameMomentLooksTheSame()
    {
        Assert.Equal(WaterRenderer.Shimmer(4, 9, 2.5f), WaterRenderer.Shimmer(4, 9, 2.5f));
    }

    [Fact]
    public void TheSurfaceMoves()
    {
        float start = WaterRenderer.Shimmer(3, 3, 0f);
        bool moved = false;
        for (float t = 0.25f; t < 8f && !moved; t += 0.25f)
        {
            moved = MathF.Abs(WaterRenderer.Shimmer(3, 3, t) - start) > 0.1f;
        }

        Assert.True(moved, "Hladina se nehýbe — voda vypadá jako podlaha.");
    }

    [Fact]
    public void SparklesAreScatteredNotStriped()
    {
        // Jedna vlna by po hladině táhla viditelné pruhy. Dvě se mají rozbíjet
        // na nepravidelné šupinky, takže v jedné řadě nesmí být odlesk všude
        // ani nikde.
        int lit = 0;
        const int width = 200;
        for (int x = 0; x < width; x++)
        {
            if (WaterRenderer.Shimmer(x, 12, 1.5f) > 0.72f)
            {
                lit++;
            }
        }

        Assert.InRange(lit / (double)width, 0.02, 0.5);
    }

    [Fact]
    public void OnlyAMinorityOfTheSurfaceSparklesAtOnce()
    {
        // Kdyby se rozsvítila celá hladina, nebyl by to odlesk, ale bílá plocha.
        int lit = 0;
        int total = 0;
        for (int y = 0; y < 40; y++)
        {
            for (int x = 0; x < 40; x++)
            {
                total++;
                if (WaterRenderer.Shimmer(x, y, 3.25f) > 0.72f)
                {
                    lit++;
                }
            }
        }

        Assert.InRange(lit / (double)total, 0.01, 0.35);
    }
}
