using CivDle.Core.Content;
using Microsoft.Xna.Framework;

namespace CivDle.Rendering;

/// <summary>
/// Jakou barvu dostane jedna dlaždice terénu.
///
/// <para>Proč to vzniklo: terén byl doslova tabulka. Každá dlaždice měla barvu
/// svého biomu plus trochu jasu navíc, takže les končil rovnou hranou po
/// mřížce a celá pláň měla přesně jeden odstín. Z výšky to vypadalo jako
/// Excel — a přitom stačí dvě věci: <b>rozmlžit hranice</b> mezi biomy
/// (dithering, jako na starých osmibitových hrách) a přidat <b>velkoplošný
/// šum</b>, aby pláň měla světlejší a tmavší místa.</para>
///
/// <para>Že je to levné, není náhoda: chunky se pečou do textury jednou a pak
/// se kreslí jedním draw callem. Cokoli, co se počítá tady, se počítá jednou
/// za život chunku — proto si tu můžeme dovolit dívat se na sousedy, což by
/// v kreslicí smyčce bylo nemyslitelné.</para>
///
/// <para>Vrstva: čistý render (rozhoduje o barvě, nekreslí). Vlastní třída,
/// aby se dala ověřit bez grafiky — <see cref="TerrainRenderer"/> se stará
/// o chunky a cache, tohle o vzhled.</para>
/// </summary>
public sealed class TerrainPainter
{
    /// <summary>Poloměr okna, ze kterého se odhaduje hloubka vody.</summary>
    public const int WaterWindowRadius = 2;

    /// <summary>Kolik dlaždic je v tom okně (5×5).</summary>
    public const int WaterWindowTiles = (2 * WaterWindowRadius + 1) * (2 * WaterWindowRadius + 1);

    /// <summary>Jak často se dlaždice u hranice biomů převezme od souseda.</summary>
    private const float DitherChance = 0.42f;

    /// <summary>Velikost oka makro-šumu v dlaždicích. Menší = neklidnější krajina.</summary>
    private const int MacroCell = 24;

    /// <summary>Jak silně makro-šum mění jas (±). Nad 8 % už je vidět mřížka šumu.</summary>
    private const float MacroStrength = 0.055f;

    /// <summary>Jak moc ztmavne otevřená voda oproti mělčině.</summary>
    private const float DepthDarken = 0.20f;

    /// <summary>
    /// Kolik dlaždic „rozmaže" hloubku, aby z gradientu nebyly schody.
    ///
    /// <para>Okno hloubky se posunem o dlaždici změní skokem o celou řadu, takže
    /// by hladina dostala viditelné pásy — a to je přesně ta mřížka, kterou tenhle
    /// soubor jinde rozbíjí. Rozhodí se proto samotný <b>vstup</b>: hranice mezi
    /// dvěma hloubkami se rozsype na stipl a oko z něj přečte plynulý přechod.</para>
    /// </summary>
    private const float DepthDither = 4.5f;

    /// <summary>Barva pěny na pobřeží.</summary>
    private static readonly Color Foam = new(232, 244, 250);

    private readonly BiomeRegistry _biomes;
    private readonly long _seed;

    public TerrainPainter(BiomeRegistry biomes, long seed)
    {
        _biomes = biomes;
        _seed = seed;
    }

    /// <summary>
    /// Barva dlaždice.
    /// </summary>
    /// <param name="ring">
    /// Okolí 3×3 v řádkovém pořadí; prostřední prvek (index 4) je dlaždice sama.
    /// </param>
    /// <param name="waterInWindow">
    /// Kolik vodních dlaždic je v okně 5×5 kolem. Z toho se odvozuje hloubka:
    /// zátoka obklopená pevninou je mělká a světlá, otevřené moře tmavé.
    /// </param>
    public Color Tile(int worldX, int worldY, ReadOnlySpan<byte> ring, int waterInWindow)
    {
        byte self = ring[4];
        var biome = _biomes[self];

        // Jas dlaždice: drobná per-dlaždicová variace (proti repetici textury)
        // a přes ni velkoplošná vlna (aby měla pláň světlejší a tmavší kus).
        float brightness = 1f + (Unit(worldX, worldY, 1) * 2f - 1f) * biome.ColorVariation;
        brightness *= 1f + (MacroNoise(worldX, worldY) * 2f - 1f) * MacroStrength;

        if (biome.IsWater)
        {
            return PaintWater(worldX, worldY, ring, waterInWindow, biome, brightness);
        }

        // Souš: u hranice biomů se dlaždice tu a tam převezme od souseda, takže
        // se z rovné hrany stane rozstřapatělý přechod.
        byte painted = DitherWithNeighbour(worldX, worldY, ring, self);
        var color = _biomes[painted].MapColor;
        return Shade(color, brightness);
    }

