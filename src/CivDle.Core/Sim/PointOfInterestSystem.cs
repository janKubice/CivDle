using CivDle.Core.Content;
using CivDle.Core.World;

namespace CivDle.Core.Sim;

/// <summary>Anomálie na mapě: kde leží a co je zač.</summary>
/// <param name="X">Dlaždice vodorovně.</param>
/// <param name="Y">Dlaždice svisle.</param>
/// <param name="KindIndex">Který druh (index do katalogu).</param>
public readonly record struct PointOfInterest(int X, int Y, int KindIndex);

/// <summary>
/// Anomálie ve světě — místa, kam se vyplatí poslat výpravu.
///
/// <para><b>Nedrží žádný seznam.</b> Mapa je nekonečná; kdyby se anomálie
/// generovaly dopředu, musel by se generovat i svět dopředu, a kdyby se
/// dogenerovávaly za běhu, musely by se ukládat — a save by rostl s tím, kam
/// hráč odjel kamerou. Místo toho se pozice <b>dopočítá z hashe</b> seedu
/// a souřadnic kusu mapy: tentýž svět dá vždycky tatáž místa a do savu jde jen
/// to, co hráč opravdu udělal — které anomálie už vybral.</para>
///
/// <para>Vzdálenost se měří od počátku světa, ne od těžiště města. Těžiště se
/// během hry hýbe a anomálie by se podle toho, kam hráč zrovna staví,
/// objevovaly a mizely.</para>
///
/// <para>Vrstva: čistá simulace. Deterministická — žádná náhoda bez seedu.</para>
/// </summary>
public sealed class PointOfInterestSystem
{
    private readonly PoiCatalog _catalog;
    private readonly ITerrain _terrain;
    private readonly long _seed;

    /// <summary>Co už hráč vybral. Jediná věc, kterou musí nést save.</summary>
    private readonly HashSet<long> _claimed = new();

    public PointOfInterestSystem(PoiCatalog catalog, ITerrain terrain, long seed)
    {
        _catalog = catalog;
        _terrain = terrain;
        _seed = seed;
    }

    /// <summary>Kolik anomálií už hráč vybral.</summary>
    public int ClaimedCount => _claimed.Count;

    /// <summary>Vybrané anomálie pro serializaci savu.</summary>
    public IEnumerable<long> ClaimedKeys => _claimed;

    /// <summary>Je tahle anomálie už vybraná?</summary>
    public bool IsClaimed(int x, int y) => _claimed.Contains(TileKey.Pack(x, y));

    /// <summary>Zapíše anomálii jako vybranou.</summary>
    public void Claim(int x, int y) => _claimed.Add(TileKey.Pack(x, y));

    /// <summary>Vyprázdní (Vzestup, nový svět).</summary>
    public void Reset() => _claimed.Clear();

    /// <summary>Obnova ze savu.</summary>
    public void Restore(IEnumerable<long> claimed)
    {
        _claimed.Clear();
        foreach (long key in claimed)
        {
            _claimed.Add(key);
        }
    }

    /// <summary>
    /// Leží v tomhle kusu mapy anomálie? Čistá funkce seedu a souřadnic —
    /// stejný svět dá vždycky stejnou odpověď.
    /// </summary>
    public bool TryAt(int regionX, int regionY, out PointOfInterest poi)
    {
        poi = default;
        if (!_catalog.IsEnabled)
        {
            return false;
        }

        ulong hash = Hash(_seed, regionX, regionY, 0x9E37);
        if ((int)(hash % 100) >= _catalog.ChancePercent)
        {
            return false;
        }

        int region = _catalog.RegionTiles;

        // Uvnitř kusu se drží od okraje: anomálie přesně na hranici by po
        // zaokrouhlení mohla patřit sousedovi a blikala by mezi dvěma místy.
        int margin = Math.Max(1, region / 8);
        int span = Math.Max(1, region - (2 * margin));
        int x = (regionX * region) + margin + (int)((hash >> 8) % (ulong)span);
        int y = (regionY * region) + margin + (int)((hash >> 24) % (ulong)span);

        // Blízko startu ne: první hodina hry je o stavbě, ne o výpravách.
        long distanceSquared = ((long)x * x) + ((long)y * y);

        int biome = _terrain.BiomeAt(x, y);
        int kind = (int)((hash >> 40) % (ulong)_catalog.Count);

        // Zkusí se každý druh od vylosovaného dál — biom i vzdálenost místo
        // vyřadí a bez téhle smyčky by na pouštích a v horách nebylo nic.
        for (int i = 0; i < _catalog.Count; i++)
        {
            var def = _catalog[(kind + i) % _catalog.Count];
            if (!def.IsBiomeAllowed(biome))
            {
                continue;
            }

            if (distanceSquared < (long)def.MinDistanceFromStart * def.MinDistanceFromStart)
            {
                continue;
            }

            poi = new PointOfInterest(x, y, _catalog.IndexOf(def.Id));
            return true;
        }

        return false;
    }

