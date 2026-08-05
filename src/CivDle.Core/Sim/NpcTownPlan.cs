using CivDle.Core.World;
using CivDle.Core.Content;

namespace CivDle.Core.Sim;

/// <summary>Jedna budova cizího města: co a kde stojí.</summary>
/// <param name="DefIndex">Index do registru budov — <b>tytéž definice, jaké staví hráč</b>.</param>
/// <param name="X">Levý horní roh v dlaždicích.</param>
/// <param name="Y">Levý horní roh v dlaždicích.</param>
public readonly record struct NpcTownBuilding(int DefIndex, int X, int Y);

/// <summary>
/// Postavené cizí město: skutečné budovy na skutečných dlaždicích a silnice mezi
/// nimi.
///
/// <para>Do téhle chvíle bylo cizí město namalovaná kulisa — pár barevných
/// obdélníků, které se nepodobaly ničemu, co hráč staví, a spojnice, které nebyly
/// silnice. Vypadalo to jako cedule „tady je město", ne jako město. Teď se
/// skládá ze stejných definic budov a stejných silničních dlaždic, takže se
/// kreslí týmiž sprity, chodí po něm lidé a po pohlcení ho hráč prostě
/// <b>dostane</b>.</para>
/// </summary>
public sealed class NpcTown
{
    public NpcTown(long key, IReadOnlyList<NpcTownBuilding> buildings, IReadOnlyList<RoadTile> roads)
    {
        Key = key;
        Buildings = buildings;
        Roads = roads;
    }

    /// <summary>Klíč města, ke kterému plán patří.</summary>
    public long Key { get; }

    /// <summary>Co ve městě stojí.</summary>
    public IReadOnlyList<NpcTownBuilding> Buildings { get; }

    /// <summary>Silnice uvnitř města.</summary>
    public IReadOnlyList<RoadTile> Roads { get; }
}

/// <summary>
/// Rozvrhne cizí město do dlaždic.
///
/// <para>Rozvržení je <b>čistá funkce</b> klíče města a seedu světa: na nekonečné
/// mapě se města neukládají, takže musí vyjít pokaždé stejně. Z toho plyne i to,
/// co plán <b>nesmí</b> brát v úvahu — hráčův výzkum, jeho suroviny ani stav hry.
/// Cizí město stojí, ať hráč umí cokoli.</para>
///
/// <para>Které budovy se použijí, je v datech (paleta u druhu města), ne v kódu:
/// selské městečko má chalupy a pole, hornická osada doly. Kód řeší jen
/// <b>jak</b> je rozestavět — podél ulice, ať to čte jako vesnice a ne jako
/// hromada.</para>
/// </summary>
public static class NpcTownPlanner
{
    /// <summary>Jak daleko od středu můžou stát domy (dlaždice).</summary>
    private const int Spread = 7;

    /// <summary>Rozvrhne město. Vrací prázdný plán, když druh nemá paletu budov.</summary>
    public static NpcTown Plan(GameContent content, long seed, in NpcCity city)
    {
        var archetype = content.NpcCities.Archetypes[city.ArchetypeIndex];
        var palette = archetype.BuildingIndices;
        if (palette.Count == 0)
        {
            return new NpcTown(city.Key, Array.Empty<NpcTownBuilding>(), Array.Empty<RoadTile>());
        }

        var buildings = new List<NpcTownBuilding>();
        var roads = new List<RoadTile>();
        var taken = new HashSet<long>();

        ulong hash = Mix((ulong)seed ^ (ulong)city.Key);
        int houses = 6 + (int)(hash % 7); // 6–12 budov: vesnice, ne metropole

        // Hlavní ulice středem. Bez ní by domy stály v neuspořádané kupě —
        // a hlavně: silnice je to, co z toho dělá město, ne jen shluk staveb.
        bool horizontal = (hash & 0x100) == 0;
        for (int i = -Spread; i <= Spread; i++)
        {
            int rx = horizontal ? city.X + i : city.X;
            int ry = horizontal ? city.Y : city.Y + i;
            roads.Add(new RoadTile(rx, ry));
            taken.Add(TileKey.Pack(rx, ry));
        }

        // Domy střídavě po obou stranách ulice, od středu ven — tak roste
        // skutečná vesnice a zároveň je zaručeno, že každý dům u cesty stojí.
        for (int i = 0; i < houses; i++)
        {
            ulong roll = Mix(hash + (ulong)i * 0x9E3779B97F4A7C15UL);
            int defIndex = palette[(int)(roll % (ulong)palette.Count)];
            var def = content.Buildings[defIndex];

            int along = (i / 2) - houses / 4;
            int side = (i % 2 == 0) ? 1 : -1;
            int gap = 1 + (int)((roll >> 8) % 2);

            int x = horizontal ? city.X + along * 2 : city.X + side * gap;
            int y = horizontal ? city.Y + side * gap : city.Y + along * 2;

            if (!TryReserve(taken, x, y, def.FootprintWidth, def.FootprintHeight))
            {
                continue; // místo je zabrané ulicí nebo sousedem — dům se vynechá
            }

            buildings.Add(new NpcTownBuilding(defIndex, x, y));
        }

        return new NpcTown(city.Key, buildings, roads);
    }

    /// <summary>Zabere půdorys, pokud je volný. Vrací <c>false</c>, když se nevejde.</summary>
    private static bool TryReserve(HashSet<long> taken, int x, int y, int width, int height)
    {
        for (int dy = 0; dy < height; dy++)
        {
            for (int dx = 0; dx < width; dx++)
            {
                if (taken.Contains(TileKey.Pack(x + dx, y + dy)))
                {
                    return false;
                }
            }
        }

        for (int dy = 0; dy < height; dy++)
        {
            for (int dx = 0; dx < width; dx++)
            {
                taken.Add(TileKey.Pack(x + dx, y + dy));
            }
        }

        return true;
    }

    private static ulong Mix(ulong value)
    {
        unchecked
        {
            value ^= value >> 30;
            value *= 0xBF58476D1CE4E5B9UL;
            value ^= value >> 27;
            value *= 0x94D049BB133111EBUL;
            value ^= value >> 31;
            return value;
        }
    }
}
