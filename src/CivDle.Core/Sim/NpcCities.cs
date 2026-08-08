using CivDle.Core.Content;

namespace CivDle.Core.Sim;

/// <summary>Jedno cizí město na mapě — poloha, druh a jméno.</summary>
/// <param name="Key">Stabilní klíč (z mřížky) — pod ním se drží stav i v savu.</param>
/// <param name="X">Střed města v dlaždicích.</param>
/// <param name="Y">Střed města v dlaždicích.</param>
/// <param name="ArchetypeIndex">Druh města (co nabízí, jak vypadá).</param>
/// <param name="NameIndex">Index do seznamu jmen.</param>
public readonly record struct NpcCity(long Key, int X, int Y, int ArchetypeIndex, int NameIndex);

/// <summary>
/// Cesta mezi dvěma cizími městy. Cizí města si mezi sebou obchodují bez ohledu
/// na hráče — tohle je to, co z toho jde vidět.
/// </summary>
/// <param name="From">Město na jednom konci.</param>
/// <param name="To">Město na druhém konci.</param>
public readonly record struct NpcCityLink(NpcCity From, NpcCity To);

/// <summary>Co hráč s daným městem zatím zažil.</summary>
public struct NpcCityState
{
    /// <summary>Vztah 0–100. Roste dary a obchodem.</summary>
    public int Relation;

    /// <summary>Kolik obchodů proběhlo.</summary>
    public long Trades;

    /// <summary>Vede k městu cesta? (Bez ní se obchodovat nedá.)</summary>
    public bool RoadLinked;

    /// <summary>Je město už součástí hráčovy civilizace?</summary>
    public bool Absorbed;

    /// <summary>
    /// Srovnal ho meteorit nebo povodeň se zemí.
    ///
    /// <para>Poloha města plyne ze seedu a tu smazat nejde — proto se zkáza musí
    /// uložit jako příznak. Zničené město zmizí z mapy i ze seznamu sídel
    /// a nevrátí se.</para>
    /// </summary>
    public bool Destroyed;
}

/// <summary>
/// Cizí města na nekonečné mapě.
///
/// <para>Poloha měst se <b>nepočítá ani neukládá</b> — odvozuje se ze seedu
/// stejně jako terén. Na nekonečné mapě je to jediný postup, který dává smysl:
/// města existují všude, ale zjistí se, teprve až tam hráč dojde. Ukládá se jen
/// to, co hráč změnil (vztah, obchody, pohlcení), a toho jsou desítky položek,
/// ne tisíce.</para>
///
/// <para>Mřížka je hrubá schválně: jedno město na <see cref="CellTiles"/> dlaždic
/// znamená, že soused je vždycky kus cesty daleko — a to je celý ten důvod
/// stavět k němu silnici.</para>
///
/// <para>Vrstva: čistá simulace, nezná render.</para>
/// </summary>
public sealed class NpcCityMap
{
    /// <summary>Hrana buňky mřížky měst v dlaždicích.</summary>
    public const int CellTiles = 96;

    /// <summary>Kolik buněk kolem startu zůstane prázdných — hráč má mít chvíli klid.</summary>
    private const int QuietCells = 1;

    /// <summary>Jak často v buňce město opravdu stojí (0–1).</summary>
    private const double Density = 0.55;

    /// <summary>Jak často spolu dvě sousední města obchodují — a mají tedy cestu (0–1).</summary>
    private const double LinkDensity = 0.62;

    private readonly long _seed;
    private readonly int _archetypeCount;
    private readonly int _nameCount;

    public NpcCityMap(long seed, int archetypeCount, int nameCount)
    {
        _seed = seed;
        _archetypeCount = Math.Max(1, archetypeCount);
        _nameCount = Math.Max(1, nameCount);
    }

    /// <summary>Klíč buňky, do které dlaždice spadá.</summary>
    public static long KeyOf(int cellX, int cellY) => ((long)(uint)cellX << 32) | (uint)cellY;

    /// <summary>Stojí v téhle buňce město? Odpověď je čistá funkce seedu a souřadnic.</summary>
    public bool TryCityIn(int cellX, int cellY, out NpcCity city)
    {
        city = default;

        // Kolem startu je klid: hráč nemá potkat cizí město dřív, než postaví
        // vlastní druhý dům.
        if (Math.Abs(cellX) <= QuietCells && Math.Abs(cellY) <= QuietCells)
        {
            return false;
        }

        ulong h = Hash(cellX, cellY);
        if ((h & 0xFFFF) / (double)0x10000 >= Density)
        {
            return false;
        }

        // Poloha uvnitř buňky, ať města nesedí v pravidelné mřížce jako panelák.
        int offsetX = (int)((h >> 16) % (CellTiles - 24)) + 12;
        int offsetY = (int)((h >> 30) % (CellTiles - 24)) + 12;

        city = new NpcCity(
            KeyOf(cellX, cellY),
            cellX * CellTiles + offsetX,
            cellY * CellTiles + offsetY,
            (int)((h >> 44) % (ulong)_archetypeCount),
            (int)((h >> 50) % (ulong)_nameCount));
        return true;
    }

