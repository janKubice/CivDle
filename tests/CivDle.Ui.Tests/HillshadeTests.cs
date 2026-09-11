using CivDle.Core.Content;
using CivDle.Rendering;
using Microsoft.Xna.Framework;
using Xunit;

namespace CivDle.Ui.Tests;

/// <summary>
/// Reliéf terénu: svah ke slunci se rozsvítí, odvrácený ztmavne.
///
/// <para>Výška se dosud spočítala, vybral se z ní biom a pak se zahodila —
/// přitom teprve sklon řekne, kde je kopec a kde údolí. Bez něj je pohled
/// shora tabulka barev. Testuje se to, co se dá snadno rozbít: že rovina
/// zůstane nedotčená, že opačné svahy jdou opačným směrem, a že se voda
/// nestínuje (hladina je vodorovná, ať je pod ní cokoli).</para>
/// </summary>
public class HillshadeTests
{
    [Fact]
    public void FlatGroundIsUnchanged()
    {
        // Kdyby rovina nebyla neutrální, měla by celá mapa trvalý nádech
        // a nikdo by nevěděl proč.
        var painter = Painter(out var content);
        var ring = Ring(content, "grassland");

        var flat = painter.Tile(10, 10, ring, waterInWindow: 0, slopeShade: 0f);
        var noArgument = painter.Tile(10, 10, ring, waterInWindow: 0);

        Assert.Equal(noArgument, flat);
    }

    [Fact]
    public void ASlopeTowardTheSunIsBrighterThanOneAwayFromIt()
    {
        var painter = Painter(out var content);
        var ring = Ring(content, "grassland");

        var lit = painter.Tile(10, 10, ring, waterInWindow: 0, slopeShade: 1f);
        var shaded = painter.Tile(10, 10, ring, waterInWindow: 0, slopeShade: -1f);

        Assert.True(
            Brightness(lit) > Brightness(shaded),
            $"přisvícený svah není světlejší ({Brightness(lit):0.000} vs {Brightness(shaded):0.000})");
    }

    [Fact]
    public void TheEffectIsVisibleButNotOverwhelming()
    {
        // Přes čtvrtinu už krajina vypadá jako reliéfní mapa z muzea a barvy
        // biomů se v tom ztratí; pod desetinou to není vidět vůbec.
        var painter = Painter(out var content);
        var ring = Ring(content, "grassland");

        float flat = Brightness(painter.Tile(10, 10, ring, 0, 0f));
        float lit = Brightness(painter.Tile(10, 10, ring, 0, 1f));

        float change = (lit - flat) / flat;
        Assert.InRange(change, 0.10f, 0.30f);
    }

    [Fact]
    public void WaterIsNeverShaded()
    {
        // Hladina je vodorovná, ať je pod ní cokoli. Stínovaná voda vypadá
        // jako pomačkaný igelit.
        var painter = Painter(out var content);
        var ring = Ring(content, "ocean");

        var flat = painter.Tile(10, 10, ring, waterInWindow: 25, slopeShade: 0f);
        var steep = painter.Tile(10, 10, ring, waterInWindow: 25, slopeShade: 1f);

        Assert.Equal(flat, steep);
    }

    private static float Brightness(Color c) => (c.R + c.G + c.B) / (3f * 255f);

    /// <summary>Okolí 3×3, celé z jednoho biomu — ať test měří stínování a nic jiného.</summary>
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
