using CivDle.Core.Content;
using CivDle.Core.World;

namespace CivDle.Core.Sim;

/// <summary>Dlaždice se silnicí (na nekonečné mapě už není „index", ale souřadnice).</summary>
public readonly record struct RoadTile(int X, int Y);

/// <summary>
/// Stav a tik simulace nad NEKONEČNÝM terénem: terén je čistá funkce (nic se
/// neukládá), zastavěné a cestami pokryté dlaždice jsou řídké (hashované mřížky),
/// budovy jsou struktury v plochém poli, populace agregátní číslo.
/// Tik orchestruje systémy; příkazy hráče vstupují přes veřejné metody —
/// render stav jen čte. Deterministické: žádná náhoda, žádné alokace za tik.
/// </summary>
public sealed class Simulation
{
    /// <summary>Frekvence simulace dle tech-stack.md (10–20 Hz stačí, render běží vlastním tempem).</summary>
    public const double TicksPerSecond = 10.0;

    private readonly GameContent _content;
    private readonly double[] _resources;
    private readonly double[] _storageCaps;
    private readonly Dictionary<long, int> _occupancy = new(); // klíč dlaždice → index budovy + 1
    private readonly HashSet<long> _roads = new();
    private readonly List<RoadTile> _roadTiles = new(); // pořadí vzniku — deterministické, jde do savu
    private readonly List<Settlement> _settlements = new();
    private readonly ProductionSystem _production;
    private readonly PopulationSystem _populationSystem;
    private readonly AutoBuildSystem _autoBuild;
    private readonly ZoneFillSystem _zoneFill;
    private readonly ColonySystem _colonySystem;
    private readonly List<Zone> _zones = new(); // hráčem namalované zóny (automatizace, stupeň 3)
    private readonly RoadBuilder _roadBuilder;
    private readonly SettlementSystem _settlementSystem;
    private readonly QuestSystem _questSystem;
    private readonly bool[] _questsCompleted;
    private readonly AchievementSystem _achievementSystem;
    private readonly bool[] _achievementsUnlocked;

    private readonly bool[] _buildingUnlocked;
    private readonly bool[] _techResearched;
    private readonly bool[] _upgradesPurchased; // koupené trvalé upgrady Vzestupu
    private readonly bool[] _policiesActive;    // zapnuté politiky růstu (automatizace, stupeň 4)
    private readonly long[] _harvestedTotals; // kumulativní sběr surovin klikáním (metriky cílů)
    private readonly HashSet<long> _claimedDiscoveries = new(); // vyzvednuté skrýše na mapě
    private readonly Dictionary<long, ClickYield> _plantedNodes = new(); // hráčem zasazené obnovitelné zdroje
    private readonly Queue<GameNotification> _notifications = new();
    private PrestigeBonuses _bonuses = PrestigeBonuses.None;

    private int _boostTicksRemaining;    // slavnost aktivní, dokud > 0
    private int _boostCooldownRemaining;  // dokud > 0, nejde spustit další
    private long _harvestCounter;         // pořadí sběru — seed deterministického kritu

    private BuildingInstance[] _buildings = new BuildingInstance[16];
    private int _buildingCount;

    /// <param name="seed">Seed světa — řídí deterministickou „náhodu" simulace (auto-stavba).</param>
    public Simulation(GameContent content, ITerrain terrain, long seed = 0)
    {
        _content = content;
        Terrain = terrain;
        Seed = seed;

        // Budova je odemčená od startu, pokud ji žádná technologie nehlídá.
        _buildingUnlocked = new bool[content.Buildings.Count];
        Array.Fill(_buildingUnlocked, true);
        foreach (var tech in content.Techs.All)
        {
            foreach (int buildingIndex in tech.UnlockedBuildingIndices)
            {
                _buildingUnlocked[buildingIndex] = false; // hlídané technologií → zamčené
            }
        }

        _techResearched = new bool[content.Techs.Count];
        _upgradesPurchased = new bool[content.PrestigeUpgrades.Count];
        _policiesActive = new bool[content.Policies.Count];
        _harvestedTotals = new long[content.Resources.Count];

        _resources = new double[content.Resources.Count];
        _storageCaps = new double[content.Resources.Count];
        for (int i = 0; i < _resources.Length; i++)
        {
            _storageCaps[i] = content.Resources[i].BaseStorage;
            _resources[i] = Math.Min(content.Resources[i].StartAmount, _storageCaps[i]);
        }

        Population = content.Gameplay.StartingPopulation;
        HousingCapacity = content.Gameplay.BaseHousingCapacity;

        _production = new ProductionSystem(content);
        _populationSystem = new PopulationSystem(content.Gameplay);
        _autoBuild = new AutoBuildSystem(content, seed);
        _zoneFill = new ZoneFillSystem(content, seed);
        _colonySystem = new ColonySystem(content, seed);
        _roadBuilder = new RoadBuilder(content);
        _settlementSystem = new SettlementSystem(content, seed);
        _questSystem = new QuestSystem(content);
        _questsCompleted = new bool[content.Quests.Count];
        _achievementSystem = new AchievementSystem(content);
        _achievementsUnlocked = new bool[content.Achievements.Count];
    }

    /// <summary>Nekonečný terén, nad kterým simulace běží.</summary>
    public ITerrain Terrain { get; }

    /// <summary>Seed světa.</summary>
    public long Seed { get; }

    /// <summary>Počet proběhlých tiků od startu hry (internal set kvůli načtení uložené hry).</summary>
    public long TickCount { get; internal set; }

    /// <summary>Populace jako agregát (viz tech-stack.md — milion lidí je jen číslo).</summary>
    public double Population { get; internal set; }

    /// <summary>
    /// Denní čas 0–1 (0 = půlnoc, 0.5 = poledne). Čistě odvozený z tiků —
    /// deterministický a v savu zadarmo (ukládá se jen TickCount).
    /// </summary>
    public double TimeOfDay01
    {
        get
        {
            var dayNight = _content.Gameplay.DayNight;
            double elapsedDays = dayNight.StartTimeOfDay + TickCount / (TicksPerSecond * dayNight.DayLengthSeconds);
            return elapsedDays - Math.Floor(elapsedDays);
        }
    }

    /// <summary>Pořadové číslo dne od začátku hry (první den = 1).</summary>
    public long DayNumber
    {
        get
        {
            var dayNight = _content.Gameplay.DayNight;
            double elapsedDays = dayNight.StartTimeOfDay + TickCount / (TicksPerSecond * dayNight.DayLengthSeconds);
            return (long)Math.Floor(elapsedDays) + 1;
        }
    }

