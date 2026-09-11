using CivDle.Core.Content;
using CivDle.Rendering;
using CivDle.Rendering.Sprites;
using Microsoft.Xna.Framework;
using Xunit;

namespace CivDle.Ui.Tests;

/// <summary>
/// Zem a to, co na ní stojí, ze stejné palety.
///
/// <para>Sprity se srovnávají na společnou paletu, ale zem si nesla vlastní
/// barvy z JSON. Změřeno: barvy biomů leží od palety v mediánu osmadvacet
/// jednotek RGB a nejdál třiapadesát — dost na to, aby zem a město vypadaly
/// jako ze dvou různých her.</para>
///
/// <para>Přitahuje se jen zčásti. Úplné přemapování by z plynulých přechodů
/// terénu udělalo pruhy a z moře vrstevnicovou mapu, a přesně to se tu
/// testuje: že se barvy přiblížily, ale nesrovnaly do stejné.</para>
/// </summary>
public class PaletteAffinityTests
{
    [Fact]
    public void NudgingMovesColorsTowardThePalette()
    {
        var far = new Color(57, 169, 155); // korálový útes — v paletě není tyrkysová
        var nudged = GamePalette.Nudge(far, 0.4f);

        Assert.True(DistanceToPalette(nudged) < DistanceToPalette(far),
            "přitažení barvu k paletě nepřiblížilo");
    }

    [Fact]
    public void NudgingDoesNotGoAllTheWay()
    {
        // Kdyby dotáhlo až na paletu, byly by z přechodů terénu pruhy.
        var far = new Color(57, 169, 155);

        Assert.NotEqual(GamePalette.Snap(far), GamePalette.Nudge(far, 0.4f));
    }

    [Fact]
    public void ZeroAffinityChangesNothing()
    {
        var color = new Color(57, 169, 155);

        Assert.Equal(color, GamePalette.Nudge(color, 0f));
    }

    [Fact]
    public void FullAffinityIsTheSameAsSnapping()
    {
        var color = new Color(57, 169, 155);

        Assert.Equal(GamePalette.Snap(color), GamePalette.Nudge(color, 1f));
    }

    [Fact]
    public void BiomesStayTellableApart()
    {
        // Sjednocená paleta nesmí splést louku s pouští — biom je informace,
        // ne jen nálada.
        var painter = Painter(out var content);

        var grass = painter.Tile(10, 10, Ring(content, "grassland"), 0);
        var desert = painter.Tile(10, 10, Ring(content, "desert"), 0);
        var ocean = painter.Tile(10, 10, Ring(content, "ocean"), 25);

        Assert.True(Distance(grass, desert) > 40f, "louka a poušť splynuly");
        Assert.True(Distance(grass, ocean) > 40f, "louka a moře splynuly");
    }

    [Fact]
    public void WaterStillGetsDeeperAwayFromShore()
    {
        // Hloubkový přechod je to, co by tvrdé přemapování rozbilo nejdřív.
        var painter = Painter(out var content);
        var ring = Ring(content, "ocean");

        var shallow = painter.Tile(10, 10, ring, waterInWindow: 5);
        var deep = painter.Tile(10, 10, ring, waterInWindow: 60);

        Assert.True(Brightness(deep) < Brightness(shallow), "moře přestalo tmavnout do hloubky");
    }

    private static float Brightness(Color c) => (c.R + c.G + c.B) / (3f * 255f);

    private static float Distance(Color a, Color b)
    {
        float dr = a.R - b.R, dg = a.G - b.G, db = a.B - b.B;
        return MathF.Sqrt(dr * dr + dg * dg + db * db);
    }

    private static float DistanceToPalette(Color color)
    {
        float best = float.MaxValue;
        for (int i = 0; i < GamePalette.Count; i++)
        {
            best = MathF.Min(best, Distance(color, GamePalette.At(i)));
        }

        return best;
    }

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