    /// <summary>Města v okolí dlaždice (v dlaždicích). Používá render i hledání cíle.</summary>
    public IEnumerable<NpcCity> CitiesNear(int tileX, int tileY, int radiusTiles)
    {
        int minCell = FloorDiv(tileX - radiusTiles, CellTiles);
        int maxCell = FloorDiv(tileX + radiusTiles, CellTiles);
        int minCellY = FloorDiv(tileY - radiusTiles, CellTiles);
        int maxCellY = FloorDiv(tileY + radiusTiles, CellTiles);

        for (int cy = minCellY; cy <= maxCellY; cy++)
        {
            for (int cx = minCell; cx <= maxCell; cx++)
            {
                if (TryCityIn(cx, cy, out var city))
                {
                    yield return city;
                }
            }
        }
    }

    /// <summary>Město pod klíčem — pro obnovu ze savu a pro akce z UI.</summary>
    public bool TryCityByKey(long key, out NpcCity city) =>
        TryCityIn((int)(key >> 32), (int)(key & 0xFFFFFFFF), out city);

    /// <summary>
    /// Silnice mezi dvěma cizími městy v okolí dlaždice.
    ///
    /// <para>Spojení se hledá jen na východ a na jih od každé buňky. Není to
    /// lenost: kdyby se koukalo na všechny čtyři strany, každá cesta by vyšla
    /// dvakrát a render by ji dvakrát kreslil.</para>
    ///
    /// <para>Cesta nevede mezi každou dvojicí — ať je vidět, že svět má svoje
    /// spády a ne pravidelnou mříž.</para>
    /// </summary>
    public IEnumerable<NpcCityLink> LinksNear(int tileX, int tileY, int radiusTiles)
    {
        int minCellX = FloorDiv(tileX - radiusTiles, CellTiles) - 1;
        int maxCellX = FloorDiv(tileX + radiusTiles, CellTiles);
        int minCellY = FloorDiv(tileY - radiusTiles, CellTiles) - 1;
        int maxCellY = FloorDiv(tileY + radiusTiles, CellTiles);

        for (int cy = minCellY; cy <= maxCellY; cy++)
        {
            for (int cx = minCellX; cx <= maxCellX; cx++)
            {
                if (!TryCityIn(cx, cy, out var from))
                {
                    continue;
                }

                if (TryCityIn(cx + 1, cy, out var east) && HasLink(from.Key, east.Key))
                {
                    yield return new NpcCityLink(from, east);
                }

                if (TryCityIn(cx, cy + 1, out var south) && HasLink(from.Key, south.Key))
                {
                    yield return new NpcCityLink(from, south);
                }
            }
        }
    }

    /// <summary>Vede mezi těmi dvěma městy cesta? Čistá funkce klíčů a seedu.</summary>
    private bool HasLink(long a, long b)
    {
        unchecked
        {
            ulong h = (ulong)_seed * 0xD6E8FEB86659FD93UL;
            h ^= (ulong)a * 0xA24BAED4963EE407UL;
            h ^= (ulong)b * 0x9FB21C651E98DF25UL;
            h ^= h >> 29;
            h *= 0xBF58476D1CE4E5B9UL;
            h ^= h >> 32;
            return (h & 0xFFFF) / (double)0x10000 < LinkDensity;
        }
    }

    private ulong Hash(int cellX, int cellY)
    {
        unchecked
        {
            ulong h = (ulong)_seed * 0x9E3779B97F4A7C15UL;
            h ^= (ulong)(uint)cellX * 0xBF58476D1CE4E5B9UL;
            h ^= (ulong)(uint)cellY * 0x94D049BB133111EBUL;
            h ^= h >> 30;
            h *= 0xBF58476D1CE4E5B9UL;
            h ^= h >> 27;
            h *= 0x94D049BB133111EBUL;
            h ^= h >> 31;
            return h;
        }
    }

    private static int FloorDiv(int value, int divisor)
    {
        int quotient = value / divisor;
        return value % divisor != 0 && (value < 0) != (divisor < 0) ? quotient - 1 : quotient;
    }
}

/// <summary>Jak dopadl pokus o jednání s cizím městem.</summary>
public enum DiplomacyResult
{
    /// <summary>Povedlo se.</summary>
    Ok,

    /// <summary>Nedost surovin.</summary>
    NotEnoughResources,

    /// <summary>Vztah ještě není dost dobrý.</summary>
    RelationTooLow,

    /// <summary>Chybí spojení (cesta nebo přístav).</summary>
    NoConnection,

    /// <summary>Město už hráči patří.</summary>
    AlreadyYours,

    /// <summary>Mechanika je v datech vypnutá nebo město neexistuje.</summary>
    Unavailable,

    /// <summary>
    /// Cesta se nedá vést — mezi hráčem a městem je něco, co silnice nepřekoná
    /// (oceán širší než most). Neplatí se za ni; slíbená a nepostavená cesta by
    /// byla horší než odmítnutí.
    /// </summary>
    NoRoute,
}
