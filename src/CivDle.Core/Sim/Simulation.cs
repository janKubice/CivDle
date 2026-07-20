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
    private readonly double[] _storageCaps;
    private readonly int[] _occupancy; // 0 = volno, jinak index budovy + 1
    private readonly bool[] _roads;
    private readonly List<int> _roadTiles = new(); // pořadí vzniku — deterministické, jde do savu
    private readonly List<Settlement> _settlements = new();
    private readonly ProductionSystem _production;
    private readonly PopulationSystem _populationSystem;
    private readonly AutoBuildSystem _autoBuild;
    private readonly RoadBuilder _roadBuilder;
    private readonly SettlementSystem _settlementSystem;

    private BuildingInstance[] _buildings = new BuildingInstance[16];
    private int _buildingCount;

    /// <param name="seed">Seed světa — řídí deterministickou „náhodu" simulace (auto-stavba).</param>
    public Simulation(GameContent content, WorldMap map, long seed = 0)
    {
        _content = content;
        Map = map;

        _resources = new double[content.Resources.Count];
        _storageCaps = new double[content.Resources.Count];
        for (int i = 0; i < _resources.Length; i++)
        {
            _storageCaps[i] = content.Resources[i].BaseStorage;
            _resources[i] = Math.Min(content.Resources[i].StartAmount, _storageCaps[i]);
        }

        _occupancy = new int[map.Width * map.Height];
        _roads = new bool[map.Width * map.Height];
        Population = content.Gameplay.StartingPopulation;
        HousingCapacity = content.Gameplay.BaseHousingCapacity;

        _production = new ProductionSystem(content);
        _populationSystem = new PopulationSystem(content.Gameplay);
        _autoBuild = new AutoBuildSystem(content, seed);
        _roadBuilder = new RoadBuilder(content);
        _settlementSystem = new SettlementSystem(content, seed);
    }

    /// <summary>Mapa světa, nad kterou simulace běží.</summary>
    public WorldMap Map { get; }

    /// <summary>Počet proběhlých tiků od startu hry (internal set kvůli načtení uložené hry).</summary>
    public long TickCount { get; internal set; }

    /// <summary>Populace jako agregát (viz tech-stack.md — milion lidí je jen číslo).</summary>
    public double Population { get; internal set; }

    /// <summary>Kolik lidí se vejde (základní tábor + domy).</summary>
    public int HousingCapacity { get; private set; }

    /// <summary>Součet pracovních míst výrobních budov — obsazenost škáluje výrobu.</summary>
    public int TotalWorkerSlots { get; private set; }

    /// <summary>Postavené budovy (jen ke čtení; render z nich kreslí).</summary>
    public ReadOnlySpan<BuildingInstance> Buildings => _buildings.AsSpan(0, _buildingCount);

    /// <summary>Indexy dlaždic se silnicí v pořadí vzniku (render + save).</summary>
    public IReadOnlyList<int> RoadTiles => _roadTiles;

    /// <summary>Rozpoznané osady (odvozený stav, přepočítává <c>SettlementSystem</c>).</summary>
    public IReadOnlyList<Settlement> Settlements => _settlements;

    /// <summary>Je na dlaždici silnice?</summary>
    public bool IsRoad(int x, int y) => _roads[Map.Index(x, y)];

    /// <summary>Stojí na dlaždici budova?</summary>
    public bool IsOccupied(int x, int y) => _occupancy[Map.Index(x, y)] != 0;

    /// <summary>Aktuální zásoba suroviny.</summary>
    public double GetResource(int resourceIndex) => _resources[resourceIndex];

    /// <summary>Kapacita skladu suroviny (základ + skladové budovy).</summary>
    public double GetStorageCap(int resourceIndex) => _storageCaps[resourceIndex];

    /// <summary>Zásoby pro systémy simulace (mutace jen uvnitř assembly).</summary>
    internal double[] Resources => _resources;

    /// <summary>Kapacity skladů pro systémy simulace.</summary>
    internal double[] StorageCaps => _storageCaps;

    /// <summary>Obsah, nad kterým simulace běží — pro serializaci savu (v rámci assembly).</summary>
    internal GameContent ContentRef => _content;

    /// <summary>Occupancy grid pro systémy (RoadBuilder) — jen čtení.</summary>
    internal int[] OccupancyGrid => _occupancy;

    /// <summary>Osady k přepsání systémem detekce.</summary>
    internal List<Settlement> SettlementsMutable => _settlements;

    /// <summary>Zástavba se změnila — osady čekají na přepočet.</summary>
    internal bool SettlementsDirty { get; set; }

    /// <summary>Označí dlaždici jako silnici (RoadBuilder, načtení savu). Duplicitní volání je no-op.</summary>
    internal void AddRoadTile(int tileIndex)
    {
        if (!_roads[tileIndex])
        {
            _roads[tileIndex] = true;
            _roadTiles.Add(tileIndex);
        }
    }

    /// <summary>Budovy pro systémy simulace (mutace progressu výroby).</summary>
    internal Span<BuildingInstance> BuildingsMutable => _buildings.AsSpan(0, _buildingCount);

    /// <summary>Jeden krok simulace. Deterministický — žádná náhoda bez seedu.</summary>
    public void Tick()
    {
        TickCount++;
        _production.Tick(this);
        _populationSystem.Tick(this);
        _autoBuild.Tick(this);
        _settlementSystem.Tick(this);
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
                if (_occupancy[index] != 0 || _roads[index])
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

        ApplyBuildingBonuses(def);
        _roadBuilder.ConnectLastBuilding(this);
        SettlementsDirty = true;
        return PlacementResult.Ok;
    }

    /// <summary>
    /// Příkaz hráče: ruční sběr kliknutím na dlaždici („klik na strom → dřevo").
    /// Výnos určuje biom (<see cref="ClickYield"/> z dat); zastavěná dlaždice
    /// ani plný sklad nedávají nic.
    /// </summary>
    public bool TryHarvest(int x, int y, out int resourceIndex, out int amount)
    {
        resourceIndex = 0;
        amount = 0;

        if (!Map.InBounds(x, y))
        {
            return false;
        }

        int index = Map.Index(x, y);
        if (_occupancy[index] != 0)
        {
            return false;
        }

        var yield = _content.Biomes[Map.BiomeIndices[index]].ClickYield;
        if (yield is null)
        {
            return false;
        }

        // Plný sklad = žádný sběr (a žádný lživý „+2" popup v UI).
        if (_resources[yield.ResourceIndex] + yield.Amount > _storageCaps[yield.ResourceIndex])
        {
            return false;
        }

        _resources[yield.ResourceIndex] += yield.Amount;
        resourceIndex = yield.ResourceIndex;
        amount = yield.Amount;
        return true;
    }

    // ----- obnova ze savu (jen pro SaveGameSerializer, obchází cenu a validaci biomů) -----

    /// <summary>Nastaví globální stav načtený ze savu (zásoby se přiškrtí na aktuální kapacity).</summary>
    internal void RestoreState(double[] resourceAmounts, double population, long tickCount)
    {
        for (int i = 0; i < _resources.Length; i++)
        {
            _resources[i] = Math.Min(resourceAmounts[i], _storageCaps[i]);
        }

        Population = population;
        TickCount = tickCount;
    }

    /// <summary>
    /// Přidá budovu ze savu bez ceny a kontroly biomu — data se od uložení mohla
    /// změnit a už postavené budovy hráči nemažeme. Meze mapy se kontrolují,
    /// aby poškozený save neshodil hru indexem mimo pole.
    /// </summary>
    internal void RestoreBuilding(int defIndex, int x, int y, float progress)
    {
        var def = _content.Buildings[defIndex];
        if (x < 0 || y < 0 || x + def.FootprintWidth > Map.Width || y + def.FootprintHeight > Map.Height)
        {
            throw new ArgumentOutOfRangeException(nameof(x), $"Budova '{def.Id}' na [{x}, {y}] leží mimo mapu.");
        }

        if (_buildingCount == _buildings.Length)
        {
            Array.Resize(ref _buildings, _buildings.Length * 2);
        }

        _buildings[_buildingCount] = new BuildingInstance { DefIndex = defIndex, X = x, Y = y, Progress = progress };
        _buildingCount++;

        for (int tileY = y; tileY < y + def.FootprintHeight; tileY++)
        {
            for (int tileX = x; tileX < x + def.FootprintWidth; tileX++)
            {
                _occupancy[Map.Index(tileX, tileY)] = _buildingCount;
            }
        }

        ApplyBuildingBonuses(def);
        SettlementsDirty = true; // silnice ze savu chodí zvlášť, přepočet osad ale spustit musíme
    }

    /// <summary>Globální bonusy budovy: bydlení, pracovní místa, kapacita skladů.</summary>
    private void ApplyBuildingBonuses(BuildingDef def)
    {
        HousingCapacity += def.HousingCapacity;
        TotalWorkerSlots += def.WorkerSlots;
        for (int i = 0; i < def.StorageBonus.Count; i++)
        {
            _storageCaps[def.StorageBonus[i].ResourceIndex] += def.StorageBonus[i].Amount;
        }
    }
}
