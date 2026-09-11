using CivDle.Core.Content;
using CivDle.Rendering;
using Microsoft.Xna.Framework;
using Xunit;

namespace CivDle.Ui.Tests;

/// <summary>
/// Skála na strmém svahu.
///
/// <para>Hory byly stejně hladce obarvené jako louka, jen jinou barvou.
/// Skutečný kopec má na hřebenech a v roklích holé podloží — a právě ono
/// odliší pohoří od zeleného kopečku. Stínování sklonu ukáže, <i>kde</i> je
/// svah; útes ukáže, že je <i>strmý</i>.</para>
///
/// <para>Testuje se, co se dá snadno rozbít: že rovina zůstane nedotčená, že
/// skála přijde až za prahem, že si nechá barvu podloží (pískovec má zůstat
/// pískovcem) a že se nikdy nevykreslí přes vodu.</para>
/// </summary>
public class CliffTests
{
    [Fact]
    public void GentleGroundHasNoRock()
    {
        // Kdyby skála prosvítala všude, byla by z krajiny betonová deska.
        var painter = Painter(out var content);
        var ring = Ring(content, "mountains");

        var flat = painter.Tile(10, 10, ring, waterInWindow: 0, slopeShade: 0f, steepness: 0f);
        var gentle = painter.Tile(10, 10, ring, waterInWindow: 0, slopeShade: 0f, steepness: 0.3f);

        Assert.Equal(flat, gentle);
    }

    [Fact]
    public void ASteepSlopeShowsBedrock()
    {
        // Odkryté podloží je odbarvené: to je ten rozdíl proti pouhému ztmavení.
        var painter = Painter(out var content);
        var ring = Ring(content, "mountains");

        var slope = painter.Tile(10, 10, ring, waterInWindow: 0, slopeShade: 0f, steepness: 0f);
        var cliff = painter.Tile(10, 10, ring, waterInWindow: 0, slopeShade: 0f, steepness: 1f);

        Assert.True(Saturation(cliff) < Saturation(slope),
            $"sráz není odbarvený ({Saturation(cliff):0.000} vs {Saturation(slope):0.000})");
    }

    [Fact]
    public void SoftGroundKeepsItsTurfHoweverSteep()
    {
        // Na louce se drn udrží i na prudkém kopci. Bez tohohle pravidla by
        // kameny prorážely doprostřed pastvin, protože výška roste ve světě
        // rovnoměrně a nejstrmější dlaždice leží roztroušené všude.
        var painter = Painter(out var content);
        var ring = Ring(content, "grassland");

        var flat = painter.Tile(10, 10, ring, waterInWindow: 0, slopeShade: 0f, steepness: 0f);
        var steep = painter.Tile(10, 10, ring, waterInWindow: 0, slopeShade: 0f, steepness: 1f);

        Assert.Equal(flat, steep);
    }

    [Fact]
    public void RockKeepsTheHueOfWhatItIsMadeOf()
    {
        // Pískovcový kaňon má zůstat pískovcový. Kdyby byla skála pevná šeď,
        // vypadaly by všechny hory na světě stejně.
        var painter = Painter(out var content);

        var sand = painter.Tile(10, 10, Ring(content, "badlands"), 0, 0f, steepness: 1f);
        var ice = painter.Tile(10, 10, Ring(content, "glacier"), 0, 0f, steepness: 1f);

        Assert.True(sand.R > ice.R || sand.B < ice.B,
            "pustina a ledovec dávají tentýž kámen — skála ztratila podloží");
    }

    [Fact]
    public void TheRockDoesNotSwallowTheBiomeEntirely()
    {
        // Naplno by z hory byla šedá skvrna a biom by v ní zanikl.
        var painter = Painter(out var content);
        var ring = Ring(content, "mountains");

        var slope = painter.Tile(10, 10, ring, 0, 0f, steepness: 0f);
        var cliff = painter.Tile(10, 10, ring, 0, 0f, steepness: 1f);

        Assert.True(Saturation(cliff) > Saturation(slope) * 0.15f,
            "ze srázu zmizela barva podloží úplně");
    }

    [Fact]
    public void WaterNeverTurnsIntoRock()
    {
        // Pod hladinou může být sebestrmější sráz — vidět je pořád voda.
        var painter = Painter(out var content);
        var ring = Ring(content, "ocean");

        var calm = painter.Tile(10, 10, ring, waterInWindow: 25, slopeShade: 0f, steepness: 0f);
        var steep = painter.Tile(10, 10, ring, waterInWindow: 25, slopeShade: 0f, steepness: 1f);

        Assert.Equal(calm, steep);
    }

    [Fact]
    public void StrataBandTheFaceInsteadOfLeavingItSmooth()
    {
        // Vrstvy jsou to, co ze šedé skvrny udělá skálu. Když by každý řádek
        // vycházel stejně, byl by sráz plochý.
        var painter = Painter(out var content);
        var ring = Ring(content, "mountains");

        var seen = new HashSet<float>();
        for (int y = 0; y < 6; y++)
        {
            seen.Add(Brightness(painter.Tile(10, y, ring, 0, 0f, steepness: 1f)));
        }

        Assert.True(seen.Count > 1, "sráz nemá vrstvy — všechny řádky vycházejí stejně");
    }

    private static float Brightness(Color c) => (c.R + c.G + c.B) / (3f * 255f);

    /// <summary>Jak barevná dlaždice je: rozdíl nejsilnější a nejslabší složky.</summary>
    private static float Saturation(Color c)
    {
        int max = Math.Max(c.R, Math.Max(c.G, c.B));
        int min = Math.Min(c.R, Math.Min(c.G, c.B));
        return (max - min) / 255f;
    }

    /// <summary>Okolí 3×3, celé z jednoho biomu — ať test měří skálu a nic jiného.</summary>
    private static byte[] Ring(GameContent content, string biome)
    {
        byte index = (byte)content.Biomes.IndexOf(biome);
        var ring = new byte[9];
        Array.Fill(ring, index);
        return ring;
    }

    private static TerrainPainter Painter(out GameContent content)
    {
        content = new ContentLoader().LoadFrom(Path.Combine(AppContext.BaseDirectory, "data"));
        return new TerrainPainter(content.Biomes, seed: 20260728);
    }
}
