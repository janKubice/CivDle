using CivDle.Core.Content;
using CivDle.Core.World;

namespace CivDle.Core.Sim;

/// <summary>
/// Auto-silnice (fáze 4: „nová budova se sama napojí cestou"). Po položení budovy
/// najde BFS nejkratší cestu z jejího obvodu k nejbližší silnici nebo k obvodu
/// jiné budovy a dlaždice cesty označí jako silnici. Cesty jsou zdarma a plně
/// automatické — relaxační jádro, hráč je neřeší.
///
/// Nekonečná mapa: BFS pracuje nad řídkými mapami (Dictionary/HashSet) v okně
/// omezeném <c>maxSearchDistance</c>, ne nad plochým polem celého světa. Pevné
/// pořadí sousedů drží determinismus; existující síť je cílem hledání, takže se
/// cesty napojují místo duplikování. Budovy jsou neprůchozí.
///
/// <para><b>Mosty:</b> voda je průchozí jen v souvislém úseku do
/// <c>roads.maxBridgeSpan</c> dlaždic — cesta tak překlene řeku nebo úžinu, ale
/// nepostaví most přes oceán. Most se nikde neukládá: silniční dlaždice na vodě
/// JE most (odvozené z terénu), takže save formát zůstává beze změny.</para>
/// </summary>
internal sealed class RoadBuilder
{
    private readonly GameContent _content;
    private readonly HashSet<long> _targets = new();
    private readonly HashSet<long> _visited = new();
    private readonly Dictionary<long, long> _cameFrom = new();
    private readonly Queue<long> _queue = new();

    /// <summary>Kolik vodních dlaždic v řadě už cesta k danému místu překlenula (délka mostu).</summary>
    private readonly Dictionary<long, int> _waterRun = new();

    /// <summary>
    /// Vyhýbá se hledání dlažebním „plackám" 2×2? V prvním pokusu ano — z toho
    /// vznikají ulice místo vyasfaltovaných ploch. Když se ale kvůli tomu cesta
    /// nenajde, druhý pokus pravidlo pustí: napojení je důležitější než vzhled.
    /// </summary>
    private bool _avoidBlocks = true;

    public RoadBuilder(GameContent content)
    {
        _content = content;
    }

    /// <summary>Napojí právě postavenou budovu (poslední v poli) na síť. První budova se nenapojuje.</summary>
    public void ConnectLastBuilding(Simulation sim)
    {
        var buildings = sim.Buildings;
        if (buildings.Length <= 1)
        {
            return;
        }

        // Dřív se dláždilo po každé stavbě, takže i mezi dvěma sousedními domy
        // vyrostl kus cesty — město pak vypadalo jako šachovnice a nešlo postavit
        // ani blok 2×2 (dlaždice pro čtvrtý dům byla zabraná silnicí). Cesta se
        // proto staví jen tam, kde opravdu chybí:
        int last = buildings.Length - 1;

        // 1) Budova přiléhá k jiné → patří do stejného bloku a mezi domy v bloku
        //    se nedláždí; k silnici se dostane přes svůj blok.
        if (TouchesAnotherBuilding(sim, buildings[last], last))
        {
            return;
        }

        // 2) Blok už se silnice dotýká (podmínka roads.Count > 0 kvůli tomu, že
        //    bez jediné silnice platí výjimka „všichni jsou napojení").
        if (sim.RoadTiles.Count > 0 && sim.IsBuildingConnected(last))
        {
            return;
        }

        ResetSearch(sim, buildings);

        _avoidBlocks = true;
        long found = Search(sim);
        if (found == -1)
        {
            // Druhý pokus bez estetického pravidla — nenapojená budova vyrábí
            // hůř, a to je horší než ošklivý kus dlažby.
            ResetSearch(sim, buildings);
            _avoidBlocks = false;
            found = Search(sim);
        }

        if (found == -1)
        {
            return; // příliš daleko nebo bez suchozemské cesty — nechá se bez napojení
        }

        for (long key = found; key != -1; key = _cameFrom[key])
        {
            sim.AddRoadTile(TileKey.X(key), TileKey.Y(key));
        }
    }