    /// <summary>Počet dosažených Vzestupů (prestige). Řídí prestige systém; metriky ho čtou.</summary>
    public int AscensionLevel { get; internal set; }

    /// <summary>Nasbírané body Vzestupu (trvalá měna na permanentní upgrady).</summary>
    public long PrestigePoints { get; internal set; }

    /// <summary>Aktuální trvalé násobiče z koupených upgradů Vzestupu (systémy je čtou).</summary>
    public PrestigeBonuses Bonuses => _bonuses;

    /// <summary>Násobič od slavnosti (1.0 když neběží) — výroba i sběr.</summary>
    public double BoostMultiplier => _boostTicksRemaining > 0 ? _content.Gameplay.Boost.Multiplier : 1.0;

    /// <summary>Běží právě slavnost?</summary>
    public bool IsBoostActive => _boostTicksRemaining > 0;

    /// <summary>Zbývající sekundy slavnosti.</summary>
    public double BoostSecondsRemaining => _boostTicksRemaining / TicksPerSecond;

    /// <summary>Zbývající sekundy do dalšího spuštění slavnosti.</summary>
    public double BoostCooldownSecondsRemaining => _boostCooldownRemaining / TicksPerSecond;

    /// <summary>Lze slavnost spustit (neběží a není cooldown)?</summary>
    public bool CanStartBoost => _boostTicksRemaining == 0 && _boostCooldownRemaining == 0;

    /// <summary>Příkaz hráče: spustí slavnost (dočasný boost výroby i sběru). Vrací false, když nelze.</summary>
    public bool TryStartBoost()
    {
        if (!CanStartBoost)
        {
            return false;
        }

        _boostTicksRemaining = (int)(_content.Gameplay.Boost.DurationSeconds * TicksPerSecond);
        _boostCooldownRemaining = (int)(_content.Gameplay.Boost.CooldownSeconds * TicksPerSecond);
        return true;
    }

    /// <summary>Kolik lidí se vejde (základní tábor + domy).</summary>
    public int HousingCapacity { get; private set; }

    /// <summary>Součet pracovních míst výrobních budov — obsazenost škáluje výrobu.</summary>
    public int TotalWorkerSlots { get; private set; }

    /// <summary>Celkový výkon elektráren (jednotky elektřiny).</summary>
    public int TotalPowerSupply { get; private set; }

    /// <summary>Celková poptávka po elektřině (budovy, co ji potřebují).</summary>
    public int TotalPowerDemand { get; private set; }

    /// <summary>Pokrytí elektrické sítě (0–1): škáluje výrobu budov závislých na proudu.</summary>
    public double PowerFactor => TotalPowerDemand == 0 ? 1.0 : Math.Min(1.0, (double)TotalPowerSupply / TotalPowerDemand);

    /// <summary>Postavené budovy (jen ke čtení; render z nich kreslí).</summary>
    public ReadOnlySpan<BuildingInstance> Buildings => _buildings.AsSpan(0, _buildingCount);

    /// <summary>Dlaždice se silnicí v pořadí vzniku (render + save).</summary>
    public IReadOnlyList<RoadTile> RoadTiles => _roadTiles;

    /// <summary>Rozpoznané osady (odvozený stav, přepočítává <c>SettlementSystem</c>).</summary>
    public IReadOnlyList<Settlement> Settlements => _settlements;

    /// <summary>Index biomu na dlaždici.</summary>
    public byte BiomeAt(int x, int y) => Terrain.BiomeAt(x, y);

    /// <summary>Je budova odemčená (technologií)? Neřeší, zda ji lze stavět přímo.</summary>
    public bool IsBuildingUnlocked(int defIndex) => _buildingUnlocked[defIndex];

    /// <summary>Smí hráč budovu přímo postavit (odemčená a nemarkovaná jako jen-upgrade)?</summary>
    public bool IsBuildingBuildable(int defIndex) => _buildingUnlocked[defIndex] && _content.Buildings[defIndex].Buildable;

