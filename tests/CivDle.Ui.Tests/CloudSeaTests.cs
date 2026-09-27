using CivDle.Rendering;
using Xunit;

namespace CivDle.Ui.Tests;

/// <summary>
/// Oblačný oceán Nebes (svety-design.md 4.5): pásy mraků jsou čistá funkce
/// souřadnice — stejné místo má vždy stejnou barvu, napříč pásy se barva mění
/// a obě vrstvy (paralaxa) nejsou totožné.
/// </summary>
public sealed class CloudSeaTests
{
    [Fact]
    public void TheBandColorIsAPureFunctionOfPlace()
    {
        Assert.Equal(CloudSeaRenderer.BandColor(12.5f, -40f, 0), CloudSeaRenderer.BandColor(12.5f, -40f, 0));
    }

    [Fact]
    public void BandsChangeAcrossTheGiant()
    {
        var colors = new HashSet<uint>();
        for (int y = 0; y < 120; y += 3)
        {
            colors.Add(CloudSeaRenderer.BandColor(0, y, 0).PackedValue);
        }

        Assert.True(colors.Count > 10, $"pásy mají mít víc barev, mají {colors.Count}");
    }

    [Fact]
    public void TheTwoLayersDiffer()
    {
        int same = 0;
        for (int y = 0; y < 100; y++)
        {
            same += CloudSeaRenderer.BandColor(5, y, 0) == CloudSeaRenderer.BandColor(5, y, 1) ? 1 : 0;
        }

        Assert.True(same < 20, "spodní a horní vrstva mraků mají vypadat jinak");
    }
}
