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
/// cesty napojují místo duplikování. Voda a budovy jsou neprůchozí (mosty zatím ne).
/// </summary>
internal sealed class RoadBuilder
{
    private readonly GameContent _content;
    private readonly HashSet<long> _targets = new();
    private readonly HashSet<long> _visited = new();
    private readonly Dictionary<long, long> _cameFrom = new();
    private readonly Queue<long> _queue = new();

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

        _targets.Clear();
        _visited.Clear();
        _cameFrom.Clear();
        _queue.Clear();

        // Cíle: existující silnice + průchozí obvod všech starších budov.
        foreach (var road in sim.RoadTiles)
        {
            _targets.Add(TileKey.Pack(road.X, road.Y));
        }

        for (int i = 0; i < buildings.Length - 1; i++)
        {
            MarkPerimeter(sim, buildings[i], key => _targets.Add(key));
        }

        // Starty: průchozí obvod nové budovy.
        MarkPerimeter(sim, buildings[^1], key =>
        {
            if (_visited.Add(key))
            {
                _cameFrom[key] = -1;
                _queue.Enqueue(key);
            }
        });

        long found = Search(sim);
        if (found == -1)
        {
            return; // příliš daleko nebo bez suchozemské cesty — nechá se bez napojení
        }

        for (long key = found; key != -1; key = _cameFrom[key])
        {
            sim.AddRoadTile(TileKey.X(key), TileKey.Y(key));
        }
    }

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
            // Pevné pořadí sousedů → deterministický tvar sítě.
            if (Visit(sim, x + 1, y, current, out long hit)) return hit;
            if (Visit(sim, x - 1, y, current, out hit)) return hit;
            if (Visit(sim, x, y + 1, current, out hit)) return hit;
            if (Visit(sim, x, y - 1, current, out hit)) return hit;
        }

        return -1;
    }

    private bool Visit(Simulation sim, int x, int y, long from, out long foundTarget)
    {
        foundTarget = -1;
        long key = TileKey.Pack(x, y);
        if (_visited.Contains(key) || !IsPassable(sim, x, y))
        {
            return false;
        }

        _visited.Add(key);
        _cameFrom[key] = from;

        if (_targets.Contains(key))
        {
            foundTarget = key;
            return true;
        }

        _queue.Enqueue(key);
        return false;
    }

    private bool IsPassable(Simulation sim, int x, int y)
    {
        if (sim.IsOccupied(x, y))
        {
            return false;
        }

        return !_content.Biomes[sim.Terrain.BiomeAt(x, y)].IsWater;
    }

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
