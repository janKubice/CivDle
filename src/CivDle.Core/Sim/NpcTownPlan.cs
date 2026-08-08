using CivDle.Core.Content;
using CivDle.Core.World;

namespace CivDle.Core.Sim;

/// <summary>Jedna budova cizího města: co a kde stojí.</summary>
/// <param name="DefIndex">Index do registru budov — <b>tytéž definice, jaké staví hráč</b>.</param>
/// <param name="X">Levý horní roh v dlaždicích.</param>
/// <param name="Y">Levý horní roh v dlaždicích.</param>
public readonly record struct NpcTownBuilding(int DefIndex, int X, int Y);

/// <summary>Rozvrh cizího města: co kde postavit a kudy vést ulice.</summary>
/// <param name="Key">Klíč města, ke kterému rozvrh patří.</param>
/// <param name="Buildings">Domy k postavení.</param>
/// <param name="Roads">Dlaždice ulic.</param>
public readonly record struct NpcTownPlan(
    long Key,
    IReadOnlyList<NpcTownBuilding> Buildings,
    IReadOnlyList<RoadTile> Roads);

/// <summary>
/// Rozvrhne cizí město do dlaždic.
///
/// <para>Rozvržení je <b>čistá funkce</b> klíče města a seedu světa: na nekonečné
/// mapě se plán neukládá, takže musí vyjít pokaždé stejně. Z toho plyne i to,
/// co plán <b>nesmí</b> brát v úvahu — hráčův výzkum ani jeho suroviny. Cizí
/// město stojí, ať hráč umí cokoli.</para>
///
/// <para>Tvar: <b>náměstí, hlavní třída, příčná ulice a kolmé uličky</b>, domy
/// po obou stranách každé ulice a hustota klesající od středu. Předchozí verze
/// stavěla jednu řadu domků podél jedné čáry — vypadalo to jako plot, ne jako
/// sídlo. Náměstí uprostřed je to, z čeho oko pozná město: shluk domů je
/// vesnice, shluk domů kolem prázdného čtverce je město.</para>
///
/// <para>Dům stojí <b>čelem k ulici</b>: kotva se posune tak, aby se půdorys
/// dotýkal silnice tou stranou, u které leží. Bez toho by dvoupolní budova
/// vyrostla přes ulici na druhou stranu a řada by se rozpadla.</para>
///
/// <para>Terén rozhoduje: dům se postaví, jen když ho jeho definice na tom biomu
/// dovoluje a dlaždice je volná. Paleta u druhu města je <b>seznam přání</b>,
/// ne příkaz — u vody vyroste jiné město než na stepi, stejně jako hráči.</para>
/// </summary>
public static class NpcTownPlanner
{
    /// <summary>Polovina hrany náměstí — 1 znamená čtverec 3×3.</summary>
    private const int PlazaHalf = 1;

    /// <summary>Nejmenší dosah hlavní třídy od středu (v dlaždicích).</summary>
    private const int MinReach = 7;

    /// <summary>O kolik obyvatel se dosah zvětší o dlaždici.</summary>
    private const int TilesPerPopulation = 18;

    /// <summary>Nejdelší dosah — dál už by se město slilo se sousedním.</summary>
    private const int MaxReach = 16;

    /// <summary>Nejmenší rozestup uliček podél hlavní třídy.</summary>
    private const int MinSideGap = 3;

    /// <summary>Rozptyl rozestupu uliček — pravidelná mříž vypadá jako tabulka.</summary>
    private const int SideGapSpread = 3;

    /// <summary>Odkud výš se městu vyplatí obchvat kolem dokola.</summary>
    private const int RingReach = 12;

    /// <summary>Jak daleko se hledá suchá zem, když střed z mřížky padne do vody.</summary>
    private const int OriginSearch = 10;

    /// <summary>Kolik procent hustoty přední řady si nechá ta zadní.</summary>
    private const int SecondRowShare = 55;

