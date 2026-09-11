using CivDle.Rendering;
using Xunit;

namespace CivDle.Ui.Tests;

/// <summary>
/// Šum, ze kterého jsou mraky.
///
/// <para>Mraky jsou ve hře dvakrát — jednou jako stíny plující po zemi,
/// podruhé jako vrstva nad městem. Obojí vychází z téhle jedné textury; kdyby
/// si každá vrstva generovala svou, rozešly by se po první změně.</para>
///
/// <para>Textura se dlaždicově opakuje, a to je to jediné, co se tu dá
/// netriviálně zkazit: kdyby okraj nenavazoval, táhl by se přes oblohu
/// pravidelný šev. Zařízení k tomu není potřeba — testuje se sám vzorec.</para>
/// </summary>
public class CloudNoiseTests
{
    [Fact]
    public void TheLeftEdgeMatchesTheRight()
    {
        // Šev přes celou oblohu je ta nejnápadnější možná chyba dlaždicové
        // textury a v pohybu by putoval přes obraz.
        for (int y = 0; y < 256; y += 16)
        {
            Assert.Equal(Sample(0, y), Sample(256, y), precision: 5);
        }
    }

    [Fact]
    public void TheTopEdgeMatchesTheBottom()
    {
        for (int x = 0; x < 256; x += 16)
        {
            Assert.Equal(Sample(x, 0), Sample(x, 256), precision: 5);
        }
    }

    [Fact]
    public void ThereIsBothClearSkyAndOvercast()
    {
        // Bez prahu je z toho rovnoměrná šeď, ne mraky. Musí vzniknout
        // ostrůvky zataženo a mezi nimi jasno.
        float min = 1f, max = 0f;
        for (int y = 0; y < 256; y += 4)
        {
            for (int x = 0; x < 256; x += 4)
            {
                float v = Sample(x, y);
                min = MathF.Min(min, v);
                max = MathF.Max(max, v);
            }
        }

        Assert.True(min < 0.05f, $"nikde není jasno (nejnižší hustota {min:0.000})");
        Assert.True(max > 0.95f, $"nikde není zataženo (nejvyšší hustota {max:0.000})");
    }

    [Fact]
    public void DifferentSeedsGiveDifferentSkies()
    {
        // Mraky nad městem nesmí ležet přesně na svých stínech: letí výš
        // a jinou rychlostí, takže by zákryt byl vidět jako chyba.
        int different = 0;
        for (int y = 0; y < 256; y += 8)
        {
            for (int x = 0; x < 256; x += 8)
            {
                if (MathF.Abs(Sample(x, y, seed: 1) - Sample(x, y, seed: 7)) > 0.01f)
                {
                    different++;
                }
            }
        }

        Assert.True(different > 200, $"dva seedy dávají skoro totéž nebe ({different} rozdílných míst)");
    }

    [Fact]
    public void TheShadowTextureCarriesDensityInAlphaNotInColor()
    {
        // Tohle je ta chyba, kvůli které vypadaly mraky jako šmouhy.
        //
        // Verze s hustotou v BARVĚ se dá kreslit jedině násobením, a násobení
        // počítá cíl × zdroj — hustý mrak (černá) tedy vynásobí scénu nulou
        // a udělá díru do černa. Sílu stínu přitom nejde nijak řídit, protože
        // tint umí jen ubrat, ne přidat.
        var solid = CloudNoise.Texel(1f, asShade: false);

        Assert.Equal(255, solid.R);
        Assert.Equal(255, solid.G);
        Assert.Equal(255, solid.B);
        Assert.Equal(255, solid.A);
    }

    [Fact]
    public void ClearSkyIsFullyTransparent()
    {
        // Kde mrak není, se nesmí kreslit vůbec nic.
        var clear = CloudNoise.Texel(0f, asShade: false);

        Assert.Equal(0, clear.A);
    }

    [Fact]
    public void DensityMapsStraightToOpacity()
    {
        // Řídký okraj mraku má stínit slabě, jádro naplno. Kdyby byl vztah
        // nelineární, ztratil by mrak měkký okraj a byl by z něj zase flek.
        Assert.True(CloudNoise.Texel(0.25f, asShade: false).A < CloudNoise.Texel(0.75f, asShade: false).A);
    }

    /// <summary>Hustota mraku s týmiž parametry, jaké používá stínová vrstva.</summary>
    private static float Sample(int x, int y, int seed = 1) =>
        CloudNoise.DensityAt(x, y, 256, threshold: 0.42f, softness: 0.34f, seed: seed);
}