    /// <summary>
    /// Voda: pěna u břehu a hloubkový gradient. Bez toho byla vodní plocha
    /// jednolitá modrá skvrna s tvrdou hranou — a hráč po ní má koukat
    /// nejčastěji ze všeho, protože kolem ní staví.
    /// </summary>
    private Color PaintWater(
        int worldX, int worldY, ReadOnlySpan<byte> ring, int waterInWindow, Biome biome, float brightness)
    {
        // Hloubka: podíl vody v okolí. Zátoka mezi mysy zůstane světlá,
        // otevřené moře ztmavne — z jedné barvy je najednou pobřeží.
        float dithered = waterInWindow + (Unit(worldX, worldY, 9) - 0.5f) * DepthDither;
        float openness = Math.Clamp(dithered / WaterWindowTiles, 0f, 1f);
        float depth = openness * openness; // mělčina se drží déle, hloubka nastupuje rychle
        var color = Shade(biome.MapColor, brightness * (1f - DepthDarken * depth));

        if (!TouchesLand(ring))
        {
            return color;
        }

        // Pěna: ne rovnoměrný lem, ale rozstřapatělá čára — jinak by vypadala
        // jako obtažení fixem. Sílu určí hash dlaždice.
        float foam = 0.30f + 0.45f * Unit(worldX, worldY, 5);
        return Color.Lerp(color, Foam, foam);
    }

    /// <summary>
    /// Dotýká se dlaždice souše některou stranou? (Diagonály ne — od nich by
    /// pěna vznikala i v místech, kde se voda se souší jen mine rohem.)
    /// </summary>
    private bool TouchesLand(ReadOnlySpan<byte> ring) =>
        !_biomes[ring[1]].IsWater || !_biomes[ring[3]].IsWater
        || !_biomes[ring[5]].IsWater || !_biomes[ring[7]].IsWater;

    /// <summary>
    /// Vybere, jestli se dlaždice nakreslí barvou souseda.
    ///
    /// <para>Voda se do dithering nezapočítává schválně: modrá tečka uvnitř
    /// louky vypadá jako kaluž, kterou hráč zkusí obestavět. Rozmlžují se jen
    /// hranice mezi souší a souší (les / step / písek / skála), kde jde
    /// opravdu jen o vzhled.</para>
    /// </summary>
    private byte DitherWithNeighbour(int worldX, int worldY, ReadOnlySpan<byte> ring, byte self)
    {
        if (Unit(worldX, worldY, 2) >= DitherChance)
        {
            return self;
        }

        // Ze čtyř stran se vybere jedna — deterministicky, ať se dlaždice
        // mezi překreslením chunku nemění.
        int start = (int)(Unit(worldX, worldY, 3) * 4f) & 3;
        for (int i = 0; i < 4; i++)
        {
            byte neighbour = ring[CardinalIndex[(start + i) & 3]];
            if (neighbour != self && !_biomes[neighbour].IsWater)
            {
                return neighbour;
            }
        }

        return self;
    }

    /// <summary>Indexy čtyř stran v okolí 3×3 (nahoře, vlevo, vpravo, dole).</summary>
    private static readonly int[] CardinalIndex = { 1, 3, 5, 7 };

    private static Color Shade(RgbColor color, float brightness) => new(
        ClampByte(color.R * brightness),
        ClampByte(color.G * brightness),
        ClampByte(color.B * brightness));

    private static Color Shade(Color color, float brightness) => new(
        ClampByte(color.R * brightness),
        ClampByte(color.G * brightness),
        ClampByte(color.B * brightness));

    private static byte ClampByte(float value) => (byte)Math.Clamp(value, 0f, 255f);

    /// <summary>
    /// Velkoplošná vlna 0–1 — hodnotový šum na hrubé mřížce s hladkou
    /// interpolací. Právě ta hrubá mřížka je smysl věci: krajina má mít
    /// světlejší a tmavší <b>kraje</b>, ne zrno.
    /// </summary>
    public float MacroNoise(int worldX, int worldY)
    {
        int cellX = FloorDiv(worldX, MacroCell);
        int cellY = FloorDiv(worldY, MacroCell);
        float fx = Smooth((worldX - cellX * MacroCell) / (float)MacroCell);
        float fy = Smooth((worldY - cellY * MacroCell) / (float)MacroCell);

        float topLeft = Unit(cellX, cellY, 11);
        float topRight = Unit(cellX + 1, cellY, 11);
        float bottomLeft = Unit(cellX, cellY + 1, 11);
        float bottomRight = Unit(cellX + 1, cellY + 1, 11);

        float top = topLeft + (topRight - topLeft) * fx;
        float bottom = bottomLeft + (bottomRight - bottomLeft) * fx;
        return top + (bottom - top) * fy;
    }

    private static float Smooth(float t) => t * t * (3f - 2f * t);

    private static int FloorDiv(int a, int b) => (int)MathF.Floor((float)a / b);

    /// <summary>Deterministický hash dlaždice → 0–1 (mix konstantami ze SplitMix64).</summary>
    private float Unit(int x, int y, int salt)
    {
        ulong h = unchecked((ulong)_seed) ^ (ulong)(uint)salt * 0x2545F4914F6CDD1DUL;
        h ^= (ulong)(uint)x * 0x9E3779B97F4A7C15UL;
        h ^= (ulong)(uint)y * 0xBF58476D1CE4E5B9UL;
        h = (h ^ (h >> 30)) * 0xBF58476D1CE4E5B9UL;
        h = (h ^ (h >> 27)) * 0x94D049BB133111EBUL;
        h ^= h >> 31;
        return (h >> 40) / (float)(1 << 24);
    }
}
