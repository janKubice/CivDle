using CivDle.Core.Content;
using CivDle.Rendering;
using Microsoft.Xna.Framework;
using Xunit;

namespace CivDle.Ui.Tests;

/// <summary>
/// Období na zemi, ne jako závoj přes obraz.
///
/// <para>Průhledný závoj přes celou obrazovku kontrast <b>snižuje</b> — obraz
/// zmléční a všechno se posune stejným směrem, včetně střech, lidí a vody.
/// Skutečný podzim přebarví listí a trávu, ale ne omítku. Zapečením do terénu
/// se změní právě to, co se v přírodě mění.</para>
///
/// <para>Testuje se, že se zem opravdu mění, že se nemění voda, a že si každé
/// období nese vlastní klíč do cache — bez něj by na jaře zůstal na mapě
/// podzim, protože chunky se pečou jen jednou.</para>
/// </summary>
public class SeasonGroundTests
{
    [Fact]
    public void AutumnBrownsTheGrass()
    {
        var painter = Painter(out var content);
        var ring = Ring(content, "grassland");
        var autumn = new SeasonGround(new Color(196, 130, 58), 0.3f, 0f);

        var summer = painter.Tile(10, 10, ring, 0, 0f, 0f, SeasonGround.None);
        var brown = painter.Tile(10, 10, ring, 0, 0f, 0f, autumn);

        Assert.True(brown.R > summer.R, "podzim nezhnědl trávu");
        Assert.True(brown.G < summer.G || brown.R - brown.G > summer.R - summer.G);
    }

    [Fact]
    public void SnowWhitensTheGround()
    {
        var painter = Painter(out var content);
        var ring = Ring(content, "grassland");

        var bare = painter.Tile(10, 10, ring, 0, 0f, 0f, SeasonGround.None);
        var snowy = painter.Tile(10, 10, ring, 0, 0f, 0f, new SeasonGround(Color.White, 0f, 0.5f));

        Assert.True(Brightness(snowy) > Brightness(bare), "sníh zem nezesvětlil");
    }

    [Fact]
    public void SnowLiesUnevenly()
    {
        // Stejně hluboká vrstva všude vypadá jako natřená plocha, ne jako sníh.
        var painter = Painter(out var content);
        var ring = Ring(content, "grassland");
        var winter = new SeasonGround(Color.White, 0f, 0.5f);

        var seen = new HashSet<uint>();
        for (int i = 0; i < 20; i++)
        {
            seen.Add(painter.Tile(i, 10, ring, 0, 0f, 0f, winter).PackedValue);
        }

        Assert.True(seen.Count > 3, "sníh leží všude stejně hluboko");
    }

    [Fact]
    public void WaterIgnoresTheSeason()
    {
        // Zamrzlá voda je vlastní biom, ne obarvená obyčejná — a sníh na vodě
        // by plaval jako polystyren.
        var painter = Painter(out var content);
        var ring = Ring(content, "ocean");

        var summer = painter.Tile(10, 10, ring, 25, 0f, 0f, SeasonGround.None);
        var winter = painter.Tile(10, 10, ring, 25, 0f, 0f, new SeasonGround(Color.White, 0.4f, 0.8f));

        Assert.Equal(summer, winter);
    }

    [Fact]
    public void SummerIsTheNeutralBaseline()
    {
        // Léto je ten stav, ke kterému se ostatní období vztahují: nesmí
        // s obrazem dělat nic.
        var painter = Painter(out var content);
        var ring = Ring(content, "grassland");

        var plain = painter.Tile(10, 10, ring, 0, 0f, 0f);
        var summer = painter.Tile(10, 10, ring, 0, 0f, 0f, SeasonGround.None);

        Assert.Equal(plain, summer);
    }

    [Fact]
    public void EachSeasonHasItsOwnCacheKey()
    {
        // Chunky se pečou jednou. Bez rozdílného klíče by na jaře zůstal
        // na mapě podzim.
        var content = LoadContent();
        var keys = content.Seasons.Seasons
            .Select(s => SeasonGround.From(s).CacheKey)
            .ToList();

        Assert.Equal(keys.Count, keys.Distinct().Count());
    }

    [Fact]
    public void TheRealContentActuallyRepaintsTheGround()
    {
        // Kdyby žádné období zem neměnilo, byla by celá tahle cesta mrtvý kód.
        var content = LoadContent();

        Assert.Contains(content.Seasons.Seasons, s => SeasonGround.From(s).Changes);
    }

    private static float Brightness(Color c) => (c.R + c.G + c.B) / (3f * 255f);

    private static byte[] Ring(GameContent content, string biome)
    {
        byte index = (byte)content.Biomes.IndexOf(biome);
        var ring = new byte[9];
        Array.Fill(ring, index);
        return ring;
    }

    private static TerrainPainter Painter(out GameContent content)
    {
        content = LoadContent();
        return new TerrainPainter(content.Biomes, seed: 20260728);
    }

    private static GameContent LoadContent() =>
        new ContentLoader().LoadFrom(Path.Combine(AppContext.BaseDirectory, "data"));
}
