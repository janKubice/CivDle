using CivDle.Core.Content;
using CivDle.Rendering;
using Microsoft.Xna.Framework;
using Xunit;

namespace CivDle.Ui.Tests;

/// <summary>
/// Jakou barvu dostane dlaždice terénu.
///
/// <para>Terén byl doslova tabulka: les končil rovnou hranou po mřížce a celá
/// pláň měla přesně jeden odstín. Testuje se proto to, co z ní dělá krajinu —
/// že se hranice biomů rozstřapatí, že má pláň světlejší a tmavší kraje, že
/// má pobřeží pěnu a otevřené moře hloubku. A hlavně: že je to všechno
/// <b>deterministické</b>, protože chunk se může kdykoli přepéct a scéna se
/// tím nesmí změnit.</para>
/// </summary>
public sealed class TerrainPainterTests
{
    private const byte Water = 0;
    private const byte Grass = 1;
    private const byte Forest = 2;

    private static readonly Biome[] Biomes =
    {
        Biome("sea", 40, 78, 132, isWater: true),
        Biome("grass", 110, 160, 90),
        Biome("forest", 60, 110, 70),
    };

    private static Biome Biome(string id, byte r, byte g, byte b, bool isWater = false) =>
        new(id, new RgbColor(r, g, b), ColorVariation: 0.05f, IsWater: isWater,
            DepthRange: new ValueRange(0, 1), ElevationRange: new ValueRange(0, 1),
            MoistureRange: new ValueRange(0, 1), TemperatureRange: new ValueRange(0, 1));

    private static TerrainPainter Painter(long seed = 12345) =>
        new(new BiomeRegistry(Biomes), seed);

    /// <summary>Okolí 3×3, kde je všude totéž.</summary>
    private static byte[] Uniform(byte biome) => Enumerable.Repeat(biome, 9).ToArray();

    /// <summary>Okolí 3×3 se sousedem na dané straně (index v řádkovém pořadí).</summary>
    private static byte[] With(byte self, int index, byte neighbour)
    {
        var ring = Uniform(self);
        ring[index] = neighbour;
        return ring;
    }

    [Fact]
    public void TheSameTileAlwaysGetsTheSameColour()
    {
        // Chunk se přepeče při každé teraformaci. Kdyby se barva měnila,
        // celá krajina by po jednom výkopu přeblikla.
        var painter = Painter();
        var ring = Uniform(Grass);

        Assert.Equal(painter.Tile(5, 9, ring, 0), painter.Tile(5, 9, ring, 0));
    }

    [Fact]
    public void DifferentSeedsGiveDifferentLandscapes()
    {
        var ring = Uniform(Grass);
        var a = Painter(1);
        var b = Painter(2);

        bool anyDifferent = false;
        for (int i = 0; i < 200 && !anyDifferent; i++)
        {
            anyDifferent = a.Tile(i, 0, ring, 0) != b.Tile(i, 0, ring, 0);
        }

        Assert.True(anyDifferent, "Dva různé seedy dávají identickou krajinu.");
    }

    [Fact]
    public void ABiomeBoundaryGetsRagged()
    {
        // Tohle je celý smysl věci: hranice lesa nemá být rovná čára po mřížce.
        var painter = Painter();
        var edge = With(Grass, 1, Forest); // les nahoře
        var forestColor = Biomes[Forest].MapColor;

        int borrowed = 0;
        for (int x = 0; x < 200; x++)
        {
            var color = painter.Tile(x, 0, edge, 0);
            if (IsShadeOf(color, forestColor))
            {
                borrowed++;
            }
        }

        Assert.InRange(borrowed / 200.0, 0.15, 0.7);
    }

    [Fact]
    public void TilesInsideABiomeKeepTheirOwnColour()
    {
        // Dithering se smí projevit jen U HRANICE. Uprostřed pláně by z něj
        // byly nevysvětlitelné skvrny cizího biomu.
        var painter = Painter();
        var inside = Uniform(Grass);
        var grassColor = Biomes[Grass].MapColor;

        for (int x = 0; x < 100; x++)
        {
            Assert.True(IsShadeOf(painter.Tile(x, 3, inside, 0), grassColor),
                $"Dlaždice uvnitř pláně dostala cizí barvu na x={x}.");
        }
    }