    /// <summary>
    /// Má hráč dost surovin na stavbu (bez ohledu na místo/biom)? Pro HUD — barevné
    /// zvýraznění tlačítek, ať je na první pohled jasné, co si můžu dovolit.
    /// </summary>
    public bool CanAfford(int defIndex)
    {
        var cost = _content.Buildings[defIndex].BuildCost;
        for (int i = 0; i < cost.Count; i++)
        {
            if (_resources[cost[i].ResourceIndex] < cost[i].Amount)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Je technologie vyzkoumaná?</summary>
    public bool IsTechResearched(int techIndex) => _techResearched[techIndex];

    /// <summary>
    /// Index aktuální éry (nejvyšší dosažené): éra je dosažená, když je vyzkoumaná
    /// její otevírací technologie (základní éry bez ní jsou od startu). −1 = žádné éry.
    /// </summary>
    public int CurrentEraIndex
    {
        get
        {
            int bestOrder = -1;
            int bestIndex = -1;
            for (int i = 0; i < _content.Eras.Count; i++)
            {
                var era = _content.Eras[i];
                bool reached = string.IsNullOrEmpty(era.UnlockTechId)
                    || (_content.Techs.TryIndexOf(era.UnlockTechId, out int techIndex) && _techResearched[techIndex]);
                if (reached && era.Order > bestOrder)
                {
                    bestOrder = era.Order;
                    bestIndex = i;
                }
            }

            return bestIndex;
        }
    }

    /// <summary>Je na dlaždici silnice?</summary>
    public bool IsRoad(int x, int y) => _roads.Contains(TileKey.Pack(x, y));

    /// <summary>Stojí na dlaždici budova?</summary>
    public bool IsOccupied(int x, int y) => _occupancy.ContainsKey(TileKey.Pack(x, y));

    /// <summary>Počet druhů surovin.</summary>
    public int ResourceCount => _resources.Length;

    /// <summary>Aktuální zásoba suroviny.</summary>
    public double GetResource(int resourceIndex) => _resources[resourceIndex];

    /// <summary>
    /// Přidá surovinu (ořízne na kapacitu skladu). Vstupní bod pro budoucí eventy
    /// a pro režiséra menu-pozadí, které si řídí vlastní ukázkovou simulaci.
    /// </summary>
    public void AddResource(int resourceIndex, double amount)
    {
        _resources[resourceIndex] = Math.Clamp(_resources[resourceIndex] + amount, 0, _storageCaps[resourceIndex]);
    }

    /// <summary>Kapacita skladu suroviny (základ + skladové budovy).</summary>
    public double GetStorageCap(int resourceIndex) => _storageCaps[resourceIndex];

    /// <summary>Zásoby pro systémy simulace (mutace jen uvnitř assembly).</summary>
    internal double[] Resources => _resources;

    /// <summary>Kapacity skladů pro systémy simulace.</summary>
    internal double[] StorageCaps => _storageCaps;

    /// <summary>Obsah, nad kterým simulace běží — pro serializaci savu (v rámci assembly).</summary>
    internal GameContent ContentRef => _content;

    /// <summary>Osady k přepsání systémem detekce.</summary>
    internal List<Settlement> SettlementsMutable => _settlements;

    /// <summary>Zástavba se změnila — osady čekají na přepočet.</summary>
    internal bool SettlementsDirty { get; set; }

    /// <summary>Označí dlaždici jako silnici (RoadBuilder, načtení savu). Duplicitní volání je no-op.</summary>
    internal void AddRoadTile(int x, int y)
    {
        if (_roads.Add(TileKey.Pack(x, y)))
        {
            _roadTiles.Add(new RoadTile(x, y));
        }
    }

    /// <summary>Budovy pro systémy simulace (mutace progressu výroby).</summary>
    internal Span<BuildingInstance> BuildingsMutable => _buildings.AsSpan(0, _buildingCount);

    /// <summary>Jeden krok simulace. Deterministický — žádná náhoda bez seedu.</summary>
    public void Tick()
    {
        TickCount++;
        if (_boostTicksRemaining > 0)
        {
            _boostTicksRemaining--;
        }

        if (_boostCooldownRemaining > 0)
        {
            _boostCooldownRemaining--;
        }

        _production.Tick(this);
        _populationSystem.Tick(this);
        _autoBuild.Tick(this);
        _zoneFill.Tick(this);
        _colonySystem.Tick(this); // guvernér: expanze do nových kolonií
        _settlementSystem.Tick(this);
        _questSystem.Tick(this);
        _achievementSystem.Tick(this);
    }

    /// <summary>
    /// Ověří umístění budovy bez vedlejších efektů — UI z výsledku ukazuje ghost
    /// a lokalizovanou hlášku, proč stavět nejde. Na nekonečné mapě už není
    /// „mimo mapu", jen kolize, špatný biom nebo nedostatek surovin.
    /// </summary>
    public PlacementResult CanPlace(int defIndex, int x, int y)
    {
        var def = _content.Buildings[defIndex];

        if (!IsBuildingBuildable(defIndex))
        {
            return PlacementResult.NotUnlocked;
        }

        for (int tileY = y; tileY < y + def.FootprintHeight; tileY++)
        {
            for (int tileX = x; tileX < x + def.FootprintWidth; tileX++)
            {
                long key = TileKey.Pack(tileX, tileY);
                if (_occupancy.ContainsKey(key) || _roads.Contains(key))
                {
                    return PlacementResult.Occupied;
                }

                if (!def.IsBiomeAllowed(Terrain.BiomeAt(tileX, tileY)))
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

        AddBuilding(defIndex, x, y, progress: 0f);
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
        => TryHarvest(x, y, out resourceIndex, out amount, out _);

    /// <summary>
    /// Jako <see cref="TryHarvest(int,int,out int,out int)"/>, ale navíc hlásí „krit"
    /// (deterministicky ze seedu a pořadí sběru) — velký výnos, který se ukáže efektem.
    /// </summary>
    public bool TryHarvest(int x, int y, out int resourceIndex, out int amount, out bool wasCrit)
    {
        resourceIndex = 0;
        amount = 0;
        wasCrit = false;

        long tile = TileKey.Pack(x, y);
        if (_occupancy.ContainsKey(tile))
        {
            return false;
        }

        // Zasazený uzel má přednost před přírodním výnosem biomu.
        var yield = _plantedNodes.TryGetValue(tile, out var planted) ? planted : _content.Biomes[Terrain.BiomeAt(x, y)].ClickYield;
        if (yield is null)
        {
            return false;
        }

        // Trvalý bonus Vzestupu + slavnost zvedají výnos (nejmíň původní hodnota).
        int gained = Math.Max(yield.Amount, (int)Math.Round(yield.Amount * _bonuses.HarvestMult * BoostMultiplier));

        // Deterministický krit (aktivní klikání se vyplatí).
        var harvestConfig = _content.Gameplay.Harvest;
        if (harvestConfig.CritChance > 0 && CritRoll(_harvestCounter) < harvestConfig.CritChance)
        {
            gained = (int)Math.Round(gained * harvestConfig.CritMultiplier);
            wasCrit = true;
        }

        // Plný sklad = žádný sběr (a žádný lživý popup v UI).
        if (_resources[yield.ResourceIndex] + gained > _storageCaps[yield.ResourceIndex])
        {
            wasCrit = false;
            return false;
        }

        _resources[yield.ResourceIndex] += gained;
        _harvestedTotals[yield.ResourceIndex] += gained;
        _harvestCounter++;
        resourceIndex = yield.ResourceIndex;
        amount = gained;
        return true;
    }

    /// <summary>Deterministické „hození kostkou" pro krit — z seedu a pořadí sběru, výsledek v [0, 1).</summary>
    private double CritRoll(long counter)
    {
        ulong h = (ulong)Seed * 0x9E3779B97F4A7C15UL ^ (ulong)counter * 0xD1B54A32D192ED03UL;
        h ^= h >> 29;
        h *= 0xBF58476D1CE4E5B9UL;
        h ^= h >> 32;
        return (h >> 11) * (1.0 / (1UL << 53));
    }

    /// <summary>Kumulativně nasbíráno suroviny klikáním (pro cíle/achievementy).</summary>
    public long GetHarvestedTotal(int resourceIndex) => _harvestedTotals[resourceIndex];

    // ----- sázení (obnovitelné zdroje) -----

    /// <summary>Lze na (x, y) zasadit obnovitelný zdroj (suchá dlaždice, prázdná, dost surovin)?</summary>
    public PlacementResult CanPlant(int x, int y)
    {
        long tile = TileKey.Pack(x, y);
        if (_content.Biomes[Terrain.BiomeAt(x, y)].IsWater)
        {
            return PlacementResult.WrongBiome;
        }

        if (_occupancy.ContainsKey(tile) || _roads.Contains(tile) || _plantedNodes.ContainsKey(tile))
        {
            return PlacementResult.Occupied;
        }

        var cost = _content.Gameplay.Planting.Cost;
        for (int i = 0; i < cost.Count; i++)
        {
            if (_resources[cost[i].ResourceIndex] < cost[i].Amount)
            {
                return PlacementResult.NotEnoughResources;
            }
        }

        return PlacementResult.Ok;
    }

    /// <summary>Příkaz hráče: zasadí obnovitelný zdroj (háj) — pak se dá těžit klikem jako přírodní.</summary>
    public PlacementResult TryPlant(int x, int y)
    {
        var result = CanPlant(x, y);
        if (result != PlacementResult.Ok)
        {
            return result;
        }

        var planting = _content.Gameplay.Planting;
        for (int i = 0; i < planting.Cost.Count; i++)
        {
            _resources[planting.Cost[i].ResourceIndex] -= planting.Cost[i].Amount;
        }

        _plantedNodes[TileKey.Pack(x, y)] = new ClickYield(planting.ResourceIndex, planting.Amount);
        return PlacementResult.Ok;
    }

    /// <summary>Je na dlaždici zasazený uzel? (pro render.)</summary>
    public bool TryGetPlantedNode(int x, int y, out int resourceIndex)
    {
        if (_plantedNodes.TryGetValue(TileKey.Pack(x, y), out var yield))
        {
            resourceIndex = yield.ResourceIndex;
            return true;
        }

        resourceIndex = 0;
        return false;
    }

    /// <summary>Zasazené uzly pro serializaci savu.</summary>
    internal IEnumerable<(int X, int Y, int ResourceIndex, int Amount)> PlantedNodes()
    {
        foreach (var (key, yield) in _plantedNodes)
        {
            yield return (TileKey.X(key), TileKey.Y(key), yield.ResourceIndex, yield.Amount);
        }
    }

    /// <summary>Obnoví zasazený uzel při načtení savu.</summary>
    internal void RestorePlantedNode(int x, int y, int resourceIndex, int amount)
        => _plantedNodes[TileKey.Pack(x, y)] = new ClickYield(resourceIndex, amount);

    // ----- zóny (automatizace, stupeň 3) -----

    /// <summary>Horní limit rozměru zóny — brání nesmyslně velkým zónám (výkon, sanity savu).</summary>
    public const int MaxZoneDimension = 64;

    /// <summary>Horní limit počtu zón (sanity savu).</summary>
    public const int MaxZones = 4096;

    /// <summary>Namalované zóny (jen pro čtení — render a fill systém).</summary>
    public IReadOnlyList<Zone> Zones => _zones;

    /// <summary>
    /// Příkaz hráče: přidat obdélníkovou zónu daného typu. Souřadnice se normalizují
    /// (rohy v libovolném pořadí), rozměr se ořízne na <see cref="MaxZoneDimension"/>.
    /// Vrací false, když je typ mimo rozsah nebo je zón už příliš mnoho.
    /// </summary>
    public bool AddZone(int typeIndex, int x, int y, int width, int height)
    {
        if (typeIndex < 0 || typeIndex >= _content.ZoneTypes.Count)
        {
            return false;
        }

        if (width <= 0 || height <= 0 || _zones.Count >= MaxZones)
        {
            return false;
        }

        width = Math.Min(width, MaxZoneDimension);
        height = Math.Min(height, MaxZoneDimension);
        _zones.Add(new Zone(typeIndex, x, y, width, height));
        return true;
    }

    /// <summary>Příkaz hráče: smaže zónu obsahující danou dlaždici (poslední namalovaná má přednost). Vrací true, když něco smazal.</summary>
    public bool RemoveZoneAt(int x, int y)
    {
        for (int i = _zones.Count - 1; i >= 0; i--)
        {
            if (_zones[i].Contains(x, y))
            {
                _zones.RemoveAt(i);
                return true;
            }
        }

        return false;
    }

    /// <summary>Obnoví zónu při načtení savu (bez normalizace — data už jsou platná).</summary>
    internal void RestoreZone(int typeIndex, int x, int y, int width, int height)
    {
        if (typeIndex >= 0 && typeIndex < _content.ZoneTypes.Count && width > 0 && height > 0)
        {
            _zones.Add(new Zone(typeIndex, x, y, width, height));
        }
    }

    // ----- politiky růstu (automatizace, stupeň 4) -----

    /// <summary>Kolik budov smí auto-stavba i plnění zón položit za interval (výchozí 1; politika „build_pace" zvedá).</summary>
    public int BuildsPerInterval { get; private set; } = 1;

    /// <summary>Preferovat hustotu: auto-stavba nejdřív povýší existující bydlení, než postaví nové (politika „housing_density").</summary>
    public bool PreferHousingDensity { get; private set; }

    /// <summary>Guvernér: zakládat samostatné kolonie, když je doma plno (politika „auto_expand").</summary>
    public bool AutoExpandColonies { get; private set; }

    /// <summary>Jak daleko od těžiště zástavby guvernér zakládá kolonii (dlaždice).</summary>
    public int ColonyDistance { get; private set; } = DefaultColonyDistance;

    /// <summary>Výchozí vzdálenost kolonie, když ji politika neurčí jinak.</summary>
    private const int DefaultColonyDistance = 18;

    /// <summary>Oznámí založení kolonie (guvernér) — HUD z toho udělá „founder moment".</summary>
    internal void EnqueueColonyFounded() =>
        EnqueueNotification(new GameNotification(NotificationKind.Milestone, "toast.milestone", "colony.founded"));

    /// <summary>Je politika zapnutá?</summary>
    public bool IsPolicyActive(int policyIndex) => _policiesActive[policyIndex];

    /// <summary>Příkaz hráče: přepne politiku a přepočítá její vliv na růst; vrací nový stav.</summary>
    public bool TogglePolicy(int policyIndex)
    {
        if (policyIndex < 0 || policyIndex >= _policiesActive.Length)
        {
            return false;
        }

        _policiesActive[policyIndex] = !_policiesActive[policyIndex];
        RecomputePolicyEffects();
        return _policiesActive[policyIndex];
    }

    /// <summary>Indexy zapnutých politik (pro serializaci savu).</summary>
    internal IEnumerable<int> ActivePolicyIndices()
    {
        for (int i = 0; i < _policiesActive.Length; i++)
        {
            if (_policiesActive[i])
            {
                yield return i;
            }
        }
    }

    /// <summary>Obnoví zapnutou politiku při načtení savu (efekt se přepočítá ve <see cref="FinalizeLoad"/>).</summary>
    internal void RestorePolicyActive(int policyIndex)
    {
        if (policyIndex >= 0 && policyIndex < _policiesActive.Length)
        {
            _policiesActive[policyIndex] = true;
        }
    }

    /// <summary>
    /// Přemapuje zapnuté politiky na odvozené parametry růstu (data = co, kód = jak,
    /// mapování přes behavior-ID). Neznámý efekt se tiše ignoruje — data smí předběhnout kód.
    /// </summary>
    private void RecomputePolicyEffects()
    {
        int buildsPerInterval = 1;
        bool preferDensity = false;
        bool autoExpand = false;
        int colonyDistance = DefaultColonyDistance;
        for (int i = 0; i < _policiesActive.Length; i++)
        {
            if (!_policiesActive[i])
            {
                continue;
            }

            var policy = _content.Policies[i];
            switch (policy.Effect)
            {
                case "build_pace":
                    buildsPerInterval = Math.Max(buildsPerInterval, Math.Max(1, (int)policy.Magnitude));
                    break;
                case "housing_density":
                    preferDensity = true;
                    break;
                case "auto_expand":
                    autoExpand = true;
                    if (policy.Magnitude > 0)
                    {
                        colonyDistance = (int)policy.Magnitude;
                    }

                    break;
            }
        }

        BuildsPerInterval = buildsPerInterval;
        PreferHousingDensity = preferDensity;
        AutoExpandColonies = autoExpand;
        ColonyDistance = colonyDistance;
    }

    // ----- objevování mapy (skrýše) -----

    private const int DiscoveryRate = 500; // zhruba každá N-tá suchá dlaždice skrývá skrýš

    /// <summary>Je na (suché) dlaždici skrýš k objevení? Deterministické z pozice (nekonečná mapa).</summary>
    public bool IsDiscoveryTile(int x, int y)
    {
        if (_content.Biomes[Terrain.BiomeAt(x, y)].IsWater)
        {
            return false;
        }

        return DiscoveryHash(x, y) % DiscoveryRate == 0;
    }

    /// <summary>Byla skrýš na dlaždici už vyzvednuta?</summary>
    public bool IsDiscoveryClaimed(int x, int y) => _claimedDiscoveries.Contains(TileKey.Pack(x, y));

    /// <summary>
    /// Příkaz hráče: vyzvedne skrýš (klik při objevování mapy) — deterministická
    /// odměna v surovině. Jednou za dlaždici. Vrací false, když tu skrýš není nebo už padla.
    /// </summary>
    public bool TryClaimDiscovery(int x, int y, out int resourceIndex, out int amount)
    {
        resourceIndex = 0;
        amount = 0;
        if (!IsDiscoveryTile(x, y) || !_claimedDiscoveries.Add(TileKey.Pack(x, y)))
        {
            return false;
        }

        ulong roll = DiscoveryHash(x, y);
        resourceIndex = (int)(roll % (ulong)_resources.Length);
        amount = 20 + (int)(roll / DiscoveryRate % 40); // 20–59, deterministicky z pozice
        AddResource(resourceIndex, amount);
        return true;
    }

    /// <summary>Deterministický hash dlaždice pro rozmístění a odměnu skrýší (přes seed světa).</summary>
    private ulong DiscoveryHash(int x, int y)
    {
        ulong h = (ulong)Seed * 0xD6E8FEB86659FD93UL ^ (uint)x * 0x9E3779B97F4A7C15UL ^ (uint)y * 0xC2B2AE3D27D4EB4FUL;
        h ^= h >> 29;
        h *= 0xBF58476D1CE4E5B9UL;
        return h ^ (h >> 32);
    }

    /// <summary>Vyzvednuté skrýše (dlaždice) pro serializaci savu.</summary>
    internal IEnumerable<(int X, int Y)> ClaimedDiscoveries()
    {
        foreach (long key in _claimedDiscoveries)
        {
            yield return (TileKey.X(key), TileKey.Y(key));
        }
    }

    /// <summary>Označí skrýš jako vyzvednutou při načtení savu.</summary>
    internal void RestoreDiscovery(int x, int y) => _claimedDiscoveries.Add(TileKey.Pack(x, y));

    /// <summary>
    /// Přečte aktuální hodnotu metriky pro cíl/achievement. Data určují „co"
    /// (metrika + parametr), tahle metoda „jak" se čte ze stavu simulace.
    /// </summary>
    public long EvaluateMetric(MetricKind kind, int param) => kind switch
    {
        MetricKind.Population => (long)Population,
        MetricKind.HousingCapacity => HousingCapacity,
        MetricKind.Harvested => _harvestedTotals[param],
        MetricKind.ResourceStock => (long)_resources[param],
        MetricKind.TotalBuildings => _buildingCount,
        MetricKind.BuildingOfType => CountBuildingsOfType(param),
        MetricKind.ResearchedTech => _techResearched[param] ? 1 : 0,
        MetricKind.AscensionLevel => AscensionLevel,
        MetricKind.DayNumber => DayNumber,
        _ => 0,
    };

    private long CountBuildingsOfType(int defIndex)
    {
        long count = 0;
        for (int i = 0; i < _buildingCount; i++)
        {
            if (_buildings[i].DefIndex == defIndex)
            {
                count++;
            }
        }

        return count;
    }

    // ----- oznámení (toasty) — sim je jen vyrobí, render je přeloží a vykreslí -----

    /// <summary>Zařadí oznámení k zobrazení (splněný úkol, achievement, milník…).</summary>
    internal void EnqueueNotification(GameNotification notification) => _notifications.Enqueue(notification);

    /// <summary>Zahodí čekající oznámení (po offline dohonu, ať nezaplaví toasty).</summary>
    internal void ClearNotifications() => _notifications.Clear();

    /// <summary>Vyzvedne další oznámení pro render vrstvu; false = fronta prázdná.</summary>
    public bool TryDequeueNotification(out GameNotification notification)
    {
        if (_notifications.Count > 0)
        {
            notification = _notifications.Dequeue();
            return true;
        }

        notification = default;
        return false;
    }

    // ----- úkoly (quests) -----

    /// <summary>Je pevný úkol splněný?</summary>
    public bool IsQuestCompleted(int questIndex) => _questsCompleted[questIndex];

    /// <summary>Kolikátý dynamický úkol se plní (0 = první). Roste s hrou.</summary>
    public int DynamicQuestTier { get; internal set; }

    /// <summary>Aktuální práh dynamického úkolu (roste násobičem za každý splněný tier).</summary>
    public long DynamicQuestTarget
    {
        get
        {
            var dynamic = _content.QuestsDynamic;
            double target = dynamic.BaseCondition.Target * Math.Pow(dynamic.TargetGrowth, DynamicQuestTier);
            return (long)Math.Ceiling(target);
        }
    }

    /// <summary>Splněné pevné úkoly pro systém úkolů (mutace jen uvnitř assembly).</summary>
    internal bool[] QuestsCompleted => _questsCompleted;

    /// <summary>Indexy splněných pevných úkolů (pro serializaci savu).</summary>
    internal IEnumerable<int> CompletedQuestIndices()
    {
        for (int i = 0; i < _questsCompleted.Length; i++)
        {
            if (_questsCompleted[i])
            {
                yield return i;
            }
        }
    }

    /// <summary>Označí úkol jako splněný při načtení savu (bez odměny).</summary>
    internal void RestoreQuestCompleted(int questIndex) => _questsCompleted[questIndex] = true;

    // ----- achievementy (účet-wide) -----

    /// <summary>Je achievement odemčený?</summary>
    public bool IsAchievementUnlocked(int achievementIndex) => _achievementsUnlocked[achievementIndex];

    /// <summary>
    /// Naseeduje už odemčené achievementy z profilu (aby se v této hře znovu
    /// nespouštěly). Volá aplikační vrstva po vytvoření simulace.
    /// </summary>
    public void SeedUnlockedAchievements(IEnumerable<string> achievementIds)
    {
        foreach (string id in achievementIds)
        {
            if (_content.Achievements.TryIndexOf(id, out int index))
            {
                _achievementsUnlocked[index] = true;
            }
        }
    }

    /// <summary>Odemčené achievementy pro systém achievementů (mutace jen uvnitř assembly).</summary>
    internal bool[] AchievementsUnlocked => _achievementsUnlocked;

    /// <summary>Najde budovu na dlaždici (pro klik → info/upgrade panel). Vrací index do <see cref="Buildings"/>.</summary>
    public bool TryGetBuildingAt(int x, int y, out int buildingIndex)
    {
        buildingIndex = -1;
        if (_occupancy.TryGetValue(TileKey.Pack(x, y), out int stored))
        {
            buildingIndex = stored - 1; // occupancy ukládá index+1
            return true;
        }

        return false;
    }

    /// <summary>Lze budovu (podle indexu v <see cref="Buildings"/>) vylepšit na další úroveň?</summary>
    public PlacementResult CanUpgrade(int buildingIndex)
    {
        if (buildingIndex < 0 || buildingIndex >= _buildingCount)
        {
            return PlacementResult.Occupied;
        }

        var def = _content.Buildings[_buildings[buildingIndex].DefIndex];
        if (!def.HasUpgrade)
        {
            return PlacementResult.NotUnlocked;
        }

        var cost = def.UpgradeCost;
        for (int i = 0; i < cost.Count; i++)
        {
            if (_resources[cost[i].ResourceIndex] < cost[i].Amount)
            {
                return PlacementResult.NotEnoughResources;
            }
        }

        return PlacementResult.Ok;
    }

    /// <summary>
    /// Příkaz hráče: vylepší budovu na další úroveň (mění se na místě — stejný půdorys).
    /// Odečte cenu vylepšení a přepočítá globální bonusy (bydlení, práce, sklady).
    /// </summary>
    public PlacementResult TryUpgradeBuilding(int buildingIndex)
    {
        var result = CanUpgrade(buildingIndex);
        if (result != PlacementResult.Ok)
        {
            return result;
        }

        ref var instance = ref _buildings[buildingIndex];
        var oldDef = _content.Buildings[instance.DefIndex];

        var cost = oldDef.UpgradeCost;
        for (int i = 0; i < cost.Count; i++)
        {
            _resources[cost[i].ResourceIndex] -= cost[i].Amount;
        }

        RemoveBuildingBonuses(oldDef);
        instance.DefIndex = oldDef.UpgradesToIndex;
        instance.Progress = 0f;
        ApplyBuildingBonuses(_content.Buildings[instance.DefIndex]);
        SettlementsDirty = true;
        return PlacementResult.Ok;
    }

    /// <summary>Podíl ceny stavby, který se vrátí při zbourání (balanční konstanta).</summary>
    private const double DemolishRefundFraction = 0.5;

    /// <summary>Příkaz hráče: zbourá budovu — uvolní dlaždice, vrátí část ceny, přepočítá bonusy.</summary>
    public PlacementResult TryDemolish(int buildingIndex)
    {
        if (buildingIndex < 0 || buildingIndex >= _buildingCount)
        {
            return PlacementResult.Occupied;
        }

        var def = _content.Buildings[_buildings[buildingIndex].DefIndex];
        int x = _buildings[buildingIndex].X, y = _buildings[buildingIndex].Y;

        for (int tileY = y; tileY < y + def.FootprintHeight; tileY++)
        {
            for (int tileX = x; tileX < x + def.FootprintWidth; tileX++)
            {
                _occupancy.Remove(TileKey.Pack(tileX, tileY));
            }
        }

        RemoveBuildingBonuses(def);
        var cost = def.BuildCost;
        for (int i = 0; i < cost.Count; i++)
        {
            AddResource(cost[i].ResourceIndex, Math.Floor(cost[i].Amount * DemolishRefundFraction));
        }

        // Swap-remove z plochého pole: poslední budovu přesuň na uvolněné místo
        // a přemapuj její occupancy na nový index.
        int last = _buildingCount - 1;
        if (buildingIndex != last)
        {
            _buildings[buildingIndex] = _buildings[last];
            RemapOccupancy(buildingIndex);
        }

        _buildingCount--;
        SettlementsDirty = true;
        return PlacementResult.Ok;
    }

    /// <summary>Lze budovu přesunout na (x, y)? Vlastní současné dlaždice se ignorují.</summary>
    public PlacementResult CanMoveBuilding(int buildingIndex, int x, int y)
    {
        if (buildingIndex < 0 || buildingIndex >= _buildingCount)
        {
            return PlacementResult.Occupied;
        }

        var def = _content.Buildings[_buildings[buildingIndex].DefIndex];
        for (int tileY = y; tileY < y + def.FootprintHeight; tileY++)
        {
            for (int tileX = x; tileX < x + def.FootprintWidth; tileX++)
            {
                long key = TileKey.Pack(tileX, tileY);
                if (_roads.Contains(key))
                {
                    return PlacementResult.Occupied;
                }

                if (_occupancy.TryGetValue(key, out int stored) && stored - 1 != buildingIndex)
                {
                    return PlacementResult.Occupied;
                }

                if (!def.IsBiomeAllowed(Terrain.BiomeAt(tileX, tileY)))
                {
                    return PlacementResult.WrongBiome;
                }
            }
        }

        return PlacementResult.Ok;
    }

    /// <summary>Příkaz hráče: přesune budovu na (x, y) zdarma (stejný typ, jen jinam).</summary>
    public PlacementResult TryMoveBuilding(int buildingIndex, int x, int y)
    {
        var result = CanMoveBuilding(buildingIndex, x, y);
        if (result != PlacementResult.Ok)
        {
            return result;
        }

        ref var building = ref _buildings[buildingIndex];
        var def = _content.Buildings[building.DefIndex];
        for (int tileY = building.Y; tileY < building.Y + def.FootprintHeight; tileY++)
        {
            for (int tileX = building.X; tileX < building.X + def.FootprintWidth; tileX++)
            {
                _occupancy.Remove(TileKey.Pack(tileX, tileY));
            }
        }

        building.X = x;
        building.Y = y;
        for (int tileY = y; tileY < y + def.FootprintHeight; tileY++)
        {
            for (int tileX = x; tileX < x + def.FootprintWidth; tileX++)
            {
                _occupancy[TileKey.Pack(tileX, tileY)] = buildingIndex + 1;
            }
        }

        SettlementsDirty = true;
        return PlacementResult.Ok;
    }

    /// <summary>Přemapuje occupancy dlaždic budovy na její (nový) index.</summary>
    private void RemapOccupancy(int buildingIndex)
    {
        var building = _buildings[buildingIndex];
        var def = _content.Buildings[building.DefIndex];
        for (int tileY = building.Y; tileY < building.Y + def.FootprintHeight; tileY++)
        {
            for (int tileX = building.X; tileX < building.X + def.FootprintWidth; tileX++)
            {
                _occupancy[TileKey.Pack(tileX, tileY)] = buildingIndex + 1;
            }
        }
    }

    // ----- tech tree -----

    /// <summary>Lze technologii vyzkoumat (prerekvizity splněny, dost surovin, není hotová)?</summary>
    public PlacementResult CanResearch(int techIndex)
    {
        if (_techResearched[techIndex])
        {
            return PlacementResult.Occupied; // už hotová
        }

        var tech = _content.Techs[techIndex];
        foreach (int prereq in tech.PrerequisiteIndices)
        {
            if (!_techResearched[prereq])
            {
                return PlacementResult.NotUnlocked;
            }
        }

        var cost = tech.Cost;
        for (int i = 0; i < cost.Count; i++)
        {
            if (_resources[cost[i].ResourceIndex] < cost[i].Amount)
            {
                return PlacementResult.NotEnoughResources;
            }
        }

        return PlacementResult.Ok;
    }

    /// <summary>Příkaz hráče: vyzkoumat technologii — odečte cenu a odemkne její budovy.</summary>
    public PlacementResult TryResearch(int techIndex)
    {
        var result = CanResearch(techIndex);
        if (result != PlacementResult.Ok)
        {
            return result;
        }

        var tech = _content.Techs[techIndex];
        for (int i = 0; i < tech.Cost.Count; i++)
        {
            _resources[tech.Cost[i].ResourceIndex] -= tech.Cost[i].Amount;
        }

        UnlockTech(techIndex);
        return PlacementResult.Ok;
    }

    private void UnlockTech(int techIndex)
    {
        _techResearched[techIndex] = true;
        foreach (int buildingIndex in _content.Techs[techIndex].UnlockedBuildingIndices)
        {
            _buildingUnlocked[buildingIndex] = true;
        }
    }

    // ----- obnova ze savu (jen pro SaveGameSerializer, obchází cenu a validaci biomů) -----

    /// <summary>Označí technologii jako vyzkoumanou při načtení savu (bez ceny).</summary>
    internal void RestoreTech(int techIndex) => UnlockTech(techIndex);

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
    /// změnit a už postavené budovy hráči nemažeme.
    /// </summary>
    internal void RestoreBuilding(int defIndex, int x, int y, float progress)
    {
        AddBuilding(defIndex, x, y, progress);
        ApplyBuildingBonuses(_content.Buildings[defIndex]);
        SettlementsDirty = true; // silnice ze savu chodí zvlášť, přepočet osad ale spustit musíme
    }

    private void AddBuilding(int defIndex, int x, int y, float progress)
    {
        var def = _content.Buildings[defIndex];
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
                _occupancy[TileKey.Pack(tileX, tileY)] = _buildingCount;
            }
        }
    }

    /// <summary>Globální bonusy budovy: bydlení, pracovní místa, kapacita skladů (× bonusy Vzestupu).</summary>
    private void ApplyBuildingBonuses(BuildingDef def)
    {
        HousingCapacity += (int)(def.HousingCapacity * _bonuses.HousingMult);
        TotalWorkerSlots += def.WorkerSlots;
        TotalPowerSupply += def.PowerSupply;
        TotalPowerDemand += def.PowerDemand;
        for (int i = 0; i < def.StorageBonus.Count; i++)
        {
            _storageCaps[def.StorageBonus[i].ResourceIndex] += def.StorageBonus[i].Amount * _bonuses.StorageMult;
        }
    }

    /// <summary>Odebere globální bonusy budovy (vylepšení nahrazuje starou úroveň novou).</summary>
    private void RemoveBuildingBonuses(BuildingDef def)
    {
        HousingCapacity -= (int)(def.HousingCapacity * _bonuses.HousingMult);
        TotalWorkerSlots -= def.WorkerSlots;
        TotalPowerSupply -= def.PowerSupply;
        TotalPowerDemand -= def.PowerDemand;
        for (int i = 0; i < def.StorageBonus.Count; i++)
        {
            _storageCaps[def.StorageBonus[i].ResourceIndex] -= def.StorageBonus[i].Amount * _bonuses.StorageMult;
        }
    }

    // ----- Vzestup (prestige) -----

    /// <summary>Je splněná podmínka pro Vzestup?</summary>
    public bool CanAscend() =>
        EvaluateMetric(_content.Prestige.Requirement.Kind, _content.Prestige.Requirement.Param)
            >= _content.Prestige.Requirement.Target;

    /// <summary>Kolik bodů Vzestupu by teď Vzestup udělil.</summary>
    public long PendingAscensionPoints()
    {
        var prestige = _content.Prestige;
        long metric = EvaluateMetric(prestige.PointsMetric, prestige.PointsParam);
        return metric / prestige.PointsDivisor;
    }

    /// <summary>Je trvalý upgrade Vzestupu koupený?</summary>
    public bool IsUpgradePurchased(int upgradeIndex) => _upgradesPurchased[upgradeIndex];

    /// <summary>Lze upgrade koupit (splněné prereky, dost bodů, ještě nekoupený)?</summary>
    public PlacementResult CanBuyUpgrade(int upgradeIndex)
    {
        if (_upgradesPurchased[upgradeIndex])
        {
            return PlacementResult.Occupied; // už koupený
        }

        var upgrade = _content.PrestigeUpgrades[upgradeIndex];
        foreach (int prereq in upgrade.PrerequisiteIndices)
        {
            if (!_upgradesPurchased[prereq])
            {
                return PlacementResult.NotUnlocked;
            }
        }

        return PrestigePoints < upgrade.Cost ? PlacementResult.NotEnoughResources : PlacementResult.Ok;
    }

    /// <summary>Koupí trvalý upgrade Vzestupu — odečte body a přepočítá bonusy i odvozený stav.</summary>
    public PlacementResult TryBuyUpgrade(int upgradeIndex)
    {
        var result = CanBuyUpgrade(upgradeIndex);
        if (result != PlacementResult.Ok)
        {
            return result;
        }

        PrestigePoints -= _content.PrestigeUpgrades[upgradeIndex].Cost;
        _upgradesPurchased[upgradeIndex] = true;
        RecomputeBonuses();
        RecomputeDerivedState();
        return PlacementResult.Ok;
    }

    /// <summary>
    /// Vzestup: udělí body podle dosaženého pokroku, zvýší úroveň a začne novou éru
    /// (resetuje mapu) — trvalé upgrady a body zůstávají. Zdroj háčku dema.
    /// </summary>
    public PlacementResult TryAscend()
    {
        if (!CanAscend())
        {
            return PlacementResult.NotEnoughResources;
        }

        PrestigePoints += PendingAscensionPoints();
        AscensionLevel++;
        ResetEra();
        EnqueueNotification(new GameNotification(NotificationKind.Ascended, "toast.ascended", "prestige.ascendedSubject"));
        return PlacementResult.Ok;
    }

    private void RecomputeBonuses()
    {
        double production = 1.0, harvest = 1.0, growth = 1.0, housing = 1.0, storage = 1.0, start = 1.0, offline = 1.0;
        for (int i = 0; i < _upgradesPurchased.Length; i++)
        {
            if (!_upgradesPurchased[i])
            {
                continue;
            }

            var upgrade = _content.PrestigeUpgrades[i];
            switch (upgrade.Effect)
            {
                case "production_mult": production += upgrade.Magnitude; break;
                case "harvest_mult": harvest += upgrade.Magnitude; break;
                case "growth_mult": growth += upgrade.Magnitude; break;
                case "housing_mult": housing += upgrade.Magnitude; break;
                case "storage_mult": storage += upgrade.Magnitude; break;
                case "start_resources": start += upgrade.Magnitude; break;
                case "offline_mult": offline += upgrade.Magnitude; break;
            }
        }

        _bonuses = new PrestigeBonuses(production, harvest, growth, housing, storage, start, offline);
    }

    /// <summary>
    /// Přepočítá odvozený stav (bydlení, pracovní místa, kapacity skladů) z nuly
    /// podle aktuálních budov a bonusů Vzestupu. Volá se po koupi upgradu, po Vzestupu
    /// a po načtení savu — drží stav konzistentní bez ohledu na pořadí změn.
    /// </summary>
    internal void RecomputeDerivedState()
    {
        HousingCapacity = _content.Gameplay.BaseHousingCapacity;
        TotalWorkerSlots = 0;
        TotalPowerSupply = 0;
        TotalPowerDemand = 0;
        for (int i = 0; i < _storageCaps.Length; i++)
        {
            _storageCaps[i] = _content.Resources[i].BaseStorage * _bonuses.StorageMult;
        }

        for (int i = 0; i < _buildingCount; i++)
        {
            ApplyBuildingBonuses(_content.Buildings[_buildings[i].DefIndex]);
        }

        for (int i = 0; i < _resources.Length; i++)
        {
            _resources[i] = Math.Min(_resources[i], _storageCaps[i]);
        }
    }

    private void ResetEra()
    {
        _occupancy.Clear();
        _roads.Clear();
        _roadTiles.Clear();
        _settlements.Clear();
        _zones.Clear(); // zóny řídí přestavbu — po Vzestupu (nové měřítko) začínáš nanovo
        _buildingCount = 0;

        Array.Clear(_techResearched);
        Array.Fill(_buildingUnlocked, true);
        foreach (var tech in _content.Techs.All)
        {
            foreach (int buildingIndex in tech.UnlockedBuildingIndices)
            {
                _buildingUnlocked[buildingIndex] = false;
            }
        }

        RecomputeBonuses();
        RecomputeDerivedState(); // bez budov → základní kapacity × StorageMult
        for (int i = 0; i < _resources.Length; i++)
        {
            _resources[i] = Math.Min(_content.Resources[i].StartAmount * _bonuses.StartResourceMult, _storageCaps[i]);
        }

        Population = _content.Gameplay.StartingPopulation;
        TickCount = 0;
        SettlementsDirty = true;
    }

    /// <summary>Indexy vyzkoumaných technologií (pro serializaci savu).</summary>
    internal IEnumerable<int> ResearchedTechIndices()
    {
        for (int i = 0; i < _techResearched.Length; i++)
        {
            if (_techResearched[i])
            {
                yield return i;
            }
        }
    }

    /// <summary>Nastaví úroveň a body Vzestupu při načtení savu.</summary>
    internal void RestoreAscension(int ascensionLevel, long prestigePoints)
    {
        AscensionLevel = ascensionLevel;
        PrestigePoints = prestigePoints;
    }

    /// <summary>Označí koupený upgrade Vzestupu při načtení savu (bonusy dopočítá <see cref="FinalizeLoad"/>).</summary>
    internal void RestoreUpgrade(int upgradeIndex) => _upgradesPurchased[upgradeIndex] = true;

    /// <summary>Indexy koupených upgradů Vzestupu (pro serializaci savu).</summary>
    internal IEnumerable<int> PurchasedUpgradeIndices()
    {
        for (int i = 0; i < _upgradesPurchased.Length; i++)
        {
            if (_upgradesPurchased[i])
            {
                yield return i;
            }
        }
    }

    /// <summary>
    /// Dokončí načtení savu: z koupených upgradů spočítá bonusy a přepočítá odvozený
    /// stav (bydlení, sklady) — budovy se při načtení přičítaly ještě bez bonusů.
    /// </summary>
    internal void FinalizeLoad()
    {
        RecomputeBonuses();
        RecomputeDerivedState();
        RecomputePolicyEffects(); // obnovené politiky → odvozené parametry růstu
    }
}
