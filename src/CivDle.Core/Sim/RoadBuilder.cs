using CivDle.Core.Content;

namespace CivDle.Core.Sim;

/// <summary>
/// Auto-silnice (fáze 4: „nová budova se sama napojí cestou"). Po položení budovy
/// najde BFS nejkratší cestu z jejího obvodu k nejbližší silnici nebo k obvodu
/// jiné budovy a dlaždice cesty označí jako silnici. Cesty jsou zdarma a plně
/// automatické — relaxační jádro, hráč je neřeší.
///
/// BFS s pevným pořadím sousedů je deterministické; existující síť je cílem
/// hledání, takže se cesty přirozeně napojují místo duplikování. Voda a budovy
/// jsou neprůchozí (mosty zatím nejsou). Pomocná pole se recyklují přes
/// generační razítka — žádné čištění 250k položek při každé stavbě.
/// </summary>
internal sealed class RoadBuilder
{
    private readonly GameContent _content;

    private int[] _visitedStamp = Array.Empty<int>();
    private int[] _targetStamp = Array.Empty<int>();
    private int[] _cameFrom = Array.Empty<int>();
    private int _stamp;
    private readonly Queue<int> _queue = new();

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

        var map = sim.Map;
        EnsureBuffers(map.Width * map.Height);
        _stamp++;
        _queue.Clear();

        // Cíle: existující silnice + průchozí obvod všech starších budov.
        var roadTiles = sim.RoadTiles;
        for (int i = 0; i < roadTiles.Count; i++)
        {
            _targetStamp[roadTiles[i]] = _stamp;
        }

        for (int i = 0; i < buildings.Length - 1; i++)
        {
            MarkPerimeter(sim, buildings[i], tileIndex => _targetStamp[tileIndex] = _stamp);
        }

        // Starty: průchozí obvod nové budovy.
        ref readonly var newBuilding = ref buildings[^1];
        MarkPerimeter(sim, newBuilding, tileIndex =>
        {
            if (_visitedStamp[tileIndex] != _stamp)
            {
                _visitedStamp[tileIndex] = _stamp;
                _cameFrom[tileIndex] = -1;
                _queue.Enqueue(tileIndex);
            }
        });

        int found = Search(sim);
        if (found < 0)
        {
            return; // příliš daleko nebo bez suchozemské cesty — nechá se bez napojení
        }

        for (int tileIndex = found; tileIndex >= 0; tileIndex = _cameFrom[tileIndex])
        {
            sim.AddRoadTile(tileIndex);
        }
    }

    /// <summary>BFS s limitem vzdálenosti; vrací index nalezené cílové dlaždice, jinak −1.</summary>
    private int Search(Simulation sim)
    {
        var map = sim.Map;
        int maxDistance = _content.Gameplay.Roads.MaxSearchDistance;
        int frontier = _queue.Count;
        int depth = 0;

        // Start může být rovnou cílem (budova u silnice/souseda) → cesta je jedna dlaždice.
        foreach (int tileIndex in _queue)
        {
            if (_targetStamp[tileIndex] == _stamp)
            {
                return tileIndex;
            }
        }

        while (_queue.Count > 0 && depth < maxDistance)
        {
            if (frontier == 0)
            {
                frontier = _queue.Count;
                depth++;
                if (depth >= maxDistance)
                {
                    break;
                }
            }

            int current = _queue.Dequeue();
            frontier--;

            int x = current % map.Width;
            int y = current / map.Width;
            // Pevné pořadí sousedů → deterministický tvar sítě.
            if (Visit(sim, x + 1, y, current, out int hit)) return hit;
            if (Visit(sim, x - 1, y, current, out hit)) return hit;
            if (Visit(sim, x, y + 1, current, out hit)) return hit;
            if (Visit(sim, x, y - 1, current, out hit)) return hit;
        }

        return -1;
    }

    private bool Visit(Simulation sim, int x, int y, int from, out int foundTarget)
    {
        foundTarget = -1;
        var map = sim.Map;
        if (!map.InBounds(x, y))
        {
            return false;
        }

        int tileIndex = map.Index(x, y);
        if (_visitedStamp[tileIndex] == _stamp || !IsPassable(sim, tileIndex))
        {
            return false;
        }

        _visitedStamp[tileIndex] = _stamp;
        _cameFrom[tileIndex] = from;

        if (_targetStamp[tileIndex] == _stamp)
        {
            foundTarget = tileIndex;
            return true;
        }

        _queue.Enqueue(tileIndex);
        return false;
    }

    private bool IsPassable(Simulation sim, int tileIndex)
    {
        if (sim.OccupancyGrid[tileIndex] != 0)
        {
            return false;
        }

        return !_content.Biomes[sim.Map.BiomeIndices[tileIndex]].IsWater;
    }

    /// <summary>Zavolá akci pro každou průchozí dlaždici po obvodu půdorysu budovy.</summary>
    private void MarkPerimeter(Simulation sim, in BuildingInstance building, Action<int> action)
    {
        var def = _content.Buildings[building.DefIndex];
        var map = sim.Map;

        for (int x = building.X; x < building.X + def.FootprintWidth; x++)
        {
            TryMark(sim, map, x, building.Y - 1, action);
            TryMark(sim, map, x, building.Y + def.FootprintHeight, action);
        }

        for (int y = building.Y; y < building.Y + def.FootprintHeight; y++)
        {
            TryMark(sim, map, building.X - 1, y, action);
            TryMark(sim, map, building.X + def.FootprintWidth, y, action);
        }
    }

    private void TryMark(Simulation sim, World.WorldMap map, int x, int y, Action<int> action)
    {
        if (map.InBounds(x, y) && IsPassable(sim, map.Index(x, y)))
        {
            action(map.Index(x, y));
        }
    }

    private void EnsureBuffers(int tileCount)
    {
        if (_visitedStamp.Length < tileCount)
        {
            _visitedStamp = new int[tileCount];
            _targetStamp = new int[tileCount];
            _cameFrom = new int[tileCount];
        }
    }
}