    /// <summary>
    /// Rozvrhne město.
    /// </summary>
    /// <param name="content">Registry — paleta budov druhu města a jejich půdorysy.</param>
    /// <param name="seed">Seed světa: tentýž svět postaví totéž město.</param>
    /// <param name="city">Město, jehož střed a druh se rozvrhují.</param>
    /// <param name="canBuild">
    /// Smí daná definice stát na daném místě? Tudy do plánu vstupuje terén i to,
    /// co už kolem stojí. <b>Nesmí</b> se ptát na hráčův výzkum ani na jeho
    /// suroviny — cizí město si o dovolení nežádá.
    /// </param>
    /// <param name="canPave">
    /// Smí na dlaždici ležet ulice? Bez tohohle vedla hlavní třída rovnou přes
    /// zátoku a z pobřežního města se stal molo-labyrint nad mořem.
    /// </param>
    public static NpcTownPlan Plan(
        GameContent content, long seed, in NpcCity city,
        Func<int, int, int, bool> canBuild, Func<int, int, bool> canPave)
    {
        var archetype = content.NpcCities.Archetypes[city.ArchetypeIndex];
        var palette = archetype.BuildingIndices;
        if (palette.Count == 0 || !TryFindOrigin(city, canPave, out int originX, out int originY))
        {
            return new NpcTownPlan(city.Key, Array.Empty<NpcTownBuilding>(), Array.Empty<RoadTile>());
        }

        ulong hash = Mix((ulong)seed ^ (ulong)city.Key * 0x9E3779B97F4A7C15UL);
        bool horizontal = (hash & 0x100) == 0;
        int reach = Math.Clamp(MinReach + archetype.Population / TilesPerPopulation, MinReach, MaxReach);

        var roads = new List<RoadTile>();
        var taken = new HashSet<long>();
        var buildings = new List<NpcTownBuilding>();

        LayOutStreets(originX, originY, hash, horizontal, reach, canPave, roads, taken);
        FillPlots(content, originX, originY, hash, reach, palette, canBuild, roads, taken, buildings);

        return new NpcTownPlan(city.Key, buildings, roads);
    }

    /// <summary>Kolem kolika dlaždic se u kandidáta na střed zjišťuje, kolik je tam země.</summary>
    private const int OriginProbe = 3;

    /// <summary>
    /// Střed města posunutý na suchou zem.
    ///
    /// <para>Poloha měst plyne z hrubé mřížky, která o terénu neví — občas tedy
    /// padne doprostřed zálivu. Nejbližší suchá dlaždice ale nestačí: bývá na
    /// samém cípu mysu, takže půlka rozvržení zůstane v moři a z města je pár
    /// domků rozsypaných po pobřeží. Proto se z okolí vybírá místo s <b>nejvíc
    /// zemí kolem</b>, při shodě to bližší ke značce na mapě.</para>
    ///
    /// <para>Když do <see cref="OriginSearch"/> dlaždic žádná zem není, město se
    /// nestaví vůbec — je to bod uprostřed oceánu a značka na mapě tam stačí.</para>
    /// </summary>
    private static bool TryFindOrigin(
        in NpcCity city, Func<int, int, bool> canPave, out int originX, out int originY)
    {
        originX = city.X;
        originY = city.Y;

        int bestLand = 0;
        int bestDistance = int.MaxValue;

        for (int dy = -OriginSearch; dy <= OriginSearch; dy++)
        {
            for (int dx = -OriginSearch; dx <= OriginSearch; dx++)
            {
                int x = city.X + dx;
                int y = city.Y + dy;
                if (!canPave(x, y))
                {
                    continue;
                }

                int land = CountLandAround(canPave, x, y);
                int distance = Math.Abs(dx) + Math.Abs(dy);
                if (land > bestLand || (land == bestLand && distance < bestDistance))
                {
                    bestLand = land;
                    bestDistance = distance;
                    originX = x;
                    originY = y;
                }
            }
        }

        // Jedna suchá dlaždice nestačí. Dřív tu bylo `bestLand > 0`, takže
        // město na cípu mysu nebo na ostrůvku prošlo — rozvržení se pak
        // z devadesáti procent trefilo do vody a hráč našel značku města,
        // pod kterou nestálo skoro nic. Radši žádné město než rozpadlé.
        return bestLand >= MinimumLand;
    }

    /// <summary>
    /// Kolik zastavitelných dlaždic musí být kolem středu, aby tam vůbec
    /// mělo smysl město plánovat. Zhruba polovina zkoumaného okolí — pod tím
    /// se z města stane pár domků rozsypaných po pobřeží.
    /// </summary>
    private static readonly int MinimumLand =
        ((OriginProbe * 2 + 1) * (OriginProbe * 2 + 1)) / 2;

    /// <summary>Kolik dlaždic v okolí kandidáta se dá zastavět.</summary>
    private static int CountLandAround(Func<int, int, bool> canPave, int x, int y)
    {
        int land = 0;
        for (int dy = -OriginProbe; dy <= OriginProbe; dy++)
        {
            for (int dx = -OriginProbe; dx <= OriginProbe; dx++)
            {
                if (canPave(x + dx, y + dy))
                {
                    land++;
                }
            }
        }

        return land;
    }