    /// <summary>Připraví hledání cesty: cíle (silnice a starší budovy) a starty (nová budova).</summary>
    private void ResetSearch(Simulation sim, ReadOnlySpan<BuildingInstance> buildings)
    {
        _targets.Clear();
        _visited.Clear();
        _cameFrom.Clear();
        _queue.Clear();
        _waterRun.Clear();

        foreach (var road in sim.RoadTiles)
        {
            _targets.Add(TileKey.Pack(road.X, road.Y));
        }

        for (int i = 0; i < buildings.Length - 1; i++)
        {
            MarkPerimeter(sim, buildings[i], key => _targets.Add(key));
        }

        MarkPerimeter(sim, buildings[^1], key =>
        {
            if (_visited.Add(key))
            {
                _cameFrom[key] = -1;
                _waterRun[key] = 0; // obvod budovy je vždy na souši
                _queue.Enqueue(key);
            }
        });
    }

    /// <summary>Dotýká se půdorys budovy hranou jiné budovy? (Roh se nepočítá.)</summary>
    private bool TouchesAnotherBuilding(Simulation sim, in BuildingInstance building, int ownIndex)
    {
        var def = _content.Buildings[building.DefIndex];
        for (int x = building.X; x < building.X + def.FootprintWidth; x++)
        {
            if (IsOtherBuilding(sim, x, building.Y - 1, ownIndex)
                || IsOtherBuilding(sim, x, building.Y + def.FootprintHeight, ownIndex))
            {
                return true;
            }
        }

        for (int y = building.Y; y < building.Y + def.FootprintHeight; y++)
        {
            if (IsOtherBuilding(sim, building.X - 1, y, ownIndex)
                || IsOtherBuilding(sim, building.X + def.FootprintWidth, y, ownIndex))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsOtherBuilding(Simulation sim, int x, int y, int ownIndex) =>
        sim.TryGetBuildingAt(x, y, out int index) && index != ownIndex;

    /// <summary>BFS s limitem vzdálenosti; vrací klíč nalezené cílové dlaždice, jinak −1.</summary>
    private long Search(Simulation sim)
    {
        int maxDistance = _content.Gameplay.Roads.MaxSearchDistance;

        // Start může být rovnou cílem (budova u silnice/souseda) → cesta je jedna dlaždice.
        foreach (long key in _queue)
        {
            if (_targets.Contains(key))
            {
                return key;
            }
        }

        int frontier = _queue.Count;
        int depth = 0;
        while (_queue.Count > 0)
        {
            if (frontier == 0)
            {
                frontier = _queue.Count;
                if (++depth >= maxDistance)
                {
                    break;
                }
            }

            long current = _queue.Dequeue();
            frontier--;

            int x = TileKey.X(current);
            int y = TileKey.Y(current);

            // Rovně napřed: BFS s pevným pořadím sousedů dělal ze stejně dlouhých
            // cest klikaté schodiště. Když se nejdřív zkusí směr, kterým se sem
            // došlo, vyjde z toho ulice s pár zatáčkami — stejně dlouhá, ale
            // vypadá jako cesta, ne jako rozsypaný čaj. Pořadí zůstává pevné,
            // takže síť je pořád deterministická.
            if (VisitStraightFirst(sim, x, y, current, out long hit))
            {
                return hit;
            }
        }

        return -1;
    }

    /// <summary>
    /// Projde čtyři sousedy, ale ten ve směru dosavadní jízdy vezme jako první.
    /// Zbylé tři pak v pevném pořadí, aby síť zůstala deterministická.
    /// </summary>
    private bool VisitStraightFirst(Simulation sim, int x, int y, long current, out long hit)
    {
        long previous = _cameFrom.GetValueOrDefault(current, -1);
        int dx = 0, dy = 0;
        if (previous != -1)
        {
            dx = Math.Sign(x - TileKey.X(previous));
            dy = Math.Sign(y - TileKey.Y(previous));
        }

        if ((dx != 0 || dy != 0) && Visit(sim, x + dx, y + dy, current, out hit))
        {
            return true;
        }

        if (!(dx == 1 && dy == 0) && Visit(sim, x + 1, y, current, out hit)) return true;
        if (!(dx == -1 && dy == 0) && Visit(sim, x - 1, y, current, out hit)) return true;
        if (!(dx == 0 && dy == 1) && Visit(sim, x, y + 1, current, out hit)) return true;
        if (!(dx == 0 && dy == -1) && Visit(sim, x, y - 1, current, out hit)) return true;

        hit = -1;
        return false;
    }

    private bool Visit(Simulation sim, int x, int y, long from, out long foundTarget)
    {
        foundTarget = -1;
        long key = TileKey.Pack(x, y);
        if (_visited.Contains(key) || sim.IsOccupied(x, y))
        {
            return false;
        }

        // Dlažební placky 2×2 dělají z ulic parkoviště. Kontroluje se každá
        // dlaždice, kterou bychom POLOŽILI — tedy i cíl, pokud to zatím silnice
        // není (obvod starší budovy). Na existující silnici se jen napojujeme,
        // ta se neklade a nic nezhorší.
        if (_avoidBlocks && !sim.IsRoad(x, y) && WouldFormRoadBlock(sim, x, y, from))
        {
            return false;
        }

        // Přes vodu se smí jen po most dlouhý nejvýš maxBridgeSpan dlaždic. Délka
        // se počítá po cestě: souš ji nuluje, další voda ji prodlužuje.
        bool water = IsWater(sim, x, y);
        int run = water ? _waterRun.GetValueOrDefault(from) + 1 : 0;
        if (water && run > _content.Gameplay.Roads.MaxBridgeSpan)
        {
            return false; // širší tok, než umíme přemostit (oceán zůstane bariérou)
        }

        _visited.Add(key);
        _cameFrom[key] = from;
        _waterRun[key] = run;

        if (_targets.Contains(key))
        {
            foundTarget = key;
            return true;
        }

        _queue.Enqueue(key);
        return false;
    }

    /// <summary>
    /// Vznikla by položením dlaždice souvislá plocha silnice 2×2? Kontrolují se
    /// čtyři čtverce, jejichž rohem tahle dlaždice je.
    ///
    /// <para>Za silnici se počítají i dlaždice cesty, kterou zrovna hledáme —
    /// ty ještě položené nejsou, ale budou. Stačí se dívat pár kroků zpět: do
    /// čtverce 2×2 se z jedné cesty nevejdou vzdálenější dlaždice.</para>
    /// </summary>
    private bool WouldFormRoadBlock(Simulation sim, int x, int y, long from)
    {
        Span<long> pending = stackalloc long[PendingLookback + 1];
        pending[0] = TileKey.Pack(x, y);
        int count = 1;
        for (long step = from; step != -1 && count <= PendingLookback; step = _cameFrom.GetValueOrDefault(step, -1))
        {
            pending[count++] = step;
        }

        for (int cornerY = -1; cornerY <= 0; cornerY++)
        {
            for (int cornerX = -1; cornerX <= 0; cornerX++)
            {
                if (IsRoadOrPending(sim, x + cornerX, y + cornerY, pending[..count])
                    && IsRoadOrPending(sim, x + cornerX + 1, y + cornerY, pending[..count])
                    && IsRoadOrPending(sim, x + cornerX, y + cornerY + 1, pending[..count])
                    && IsRoadOrPending(sim, x + cornerX + 1, y + cornerY + 1, pending[..count]))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Kolik dlaždic zpět po rozpracované cestě se počítá za „už silnici".
    /// Tři stačí: čtvrtý roh čtverce 2×2 je od prvního nejvýš tři kroky daleko.
    /// </summary>
    private const int PendingLookback = 3;

    private static bool IsRoadOrPending(Simulation sim, int x, int y, ReadOnlySpan<long> pending)
    {
        long key = TileKey.Pack(x, y);
        foreach (long tile in pending)
        {
            if (tile == key)
            {
                return true;
            }
        }

        return sim.IsRoad(x, y);
    }

    /// <summary>Suchá průchozí dlaždice — start i konec cesty musí stát na souši.</summary>
    private bool IsPassable(Simulation sim, int x, int y) =>
        !sim.IsOccupied(x, y) && !IsWater(sim, x, y);

    private bool IsWater(Simulation sim, int x, int y) =>
        _content.Biomes[sim.Terrain.BiomeAt(x, y)].IsWater;

    /// <summary>Zavolá akci pro každou průchozí dlaždici po obvodu půdorysu budovy.</summary>
    private void MarkPerimeter(Simulation sim, in BuildingInstance building, Action<long> action)
    {
        var def = _content.Buildings[building.DefIndex];

        for (int x = building.X; x < building.X + def.FootprintWidth; x++)
        {
            TryMark(sim, x, building.Y - 1, action);
            TryMark(sim, x, building.Y + def.FootprintHeight, action);
        }

        for (int y = building.Y; y < building.Y + def.FootprintHeight; y++)
        {
            TryMark(sim, building.X - 1, y, action);
            TryMark(sim, building.X + def.FootprintWidth, y, action);
        }
    }

    private void TryMark(Simulation sim, int x, int y, Action<long> action)
    {
        if (IsPassable(sim, x, y))
        {
            action(TileKey.Pack(x, y));
        }
    }
}