    [Fact]
    public void WaterNeverBleedsIntoLand()
    {
        // Modrá tečka uprostřed louky vypadá jako kaluž, kterou hráč zkusí
        // obestavět — a pak nechápe, proč to jde.
        var painter = Painter();
        var coast = With(Grass, 7, Water); // voda pod dlaždicí
        var waterColor = Biomes[Water].MapColor;

        for (int x = 0; x < 300; x++)
        {
            Assert.False(IsShadeOf(painter.Tile(x, 0, coast, 0), waterColor),
                $"Souš dostala barvu vody na x={x}.");
        }
    }

    [Fact]
    public void TheCoastlineGetsFoam()
    {
        var painter = Painter();
        var open = painter.Tile(10, 10, Uniform(Water), TerrainPainter.WaterWindowTiles);
        var shore = painter.Tile(10, 10, With(Water, 7, Grass), TerrainPainter.WaterWindowTiles);

        Assert.True(Luminance(shore) > Luminance(open) + 15f,
            $"Pobřeží ({shore}) není světlejší než otevřená voda ({open}).");
    }

    [Fact]
    public void OpenSeaIsDeeperThanASheltredBay()
    {
        // Hloubka se odhaduje z toho, kolik vody je kolem. Bez toho byla každá
        // vodní plocha jedna plochá modrá skvrna.
        var painter = Painter();
        var ring = Uniform(Water);

        var deep = painter.Tile(4, 4, ring, TerrainPainter.WaterWindowTiles);
        var shallow = painter.Tile(4, 4, ring, TerrainPainter.WaterWindowTiles / 3);

        Assert.True(Luminance(shallow) > Luminance(deep),
            $"Mělčina ({shallow}) není světlejší než hlubina ({deep}).");
    }

    [Fact]
    public void FoamOnlyComesFromTheSidesNotTheCorners()
    {
        // Diagonální soused znamená, že se voda se souší jen mine rohem —
        // pěna by tam visela ve vzduchu.
        var painter = Painter();
        var corner = With(Water, 0, Grass); // souš jen vlevo nahoře
        var plain = Uniform(Water);

        Assert.Equal(
            painter.Tile(6, 6, plain, TerrainPainter.WaterWindowTiles),
            painter.Tile(6, 6, corner, TerrainPainter.WaterWindowTiles));
    }

    [Fact]
    public void MacroNoiseVariesSlowlyAcrossTheMap()
    {
        // Kdyby se měnil dlaždici od dlaždice, byl by to šum, ne krajina.
        var painter = Painter();

        Assert.Equal(painter.MacroNoise(100, 100), painter.MacroNoise(100, 100));
        Assert.True(MathF.Abs(painter.MacroNoise(100, 100) - painter.MacroNoise(101, 100)) < 0.15f,
            "Makro-šum skáče mezi sousedními dlaždicemi.");

        bool differsFarAway = false;
        for (int d = 20; d <= 400 && !differsFarAway; d += 20)
        {
            differsFarAway = MathF.Abs(painter.MacroNoise(0, 0) - painter.MacroNoise(d, 0)) > 0.2f;
        }

        Assert.True(differsFarAway, "Makro-šum je přes celou mapu konstantní.");
    }

    [Fact]
    public void MacroNoiseStaysInRange()
    {
        var painter = Painter();
        for (int i = -500; i <= 500; i += 7)
        {
            Assert.InRange(painter.MacroNoise(i, -i), 0f, 1f);
        }
    }

    [Fact]
    public void NegativeCoordinatesAreOrdinaryGround()
    {
        // Svět je nekonečný na obě strany; přetečení hashe by tam udělalo
        // viditelný šev.
        var painter = Painter();
        var ring = Uniform(Grass);

        for (int i = 1; i <= 200; i++)
        {
            var color = painter.Tile(-i, -i * 3, ring, 0);
            Assert.True(IsShadeOf(color, Biomes[Grass].MapColor), $"Podivná barva na −{i}.");
        }
    }

    /// <summary>Je barva odstínem daného biomu? (Jas se mění, poměr složek zhruba drží.)</summary>
    private static bool IsShadeOf(Color color, RgbColor biome)
    {
        float scale = Luminance(color) / MathF.Max(1f, (biome.R + biome.G + biome.B) / 3f);
        return MathF.Abs(color.R - biome.R * scale) < 12f
            && MathF.Abs(color.G - biome.G * scale) < 12f
            && MathF.Abs(color.B - biome.B * scale) < 12f;
    }

    private static float Luminance(Color color) => (color.R + color.G + color.B) / 3f;
}