    /// <summary>
    /// Uliční síť: náměstí, hlavní třída, příčná ulice, kolmé uličky a u větších
    /// měst obchvat. Uličky jsou různě dlouhé a nestejně daleko od sebe —
    /// souměrná mříž vypadá jako tabulka, ne jako město, které rostlo.
    /// </summary>
    private static void LayOutStreets(
        int originX, int originY, ulong hash, bool horizontal, int reach,
        Func<int, int, bool> canPave, List<RoadTile> roads, HashSet<long> taken)
    {
        // Náměstí: prázdný čtverec uprostřed. Domy se o něj opřou ze všech stran.
        for (int dy = -PlazaHalf; dy <= PlazaHalf; dy++)
        {
            for (int dx = -PlazaHalf; dx <= PlazaHalf; dx++)
            {
                AddRoad(roads, taken, canPave, originX + dx, originY + dy);
            }
        }

        // Hlavní třída přes celé město a kratší příčná ulice — křižovatka
        // u náměstí dá městu střed, ze kterého se dá odbočit.
        AddRun(roads, taken, canPave, originX, originY, horizontal, offset: 0, from: -reach, to: reach);
        AddRun(roads, taken, canPave, originX, originY, !horizontal, offset: 0, from: -reach / 2, to: reach / 2);

        // Uličky kolmo na hlavní třídu, každá jinak dlouhá a jinak daleko.
        for (int offset = -reach + 2; offset <= reach - 2;)
        {
            ulong roll = Mix(hash + (ulong)(offset + 128) * 0xD6E8FEB86659FD93UL);
            if (offset != 0)
            {
                int up = 2 + (int)(roll % (ulong)(reach / 2 + 1));
                int down = 2 + (int)((roll >> 12) % (ulong)(reach / 2 + 1));
                AddRun(roads, taken, canPave, originX, originY, !horizontal, offset, -down, up);
            }

            offset += MinSideGap + (int)((roll >> 24) % SideGapSpread);
        }

        if (reach >= RingReach)
        {
            AddRing(roads, taken, canPave, originX, originY, reach - 3);
        }
    }

    /// <summary>
    /// Rovný úsek ulice. <paramref name="along"/> říká, jestli běží po ose X;
    /// <paramref name="offset"/> je odsazení od středu na kolmé ose.
    /// </summary>
    private static void AddRun(
        List<RoadTile> roads, HashSet<long> taken, Func<int, int, bool> canPave,
        int originX, int originY, bool along, int offset, int from, int to)
    {
        for (int i = from; i <= to; i++)
        {
            AddRoad(
                roads, taken, canPave,
                along ? originX + i : originX + offset,
                along ? originY + offset : originY + i);
        }
    }

    /// <summary>Obchvat kolem města — jen u těch, která na něj velikostí dorostla.</summary>
    private static void AddRing(
        List<RoadTile> roads, HashSet<long> taken, Func<int, int, bool> canPave,
        int originX, int originY, int radius)
    {
        for (int i = -radius; i <= radius; i++)
        {
            AddRoad(roads, taken, canPave, originX + i, originY - radius);
            AddRoad(roads, taken, canPave, originX + i, originY + radius);
            AddRoad(roads, taken, canPave, originX - radius, originY + i);
            AddRoad(roads, taken, canPave, originX + radius, originY + i);
        }
    }

