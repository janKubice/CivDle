using CivDle.Rendering;
using Microsoft.Xna.Framework;
using Xunit;

namespace CivDle.Ui.Tests;

/// <summary>
/// Čím se od sebe liší dva domy stejného druhu.
///
/// <para>Testuje se to, co by v obraze bylo vidět jako chyba: kdyby se vzhled
/// mezi snímky měnil, ulice by blikala; kdyby visel na indexu v poli, přeblikla
/// by po každém zbourání; a kdyby byla varianta pořád stejná, nebyla by to
/// variace.</para>
/// </summary>
public sealed class BuildingVariationTests
{
    [Fact]
    public void TheSameTileAlwaysLooksTheSame()
    {
        // Tohle je celý důvod, proč se to počítá z hashe a ne z náhody.
        var first = BuildingVariation.For(12, -7, 3);
        var second = BuildingVariation.For(12, -7, 3);

        Assert.Equal(first, second);
    }

    [Fact]
    public void NeighboursDoNotAllLookAlike()
    {
        // Ulice dvaceti domů má mít víc než jednu podobu — jinak je to tapeta.
        var seen = new HashSet<int>();
        for (int x = 0; x < 20; x++)
        {
            seen.Add(BuildingVariation.For(x, 0, 1).PaletteIndex);
        }

        Assert.True(seen.Count >= 3, $"Dvacet sousedů má jen {seen.Count} odstínů střech.");
    }

    [Fact]
    public void EveryPaletteGetsUsed()
    {
        var seen = new HashSet<int>();
        for (int y = 0; y < 40; y++)
        {
            for (int x = 0; x < 40; x++)
            {
                seen.Add(BuildingVariation.For(x, y, 0).PaletteIndex);
            }
        }

        Assert.Equal(BuildingVariation.PaletteCount, seen.Count);
    }

    [Fact]
    public void PaletteIndexNeverEscapesTheTable()
    {
        // Záporné souřadnice jsou běžné (svět je nekonečný na obě strany)
        // a index mimo pole by byl pád, ne ošklivá barva.
        for (int x = -50; x <= 50; x++)
        {
            var look = BuildingVariation.For(x, -x * 3, x & 7);
            Assert.InRange(look.PaletteIndex, 0, BuildingVariation.PaletteCount - 1);
            Assert.NotEqual(default, BuildingVariation.RoofTint(look.PaletteIndex));
        }
    }

    [Fact]
    public void TheOffsetIsNeverMoreThanAPixel()
    {
        // Větší posun by budovu vytáhl z jejího půdorysu přes silnici.
        for (int y = 0; y < 30; y++)
        {
            var look = BuildingVariation.For(y * 7, y, 2);
            Assert.InRange(look.OffsetX, 0, 1);
            Assert.InRange(look.OffsetY, 0, 1);
        }
    }

    [Fact]
    public void MirroringStaysAMinority()
    {
        // Zrcadlená polovina města vypadá jako chyba v kódu, ne jako různorodost.
        int mirrored = 0;
        const int total = 400;
        for (int i = 0; i < total; i++)
        {
            if (BuildingVariation.For(i % 20, i / 20, 0).Mirrored)
            {
                mirrored++;
            }
        }

        Assert.InRange(mirrored / (double)total, 0.05, 0.45);
    }

    [Fact]
    public void ExtrasAreOccasionalNotTheRule()
    {
        // Zhruba každá pátá. Kdyby měl komín každý dům, přestal by být drobností.
        int withExtra = 0;
        const int total = 500;
        for (int i = 0; i < total; i++)
        {
            if (BuildingVariation.For(i, i * 3, 1).Extra != BuildingExtra.None)
            {
                withExtra++;
            }
        }

        Assert.InRange(withExtra / (double)total, 0.08, 0.35);
    }

    [Fact]
    public void AllThreeExtrasShowUp()
    {
        var seen = new HashSet<BuildingExtra>();
        for (int i = 0; i < 2000; i++)
        {
            seen.Add(BuildingVariation.For(i, i / 40, 0).Extra);
        }

        Assert.Contains(BuildingExtra.Chimney, seen);
        Assert.Contains(BuildingExtra.Awning, seen);
        Assert.Contains(BuildingExtra.Laundry, seen);
    }

    [Fact]
    public void VariationOnlyShadesTheBuildingItNeverRepaintsIt()
    {
        // Střecha se má lišit odstínem, ne barvou. Kdyby paleta budovu přebarvila,
        // přestala by být poznat podle barvy — a to je v tomhle pohledu jediné,
        // podle čeho ji hráč z výšky rozezná.
        var full = ProsperityLook.Tint(1.0);
        for (int p = 0; p < BuildingVariation.PaletteCount; p++)
        {
            var combined = BuildingVariation.Combine(full, p);
            Assert.True(MaxChannelShift(combined, full) <= 45,
                $"Paleta {p} mění barvu budovy příliš: {combined} vs {full}.");
            Assert.Equal(255, combined.A);
        }
    }

    /// <summary>Největší posun jednoho kanálu — odstín ano, přebarvení ne.</summary>
    private static int MaxChannelShift(Color a, Color b) => Math.Max(
        Math.Abs(a.R - b.R), Math.Max(Math.Abs(a.G - b.G), Math.Abs(a.B - b.B)));
}