    /// <summary>
    /// Anomálie v obdélníku dlaždic (pro render i pro hledání pod kurzorem).
    /// Vybrané se nevrací — jsou vyčerpané.
    /// </summary>
    public void InRange(int minX, int minY, int maxX, int maxY, List<PointOfInterest> results)
    {
        results.Clear();
        if (!_catalog.IsEnabled)
        {
            return;
        }

        int region = _catalog.RegionTiles;
        int minRegionX = FloorDiv(minX, region);
        int minRegionY = FloorDiv(minY, region);
        int maxRegionX = FloorDiv(maxX, region);
        int maxRegionY = FloorDiv(maxY, region);

        // Strop na počet kusů: při maximálním oddálení jich je ve výřezu tolik,
        // že by hledání anomálií stálo víc než kreslení města.
        if ((long)(maxRegionX - minRegionX + 1) * (maxRegionY - minRegionY + 1) > 4096)
        {
            return;
        }

        for (int regionY = minRegionY; regionY <= maxRegionY; regionY++)
        {
            for (int regionX = minRegionX; regionX <= maxRegionX; regionX++)
            {
                if (TryAt(regionX, regionY, out var poi) && !IsClaimed(poi.X, poi.Y))
                {
                    results.Add(poi);
                }
            }
        }
    }

    /// <summary>Anomálie na téhle dlaždici (nebo těsně u ní), pokud tam nějaká nevybraná je.</summary>
    public bool TryPick(int x, int y, out PointOfInterest poi)
    {
        poi = default;
        if (!_catalog.IsEnabled)
        {
            return false;
        }

        int region = _catalog.RegionTiles;
        if (!TryAt(FloorDiv(x, region), FloorDiv(y, region), out var found) || IsClaimed(found.X, found.Y))
        {
            return false;
        }

        if (Math.Abs(found.X - x) > 1 || Math.Abs(found.Y - y) > 1)
        {
            return false;
        }

        poi = found;
        return true;
    }

    /// <summary>
    /// Kterou odměnu tahle anomálie schovává.
    ///
    /// <para>Losuje se z hashe místa, ne z generátoru za běhu: hráč musí dostat
    /// totéž bez ohledu na to, kdy se tam vypraví a jestli mezitím hru vypnul.
    /// Bez toho by se dalo uložit před výpravou a losovat, dokud nepadne
    /// relikvie.</para>
    /// </summary>
    public int RewardIndexFor(in PointOfInterest poi)
    {
        var def = _catalog[poi.KindIndex];
        int total = def.TotalWeight;
        if (total <= 0)
        {
            return -1;
        }

        int roll = (int)(Hash(_seed, poi.X, poi.Y, 0x5EED) % (ulong)total);
        for (int i = 0; i < def.Rewards.Count; i++)
        {
            roll -= def.Rewards[i].Weight;
            if (roll < 0)
            {
                return i;
            }
        }

        return def.Rewards.Count - 1;
    }

    /// <summary>Rozhoď seed a souřadnice na číslo, ze kterého se dá číst po kouscích.</summary>
    private static ulong Hash(long seed, int x, int y, ulong salt)
    {
        unchecked
        {
            ulong h = (ulong)seed * 0x9E3779B97F4A7C15UL;
            h ^= (ulong)(uint)x * 0xBF58476D1CE4E5B9UL;
            h ^= (ulong)(uint)y * 0x94D049BB133111EBUL;
            h ^= salt;
            h ^= h >> 30;
            h *= 0xBF58476D1CE4E5B9UL;
            h ^= h >> 27;
            h *= 0x94D049BB133111EBUL;
            return h ^ (h >> 31);
        }
    }

    /// <summary>Dělení dolů — mapa je nekonečná oběma směry.</summary>
    private static int FloorDiv(int value, int divisor)
    {
        int quotient = value / divisor;
        return value % divisor != 0 && (value < 0) != (divisor < 0) ? quotient - 1 : quotient;
    }
}