    /// <summary>
    /// Obestaví ulice domy. Prochází se dlaždice <b>vedle silnic</b>: dům u cesty
    /// je to, co odlišuje město od náhodných staveb v poli.
    ///
    /// <para>Dvě řady: první přímo u ulice, druhá za ní. S jedinou řadou byly
    /// bloky duté a město vypadalo jako plot kolem prázdných dvorů — druhá řada
    /// z něj udělá zástavbu. Ta zadní je řidší, takže se nezvrhne v beton.</para>
    /// </summary>
    private static void FillPlots(
        GameContent content, int originX, int originY, ulong hash, int reach,
        IReadOnlyList<int> palette, Func<int, int, int, bool> canBuild,
        List<RoadTile> roads, HashSet<long> taken, List<NpcTownBuilding> buildings)
    {
        // Kolem každé silnice se zkouší všechny čtyři strany — nároží je pak
        // zastavěné taky a ulice nekončí uprostřed pole.
        Span<int> sideX = [0, 0, -1, 1];
        Span<int> sideY = [-1, 1, 0, 0];

        // Kopie: seznam silnic se během osazování nemění, ale procházíme ho
        // podle indexu a plán musí být nezávislý na tom, kolik domů už stojí.
        int roadCount = roads.Count;

        for (int row = 0; row < 2; row++)
        {
            for (int i = 0; i < roadCount; i++)
            {
                var road = roads[i];
                for (int side = 0; side < 4; side++)
                {
                    ulong roll = Mix(hash + (ulong)((i * 4 + side + 1) * (row + 1)) * 0xBF58476D1CE4E5B9UL);

                    // Hustota klesá od středu: jádro je sevřené, okraj se rozpadá
                    // do samot. Bez toho má město ostrou hranu jako vystřižený papír.
                    int distance = Math.Abs(road.X - originX) + Math.Abs(road.Y - originY);
                    int keepChance = Math.Max(20, 92 - distance * 80 / Math.Max(1, reach * 2));
                    if (row == 1)
                    {
                        keepChance = keepChance * SecondRowShare / 100; // zadní řada je řidší
                    }

                    if ((int)(roll % 100) >= keepChance)
                    {
                        continue;
                    }

                    // Jak blízko středu ta parcela je (0 = kraj, 100 = náměstí).
                    int centrality = Math.Max(0, 100 - distance * 100 / Math.Max(1, reach * 2));

                    if (TryPlaceBeside(
                            content, palette, canBuild, taken, road.X, road.Y,
                            sideX[side], sideY[side], row, centrality, roll, out var placed))
                    {
                        buildings.Add(placed);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Zkusí na parcelu u ulice posadit dům z palety.
    ///
    /// <para>Paleta druhu města je řazená od nejběžnějšího po nejzvláštnější
    /// (dům, dům, dům, statek, sýpka, mlýn). Losování se proto <b>posouvá podle
    /// vzdálenosti od středu</b>: na náměstí padne spíš tržnice nebo chrám, na
    /// kraji domky. Bez toho stál mlýn stejně často na návsi jako v poli a město
    /// nemělo centrum — vypadalo jako náhodně vysypaná hrst budov.</para>
    ///
    /// <para>Když se vylosovaný typ na daný terén nehodí, projde se paleta dokola
    /// — městečko u vody tak vypadá jinak než v horách.</para>
    /// </summary>
    private static bool TryPlaceBeside(
        GameContent content, IReadOnlyList<int> palette, Func<int, int, int, bool> canBuild,
        HashSet<long> taken, int roadX, int roadY, int dx, int dy, int row, int centrality,
        ulong roll, out NpcTownBuilding placed)
    {
        int start = PickFromPalette(palette.Count, centrality, roll);

        for (int step = 0; step < palette.Count; step++)
        {
            int defIndex = palette[(start + step) % palette.Count];
            var def = content.Buildings[defIndex];

            // Kotva tak, aby se dům dotýkal ulice tou stranou, u které stojí.
            // Zadní řada stojí o svůj půdorys dál — hned za předním domem.
            int depth = row == 0 ? 0 : 1;
            int x = dx < 0 ? roadX - def.FootprintWidth - depth : roadX + dx + dx * depth;
            int y = dy < 0 ? roadY - def.FootprintHeight - depth : roadY + dy + dy * depth;

            if (IsFree(taken, x, y, def.FootprintWidth, def.FootprintHeight)
                && canBuild(defIndex, x, y))
            {
                Reserve(taken, x, y, def.FootprintWidth, def.FootprintHeight);
                placed = new NpcTownBuilding(defIndex, x, y);
                return true;
            }
        }

        placed = default;
        return false;
    }

    /// <summary>
    /// Odkud v paletě začít vybírat. Čím blíž středu, tím spíš z její honosnější
    /// druhé půlky.
    /// </summary>
    private static int PickFromPalette(int count, int centrality, ulong roll)
    {
        if (count < 2)
        {
            return 0;
        }

        int half = count / 2;
        bool grand = (int)((roll >> 20) % 100) < centrality;

        return grand
            ? half + (int)((roll >> 32) % (ulong)(count - half))
            : (int)((roll >> 32) % (ulong)Math.Max(1, half));
    }

    private static void AddRoad(
        List<RoadTile> roads, HashSet<long> taken, Func<int, int, bool> canPave, int x, int y)
    {
        if (canPave(x, y) && taken.Add(TileKey.Pack(x, y)))
        {
            roads.Add(new RoadTile(x, y));
        }
    }

    private static bool IsFree(HashSet<long> taken, int x, int y, int width, int height)
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

        return true;
    }

    private static void Reserve(HashSet<long> taken, int x, int y, int width, int height)
    {
        for (int dy = 0; dy < height; dy++)
        {
            for (int dx = 0; dx < width; dx++)
            {
                taken.Add(TileKey.Pack(x + dx, y + dy));
            }
        }
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
