using CivDle.Core.Content;
using CivDle.Core.World;

namespace CivDle.Core.Sim;

/// <summary>
/// Stav a tik simulace: mapa, zásoby surovin (pole podle indexu suroviny),
/// budovy (struktury v plochém poli), populace jako agregátní číslo.
/// Tik orchestruje systémy (výroba, populace); příkazy hráče vstupují přes
/// veřejné metody (<see cref="TryPlaceBuilding"/>) — render stav jen čte.
/// Deterministické: žádná náhoda, žádné alokace v tikové smyčce.
/// </summary>
public sealed class Simulation
{
    /// <summary>Frekvence simulace dle tech-stack.md (10–20 Hz stačí, render běží vlastním tempem).</summary>
    public const double TicksPerSecond = 10.0;

    private readonly GameContent _content;
    private readonly double[] _resources;
    private readonly int[] _occupancy; // 0 = volno, jinak index budovy + 1
    private readonly ProductionSystem _production;
    private readonly PopulationSystem _populationSystem;

    private BuildingInstance[] _buildings = new BuildingInstance[16];
    private int _buildingCount;

    public Simulation(GameContent content, WorldMap map)
    {
        _content = content;
        Map = map;

        _resources = new double[content.Resources.Count];
        for (int i = 0; i < _resources.Length; i++)
        {
            _resources[i] = content.Resources[i].StartAmount;
        }

        _occupancy = new int[map.Width * map.Height];
        Population = content.Gameplay.StartingPopulation;
        HousingCapacity = content.Gameplay.BaseHousingCapacity;

        _production = new ProductionSystem(content);
        _populationSystem = new PopulationSystem(content.Gameplay);
    }

    /// <summary>Mapa světa, nad kterou simulace běží.</summary>
    public WorldMap Map { get; }

    /// <summary>Počet proběhlých tiků od startu hry.</summary>
    public long TickCount { get; private set; }

    /// <summary>Populace jako agregát (viz tech-stack.md — milion lidí je jen číslo).</summary>
    public double Population { get; internal set; }

    /// <summary>Kolik lidí se vejde (základní tábor + domy).</summary>
    public int HousingCapacity { get; private set; }

    /// <summary>Součet pracovních míst výrobních budov — obsazenost škáluje výrobu.</summary>
    public int TotalWorkerSlots { get; private set; }

    /// <summary>Postavené budovy (jen ke čtení; render z nich kreslí).</summary>
    public ReadOnlySpan<BuildingInstance> Buildings => _buildings.AsSpan(0, _buildingCount);

    /// <summary>Aktuální zásoba suroviny.</summary>
    public double GetResource(int resourceIndex) => _resources[resourceIndex];

    /// <summary>Zásoby pro systémy simulace (mutace jen uvnitř assembly).</summary>
    internal double[] Resources => _resources;

    /// <summary>Budovy pro systémy simulace (mutace progressu výroby).</summary>
    internal Span<BuildingInstance> BuildingsMutable => _buildings.AsSpan(0, _buildingCount);

    /// <summary>Jeden krok simulace. Deterministický — žádná náhoda bez seedu.</summary>
    public void Tick()
    {
        TickCount++;
        _production.Tick(this);
        _populationSystem.Tick(this);
    }

    /// <summary>
    /// Ověří umístění budovy bez vedlejších efektů — UI z výsledku ukazuje ghost
    /// a lokalizovanou hlášku, proč stavět nejde.
    /// </summary>
    public PlacementResult CanPlace(int defIndex, int x, int y)
    {
        var def = _content.Buildings[defIndex];
        if (x < 0 || y < 0 || x + def.FootprintWidth > Map.Width || y + def.FootprintHeight > Map.Height)
        {
            return PlacementResult.OutOfBounds;
        }

        for (int tileY = y; tileY < y + def.FootprintHeight; tileY++)
        {
            for (int tileX = x; tileX < x + def.FootprintWidth; tileX++)
            {
                int index = Map.Index(tileX, tileY);
                if (_occupancy[index] != 0)
                {
                    return PlacementResult.Occupied;
                }

                if (!def.IsBiomeAllowed(Map.BiomeIndices[index]))
                {
                    return PlacementResult.WrongBiome;
                }
            }
        }

        var cost = def.BuildCost;
        for (int i = 0; i < cost.Count; i++)
        {
            if (_resources[cost[i].ResourceIndex] < cost[i].Amount)
            {
                return PlacementResult.NotEnoughResources;
            }
        }

        return PlacementResult.Ok;
    }

    /// <summary>Příkaz hráče: postavit budovu. Odečte cenu a obsadí dlaždice.</summary>
    public PlacementResult TryPlaceBuilding(int defIndex, int x, int y)
    {
        var result = CanPlace(defIndex, x, y);
        if (result != PlacementResult.Ok)
        {
            return result;
        }

        var def = _content.Buildings[defIndex];
        var cost = def.BuildCost;
        for (int i = 0; i < cost.Count; i++)
        {
            _resources[cost[i].ResourceIndex] -= cost[i].Amount;
        }

        if (_buildingCount == _buildings.Length)
        {
            Array.Resize(ref _buildings, _buildings.Length * 2);
        }

        _buildings[_buildingCount] = new BuildingInstance { DefIndex = defIndex, X = x, Y = y };
        _buildingCount++;

        for (int tileY = y; tileY < y + def.FootprintHeight; tileY++)
        {
            for (int tileX = x; tileX < x + def.FootprintWidth; tileX++)
            {
                _occupancy[Map.Index(tileX, tileY)] = _buildingCount;
            }
        }

        HousingCapacity += def.HousingCapacity;
        TotalWorkerSlots += def.WorkerSlots;
        return PlacementResult.Ok;
    }
}
