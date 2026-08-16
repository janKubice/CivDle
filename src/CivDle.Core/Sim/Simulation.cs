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
    private bool[] _roadLinked = Array.Empty<bool>(); // budova ↔ napojení na síť (cache)

    /// <summary>
    /// Jak daleko od skutečné ulice budova je (0 = dotýká se jí sama).
    ///
    /// <para>Drží se vedle <see cref="_roadLinked"/>, protože bez vrstvy by se
    /// vlna „napojeno" rozlila celým městem a jedna dlaždice by napojila
    /// všechno — viz <see cref="SpreadRoadLinkThroughBlocks"/>.</para>
    /// </summary>
    private byte[] _roadLinkHop = Array.Empty<byte>();
    private bool _roadLinksDirty = true;
    private readonly List<RoadTile> _roadTiles = new(); // pořadí vzniku — deterministické, jde do savu
    private readonly List<Settlement> _settlements = new();
    private readonly ProductionSystem _production;
    private readonly HaulSystem _haulSystem;
    private readonly SeasonSystem _seasonSystem;
    private readonly ToolsSystem _toolsSystem;
    private readonly PollutionSystem _pollutionSystem;
    private readonly ContractSystem _contractSystem;
    private readonly DistrictSystem _districtSystem;
    private readonly CitizenSystem _citizenSystem;
    private readonly BuildingMilestoneSystem _milestoneBonuses;
    private readonly HistorySystem _historySystem;
    private readonly ReforestSystem _reforestSystem;
    private readonly AutoTerraformSystem _autoTerraformSystem;
    private readonly StreetGridPaver _streetPaver;
    private readonly NpcTownSystem _npcTowns; // postavená cizí města (skutečné budovy a silnice)
    private readonly GrandWorkSystem _grandWork; // bezedný odběr přebytků
    private readonly List<GrandWorkStage> _grandWorkDone = new(); // dokončené stupně (drží bonusy)
    private readonly LegacySystem _legacy; // druhá prestižní vrstva (Odkaz)
    private readonly AutoResearchSystem _autoResearch = new(); // odemyká se až v Odkazu

    /// <summary>Budovy zapamatované při Vzestupu, než se svět resetuje.</summary>
    private readonly List<LegacyInheritance.KeptBuilding> _inheritedBuildings = new();

    /// <summary>Silnice zapamatované při Vzestupu.</summary>
    private readonly List<RoadTile> _inheritedRoads = new();

    /// <summary>Zásoby zapamatované při Vzestupu (indexem je surovina).</summary>
    private readonly List<double> _inheritedResources = new();

    /// <summary>Kolik dlaždic silnic při posledním Vzestupu zůstalo (pro bilanci).</summary>
    public int LastInheritedRoads { get; private set; }

    /// <summary>Kolik technologií se při posledním Vzestupu zdědilo (pro bilanci).</summary>
    public int LastInheritedTechs { get; private set; }

    /// <summary>Kolik budov při posledním Vzestupu zůstalo stát (pro bilanci).</summary>
    public int LastInheritedBuildings { get; private set; }
    private readonly ConstructionSystem _constructionSystem;
    private readonly PopulationSystem _populationSystem;
    private readonly AutoBuildSystem _autoBuild;
    private readonly ZoneFillSystem _zoneFill;
    private readonly ColonySystem _colonySystem;
    private readonly WeatherSystem _weatherSystem;
    private readonly HappinessSystem _happinessSystem;
    private readonly UfoSystem _ufoSystem;
    private long _lastUfoWindow = -1; // poslední okno, jehož zásah UFO už proběhl
    private readonly List<Zone> _zones = new(); // hráčem namalované zóny (automatizace, stupeň 3)
    private readonly RoadBuilder _roadBuilder;
    private readonly CityRoadPlanner _cityRoadPlanner = new();
    private readonly SettlementSystem _settlementSystem;
    private readonly QuestSystem _questSystem;
    private readonly TutorialSystem _tutorialSystem;
    private readonly ChallengeSystem _challengeSystem;
    private readonly ElectionSystem _electionSystem;
    private readonly VisualEventQueue _visualEvents = new();
    private readonly MilestoneSystem _milestoneSystem;
    private readonly bool[] _milestonesReached;
    private readonly int[] _ballot;
    private readonly bool[] _settledBiomes; // biomy, na kterých už tohle město stavělo (kronika)
    private readonly List<int> _activeChallenges = new();   // indexy do fondu výzev
    private readonly List<long> _challengeBaselines = new(); // hodnota metriky při vydání výzvy
    private readonly List<bool> _challengesDone = new();
    private readonly bool[] _questsCompleted;
    private readonly AchievementSystem _achievementSystem;
    private readonly bool[] _achievementsUnlocked;

    private readonly bool[] _buildingUnlocked;
    private readonly int[] _techLevel;
    private readonly int[] _upgradeLevels;      // koupené úrovně trvalých upgradů Vzestupu
    private readonly bool[] _policiesActive;    // zapnuté politiky růstu (automatizace, stupeň 4)
    private readonly long[] _harvestedTotals; // kumulativní sběr surovin klikáním (metriky cílů)
    private readonly bool[] _resourceKnown;   // surovina, kterou hráč už někdy získal (UI ji do té doby neukazuje)
    private readonly HashSet<long> _claimedDiscoveries = new(); // vyzvednuté skrýše na mapě
    private readonly Dictionary<long, ClickYield> _plantedNodes = new(); // hráčem zasazené obnovitelné zdroje
    private readonly NodeLedger _nodes = new(); // co už se v krajině vytěžilo (jen dotčené dlaždice)
    private readonly PollutionGrid _pollution = new(); // stopa průmyslu v krajině (hrubá mřížka)
    private ContractSlot[] _contractSlots = Array.Empty<ContractSlot>(); // nástěnka zakázek
    private readonly List<District> _districts = new(); // rozpoznané čtvrti (odvozený stav)
    private readonly Dictionary<long, long> _founders = new(); // kdo kterou budovu založil (dlaždice → jméno)
    private readonly Dictionary<long, byte> _biomeOverrides = new(); // terraformované dlaždice (UFO)
    private readonly Queue<GameNotification> _notifications = new();
    private readonly PrayerSystem _prayerSystem;

    private PrestigeBonuses _bonuses = PrestigeBonuses.None;

    /// <summary>Násobiče výroby po surovinách z cílených technologií (index = surovina).</summary>
    private readonly double[] _resourceProductionMult;

    private int _boostTicksRemaining;    // slavnost aktivní, dokud > 0
    private int _boostCooldownRemaining;  // dokud > 0, nejde spustit další
    private long _harvestCounter;         // pořadí sběru — seed deterministického kritu
    private long _lastHarvestTick = long.MinValue; // kdy hráč naposled sbíral (kombo)
    private int _comboStreak;             // délka rozjeté série sběrů

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

        _techLevel = new int[content.Techs.Count];
        _upgradeLevels = new int[content.PrestigeUpgrades.Count];
        _policiesActive = new bool[content.Policies.Count];
        _harvestedTotals = new long[content.Resources.Count];

        // Známé suroviny: co má hráč na startu, zná; zbytek se odemyká získáním.
        // UI ani odměny nesmí prozrazovat suroviny, ke kterým se ještě nedostal.
        _resourceKnown = new bool[content.Resources.Count];
        for (int i = 0; i < _resourceKnown.Length; i++)
        {
            _resourceKnown[i] = content.Resources[i].StartAmount > 0;
        }
        RefreshTierUnlocks(); // megastruktury zamčené, dokud měřítko nedoroste

        _resources = new double[content.Resources.Count];
        _storageCaps = new double[content.Resources.Count];
        _resourceProductionMult = new double[content.Resources.Count];
        Array.Fill(_resourceProductionMult, 1.0);
        for (int i = 0; i < _resources.Length; i++)
        {
            _storageCaps[i] = content.Resources[i].BaseStorage;
            _resources[i] = Math.Min(content.Resources[i].StartAmount, _storageCaps[i]);
        }

        Population = content.Gameplay.StartingPopulation;
        HousingCapacity = content.Gameplay.BaseHousingCapacity;

        _production = new ProductionSystem(content);
        _haulSystem = new HaulSystem(content);
        _seasonSystem = new SeasonSystem(content);
        _toolsSystem = new ToolsSystem(content);
        _pollutionSystem = new PollutionSystem(content);
        _contractSystem = new ContractSystem(content, seed);
        _prayerSystem = new PrayerSystem(content, seed);
        NpcCities = new NpcCityMap(seed, content.NpcCities.Archetypes.Count, content.NpcCities.Names.Count);
        _districtSystem = new DistrictSystem(content);
        _citizenSystem = new CitizenSystem(content, seed);
        _milestoneBonuses = new BuildingMilestoneSystem(content);
        _historySystem = new HistorySystem(content);
        _reforestSystem = new ReforestSystem(content);
        _autoTerraformSystem = new AutoTerraformSystem(content);
        _streetPaver = new StreetGridPaver(content);
        _npcTowns = new NpcTownSystem(content);
        _grandWork = new GrandWorkSystem(content.GrandWork, content.Resources.Count);
        _legacy = new LegacySystem(content.Legacy, content.LegacyUpgrades.All);
        History = new CityHistory(content.Gameplay.History.MaxFrames);
        _constructionSystem = new ConstructionSystem(content);
        ResetContractBoard();
        _populationSystem = new PopulationSystem(content.Gameplay);
        _autoBuild = new AutoBuildSystem(content, seed);
        _zoneFill = new ZoneFillSystem(content, seed);
        _colonySystem = new ColonySystem(content, seed);
        _weatherSystem = new WeatherSystem(content, seed);
        _happinessSystem = new HappinessSystem(content);
        _ufoSystem = new UfoSystem(content, seed);
        _roadBuilder = new RoadBuilder(content);
        _settlementSystem = new SettlementSystem(content, seed);
        _questSystem = new QuestSystem(content);
        _tutorialSystem = new TutorialSystem(content);
        _challengeSystem = new ChallengeSystem(content);
        _electionSystem = new ElectionSystem(content);
        _milestoneSystem = new MilestoneSystem(content);
        _milestonesReached = new bool[content.Milestones.Count];
        _ballot = new int[Math.Max(1, content.Elections.BallotSize)];
        _settledBiomes = new bool[content.Biomes.Count];
        _questsCompleted = new bool[content.Quests.Count];
        _achievementSystem = new AchievementSystem(content);
        _achievementsUnlocked = new bool[content.Achievements.Count];

        // Start je vždycky vidět — hráč musí od první vteřiny vědět, kde stojí.
        Fog.Reveal(0, 0, FogRevealRadius * 2);

        PlaceStartingBuildings();
    }

    /// <summary>
    /// Mlha války: co hráč neviděl, je tma. Odhaluje se stavěním a ruční těžbou.
    /// </summary>
    public FogOfWar Fog { get; } = new();

    /// <summary>Kolik dlaždic kolem sebe odhalí budova nebo ruční sběr.</summary>
    public const int FogRevealRadius = 10;

    // ----- cizí města: objevení, obchod, pohlcení -----

    /// <summary>Mapa cizích měst (odvozená ze seedu, neukládá se).</summary>
    public NpcCityMap NpcCities { get; private set; } = null!;

    /// <summary>Co hráč s kterým městem zažil. Ukládá se jen tohle.</summary>
    private readonly Dictionary<long, NpcCityState> _npcStates = new();

    /// <summary>Tik posledního obchodu s městem — kvůli intervalu dodávek.</summary>
    private readonly Dictionary<long, long> _npcLastTrade = new();

    /// <summary>Jsou cizí města v datech zapnutá?</summary>
    public bool NpcCitiesEnabled => _content.NpcCities.IsEnabled;

    /// <summary>Stav vztahu k městu (výchozí = nedotčené).</summary>
    public NpcCityState NpcStateOf(long key) =>
        _npcStates.TryGetValue(key, out var state) ? state : default;

    /// <summary>Kolik cizích měst už je součástí říše (odkupem nebo obestavěním).</summary>
    public int CitiesJoined
    {
        get
        {
            int count = 0;
            foreach (var pair in _npcStates)
            {
                if (pair.Value.Absorbed)
                {
                    count++;
                }
            }

            return count;
        }
    }

    /// <summary>Klíče měst, se kterými už hráč něco měl — pro save i pro seznam.</summary>
    public IReadOnlyDictionary<long, NpcCityState> NpcStates => _npcStates;

    /// <summary>Obnoví vztah k městu ze savu.</summary>
    internal void RestoreNpcState(long key, NpcCityState state) => _npcStates[key] = state;

    /// <summary>
    /// Objevil už hráč tohle město? Objevení se neukládá — plyne z mlhy, takže
    /// se nemůže rozejít s tím, co hráč vidí.
    /// </summary>
    public bool IsCityDiscovered(in NpcCity city) => Fog.IsExplored(city.X, city.Y);

    /// <summary>
    /// Města v dohledu kolem místa (pro mapu i pro seznam sídel).
    ///
    /// <para>Srovnaná města se sem nedostanou. Filtruje se právě tady, na jednom
    /// místě — kdyby to řešil každý volající sám, dřív nebo později by někde
    /// zůstal duch zničeného města.</para>
    /// </summary>
    public IEnumerable<NpcCity> CitiesNear(int tileX, int tileY, int radiusTiles)
    {
        if (!NpcCitiesEnabled)
        {
            yield break;
        }

        foreach (var city in NpcCities.CitiesNear(tileX, tileY, radiusTiles))
        {
            if (!NpcStateOf(city.Key).Destroyed)
            {
                yield return city;
            }
        }
    }

    /// <summary>
    /// Budovy objevených cizích měst — <b>skutečné instance</b>, tytéž jaké staví
    /// hráč. Render je kreslí stejným kódem jako hráčovy; kdyby měl vlastní,
    /// vypadala by cizí města jako jiná hra.
    /// </summary>
    public ReadOnlySpan<BuildingInstance> NpcBuildings => _npcTowns.Buildings;

    /// <summary>Ulice cizích měst a cesty mezi nimi — skutečné silniční dlaždice.</summary>
    public IReadOnlyList<RoadTile> NpcRoadTiles => _npcTowns.RoadTiles;

    /// <summary>
    /// Obdélník, který zástavba cizího města opravdu zabírá (v dlaždicích).
    /// Render z toho kreslí hranici — pevný poloměr u malého města visel v poli
    /// a u velkého vedl jeho středem.
    /// </summary>
    public bool TryNpcTownBounds(long key, out NpcTownSystem.TownBounds bounds) =>
        _npcTowns.TryBounds(key, out bounds);

    /// <summary>Je na dlaždici cizí silnice? (Hráčova síť to není — obchod se z ní neodvozuje.)</summary>
    public bool IsNpcRoad(int x, int y) => _npcTowns.IsRoad(x, y);

    /// <summary>Stojí na dlaždici cizí budova?</summary>
    public bool IsNpcOccupied(int x, int y) => _npcTowns.IsOccupied(x, y);

    /// <summary>
    /// Je na dlaždici silnice, ať už čí. Pro render a chodce: po cizí ulici se
    /// chodí stejně jako po hráčově, i když do jeho sítě nepatří.
    /// </summary>
    public bool HasRoadAt(int x, int y) => IsRoad(x, y) || IsNpcRoad(x, y);

    /// <summary>
    /// Objevené cizí město pod dlaždicí. Trefa se počítá na celé jeho zástavbě,
    /// ne jen na středu — hráč míří na město, ne na jeden pixel.
    /// </summary>
    public bool TryNpcCityAt(int tileX, int tileY, out NpcCity city)
    {
        city = default;
        return NpcCitiesEnabled
            && _npcTowns.TryOwnerAt(tileX, tileY, out long key)
            && NpcCities.TryCityByKey(key, out city);
    }

    /// <summary>
    /// Postaví objevená cizí města v okolí a cesty mezi nimi.
    ///
    /// <para>Staví se až po objevení, a ne dřív: město za mlhou by muselo řešit
    /// kolize se zástavbou, kterou hráč teprve postaví. Běží na nízké frekvenci
    /// nad hrubou mřížkou měst — postavené město se pak už jen kreslí.</para>
    /// </summary>
    private void TickNpcTowns()
    {
        if (!NpcCitiesEnabled || TickCount % NpcTownIntervalTicks != 0)
        {
            return;
        }

        foreach (var city in NpcCities.CitiesNear(CityCenterX, CityCenterY, NpcScanRadius))
        {
            var state = NpcStateOf(city.Key);
            if (state.Absorbed || state.Destroyed || _npcTowns.IsBuilt(city.Key)
                || !IsCityDiscovered(city))
            {
                continue;
            }

            _npcTowns.Build(Seed, city, NpcCanBuild, NpcCanPave);
        }

        foreach (var link in NpcCities.LinksNear(CityCenterX, CityCenterY, NpcScanRadius))
        {
            // Stačí, aby hráč znal JEDEN konec. Dřív se chtěly oba a hráč tak
            // cestu mezi městy prakticky nikdy neviděl — města jsou 96 dlaždic
            // od sebe a objevit dvě sousední najednou se skoro nestane. Vzdálený
            // konec stejně schová mlha, takže z města vede silnice do neznáma —
            // a to je pozvánka, ať se hráč vydá po ní.
            if (_npcTowns.IsLinked(link.From.Key, link.To.Key)
                || (!IsCityDiscovered(link.From) && !IsCityDiscovered(link.To)))
            {
                continue;
            }

            _npcTowns.Link(link, TownCenter(link.From), TownCenter(link.To), NpcCanPave);
        }
    }

    /// <summary>
    /// Odkud cesta k městu vede. Postavené město zná svůj skutečný střed (při
    /// stavbě se posouvá na suchou zem), takže cesta začíná v něm, ne v bodě
    /// z mřížky — jinak by prvních pár dlaždic končilo v moři.
    /// </summary>
    private (int X, int Y) TownCenter(in NpcCity city) =>
        _npcTowns.TryBounds(city.Key, out var b)
            ? ((b.MinX + b.MaxX) / 2, (b.MinY + b.MaxY) / 2)
            : (city.X, city.Y);

    /// <summary>
    /// Dosáhla hráčova silniční síť na cizí město? Pak se spojení naváže samo
    /// a zdarma.
    ///
    /// <para>Tohle hráči chybělo: postavil silnici až k městu a <b>nestalo se
    /// nic</b>, protože spojení šlo koupit jedině tlačítkem v menu. Cesta, která
    /// tam fyzicky vede, je ale ten nejpřirozenější způsob, jak spojení navázat —
    /// tlačítko zůstává jako zkratka pro toho, kdo stavět nechce.</para>
    /// </summary>
    private bool PlayerRoadReaches(long key)
    {
        if (!_npcTowns.TryBounds(key, out var b))
        {
            return false;
        }

        // Prohledá se pás kolem zástavby: až sem musí hráč silnici dotáhnout.
        for (int y = b.MinY - RoadReachSlack; y <= b.MaxY + RoadReachSlack; y++)
        {
            for (int x = b.MinX - RoadReachSlack; x <= b.MaxX + RoadReachSlack; x++)
            {
                if (_roads.Contains(TileKey.Pack(x, y)))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>O kolik dlaždic smí hráčova silnice minout okraj města a pořád platit.</summary>
    private const int RoadReachSlack = 2;

    /// <summary>
    /// Smí cizí město postavit tuhle budovu na tomhle místě? Ptá se jen na terén
    /// a na kolize — <b>ne</b> na hráčův výzkum ani jeho suroviny. Cizí město
    /// stálo dřív, než tam hráč přišel.
    /// </summary>
    private bool NpcCanBuild(int defIndex, int x, int y)
    {
        var def = _content.Buildings[defIndex];

        for (int tileY = y; tileY < y + def.FootprintHeight; tileY++)
        {
            for (int tileX = x; tileX < x + def.FootprintWidth; tileX++)
            {
                long tile = TileKey.Pack(tileX, tileY);
                if (_occupancy.ContainsKey(tile) || _roads.Contains(tile)
                    || _plantedNodes.ContainsKey(tile) || _npcTowns.Blocks(tileX, tileY))
                {
                    return false;
                }

                if (!def.IsBiomeAllowed(Terrain.BiomeAt(tileX, tileY)))
                {
                    return false;
                }
            }
        }

        return !def.NeedsWaterAccess || HasAdjacentWater(def, x, y);
    }

    /// <summary>Jak daleko od středu pohlceného města si sídlo ještě nese jeho jméno.</summary>
    private const int InheritedNameRadius = 20;

    /// <summary>
    /// Jméno zděděné po pohlceném cizím městě poblíž místa; <c>−1</c> = žádné.
    ///
    /// <para>Když se město přidá k říši, jeho domy zůstanou stát — a sídlo, které
    /// z nich <see cref="SettlementSystem"/> pozná, si má nechat jeho jméno.
    /// Bez tohohle by se hráči po pohlcení „založilo vedle další město": tytéž
    /// domy, ale s náhodným jménem z klobouku, takže to vypadalo, že původní
    /// zmizelo.</para>
    ///
    /// <para>Neukládá se: pohlcení měst v savu je, poloha i jméno města plynou
    /// ze seedu, takže se to po načtení dopočítá stejně.</para>
    /// </summary>
    public int InheritedNameAt(float x, float y)
    {
        if (!NpcCitiesEnabled)
        {
            return -1;
        }

        foreach (var (key, state) in _npcStates)
        {
            if (!state.Absorbed || !NpcCities.TryCityByKey(key, out var city))
            {
                continue;
            }

            if (Math.Abs(city.X - x) <= InheritedNameRadius && Math.Abs(city.Y - y) <= InheritedNameRadius)
            {
                return city.NameIndex;
            }
        }

        return -1;
    }

    /// <summary>Smí na dlaždici ležet cizí cesta? Přes vodu ne — most by tam nikdo nestavěl.</summary>
    private bool NpcCanPave(int x, int y) =>
        !_occupancy.ContainsKey(TileKey.Pack(x, y))
        && !_roads.Contains(TileKey.Pack(x, y))
        && !_npcTowns.IsOccupied(x, y)
        && !_content.Biomes[Terrain.BiomeAt(x, y)].IsWater;

    /// <summary>Cesty mezi cizími městy v okolí — svět si žije i bez hráče.</summary>
    public IEnumerable<NpcCityLink> CityLinksNear(int tileX, int tileY, int radiusTiles) =>
        NpcCitiesEnabled ? NpcCities.LinksNear(tileX, tileY, radiusTiles) : Array.Empty<NpcCityLink>();

    /// <summary>
    /// Dar cizímu městu: zaplatíš a vztah povyroste. Nejlevnější způsob, jak se
    /// k městu dostat blíž — a jediný, který jde použít hned po objevení.
    /// </summary>
    public DiplomacyResult TryGiftCity(long key)
    {
        if (!NpcCitiesEnabled || !NpcCities.TryCityByKey(key, out _))
        {
            return DiplomacyResult.Unavailable;
        }

        var state = NpcStateOf(key);
        if (state.Destroyed)
        {
            return DiplomacyResult.Unavailable; // po tom, co po něm zbylo, se jednat nedá
        }

        if (state.Absorbed)
        {
            return DiplomacyResult.AlreadyYours;
        }

        var catalog = _content.NpcCities;
        var gift = ScaledCost(catalog.GiftCost, CityScale(key));
        if (!CanPay(gift))
        {
            return DiplomacyResult.NotEnoughResources;
        }

        Pay(gift);
        state.Relation = Math.Min(MaxRelation, state.Relation + catalog.GiftRelation);
        _npcStates[key] = state;
        return DiplomacyResult.Ok;
    }

    /// <summary>
    /// Postaví cestu k městu z nejbližšího vlastního sídla.
    /// Zkratka pro <see cref="TryConnectCity(long, int, int)"/>.
    /// </summary>
    public DiplomacyResult TryConnectCity(long key)
    {
        if (!NpcCities.TryCityByKey(key, out var city))
        {
            return DiplomacyResult.Unavailable;
        }

        var (fromX, fromY) = NearestOwnOrigin(city.X, city.Y);
        return TryConnectCity(key, fromX, fromY);
    }

    /// <summary>
    /// Postaví cestu k městu z určeného místa. Bez cesty se obchodovat nedá —
    /// a to je celý důvod, proč silnice v téhle hře nejsou dekorace.
    ///
    /// <para>Cesta se <b>opravdu položí</b>, dlaždici po dlaždici. Dřív se jen
    /// zaplatilo a městu se nastavil příznak „spojeno": na mapě po tom nezbylo
    /// nic a tlačítko vypadalo rozbitě. Trasu hledá
    /// <see cref="CityRoadPlanner"/> — rovně, s objížďkami kolem překážek.</para>
    ///
    /// <para>Odkud se cesta vede, je parametr schválně: hráč s víc sídly si
    /// vybírá sám. Silnice napříč celou říší z náhodného konce mapy je poslední
    /// věc, kterou od tlačítka čeká.</para>
    /// </summary>
    /// <param name="fromX">Odkud cesta vychází (dlaždice vlastního sídla).</param>
    /// <param name="fromY">Odkud cesta vychází (dlaždice vlastního sídla).</param>
    public DiplomacyResult TryConnectCity(long key, int fromX, int fromY)
    {
        if (!NpcCitiesEnabled || !NpcCities.TryCityByKey(key, out var city))
        {
            return DiplomacyResult.Unavailable;
        }

        var state = NpcStateOf(key);
        if (state.Destroyed)
        {
            return DiplomacyResult.Unavailable; // po tom, co po něm zbylo, se jednat nedá
        }

        if (state.Absorbed)
        {
            return DiplomacyResult.AlreadyYours;
        }

        if (state.RoadLinked)
        {
            return DiplomacyResult.Ok;
        }

        if (!CanPay(_content.NpcCities.RoadCost))
        {
            return DiplomacyResult.NotEnoughResources;
        }

        // Trasa se hledá PŘED zaplacením: za cestu, která nevede (oceán mezi
        // hráčem a městem), se platit nebude.
        var route = new List<(int X, int Y)>();
        if (!_cityRoadPlanner.TryTrace(this, fromX, fromY, city.X, city.Y, route))
        {
            return DiplomacyResult.NoRoute;
        }

        Pay(_content.NpcCities.RoadCost);
        foreach (var (tileX, tileY) in route)
        {
            AddRoadTile(tileX, tileY);
        }

        state.RoadLinked = true;
        _npcStates[key] = state;
        return DiplomacyResult.Ok;
    }

    /// <summary>
    /// Jak velké je cizí město v porovnání s tím nejmenším. 1,0 = nejmenší
    /// archetyp, výš roste s počtem obyvatel.
    ///
    /// <para>Dřív stálo všechno stejně: dar metropoli o sto padesáti lidech vyšel
    /// na tolik co dar vesnici o čtyřiceti, a obestavět šlo obojí dvanácti domy.
    /// Diplomacie tím ztratila rozhodování — nebyl důvod začínat u malých.</para>
    ///
    /// <para>Škáluje se podle <b>populace archetypu</b>, ne podle postavených
    /// budov: plán města je čistá funkce seedu, takže je cena stabilní a hráč se
    /// na ni může připravit.</para>
    /// </summary>
    public double CityScale(long key)
    {
        if (!NpcCities.TryCityByKey(key, out var city))
        {
            return 1.0;
        }

        var archetypes = _content.NpcCities.Archetypes;
        double smallest = double.MaxValue;
        for (int i = 0; i < archetypes.Count; i++)
        {
            smallest = Math.Min(smallest, Math.Max(1, archetypes[i].Population));
        }

        double population = Math.Max(1, archetypes[city.ArchetypeIndex].Population);
        return population / smallest;
    }

    /// <summary>Kolik stojí dar tomuhle městu. UI musí ukazovat tuhle cenu, ne tu z dat.</summary>
    public IReadOnlyList<ResourceAmount> GiftCostFor(long key) =>
        ScaledCost(_content.NpcCities.GiftCost, CityScale(key));

    /// <summary>Kolik stojí odkoupit tohle město.</summary>
    public IReadOnlyList<ResourceAmount> BuyCostFor(long key) =>
        ScaledCost(_content.NpcCities.BuyCost, Math.Pow(CityScale(key), 1.6));

    /// <summary>Kolik vlastních budov kolem města je potřeba, aby srostlo s říší.</summary>
    public int SurroundBuildingsFor(long key) =>
        (int)Math.Ceiling(_content.NpcCities.SurroundBuildings * CityScale(key));

    /// <summary>Cena vynásobená a zaokrouhlená nahoru — z ceny nesmí vyjít nula.</summary>
    private static ResourceAmount[] ScaledCost(IReadOnlyList<ResourceAmount> cost, double scale)
    {
        var scaled = new ResourceAmount[cost.Count];
        for (int i = 0; i < cost.Count; i++)
        {
            scaled[i] = new ResourceAmount(
                cost[i].ResourceIndex, (int)Math.Ceiling(cost[i].Amount * scale));
        }

        return scaled;
    }

    /// <summary>
    /// Odkud vést cestu, když si hráč nevybral: z nejbližšího vlastního sídla,
    /// jinak ze středu města. Vzdálenost se měří k cílovému městu, ne k hráči —
    /// nejkratší cesta je i ta nejlevnější na pohled.
    /// </summary>
    public (int X, int Y) NearestOwnOrigin(int towardX, int towardY)
    {
        var best = (X: CityCenterX, Y: CityCenterY);
        long bestDistance = long.MaxValue;

        foreach (var settlement in Settlements)
        {
            int x = (int)settlement.CenterX;
            int y = (int)settlement.CenterY;
            long dx = x - towardX;
            long dy = y - towardY;
            long distance = (dx * dx) + (dy * dy);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = (x, y);
            }
        }

        return best;
    }

    /// <summary>
    /// Odkoupí město. Chce vysoký vztah <b>i</b> hodně surovin: koupit se dá
    /// jen to, co už tě má rádo — jinak by z diplomacie byl jen nákup.
    /// </summary>
    public DiplomacyResult TryBuyCity(long key)
    {
        if (!NpcCitiesEnabled || !NpcCities.TryCityByKey(key, out var city))
        {
            return DiplomacyResult.Unavailable;
        }

        var state = NpcStateOf(key);
        if (state.Destroyed)
        {
            return DiplomacyResult.Unavailable; // po tom, co po něm zbylo, se jednat nedá
        }

        if (state.Absorbed)
        {
            return DiplomacyResult.AlreadyYours;
        }

        if (state.Relation < _content.NpcCities.BuyRelation)
        {
            return DiplomacyResult.RelationTooLow;
        }

        // Odkup roste s velikostí města strměji než dar: metropoli si nemá jít
        // koupit za tolik co vesnici jen proto, že hráč nasyslil.
        var price = ScaledCost(_content.NpcCities.BuyCost, Math.Pow(CityScale(key), 1.6));
        if (!CanPay(price))
        {
            return DiplomacyResult.NotEnoughResources;
        }

        Pay(price);
        AbsorbCity(key, city);
        return DiplomacyResult.Ok;
    }

    /// <summary>
    /// Město se stalo součástí říše: přinese své lidi a přestane být cizí.
    /// Volá se z odkupu i z obestavění.
    /// </summary>
    private void AbsorbCity(long key, in NpcCity city)
    {
        var state = NpcStateOf(key);
        state.Absorbed = true;
        state.Relation = 100;
        _npcStates[key] = state;

        var archetype = _content.NpcCities.Archetypes[city.ArchetypeIndex];
        Population += archetype.Population;
        Fog.Reveal(city.X, city.Y, FogRevealRadius);

        // Hráč město nedostane jako číslo, ale jako MĚSTO: jeho domy a ulice
        // přejdou do jeho vlastnictví. To je celý smysl obestavění — sroste to
        // s jeho zástavbou, ne že se někde připíše populace.
        //
        // Objevit se ještě nemuselo (obestavět jde i město za mlhou) — pak se
        // teď postaví, aby bylo co předávat.
        if (!_npcTowns.IsBuilt(key))
        {
            _npcTowns.Build(Seed, city, NpcCanBuild, NpcCanPave);
        }

        HandOverTown(key);

        EnqueueNotification(new GameNotification(
            NotificationKind.CityJoined, "toast.cityJoined", archetype.NameKey));
    }

    /// <summary>
    /// Převede budovy a ulice cizího města hráči.
    ///
    /// <para><b>Nic se nestaví znovu.</b> Jsou to tytéž instance, jaké ve světě
    /// stály — jen změní majitele. Dřív se tady volalo <c>CanPlace</c>, což znamenalo,
    /// že se nepředalo skoro nic: cizí město stojí i z budov, které hráč ještě nemá
    /// vyzkoumané, a ty všechny umístění zamítlo. Město tak zmizelo a hráči
    /// nezůstalo nic než přičtená populace.</para>
    /// </summary>
    private void HandOverTown(long key)
    {
        var handover = _npcTowns.Take(key);

        for (int i = 0; i < handover.Roads.Count; i++)
        {
            AddRoadTile(handover.Roads[i].X, handover.Roads[i].Y);
        }

        for (int i = 0; i < handover.Buildings.Count; i++)
        {
            AdoptBuilding(handover.Buildings[i]);
        }

        SettlementsDirty = true;
        DistrictsDirty = true; // nová čtvrť zástavby může vzniknout i zaniknout
        HaulDirty = true;      // sklady připojeného města mění svoz i okolí
        _roadLinksDirty = true;
        RecomputeDerivedState();
    }

    /// <summary>
    /// Přijme hotovou budovu do říše bez placení a bez kontroly umístění.
    ///
    /// <para>Kontrolovat se nemá co: budova na tom místě už stojí a hráč ji
    /// dostává, ne staví. Násobiče se dopočítají tady — cizímu městu byly
    /// k ničemu, hráči na nich záleží.</para>
    /// </summary>
    private void AdoptBuilding(in BuildingInstance instance)
    {
        AddBuilding(instance.DefIndex, instance.X, instance.Y, progress: 0f, asConstructionSite: false);
    }

    /// <summary>
    /// Dodávky od spřátelených měst a tiché pohlcování obestavěných.
    ///
    /// <para>Běží na nízké frekvenci nad městy v okolí, ne nad celou mapou —
    /// cizích měst je nekonečně mnoho, ale zajímavá jsou jen ta blízko.</para>
    /// </summary>
    private void TickNpcCities()
    {
        var catalog = _content.NpcCities;
        if (!catalog.IsEnabled || TickCount % catalog.TradeIntervalTicks != 0)
        {
            return;
        }

        foreach (var city in NpcCities.CitiesNear(CityCenterX, CityCenterY, NpcScanRadius))
        {
            var state = NpcStateOf(city.Key);
            if (state.Absorbed || state.Destroyed)
            {
                continue;
            }

            // Obestavěné město sroste s hráčovým samo — nikdo ho nedobývá,
            // jen se kolem něj rozrostlo město a hranice přestala dávat smysl.
            int needed = (int)Math.Ceiling(catalog.SurroundBuildings * CityScale(city.Key));
            if (CountOwnBuildingsAround(city.X, city.Y, catalog.SurroundRadius) >= needed)
            {
                AbsorbCity(city.Key, city);
                continue;
            }

            if (!IsCityDiscovered(city))
            {
                continue;
            }

            // Silnice, kterou hráč opravdu postavil, spojení naváže sama.
            if (!state.RoadLinked && PlayerRoadReaches(city.Key))
            {
                state.RoadLinked = true;
                _npcStates[city.Key] = state;
                EnqueueNotification(new GameNotification(
                    NotificationKind.CityLinked, "toast.cityLinked",
                    _content.NpcCities.Archetypes[city.ArchetypeIndex].NameKey));
            }

            if (!HasTradeLink(city, state))
            {
                continue;
            }

            var trade = catalog.Archetypes[city.ArchetypeIndex].Trade;
            for (int i = 0; i < trade.Count; i++)
            {
                AddResource(trade[i].ResourceIndex, trade[i].Amount);
            }

            state.Trades++;
            state.Relation = Math.Min(100, state.Relation + 1); // obchod sbližuje
            _npcStates[city.Key] = state;
        }
    }

    /// <summary>Jak daleko se cizí města ještě řeší (dál by to byla práce nazmar).</summary>
    private const int NpcScanRadius = NpcCityMap.CellTiles * 3;

    /// <summary>
    /// Jak často se objevená cizí města staví. Sekunda: dost rychle na to, aby
    /// město stálo, než hráč doroluje, a dost pomalu na to, aby procházení hrubé
    /// mřížky měst nic nestálo.
    /// </summary>
    private const int NpcTownIntervalTicks = (int)TicksPerSecond;

    /// <summary>Nejvyšší možný vztah s cizím městem. UI ho ukazuje jako „x / 100".</summary>
    public const int MaxRelation = 100;

    /// <summary>
    /// Dá se s městem obchodovat? Buď vede cesta, nebo obě strany leží u vody
    /// a hráč má přístav — moře je cesta, kterou nikdo nemusel stavět.
    /// </summary>
    private bool HasTradeLink(in NpcCity city, in NpcCityState state) =>
        state.RoadLinked || (IsWaterNextTo(city.X, city.Y) && HasHarbour());

    /// <summary>Má hráč přístav? (Námořní obchod bez něj nedává smysl.)</summary>
    private bool HasHarbour()
    {
        for (int i = 0; i < _buildingCount; i++)
        {
            if (_buildings[i].IsComplete && _content.Buildings[_buildings[i].DefIndex].NeedsWaterAccess)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Kolik hráčových budov stojí v okruhu kolem místa.</summary>
    private int CountOwnBuildingsAround(int centerX, int centerY, int radius)
    {
        int count = 0;
        for (int i = 0; i < _buildingCount; i++)
        {
            if (WithinRadius(_buildings[i].X, _buildings[i].Y, centerX, centerY, radius))
            {
                count++;
            }
        }

        return count;
    }

    private bool CanPay(IReadOnlyList<ResourceAmount> cost)
    {
        for (int i = 0; i < cost.Count; i++)
        {
            if (_resources[cost[i].ResourceIndex] < cost[i].Amount)
            {
                return false;
            }
        }

        return true;
    }

    private void Pay(IReadOnlyList<ResourceAmount> cost)
    {
        for (int i = 0; i < cost.Count; i++)
        {
            _resources[cost[i].ResourceIndex] -= cost[i].Amount;
        }
    }

    // ----- víra: modlitby, požehnání a zásahy -----

    /// <summary>Jak dlouho vydrží požehnání ze zodpovězené modlitby.</summary>
    private const int BlessingTicks = (int)(TicksPerSecond * 120);

    private int _rainTicksLeft;
    private double _rainMagnitude;
    private int _growthTicksLeft;
    private double _growthMagnitude;

    /// <summary>Běží déšť z vyslyšené modlitby?</summary>
    public bool RainBlessingActive => _rainTicksLeft > 0;

    /// <summary>Běží plodnost z vyslyšené modlitby?</summary>
    public bool GrowthBlessingActive => _growthTicksLeft > 0;

    /// <summary>Násobič výroby jídla z deště (1.0 = beze změny).</summary>
    public double RainFoodMult => _rainTicksLeft > 0 ? 1.0 + _rainMagnitude : 1.0;

    /// <summary>Násobič růstu obyvatel z plodnosti (1.0 = beze změny).</summary>
    public double BlessedGrowthMult => _growthTicksLeft > 0 ? 1.0 + _growthMagnitude : 1.0;

    /// <summary>Je víra v datech zapnutá?</summary>
    public bool FaithEnabled => _content.Faith.IsEnabled;

    /// <summary>Nejvyšší síla, na kterou jde modlitbu vystupňovat.</summary>
    public const int MaxPrayerStrength = PrayerSystem.MaxStrength;

    /// <summary>Kolikrát se hráč modlil (statistika, kronika, achievementy).</summary>
    public long PrayerCount => _prayerSystem.PrayerCount;

    /// <summary>Obnoví počítadlo modliteb ze savu.</summary>
    internal void RestorePrayerCount(long count) => _prayerSystem.RestoreCount(count);

    /// <summary>
    /// Příkaz hráče: pronést modlitbu. Víra se obětuje předem, výsledek je
    /// nejistý — v tom je celé to rozhodnutí.
    /// </summary>
    public PrayerOutcome TryPray(int prayerIndex, int strength, int targetX, int targetY) =>
        _prayerSystem.Pray(this, prayerIndex, strength, targetX, targetY);

    /// <summary>
    /// Kam naposledy dopadla rána z modlitby (dlaždice).
    ///
    /// <para>Render podle toho umístí výbuch. Nestačí vzít místo, kam hráč klikl:
    /// když rána mine, spadne jinam — a podívaná musí být tam, kde se to opravdu
    /// stalo, jinak by hráč hledal kráter na špatném konci mapy.</para>
    /// </summary>
    public (int X, int Y) LastStrikeTile { get; private set; }

    /// <summary>Zaznamená místo dopadu (volá <see cref="PrayerSystem"/> těsně před účinkem).</summary>
    internal void ReportStrike(int x, int y) => LastStrikeTile = (x, y);

    /// <summary>Zapne dočasné požehnání (volá se z účinku modlitby).</summary>
    internal void StartBlessing(BlessingKind kind, double magnitude, int radiusTiles, int targetX, int targetY)
    {
        _ = radiusTiles; // požehnání platí pro celé město; cíl je zatím jen místo obřadu
        _ = targetX;
        _ = targetY;
        if (kind == BlessingKind.Rain)
        {
            _rainTicksLeft = BlessingTicks;
            _rainMagnitude = magnitude;
            return;
        }

        _growthTicksLeft = BlessingTicks;
        _growthMagnitude = magnitude;
    }

    /// <summary>Očistí okolí od znečištění — odpověď na modlitbu za čistou zemi.</summary>
    internal void CleanseArea(int centerX, int centerY, int radiusTiles, double magnitude)
    {
        int step = PollutionGrid.CellTiles;
        for (int y = centerY - radiusTiles; y <= centerY + radiusTiles; y += step)
        {
            for (int x = centerX - radiusTiles; x <= centerX + radiusTiles; x += step)
            {
                foreach (var kind in new[] { PollutionKind.Air, PollutionKind.Water, PollutionKind.Soil })
                {
                    _pollution.Emit(x, y, kind, -_pollution.At(x, y, kind) * Math.Clamp(magnitude, 0, 1));
                }
            }
        }
    }

    /// <summary>
    /// Meteorit: srovná zástavbu v okolí cíle a spálí krajinu. Je to trest,
    /// ne úklid — proto bourá bez náhrady.
    ///
    /// <para>Zasáhne i cizí města, a to je celý smysl té modlitby: hráč má mít
    /// jedno místo ve hře, kde může někomu jinému doopravdy ublížit. Rána na
    /// holé pláni taky není nic — po dopadu zůstane spáleniště, aby bylo vidět,
    /// že se něco stalo.</para>
    /// </summary>
    /// <returns>Kolik věcí (budov a měst) rána srovnala — UI z toho skládá hlášku.</returns>
    internal int StrikeMeteor(int centerX, int centerY, int radiusTiles)
    {
        int hits = 0;

        // Odzadu, protože demolice přerovnává pole budov.
        for (int i = _buildingCount - 1; i >= 0; i--)
        {
            if (WithinRadius(_buildings[i].X, _buildings[i].Y, centerX, centerY, radiusTiles))
            {
                TryDemolish(i);
                hits++;
            }
        }

        hits += StrikeCities(centerX, centerY, radiusTiles, waterOnly: false);

        // Po kameni z vesmíru nezůstane obyčejné spáleniště, ale zamořená půda:
        // nedá se na ní stavět (kromě uranového dolu) a dokud ji hráč
        // nedekontaminuje, je z kráteru rána v mapě, ne kosmetika.
        ScorchGround(centerX, centerY, radiusTiles,
            FalloutBiomeIndex >= 0 ? FalloutBiomeIndex : ScorchedBiomeIndex);

        Fog.Reveal(centerX, centerY, radiusTiles + 4); // ránu je vidět zdaleka
        return hits;
    }

    /// <summary>
    /// Povodeň: zaplaví okolí — co stojí u vody, bere první ránu. Na rozdíl od
    /// meteoritu nespálí krajinu, ale zvedne hladinu: pár dlaždic u břehu zůstane
    /// pod vodou natrvalo.
    /// </summary>
    /// <returns>Kolik věcí voda vzala.</returns>
    internal int StrikeFlood(int centerX, int centerY, int radiusTiles)
    {
        // Napřed se spočítá, KAM voda dosáhne, a teprve pak se boří. Dřív se
        // ptalo u každé budovy „stojíš u vody?", takže povodeň vypadala jako
        // rozbitá: mimo pobřeží nedělala doslova nic a hráč nevěděl proč.
        var flooded = SpreadWater(centerX, centerY, radiusTiles);
        if (flooded.Count == 0)
        {
            // Daleko od vody není co vylít. Řekni to — tichá modlitba, která
            // spolkla víru a nic neudělala, je horší než odmítnutí.
            EnqueueNotification(new GameNotification(
                NotificationKind.WorldEvent, "toast.floodNoWater", string.Empty));
            return 0;
        }

        int hits = 0;
        for (int i = _buildingCount - 1; i >= 0; i--)
        {
            if (flooded.Contains(TileKey.Pack(_buildings[i].X, _buildings[i].Y)))
            {
                TryDemolish(i);
                hits++;
            }
        }

        hits += StrikeCities(centerX, centerY, radiusTiles, waterOnly: true);

        foreach (long tile in flooded)
        {
            SetBiomeOverride(TileKey.X(tile), TileKey.Y(tile), (byte)FloodedBiomeIndex);
        }

        CleanseArea(centerX, centerY, radiusTiles, 0.5); // voda po sobě aspoň uklidí
        Fog.Reveal(centerX, centerY, radiusTiles + 2);
        return hits;
    }

    /// <summary>
    /// Kam se voda rozlije: vlna od hladiny do vnitrozemí.
    ///
    /// <para>Je to průchod do šířky od vodních dlaždic uvnitř dosahu, ne test
    /// „sousedí s vodou" u každé dlaždice zvlášť. Ten předchozí přístup uměl
    /// zaplavit jedinou řadu u břehu, takže povodeň nebyla skoro vidět —
    /// a ve vnitrozemí nedělala nic.</para>
    ///
    /// <para>Voda teče jen tam, kam by tekla: dosah do vnitrozemí je poloviční
    /// oproti dosahu modlitby, aby z každé povodně nevzniklo jezero.</para>
    /// </summary>
    private HashSet<long> SpreadWater(int centerX, int centerY, int radiusTiles)
    {
        var flooded = new HashSet<long>();
        if (FloodedBiomeIndex < 0)
        {
            return flooded;
        }

        var frontier = new Queue<(int X, int Y, int Depth)>();
        var seen = new HashSet<long>();

        // Startuje se od vody uvnitř dosahu.
        for (int y = centerY - radiusTiles; y <= centerY + radiusTiles; y++)
        {
            for (int x = centerX - radiusTiles; x <= centerX + radiusTiles; x++)
            {
                if (WithinRadius(x, y, centerX, centerY, radiusTiles) && IsWaterAt(x, y))
                {
                    frontier.Enqueue((x, y, 0));
                    seen.Add(TileKey.Pack(x, y));
                }
            }
        }

        int maxDepth = Math.Max(1, radiusTiles / 2);
        while (frontier.Count > 0)
        {
            var (x, y, depth) = frontier.Dequeue();
            if (depth >= maxDepth)
            {
                continue;
            }

            foreach (var (dx, dy) in Neighbours)
            {
                int nx = x + dx;
                int ny = y + dy;
                long key = TileKey.Pack(nx, ny);
                if (!seen.Add(key) || !WithinRadius(nx, ny, centerX, centerY, radiusTiles) || IsWaterAt(nx, ny))
                {
                    continue;
                }

                flooded.Add(key);
                frontier.Enqueue((nx, ny, depth + 1));
            }
        }

        return flooded;
    }

    /// <summary>Čtyři ortogonální směry — voda neteče po úhlopříčce.</summary>
    private static readonly (int X, int Y)[] Neighbours = { (1, 0), (-1, 0), (0, 1), (0, -1) };

    /// <summary>
    /// Srovná cizí města v dosahu rány. <paramref name="waterOnly"/> = vezme jen
    /// ta u vody (povodeň).
    /// </summary>
    private int StrikeCities(int centerX, int centerY, int radiusTiles, bool waterOnly)
    {
        if (!NpcCitiesEnabled)
        {
            return 0;
        }

        int hits = 0;
        foreach (var city in NpcCities.CitiesNear(centerX, centerY, radiusTiles + NpcCityMap.CellTiles))
        {
            if (!WithinRadius(city.X, city.Y, centerX, centerY, radiusTiles))
            {
                continue;
            }

            var state = NpcStateOf(city.Key);
            if (state.Destroyed || (waterOnly && !IsWaterNextTo(city.X, city.Y)))
            {
                continue;
            }

            state.Destroyed = true;
            state.RoadLinked = false;
            state.Relation = 0;
            _npcStates[city.Key] = state;
            _npcTowns.Forget(city.Key); // po ráně tam nesmí zůstat stát domy
            hits++;

            EnqueueNotification(new GameNotification(
                NotificationKind.CityStruck, "toast.cityStruck",
                _content.NpcCities.Archetypes[city.ArchetypeIndex].NameKey));
        }

        return hits;
    }

    /// <summary>
    /// Spáleniště po dopadu: stromy a žíly shoří, hlína zčerná. Kráter je menší
    /// než dosah rány — jinak by z mapy zbyla po pár modlitbách poušť.
    /// </summary>
    private void ScorchGround(int centerX, int centerY, int radiusTiles, int craterBiomeIndex)
    {
        int crater = Math.Max(1, radiusTiles / 2);
        for (int y = centerY - radiusTiles; y <= centerY + radiusTiles; y++)
        {
            for (int x = centerX - radiusTiles; x <= centerX + radiusTiles; x++)
            {
                _nodes.Deplete(x, y, TickCount); // les shoří, ale časem doroste

                if (WithinRadius(x, y, centerX, centerY, crater)
                    && craterBiomeIndex >= 0
                    && !_content.Biomes[Terrain.BiomeAt(x, y)].IsWater)
                {
                    SetBiomeOverride(x, y, (byte)craterBiomeIndex);
                }
            }
        }
    }

    /// <summary>Zaplavené pobřeží: dlaždice u vody zůstanou pod hladinou.</summary>
    private void RaiseWater(int centerX, int centerY, int radiusTiles)
    {
        if (FloodedBiomeIndex < 0)
        {
            return;
        }

        for (int y = centerY - radiusTiles; y <= centerY + radiusTiles; y++)
        {
            for (int x = centerX - radiusTiles; x <= centerX + radiusTiles; x++)
            {
                if (!_content.Biomes[Terrain.BiomeAt(x, y)].IsWater && IsWaterNextTo(x, y))
                {
                    SetBiomeOverride(x, y, (byte)FloodedBiomeIndex);
                }
            }
        }
    }

    /// <summary>Biom spáleniště; −1 = data ho nemají a kráter se nekreslí.</summary>
    private int ScorchedBiomeIndex =>
        _content.Biomes.TryIndexOf("badlands", out int index) ? index : -1;

    /// <summary>Zamořená půda po dopadu; −1 = data ji nemají a zbude obyčejné spáleniště.</summary>
    private int FalloutBiomeIndex =>
        _content.Biomes.TryIndexOf("fallout", out int index) ? index : -1;

    /// <summary>Biom mělčiny, kterou po sobě nechá povodeň.</summary>
    private int FloodedBiomeIndex =>
        _content.Biomes.TryIndexOf("shallow_water", out int index) ? index : -1;

    /// <summary>
    /// Vrátí vytěženou dlaždici do plného stavu. Vrací <c>false</c>, když tam
    /// není co obnovovat — dlaždice je zastavěná, je pod ní voda, nebo se na ní
    /// nikdy netěžilo.
    ///
    /// <para>Volá to reforestace; ruční sázení má vlastní cestu, protože kromě
    /// obnovy uzlu ještě zakládá nový druh.</para>
    /// </summary>
    internal bool TryRestoreTerrain(int x, int y)
    {
        long tile = TileKey.Pack(x, y);
        if (_occupancy.ContainsKey(tile) || _plantedNodes.ContainsKey(tile))
        {
            return false;
        }

        var yield = _content.Biomes[BiomeAt(x, y)].ClickYield;
        if (yield is null || !yield.IsExhaustible || _nodes.IsAvailable(x, y, yield, TickCount))
        {
            return false; // buď tu není co těžit, nebo je dlaždice plná
        }

        _nodes.Restore(x, y);
        return true;
    }

    /// <summary>Jak často se rozhlížejí pátrací stanice (tiky). Pomalý systém.</summary>
    private const int ScoutIntervalTicks = 50;

    /// <summary>
    /// Pátrací stanice (balony, radary) odhalují mlhu kolem sebe.
    ///
    /// <para>Odhaluje se <b>po kruzích ven</b>, ne celý okruh naráz: radar
    /// s dosahem 60 dlaždic by jedním voláním prošel přes deset tisíc dlaždic.
    /// Takhle se svět otevírá postupně, což navíc vypadá jako pátrání a ne jako
    /// mávnutí proutkem.</para>
    /// </summary>
    private void TickScouting()
    {
        if (TickCount % ScoutIntervalTicks != 0)
        {
            return;
        }

        for (int i = 0; i < _buildingCount; i++)
        {
            ref readonly var building = ref _buildings[i];
            var def = _content.Buildings[building.DefIndex];
            if (!def.Scouts || !building.IsComplete)
            {
                continue;
            }

            // Odhaluje se rovnou celý dosah, ne rostoucí prstenec.
            //
            // Prstenec tu byl a choval se špatně ze dvou důvodů: rostl podle
            // GLOBÁLNÍHO tiku, takže stanice postavená pozdě začínala na
            // náhodném poloměru, a po dosažení maxima se modulem vracel na
            // začátek. Radar s dosahem 70 tak potřeboval přes pět minut, než
            // ukázal, co měl ukázat hned — hráč to viděl jako „radar
            // neodhaluje nebo jen strašně pomalu".
            //
            // Opakuje se každý interval schválně: po Vzestupu se mlha vrací
            // a stanice musí své okolí odhalit znovu. Je to levné, mlha se
            // vede po chuncích, ne po dlaždicích.
            Fog.Reveal(building.X, building.Y, def.ScoutRadius);
        }
    }

    /// <summary>
    /// Vrátí vytěženou krajinu v okolí naráz. Rychlá (a nejistá) obdoba lesní
    /// školky — modlitba za rychlý růst.
    /// </summary>
    internal void RegrowArea(int centerX, int centerY, int radiusTiles)
    {
        for (int y = centerY - radiusTiles; y <= centerY + radiusTiles; y++)
        {
            for (int x = centerX - radiusTiles; x <= centerX + radiusTiles; x++)
            {
                TryRestoreTerrain(x, y);
            }
        }

        Fog.Reveal(centerX, centerY, radiusTiles);
    }

    /// <summary>
    /// Sucho: krajina v okolí vyschne. Opak <see cref="RegrowArea"/> — dopadá
    /// na okolí cizího města stejně jako na hráčovo.
    /// </summary>
    internal void BlightArea(int centerX, int centerY, int radiusTiles)
    {
        for (int y = centerY - radiusTiles; y <= centerY + radiusTiles; y++)
        {
            for (int x = centerX - radiusTiles; x <= centerX + radiusTiles; x++)
            {
                _nodes.Deplete(x, y, TickCount);
            }
        }

        Fog.Reveal(centerX, centerY, radiusTiles);
    }

    /// <summary>
    /// Přidá dávku suroviny, kterou hráč <b>zná</b>. Losuje se z cíle a tiku,
    /// takže je to deterministické jako všechno ostatní v modlitbách.
    /// </summary>
    internal void GrantKnownResource(double amount, int targetX, int targetY)
    {
        int known = KnownResourceCount;
        if (known == 0)
        {
            return;
        }

        int pick = (int)(((uint)(targetX * 73856093 ^ targetY * 19349663 ^ (int)TickCount)) % (uint)known);
        int index = KnownResourceAt(pick);
        if (index >= 0)
        {
            AddResource(index, amount);
        }
    }

    /// <summary>Je na dlaždici voda? (Render z toho hledá, kam vyplout.)</summary>
    public bool IsWaterAt(int x, int y) => _content.Biomes[BiomeAt(x, y)].IsWater;

    /// <summary>Sousedí dlaždice s vodou? (Povodeň bere jen to, co stojí u ní.)</summary>
    private bool IsWaterNextTo(int x, int y) =>
        _content.Biomes[Terrain.BiomeAt(x + 1, y)].IsWater
        || _content.Biomes[Terrain.BiomeAt(x - 1, y)].IsWater
        || _content.Biomes[Terrain.BiomeAt(x, y + 1)].IsWater
        || _content.Biomes[Terrain.BiomeAt(x, y - 1)].IsWater;

    private static bool WithinRadius(int x, int y, int centerX, int centerY, int radius) =>
        Math.Abs(x - centerX) <= radius && Math.Abs(y - centerY) <= radius;

    /// <summary>Odtikne požehnání. Volá se z hlavního tiku.</summary>
    private void TickBlessings()
    {
        if (_rainTicksLeft > 0)
        {
            _rainTicksLeft--;
        }

        if (_growthTicksLeft > 0)
        {
            _growthTicksLeft--;
        }
    }

    /// <summary>
    /// Postaví u startu to, co má svět mít od začátku (data: <c>startingBuildings</c>).
    ///
    /// <para>Prázdná mapa je nejhorší první dojem, jaký idle hra může udělat —
    /// hráč nevidí, o čem hra je. Jeden domek řekne „tohle stavíš" dřív, než
    /// stihne kliknout.</para>
    ///
    /// <para>Staví se zdarma a ve spirále od středu: v konstruktoru ještě nemá kdo
    /// platit a na souši u startu nemusí být místo hned na první dlaždici. Když se
    /// budova nevejde ani po celé spirále (ostrov o jedné dlaždici), svět prostě
    /// začne prázdný — spadnout kvůli tomu by bylo nepřiměřené.</para>
    /// </summary>
    private void PlaceStartingBuildings()
    {
        var indices = _content.Gameplay.StartingBuildingIndices;
        for (int i = 0; i < indices.Count; i++)
        {
            TryFoundNearCity(indices[i], out _, out _);
        }
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

    /// <summary>
    /// Index aktuálního ročního období, nebo −1, když hra období nemá.
    /// Odvozený z čísla dne — žádný stav, nic v savu.
    /// </summary>
    public int CurrentSeasonIndex => _content.Seasons.IndexForDay(DayNumber);

    /// <summary>Aktuální období, nebo <c>null</c> bez ročních období.</summary>
    public SeasonDef? CurrentSeason
    {
        get
        {
            int index = CurrentSeasonIndex;
            return index >= 0 ? _content.Seasons.Seasons[index] : null;
        }
    }

    /// <summary>Jak daleko je aktuální období (0 = právě začalo, 1 = končí).</summary>
    public double SeasonProgress01
    {
        get
        {
            var calendar = _content.Seasons;
            if (!calendar.IsEnabled)
            {
                return 0;
            }

            var dayNight = _content.Gameplay.DayNight;
            double elapsedDays = dayNight.StartTimeOfDay + TickCount / (TicksPerSecond * dayNight.DayLengthSeconds);
            double inSeason = elapsedDays % calendar.DaysPerSeason;
            return inSeason / calendar.DaysPerSeason;
        }
    }

    /// <summary>
    /// Má město čím topit? V zimě bez paliva se růst zpomalí na
    /// <see cref="SeasonDef.ColdGrowthMult"/> — mimo zimu je to vždy true.
    /// </summary>
    public bool HasFuelForHeating { get; internal set; } = true;

    /// <summary>
    /// Kolik lidí ve městě má nástroje (0–1). Nad plné pokrytí se nesčítá —
    /// hromada nástrojů navíc už nikomu nepřidá.
    /// </summary>
    public double ToolCoverage
    {
        get
        {
            var tools = _content.Gameplay.Tools;
            return tools.IsEnabled ? tools.Coverage(_resources[tools.ResourceIndex], Population) : 0;
        }
    }

    /// <summary>Násobič výroby od vybavenosti nástroji (1.0 bez nástrojů — bonus, ne daň).</summary>
    public double ToolProductionMult => 1.0 + _content.Gameplay.Tools.ProductionBonus * ToolCoverage;

    /// <summary>Násobič ručního sběru od vybavenosti nástroji.</summary>
    public double ToolHarvestMult => 1.0 + _content.Gameplay.Tools.HarvestBonus * ToolCoverage;

    /// <summary>
    /// Kolik lidí zrovna pracuje v budovách. Počítá se při rozdělování dělníků,
    /// takže je zadarmo — a nástroje se podle toho opotřebovávají.
    /// </summary>
    public long EmployedWorkers { get; internal set; }

    /// <summary>
    /// Rozpad spokojenosti na položky — kvůli čemu je zrovna taková. Počítá se
    /// na vyžádání a bez placení údržby, takže se na něj UI může ptát, kdy chce,
    /// aniž by tím sáhlo do hry.
    /// </summary>
    public HappinessBreakdown HappinessParts
    {
        get
        {
            var config = _content.Gameplay.Happiness;
            return config.IsEnabled
                ? _happinessSystem.Evaluate(this, config, payUpkeep: false)
                : HappinessBreakdown.Perfect;
        }
    }

    /// <summary>
    /// Stopa průmyslu v krajině. Render i UI z ní čtou (zákal nad mapou, HUD);
    /// zapisuje do ní jen <c>PollutionSystem</c>.
    /// </summary>
    public PollutionGrid PollutionMap => _pollution;

    /// <summary>
    /// Prosba obyvatele, která zrovna čeká na odpověď. Vždycky nejvýš jedna —
    /// zakázka je obchod, tohle je moment, a tři momenty naráz jsou seznam úkolů.
    /// </summary>
    public CitizenRequest PendingCitizenRequest { get; internal set; } = CitizenRequest.None;

    /// <summary>Kolik tiků zbývá, než se ozve někdo další.</summary>
    internal int CitizenCooldownTicks { get; set; }

    /// <summary>
    /// Kdo pošle příští karavanu.
    ///
    /// <para>Dřív to byl anonymní seznam sousedů z dat. Teď karavany posílají
    /// města, která hráč sám našel a spojil — karavana na silnici má být vidět
    /// jako důsledek toho, že někam vede cesta, ne jako náhodná událost.</para>
    ///
    /// <para>Přednost dostane město, se kterým se dlouho neobchodovalo. Bez toho
    /// by hráč obchodoval pořád s prvním nalezeným a zbytek okolí by zůstal
    /// cizí.</para>
    /// </summary>
    /// <param name="key">Klíč vybraného města; platný jen když metoda vrátí <c>true</c>.</param>
    public bool TryPickTradeCity(out long key)
    {
        key = 0;
        if (!NpcCitiesEnabled)
        {
            return false;
        }

        long fewestTrades = long.MaxValue;
        bool found = false;
        foreach (var city in NpcCities.CitiesNear(CityCenterX, CityCenterY, NpcScanRadius))
        {
            var state = NpcStateOf(city.Key);
            if (state.Absorbed || state.Destroyed || !IsCityDiscovered(city) || !HasTradeLink(city, state))
            {
                continue;
            }

            if (state.Trades < fewestTrades)
            {
                fewestTrades = state.Trades;
                key = city.Key;
                found = true;
            }
        }

        return found;
    }

    /// <summary>
    /// Karavana dorazila: připíše obchod, utuží vztah a vyplatí (s bonusem za vztah).
    ///
    /// <para>Pravidla vztahu i výplata jsou tady, ne v renderu, který karavanu
    /// kreslí — obrazovka jen hlásí, že dojela (CLAUDE.md, vrstvy).</para>
    /// </summary>
    /// <param name="cityKey">Kdo karavanu poslal (z <see cref="TryPickTradeCity"/>).</param>
    /// <param name="resourceIndex">Čím se platí.</param>
    /// <param name="basePayout">Základní výplata před bonusem za vztah.</param>
    /// <returns>Kolik se nakonec vyplatilo.</returns>
    public int CompleteCaravan(long cityKey, int resourceIndex, int basePayout)
    {
        double multiplier = 1.0;
        if (NpcCitiesEnabled && _npcStates.TryGetValue(cityKey, out var state)
            && NpcCities.TryCityByKey(cityKey, out var city))
        {
            int relationBefore = state.Relation;
            state.Trades++;
            state.Relation = Math.Min(MaxRelation, state.Relation + _content.NpcCities.TradeRelation);
            _npcStates[cityKey] = state;

            // Bonus roste plynule se vztahem: přátelé platí líp, ale nikdy víc,
            // než kolik dovolí data.
            multiplier = 1.0 + _content.NpcCities.CaravanBonusAtFullRelation * (state.Relation / (double)MaxRelation);

            // Hlásí se jen překročení desítky, ne každý bod — jinak by toasty
            // chodily po každé karavaně a přestaly by cokoli znamenat.
            if (state.Relation / 10 > relationBefore / 10)
            {
                EnqueueNotification(new GameNotification(
                    NotificationKind.CityFriendlier,
                    "toast.cityLevel",
                    _content.NpcCities.Archetypes[city.ArchetypeIndex].NameKey));
            }
        }

        int payout = Math.Max(1, (int)Math.Round(basePayout * multiplier));
        AddResource(resourceIndex, payout);
        return payout;
    }

    /// <summary>Jsou pojmenovaní obyvatelé v datech zapnutí? (UI podle toho skrývá panel.)</summary>
    public bool CitizensEnabled => _content.Citizens.IsEnabled;

    /// <summary>Jméno obyvatele, který zrovna prosí; prázdné, když nikdo neprosí.</summary>
    public string PendingCitizenName => PendingCitizenRequest.IsActive
        ? _content.Citizens.NameOf(
            PendingCitizenRequest.FirstNameIndex, PendingCitizenRequest.SurnameIndex)
        : string.Empty;

    /// <summary>Definice běžící prosby, nebo <c>null</c>.</summary>
    public CitizenRequestDef? PendingCitizenDef => PendingCitizenRequest.IsActive
        ? _content.Citizens.Requests[PendingCitizenRequest.DefIndex]
        : null;

    /// <summary>Má město na to, oč obyvatel prosí?</summary>
    public bool CanHelpCitizen()
    {
        if (PendingCitizenDef is not { } def)
        {
            return false;
        }

        for (int i = 0; i < def.Cost.Count; i++)
        {
            if (_resources[def.Cost[i].ResourceIndex] < def.Cost[i].Amount)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Příkaz hráče: pomoct obyvateli. Strhne materiál a založí mu jeho živnost —
    /// budova od té chvíle nese jeho jméno.
    ///
    /// <para>Místo hledá hra sama ve spirále od těžiště města: hráč pomáhá
    /// člověku, ne že si vybírá parcelu. Když se nikde nevejde, pomoc se
    /// neuskuteční a materiál zůstane — tichý neúspěch, žádná ztráta.</para>
    /// </summary>
    public bool TryHelpCitizen()
    {
        if (!CanHelpCitizen() || PendingCitizenDef is not { } def)
        {
            return false;
        }

        if (!TryFoundNearCity(def.BuildingIndex, out int x, out int y))
        {
            return false;
        }

        for (int i = 0; i < def.Cost.Count; i++)
        {
            _resources[def.Cost[i].ResourceIndex] -= def.Cost[i].Amount;
        }

        var request = PendingCitizenRequest;
        _founders[TileKey.Pack(x, y)] = PackName(request.FirstNameIndex, request.SurnameIndex);
        PendingCitizenRequest = CitizenRequest.None;
        CitizenCooldownTicks = _content.Citizens.GapTicks;
        FoundedByCitizens++;

        EnqueueNotification(new GameNotification(
            NotificationKind.CitizenHelped, "toast.citizenHelped", def.TextKey));
        return true;
    }

    /// <summary>Kolik živností už hráč obyvatelům založil (metrika do cílů a statistik).</summary>
    public long FoundedByCitizens { get; internal set; }

    /// <summary>
    /// Jméno zakladatele budovy na dané dlaždici, nebo prázdný řetězec.
    /// Panel budovy z toho píše „Založil: Marek Kovář".
    /// </summary>
    public string FounderOf(int x, int y)
    {
        if (!_founders.TryGetValue(TileKey.Pack(x, y), out long packed))
        {
            return string.Empty;
        }

        return _content.Citizens.NameOf((int)(packed >> 32), (int)(packed & 0xFFFFFFFF));
    }

    /// <summary>Zakladatelé k uložení do savu.</summary>
    public IEnumerable<(int X, int Y, int FirstNameIndex, int SurnameIndex)> Founders()
    {
        foreach (var (key, packed) in _founders)
        {
            yield return (TileKey.X(key), TileKey.Y(key), (int)(packed >> 32), (int)(packed & 0xFFFFFFFF));
        }
    }

    /// <summary>Obnoví zakladatele ze savu.</summary>
    internal void RestoreFounder(int x, int y, int firstNameIndex, int surnameIndex) =>
        _founders[TileKey.Pack(x, y)] = PackName(firstNameIndex, surnameIndex);

    /// <summary>Obnoví počet založených živností ze savu.</summary>
    internal void RestoreFoundedByCitizens(long count) => FoundedByCitizens = Math.Max(0, count);

    /// <summary>Obnoví běžící prosbu ze savu.</summary>
    internal void RestoreCitizenRequest(int defIndex, int first, int surname, int ticksLeft, int cooldown)
    {
        PendingCitizenRequest = defIndex >= 0 && defIndex < _content.Citizens.Requests.Count
            ? new CitizenRequest(defIndex, first, surname, ticksLeft)
            : CitizenRequest.None;
        CitizenCooldownTicks = Math.Max(0, cooldown);
    }

    private static long PackName(int firstIndex, int surnameIndex) =>
        ((long)firstIndex << 32) | (uint)surnameIndex;

    /// <summary>
    /// Najde místo pro živnost ve spirále od těžiště města a postaví ji zdarma.
    /// Spirála je stejná úvaha jako u auto-stavby: nová budova má vyrůst tam, kde
    /// se žije, ne na druhém konci mapy.
    /// </summary>
    private bool TryFoundNearCity(int defIndex, out int x, out int y)
    {
        for (int radius = 1; radius <= 24; radius++)
        {
            for (int dy = -radius; dy <= radius; dy++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != radius)
                    {
                        continue; // jen okraj prstence, ať se místa neopakují
                    }

                    x = CityCenterX + dx;
                    y = CityCenterY + dy;
                    if (TryPlaceBuildingFree(defIndex, x, y) == PlacementResult.Ok)
                    {
                        return true;
                    }
                }
            }
        }

        x = 0;
        y = 0;
        return false;
    }

    /// <summary>
    /// Časosběr města: hrubý půdorys zaznamenaný co pár minut. Prázdný, dokud
    /// není vrstva zapnutá v datech.
    ///
    /// <para>Je to jediná věc, která z dlouhé tiché práce dělá příběh, na který
    /// jde ukázat prstem — proto přežívá i to, co Vzestup smaže… až do chvíle,
    /// kdy začne nový svět.</para>
    /// </summary>
    public CityHistory History { get; }

    /// <summary>Zaznamenává se vůbec časosběr?</summary>
    public bool HistoryEnabled => _content.Gameplay.History.IsEnabled;

    /// <summary>
    /// Sejme snímek kroniky hned teď. Pro okamžiky, které si ho zaslouží bez
    /// ohledu na hodiny — hlavně těsně před uložením časosběru na disk.
    /// </summary>
    public void CaptureHistoryNow() => _historySystem.Capture(this);

    /// <summary>Kolik dostavěných budov daného typu se do milníků počítá.</summary>
    public long MilestoneCount(int defIndex) => _milestoneBonuses.CountOf(defIndex);

    /// <summary>Násobič výroby, který typ z milníků zrovna má (1.0 = žádný).</summary>
    public double MilestoneMultiplier(int defIndex) => _milestoneBonuses.MultiplierOf(defIndex);

    /// <summary>
    /// Kolik budov typu chybí do dalšího stupně; 0 = strop je vyčerpaný nebo typ
    /// milníky nemá. UI z toho píše „ještě 3 do dalšího stupně" — bez toho by byl
    /// milník neviditelný a tím pádem k ničemu.
    /// </summary>
    public long MilestoneToNextTier(int defIndex) =>
        _content.Buildings[defIndex].Milestones?.ToNextTier(MilestoneCount(defIndex)) ?? 0;

    /// <summary>Kolikátý stupeň milníku typ má.</summary>
    public int MilestoneTier(int defIndex) =>
        _content.Buildings[defIndex].Milestones?.TierFor(MilestoneCount(defIndex)) ?? 0;

    /// <summary>
    /// Nejvyšší stupeň sídla, jakého už město dosáhlo. Existuje jen kvůli tomu,
    /// aby se povýšení hlásilo jednou, a ne u každého shluku znovu.
    /// </summary>
    public int HighestSettlementRank { get; internal set; } = -1;

    /// <summary>
    /// Jak velké je sídlo nejblíž danému místu (index stupně; −1 = žádné v dosahu).
    ///
    /// <para>Používá se u budov, které potřebují velké sídlo — letiště nepatří
    /// do osady o třech chalupách. „V dosahu" je schválně velkorysé: hráč staví
    /// na kraji města a nemá být trestán za to, že netrefil přesný střed.</para>
    /// </summary>
    public int NearestSettlementRank(int x, int y)
    {
        int best = -1;
        double bestDistance = double.MaxValue;
        for (int i = 0; i < _settlements.Count; i++)
        {
            var settlement = _settlements[i];
            double dx = settlement.CenterX - x;
            double dy = settlement.CenterY - y;
            double distance = dx * dx + dy * dy;
            if (distance < bestDistance && distance <= SettlementReach * SettlementReach)
            {
                bestDistance = distance;
                best = settlement.RankIndex;
            }
        }

        return best;
    }

    /// <summary>Do jaké vzdálenosti (dlaždice) od těžiště sídla se ještě staví „v něm".</summary>
    private const int SettlementReach = 40;

    /// <summary>Rozpoznané čtvrti pro render a UI (odvozený stav, neukládá se).</summary>
    public IReadOnlyList<District> Districts => _districts;

    /// <summary>Čtvrti pro systémy simulace.</summary>
    internal List<District> DistrictsMutable => _districts;

    /// <summary>
    /// Změnila se zástavba tak, že se vyplatí čtvrti přepočítat? Stejný trik jako
    /// u osad — hledat shluky každý tik by bylo plýtvání.
    /// </summary>
    internal bool DistrictsDirty { get; set; } = true;

    /// <summary>
    /// Čtvrť, ve které budova stojí, nebo <c>null</c>. UI z toho píše „Průmyslová
    /// čtvrť (7 budov)" do panelu budovy.
    /// </summary>
    public District? DistrictOf(int buildingIndex)
    {
        if (buildingIndex < 0 || buildingIndex >= _buildingCount)
        {
            return null;
        }

        int index = _buildings[buildingIndex].DistrictIndex;
        return index >= 0 && index < _districts.Count ? _districts[index] : null;
    }

    /// <summary>Nástěnka zakázek pro UI (jen ke čtení — měnit ji smí systém).</summary>
    public ReadOnlySpan<ContractSlot> ContractSlots => _contractSlots;

    /// <summary>Nástěnka pro systémy simulace (odpočet termínů, vypisování nabídek).</summary>
    internal Span<ContractSlot> ContractSlotsMutable => _contractSlots;

    /// <summary>Kolik zakázek už město splnilo. Řídí, jak velké nabídky chodí.</summary>
    public long ContractsCompleted { get; internal set; }

    /// <summary>Jsou zakázky v datech vůbec zapnuté? (UI podle toho skrývá nástěnku.)</summary>
    public bool ContractsEnabled => _content.Contracts.IsEnabled;

    /// <summary>Definice zakázky na daném místě nástěnky, nebo <c>null</c> u prázdného.</summary>
    public ContractDef? ContractAt(int slot) =>
        slot >= 0 && slot < _contractSlots.Length && _contractSlots[slot].IsActive
            ? _content.Contracts.Contracts[_contractSlots[slot].DefIndex]
            : null;

    /// <summary>
    /// Odměna za zakázku na daném místě, už přepočtená škálováním. Vrací prázdno
    /// u prázdného místa. UI ji potřebuje vypsat dřív, než hráč klikne.
    /// </summary>
    public IReadOnlyList<ResourceAmount> ContractReward(int slot)
    {
        if (ContractAt(slot) is not { } def)
        {
            return Array.Empty<ResourceAmount>();
        }

        double scale = _contractSlots[slot].RewardScale;
        var reward = new ResourceAmount[def.Reward.Count];
        for (int i = 0; i < reward.Length; i++)
        {
            reward[i] = def.Reward[i] with
            {
                Amount = Math.Max(1, (int)Math.Round(def.Reward[i].Amount * scale)),
            };
        }

        return reward;
    }

    /// <summary>Má město dost suroviny, aby zakázku na daném místě odevzdalo?</summary>
    public bool CanFulfilContract(int slot)
    {
        if (ContractAt(slot) is not { } def)
        {
            return false;
        }

        return _resources[def.DemandResourceIndex] >= _contractSlots[slot].DemandAmount;
    }

    /// <summary>
    /// Příkaz hráče: odevzdat zakázku. Strhne objednanou surovinu, vyplatí odměnu
    /// a místo uvolní pro dalšího zákazníka.
    ///
    /// <para>Je to schválně akce, ne automatika: surovina, kterou odevzdáš, ti
    /// zrovna chybí na stavbu — v tom je celé to malé rozhodnutí.</para>
    /// </summary>
    public bool TryFulfilContract(int slot)
    {
        if (!CanFulfilContract(slot) || ContractAt(slot) is not { } def)
        {
            return false;
        }

        var reward = ContractReward(slot);
        _resources[def.DemandResourceIndex] -= _contractSlots[slot].DemandAmount;
        for (int i = 0; i < reward.Count; i++)
        {
            AddResource(reward[i].ResourceIndex, reward[i].Amount);
        }

        ContractsCompleted++;
        _contractSlots[slot] = ContractSlot.Empty(_content.Contracts.Board.RestockTicks);
        EnqueueNotification(new GameNotification(
            NotificationKind.ContractFulfilled, "toast.contractDone", def.NameKey));
        return true;
    }

    /// <summary>
    /// Postaví prázdnou nástěnku podle dat. Volá se při startu, po Vzestupu
    /// i před načtením savu, aby měla vždycky správný počet míst.
    /// </summary>
    private void ResetContractBoard()
    {
        var board = _content.Contracts.Board;
        _contractSlots = new ContractSlot[Math.Max(0, board.Slots)];
        for (int i = 0; i < _contractSlots.Length; i++)
        {
            // Rozestupem se nabídky nevypíšou naráz — nástěnka se plní postupně,
            // což vypadá živěji než tři zakázky, které se objeví v tomtéž tiku.
            _contractSlots[i] = ContractSlot.Empty(board.RestockTicks / Math.Max(1, _contractSlots.Length) * i);
        }
    }

    /// <summary>Obnoví počet splněných zakázek ze savu (řídí velikost nabídek).</summary>
    internal void RestoreContractsCompleted(long completed) => ContractsCompleted = Math.Max(0, completed);

    /// <summary>Obnoví místo na nástěnce ze savu.</summary>
    internal void RestoreContractSlot(int slot, int defIndex, long demand, int ticksLeft, double rewardScale)
    {
        if (slot < 0 || slot >= _contractSlots.Length)
        {
            return; // save z jiného nastavení nástěnky — přebytek se tiše zahodí
        }

        _contractSlots[slot] = new ContractSlot
        {
            DefIndex = defIndex,
            DemandAmount = demand,
            TicksLeft = ticksLeft,
            RewardScale = rewardScale,
        };
    }

    /// <summary>
    /// Kolik je kouře přímo nad městem. Právě tohle číslo cítí obyvatelé — ne
    /// průměr přes celou mapu, který by vzdálený důl rozmělnil do bezvýznamnosti.
    /// </summary>
    public double AirPollutionOverCity => _pollution.At(CityCenterX, CityCenterY, PollutionKind.Air);

    /// <summary>Nejhorší naměřená hodnota daného druhu kdekoli na mapě (HUD, varování).</summary>
    public double PollutionPeak(PollutionKind kind) => _pollution.Peak(kind);

    /// <summary>
    /// Jak zle je na tom město se vzduchem (0 = čisto, 1 = plný dopad). UI z toho
    /// dělá barvu i sílu zákalu, takže nemusí znát čísla z konfigurace.
    /// </summary>
    public double AirPollutionSeverity => _content.Gameplay.Pollution.Severity(AirPollutionOverCity);

    /// <summary>
    /// Jak se místu na mapě daří: 0 = zakouřená bída, 1 = kvetoucí čtvrť.
    ///
    /// <para>Proč to je v simulaci a ne v renderu: prosperita je vlastnost světa
    /// a render ji má jen <b>zobrazit</b>. Skládá se ze spokojenosti (globální —
    /// jak se ve městě žije) a zamoření pod nohama (místní — jak to tady vypadá).
    /// Díky té místní složce nevypadá celé město stejně: čisté předměstí kvete,
    /// i když nad hutěmi visí smog.</para>
    ///
    /// <para>Vypnuté vrstvy nic nekazí — bez spokojenosti i bez znečištění
    /// vychází 1.0 a město prostě vypadá spořádaně.</para>
    /// </summary>
    public double ProsperityAt(int x, int y)
    {
        double happiness = _content.Gameplay.Happiness.IsEnabled ? Math.Clamp(Happiness, 0.0, 1.0) : 1.0;

        var pollutionConfig = _content.Gameplay.Pollution;
        double grime = 0.0;
        if (pollutionConfig.IsEnabled)
        {
            // Vzduch i půda dohromady: kouř nad hlavou i otrávená země pod ní
            // dělají místo stejně nehezkým. Bere se horší z obou.
            grime = Math.Max(
                pollutionConfig.Severity(_pollution.At(x, y, PollutionKind.Air)),
                pollutionConfig.Severity(_pollution.At(x, y, PollutionKind.Soil)));
        }

        return Math.Clamp(happiness * (1.0 - grime), 0.0, 1.0);
    }

    /// <summary>Je vrstva znečištění v datech vůbec zapnutá? (UI podle toho skrývá readout.)</summary>
    public bool PollutionEnabled => _content.Gameplay.Pollution.IsEnabled;

    /// <summary>Násobič výroby jídla od ročního období (1.0 bez období).</summary>
    public double SeasonFoodMult => CurrentSeason?.FoodProductionMult ?? 1.0;

    /// <summary>Násobič ručního sběru od ročního období (1.0 bez období).</summary>
    public double SeasonHarvestMult => CurrentSeason?.HarvestMult ?? 1.0;

    /// <summary>
    /// Násobič růstu populace od ročního období. V zimě bez paliva platí
    /// zpomalený <see cref="SeasonDef.ColdGrowthMult"/> místo běžného.
    /// </summary>
    public double SeasonGrowthMult
    {
        get
        {
            if (CurrentSeason is not { } season)
            {
                return 1.0;
            }

            return season.NeedsHeating && !HasFuelForHeating ? season.ColdGrowthMult : season.GrowthMult;
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

    /// <summary>Ladicí: přidá body Odkazu (druhá prestižní vrstva).</summary>
    public void DebugGrantLegacyPoints(long amount) => _legacy.DebugGrant(amount);

    /// <summary>
    /// Ladicí: posune úroveň Vzestupu bez resetu éry. Zvedne strop měřítka
    /// i odemčení vázaná na stupeň — jinak by se pozdní hra nedala vyzkoušet
    /// jinak než hodinami hraní.
    /// </summary>
    public void DebugGrantAscensionLevels(int levels)
    {
        if (levels <= 0)
        {
            return;
        }

        AscensionLevel += levels;
        ScaleCapAnnounced = false;
        RefreshTierUnlocks();
        RecomputeDerivedState();
    }

    /// <summary>
    /// Ladicí: vyzkoumá technologii bez placení a bez ohledu na předpoklady.
    ///
    /// <para>Existuje kvůli nástrojům, které mají <b>ukázat obsah</b>, ne ho
    /// odehrát — trailer, snímky do obchodu, ladicí menu. Cestou přes
    /// <c>TryResearch</c> se tam dojít nedá: zásoby se ořezávají na kapacitu
    /// skladu, takže na hlubší uzly stromu by nástroj musel napřed postavit
    /// sklady — a to je jiná úloha než „postav hezké městečko".</para>
    ///
    /// <para><b>Demo se tudy obejít nedá.</b> Uzel mimo výřez ukázky zůstane
    /// zamčený i pro nástroj; jinak by stačilo ladicí menu a hranice dema by
    /// byla jen doporučení.</para>
    /// </summary>
    /// <returns>false, když je uzel mimo výřez demoverze.</returns>
    public bool DebugGrantTech(int techIndex)
    {
        if (IsTechBeyondDemo(techIndex))
        {
            return false;
        }

        UnlockTech(techIndex);
        return true;
    }

    /// <summary>Ladicí: naplní všechny sklady na maximum.</summary>
    public void DebugFillStorages()
    {
        for (int i = 0; i < _resources.Length; i++)
        {
            _resources[i] = GetStorageCap(i);
        }
    }

    /// <summary>Ladicí: přidá lidi rovnou (strop měřítka pak platí dál).</summary>
    public void DebugAddPopulation(double amount)
    {
        if (amount > 0)
        {
            Population = Math.Min(Population + amount, Math.Max(0, Math.Min(HousingCapacity, PopulationCap)));
        }
    }

    /// <summary>
    /// Ladicí: dočasně znásobí tempo auto-stavby. Existuje kvůli natáčení
    /// a testování pozdní hry — chvíli je vidět růst, který by jinak trval
    /// desítky minut, a pak se vše vrátí k normálu samo.
    /// </summary>
    public void DebugBoostAutoBuild(double multiplier, double seconds)
    {
        _debugBuildBoost = Math.Max(1.0, multiplier);
        _debugBuildBoostTicks = (int)Math.Max(0, seconds * TicksPerSecond);
    }

    /// <summary>Zbývá ladicí boost auto-stavby? (UI to ukazuje, ať hráč ví, proč to lítá.)</summary>
    public bool DebugBuildBoostActive => _debugBuildBoostTicks > 0;

    private double _debugBuildBoost = 1.0;
    private int _debugBuildBoostTicks;

    /// <summary>
    /// Přidá body Vzestupu — <b>jen pro ladicí menu</b>.
    ///
    /// <para>Ve hře body vznikají jedině Vzestupem. Tenhle vstup existuje proto,
    /// že jinak se prestižní vrstva dá vyzkoušet jedině tak, že se hra odehraje
    /// až k Vzestupu — u každé změny v nákupech vylepšení desítky minut.</para>
    ///
    /// <para>Je pojmenovaný tak, aby bylo z volání poznat, že to není herní
    /// mechanika; záporné číslo se ignoruje, ať se tudy nedá stav rozbít.</para>
    /// </summary>
    public void DebugGrantPrestigePoints(long amount)
    {
        if (amount > 0)
        {
            PrestigePoints += amount;
        }
    }

    /// <summary>Aktuální trvalé násobiče z koupených upgradů Vzestupu (systémy je čtou).</summary>
    public PrestigeBonuses Bonuses => _bonuses;

    /// <summary>Násobič od slavnosti (1.0 když neběží) — výroba i sběr.</summary>
    public double BoostMultiplier => _boostTicksRemaining > 0
        ? _content.Gameplay.Boost.Multiplier * _bonuses.FestivalPower
        : 1.0;

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

    /// <summary>
    /// Vyhlásí slavnost bez ohledu na ochlazení. Používá jen modlitba: hráč za
    /// ni zaplatil vírou a riskoval, že nebude vyslyšena — to je ta cena.
    /// </summary>
    internal void ForceBoost()
    {
        _boostTicksRemaining = (int)(_content.Gameplay.Boost.DurationSeconds * TicksPerSecond);
        _boostCooldownRemaining = (int)(_content.Gameplay.Boost.CooldownSeconds * TicksPerSecond);
    }

    /// <summary>
    /// Kolik lidí se vejde (základní tábor + domy).
    ///
    /// <para><b>Ne <c>int</c>.</b> Násobič bydlení z Vzestupu se skládá násobně
    /// přes desítky úrovní, takže jediný vylepšený dům přidá řádově víc, než se
    /// do 32 bitů vejde. Kapacita pak přetekla do záporu — a protože je stropem
    /// růstu, stáhla populaci s sebou. Hráč to viděl jako populaci −2,15 mld.</para>
    /// </summary>
    public double HousingCapacity { get; private set; }

    /// <summary>Součet pracovních míst výrobních budov — obsazenost škáluje výrobu.</summary>
    public int TotalWorkerSlots { get; private set; }

    /// <summary>
    /// Kolik budov zrovna stojí bez jediného dělníka. Počítá výrobní tik při
    /// rozdělování lidí; UI to ukazuje jako varování, aby hráč poznal, že další
    /// budova mu nic nepřinese, dokud nepřibudou lidi.
    /// </summary>
    public int IdleBuildings { get; internal set; }

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
    /// <summary>
    /// Dlaždice, které simulace přepsala proti vygenerovanému terénu
    /// (terraformace, kráter po meteoru, zaplavené pobřeží). Render je čte,
    /// aby změnu bylo vidět — bez toho hráč terraformoval do prázdna.
    /// </summary>
    public IReadOnlyDictionary<long, byte> BiomeOverrideMap => _biomeOverrides;

    /// <summary>
    /// Roste s každou změnou terénu. Render podle něj pozná, že musí zahodit
    /// upečené chunky; porovnávat celou mapu přepisů každý snímek by bylo drahé.
    /// </summary>
    public int TerrainRevision { get; private set; }

    public byte BiomeAt(int x, int y) =>
        _biomeOverrides.TryGetValue(TileKey.Pack(x, y), out byte overridden)
            ? overridden
            : Terrain.BiomeAt(x, y);

    /// <summary>
    /// Přepíše biom jedné dlaždice (terraformace — zatím jen UFO). Ukládá se jen
    /// těch pár změněných dlaždic, zbytek nekonečné mapy zůstává čistou funkcí.
    /// </summary>
    internal void SetBiomeOverride(int x, int y, byte biomeIndex)
    {
        _biomeOverrides[TileKey.Pack(x, y)] = biomeIndex;

        // Bez zvýšení revize by render dál kreslil starý biom z upečeného
        // chunku a terraformace by vypadala, že nic nedělá.
        TerrainRevision++;
    }

    /// <summary>
    /// Lze tímhle nástrojem přetvořit dlaždici? Kontroluje odemčení technologií,
    /// vhodný výchozí biom, volnost dlaždice a suroviny.
    /// </summary>
    public PlacementResult CanTerraform(int actionIndex, int x, int y) =>
        CanTerraform(actionIndex, x, y, requireTech: true);

    /// <param name="requireTech">
    /// <c>false</c> u budov, které terén mění samy — budova sama je to odemčení.
    /// </param>
    private PlacementResult CanTerraform(int actionIndex, int x, int y, bool requireTech)
    {
        var action = _content.Terraform[actionIndex];
        if (requireTech && action.UnlockTechIndex >= 0 && _techLevel[action.UnlockTechIndex] == 0)
        {
            return PlacementResult.NotUnlocked;
        }

        byte current = BiomeAt(x, y);
        if (current == action.TargetBiomeIndex || !action.AppliesTo(current))
        {
            return PlacementResult.WrongBiome;
        }

        long tile = TileKey.Pack(x, y);
        if (_occupancy.ContainsKey(tile) || _roads.Contains(tile))
        {
            return PlacementResult.Occupied; // pod budovou ani cestou se nekope
        }

        for (int i = 0; i < action.Cost.Count; i++)
        {
            if (_resources[action.Cost[i].ResourceIndex] < action.Cost[i].Amount)
            {
                return PlacementResult.NotEnoughResources;
            }
        }

        return PlacementResult.Ok;
    }

    /// <summary>Příkaz hráče: přetvoř dlaždici (zaplatí cenu a přepíše biom).</summary>
    public PlacementResult TryTerraform(int actionIndex, int x, int y)
    {
        var result = CanTerraform(actionIndex, x, y);
        if (result != PlacementResult.Ok)
        {
            return result;
        }

        var action = _content.Terraform[actionIndex];
        for (int i = 0; i < action.Cost.Count; i++)
        {
            _resources[action.Cost[i].ResourceIndex] -= action.Cost[i].Amount;
        }

        ApplyTerraform(action.TargetBiomeIndex, x, y);
        return PlacementResult.Ok;
    }

    /// <summary>
    /// Zásah budovy, která terén přetváří sama. Platí se stejně jako u ruční
    /// teraformace — automat šetří klikání, ne suroviny.
    /// </summary>
    internal PlacementResult TryAutoTerraform(int actionIndex, int x, int y)
    {
        var result = CanTerraform(actionIndex, x, y, requireTech: false);
        if (result != PlacementResult.Ok)
        {
            return result;
        }

        var action = _content.Terraform[actionIndex];
        for (int i = 0; i < action.Cost.Count; i++)
        {
            _resources[action.Cost[i].ResourceIndex] -= action.Cost[i].Amount;
        }

        ApplyTerraform(action.TargetBiomeIndex, x, y);
        return PlacementResult.Ok;
    }

    /// <summary>
    /// Samotná přeměna dlaždice — bez placení a bez kontrol. Sdílí ji ruční
    /// zásah i budovy, které terén přetvářejí samy.
    /// </summary>
    private void ApplyTerraform(int targetBiomeIndex, int x, int y)
    {
        // Když se dlaždice topí, nesmí na ní zůstat trčet les ani žíla: uzel
        // by dál nabízel těžbu uprostřed jezera a vypadalo by to jako chyba.
        if (_content.Biomes[targetBiomeIndex].IsWater)
        {
            _nodes.Deplete(x, y, TickCount);
        }

        SetBiomeOverride(x, y, (byte)targetBiomeIndex);
        TerraformedTiles++;
        ReportVisual(VisualEventKind.Terraformed, x, y);
    }

    /// <summary>Kolik dlaždic hráč přetvořil (metrika pro úkoly a achievementy).</summary>
    public long TerraformedTiles { get; internal set; }

    /// <summary>Terraformované dlaždice (pro uložení).</summary>
    internal IEnumerable<KeyValuePair<long, byte>> BiomeOverrides() => _biomeOverrides;

    /// <summary>Obnoví terraformovanou dlaždici z savu.</summary>
    internal void RestoreBiomeOverride(long tile, byte biomeIndex) => _biomeOverrides[tile] = biomeIndex;

    /// <summary>Poslední okno, jehož zásah UFO už proběhl (pro uložení).</summary>
    internal long LastUfoWindow => _lastUfoWindow;

    /// <summary>Obnoví počet přetvořených dlaždic ze savu.</summary>
    internal void RestoreTerraformedTiles(long count) => TerraformedTiles = count;

    /// <summary>Obnoví poslední vyřízené okno UFO ze savu (jinak by zásah proběhl znovu).</summary>
    internal void RestoreLastUfoWindow(long window) => _lastUfoWindow = window;

    /// <summary>Je budova odemčená (technologií)? Neřeší, zda ji lze stavět přímo.</summary>
    public bool IsBuildingUnlocked(int defIndex) => _buildingUnlocked[defIndex];

    /// <summary>Smí hráč budovu přímo postavit (odemčená a nemarkovaná jako jen-upgrade)?</summary>
    public bool IsBuildingBuildable(int defIndex) => _buildingUnlocked[defIndex] && _content.Buildings[defIndex].Buildable;

    // ----- měřítko (stupně Vzestupu) -----

    /// <summary>
    /// Index aktuálního stupně měřítka: nejvyšší, jehož <see cref="AscensionTierDef.Order"/>
    /// úroveň Vzestupu dosáhla. −1 = žádné stupně (bez stropu).
    /// </summary>
    public int CurrentTierIndex
    {
        get
        {
            int best = -1, bestOrder = -1;
            var tiers = _content.AscensionTiers;
            for (int i = 0; i < tiers.Count; i++)
            {
                if (tiers[i].Order <= AscensionLevel && tiers[i].Order > bestOrder)
                {
                    bestOrder = tiers[i].Order;
                    best = i;
                }
            }

            return best;
        }
    }

    /// <summary>
    /// Strop populace aktuálního měřítka (progression-prestige.md §3). Je to MĚKKÝ cíl:
    /// růst se u něj zastaví, ale nic se neboří — láká k dalšímu Vzestupu, netrestá
    /// („soft-lock, ne hard-lock"). Bez definovaných stupňů je strop nekonečný.
    /// </summary>
    public double PopulationCap
    {
        get
        {
            int tier = CurrentTierIndex;
            if (tier < 0)
            {
                return double.PositiveInfinity;
            }

            // Každý další Vzestup NAD stupněm strop zvedne. Bez toho by se
            // žebřík na posledním stupni zastavil napořád a i mezi stupni platil
            // pořád tentýž strop — hráč s vymaxovanými bonusy do něj dojel za
            // pár vteřin a hra pak jen mlčky stála.
            //
            // Kolikrát strop vyroste, si bere z DAT: je to poměr posledních dvou
            // stupňů žebříku. Když si obsah řekne o skok stokrát, roste i dál
            // stokrát — a nemůže se to s žebříkem rozejít.
            int steps = AscensionLevel - _content.AscensionTiers[tier].Order;
            double cap = _content.AscensionTiers[tier].PopulationCap;
            double scaled = steps <= 0 ? cap : cap * Math.Pow(ScaleGrowthPerAscension, steps);

            // Demo má vlastní strop. Bere se to nižší z obou, takže v rané hře
            // platí normální žebřík a demo se projeví, teprve až by ho hráč
            // přerostl — jinak by ukázka začínala tvrdším limitem, než jaký má
            // samotná první éra.
            return _content.IsDemo ? Math.Min(scaled, _content.Demo.PopulationCap) : scaled;
        }
    }

    /// <summary>Naráží populace na strop měřítka? (UI to má říct nahlas, ne mlčet.)</summary>
    public bool IsAtScaleCap => Population >= PopulationCap - 0.5;

    /// <summary>
    /// Bylo už dosažení stropu ohlášeno? Hlásí se jednou za éru — opakovaný
    /// pruh přes obrazovku by z pobídky udělal otravu. Nuluje se Vzestupem,
    /// protože ten strop zvedne a dojet k němu znovu je zase novinka.
    /// </summary>
    internal bool ScaleCapAnnounced { get; set; }

    /// <summary>Řekne hráči, že město dorostlo na strop měřítka a co s tím.</summary>
    internal void EnqueueScaleCapNotice()
    {
        int tier = CurrentTierIndex;
        EnqueueNotification(new GameNotification(
            NotificationKind.Milestone,
            "toast.scaleCapped",
            tier >= 0 ? _content.AscensionTiers[tier].NameKey : string.Empty));
    }

    /// <summary>
    /// O kolik vyroste strop měřítka s každým Vzestupem nad rámec stupně —
    /// poměr posledních dvou stupňů v datech. Míň než dva stupně = desetinásobek.
    /// </summary>
    private double ScaleGrowthPerAscension
    {
        get
        {
            var tiers = _content.AscensionTiers;
            if (tiers.Count < 2)
            {
                return 10.0;
            }

            double last = tiers[tiers.Count - 1].PopulationCap;
            double previous = tiers[tiers.Count - 2].PopulationCap;
            return previous > 0 && last > previous ? last / previous : 10.0;
        }
    }

    /// <summary>
    /// Zamkne budovy hlídané stupněm měřítka a hned odemkne ty, na jejichž stupeň
    /// už úroveň Vzestupu dosáhla. Stejný princip jako u technologií — megastruktura
    /// je ale odměna za MĚŘÍTKO, ne za výzkum. Volá se při startu, Vzestupu, resetu
    /// éry a načtení savu.
    /// </summary>
    private void RefreshTierUnlocks()
    {
        var tiers = _content.AscensionTiers;
        for (int i = 0; i < tiers.Count; i++)
        {
            var unlocks = tiers[i].UnlockedBuildingIndices;
            bool reached = tiers[i].Order <= AscensionLevel;
            for (int j = 0; j < unlocks.Count; j++)
            {
                _buildingUnlocked[unlocks[j]] = reached;
            }
        }
    }

    /// <summary>
    /// Má hráč dost surovin na stavbu (bez ohledu na místo/biom)? Pro HUD — barevné
    /// zvýraznění tlačítek, ať je na první pohled jasné, co si můžu dovolit.
    /// </summary>
    /// <summary>
    /// Má hráč na tuhle cenu? Pro akce, jejichž cena nepatří budově (dary,
    /// spojení, odkup města) — UI z toho barví tlačítka, aby se nemuselo klikat
    /// naslepo.
    /// </summary>
    public bool CanAfford(IReadOnlyList<ResourceAmount> cost) => CanPay(cost);

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

    /// <summary>
    /// Zná už hráč tuhle surovinu (někdy ji získal)? HUD neznámé suroviny NEUKAZUJE
    /// a náhodné odměny je nerozdávají — jinak by hra prozrazovala obsah, ke kterému
    /// se hráč ještě nedostal.
    /// </summary>
    public bool IsResourceKnown(int resourceIndex) => _resourceKnown[resourceIndex];

    /// <summary>Kolik surovin hráč zná (pro UI, které se překresluje při odemčení nové).</summary>
    public int KnownResourceCount
    {
        get
        {
            int count = 0;
            for (int i = 0; i < _resourceKnown.Length; i++)
            {
                if (_resourceKnown[i])
                {
                    count++;
                }
            }

            return count;
        }
    }

    /// <summary>
    /// <paramref name="ordinal"/>-tá známá surovina; −1 = hráč nezná žádnou.
    ///
    /// <para>Slouží k losování odměn. Odměna smí padnout jen ze surovin, které
    /// hráč už viděl — jinak mu ze zlatého nálezu přiletí nanomateriál dřív, než
    /// tuší, že něco takového existuje, a rozbije mu to progresi i překvapení.</para>
    /// </summary>
    public int KnownResourceAt(int ordinal)
    {
        for (int i = 0, seen = 0; i < _resourceKnown.Length; i++)
        {
            if (_resourceKnown[i] && seen++ == ordinal)
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>Označí surovinu za známou (hráč ji získal). Idempotentní.</summary>
    internal void MarkResourceKnown(int resourceIndex) => _resourceKnown[resourceIndex] = true;

    /// <summary>Známé suroviny pro serializaci savu.</summary>
    internal IEnumerable<int> KnownResourceIndices()
    {
        for (int i = 0; i < _resourceKnown.Length; i++)
        {
            if (_resourceKnown[i])
            {
                yield return i;
            }
        }
    }

    /// <summary>Je technologie vyzkoumaná?</summary>
    public bool IsTechResearched(int techIndex) => _techLevel[techIndex] > 0;

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
                    || (_content.Techs.TryIndexOf(era.UnlockTechId, out int techIndex) && _techLevel[techIndex] > 0);
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
        if (amount > 0)
        {
            _resourceKnown[resourceIndex] = true; // získáním se surovina odemyká v UI
        }
    }

    /// <summary>Kapacita skladu suroviny (základ + skladové budovy).</summary>
    public double GetStorageCap(int resourceIndex) => _storageCaps[resourceIndex];

    /// <summary>Zásoby pro systémy simulace (mutace jen uvnitř assembly).</summary>
    internal double[] Resources => _resources;

    /// <summary>
    /// Násobič výroby jedné suroviny z cílených technologií (1.0 = beze změny).
    /// Odděleno od globálního bonusu, aby „+5 % dřeva" nezvedlo i ocel.
    /// </summary>
    public double ResourceProductionMult(int resourceIndex) => _resourceProductionMult[resourceIndex];

    /// <summary>Kapacity skladů pro systémy simulace.</summary>
    internal double[] StorageCaps => _storageCaps;

    /// <summary>Obsah, nad kterým simulace běží — pro serializaci savu (v rámci assembly).</summary>
    internal GameContent ContentRef => _content;

    /// <summary>Osady k přepsání systémem detekce.</summary>
    internal List<Settlement> SettlementsMutable => _settlements;

    /// <summary>Jak často se sahá na staveniště (tiky) — viz <c>ConstructionSystem</c>.</summary>
    public const int ConstructionIntervalTicks = ConstructionSystem.IntervalTicks;

    /// <summary>
    /// Kolik budov se zrovna staví. Dokud je nula, nemá stavební systém co dělat
    /// a pole budov se kvůli němu vůbec neprochází.
    /// </summary>
    public int BuildingsUnderConstruction { get; private set; }

    /// <summary>
    /// Postup stavby budovy 0–1 (1 = hotovo). Pro ukazatel nad staveništěm —
    /// div světa, u kterého není vidět, jak daleko je, není událost, ale čekání.
    /// </summary>
    public double ConstructionProgress01(int buildingIndex)
    {
        if (buildingIndex < 0 || buildingIndex >= _buildingCount)
        {
            return 1.0;
        }

        ref readonly var building = ref _buildings[buildingIndex];
        int total = _content.Buildings[building.DefIndex].BuildTicks;
        return total <= 0 ? 1.0 : 1.0 - Math.Clamp(building.BuildTicksRemaining / (double)total, 0, 1);
    }

    /// <summary>
    /// Dostavěno: budova se zapne, připíše bonusy a hráč se to dozví. Volá
    /// stavební systém; dokončení je jediný okamžik, kdy div světa začne platit.
    /// </summary>
    internal void CompleteConstruction(int buildingIndex, BuildingDef def)
    {
        BuildingsUnderConstruction = Math.Max(0, BuildingsUnderConstruction - 1);
        ApplyBuildingBonuses(def);
        WondersCompleted++;
        SettlementsDirty = true;
        DistrictsDirty = true; // změna zástavby může vytvořit i rozpadnout čtvrť
        _roadLinksDirty = true;
        ReportVisual(VisualEventKind.BuildingUpgraded, _buildings[buildingIndex].X, _buildings[buildingIndex].Y);
        EnqueueNotification(new GameNotification(NotificationKind.Milestone, "toast.wonderDone", def.NameKey));
    }

    /// <summary>Kolik divů světa už město dostavělo (metrika pro cíle a achievementy).</summary>
    public long WondersCompleted { get; internal set; }

    /// <summary>Obnoví počet dostavěných divů ze savu.</summary>
    internal void RestoreWondersCompleted(long count) => WondersCompleted = count;

    /// <summary>
    /// Vrátí budovu ze savu zpět na staveniště. Volá se až po načtení budov,
    /// protože sav nese odpočet zvlášť — půlka rozestavěného divu se po načtení
    /// nesmí tvářit jako hotová.
    /// </summary>
    internal void RestoreConstruction(int buildingIndex, int remainingTicks)
    {
        if (buildingIndex < 0 || buildingIndex >= _buildingCount || remainingTicks <= 0)
        {
            return;
        }

        ref var building = ref _buildings[buildingIndex];
        var def = _content.Buildings[building.DefIndex];
        if (!def.TakesTimeToBuild)
        {
            return; // typ se mezitím v datech změnil na okamžitý — nech budovu stát
        }

        building.BuildTicksRemaining = Math.Min(remainingTicks, def.BuildTicks);
        BuildingsUnderConstruction++;
        RemoveBuildingBonuses(def); // obnova je připsala, staveniště je zase nemá
    }

    /// <summary>Zástavba se změnila — osady čekají na přepočet.</summary>
    internal bool SettlementsDirty { get; set; }

    /// <summary>
    /// Přibylo/ubylo sběrné místo (sklad) nebo se posunulo těžiště města —
    /// násobiče svozu čekají na rozložený přepočet.
    /// </summary>
    internal bool HaulDirty { get; set; } = true;

    /// <summary>
    /// Je budova napojená na silniční síť? Bez cesty se zboží odváží hůř a výroba
    /// klesne na <see cref="RoadConfig.DisconnectedProductionMult"/> — díky tomu
    /// silnice nejsou jen čára na mapě. Auto-stavba je staví sama, takže jde
    /// o odměnu za fungující síť, ne o past.
    ///
    /// <para>Dokud ve městě není ANI JEDNA cesta, platí všechny budovy za napojené.
    /// První chalupa nemá k čemu se připojit a trestat ji za to by hráče jen mátlo —
    /// mechanika se zapne, teprve až síť vznikne.</para>
    /// </summary>
    public bool IsBuildingConnected(int buildingIndex)
    {
        if (_roads.Count == 0)
        {
            return true;
        }

        EnsureRoadLinkFresh();
        return buildingIndex >= 0 && buildingIndex < _buildingCount && _roadLinked[buildingIndex];
    }

    /// <summary>Kolik postavených budov má napojení na síť (HUD, balanc).</summary>
    public int ConnectedBuildingCount
    {
        get
        {
            EnsureRoadLinkFresh();
            int count = 0;
            for (int i = 0; i < _buildingCount; i++)
            {
                if (_roadLinked[i]) count++;
            }

            return count;
        }
    }

    /// <summary>Napojení se mění jen se stavbou nebo novou cestou — jinak se nepřepočítává.</summary>
    internal void InvalidateRoadLinks() => _roadLinksDirty = true;

    private void EnsureRoadLinkFresh()
    {
        if (!_roadLinksDirty)
        {
            return;
        }

        _roadLinksDirty = false;

        // Podle POČTU budov, ne jen podle kapacity pole — occupancy umí vrátit
        // index kterékoli žijící budovy a ten musí do cache vždycky padnout.
        int needed = Math.Max(_buildings.Length, _buildingCount);
        if (_roadLinked.Length < needed)
        {
            Array.Resize(ref _roadLinked, needed);
        }

        if (_roadLinkHop.Length < needed)
        {
            Array.Resize(ref _roadLinkHop, needed);
        }

        // Napojení se počítá po BLOCÍCH, ne po jednotlivých budovách: co se
        // dotýká hranou, patří k sobě a stačí, když se silnice dotkne kteréhokoli
        // domu v řadě. Bez toho by řadová zástavba vyžadovala dlažbu mezi každými
        // dvěma domy — a přesně tak město vypadat nemá.
        for (int i = 0; i < _buildingCount; i++)
        {
            _roadLinked[i] = TouchesRoad(_buildings[i]);
            _roadLinkHop[i] = 0;
        }

        SpreadRoadLinkThroughBlocks();
    }

    /// <summary>
    /// Jak daleko se smí napojení šířit zástavbou — kolik domů od toho, který
    /// se silnice opravdu dotýká.
    ///
    /// <para>Dvojka odpovídá bloku řadovek: krajní dům stojí u ulice, prostřední
    /// dva se k ní dostanou přes něj. Dlaždice mezi každými dvěma domy je
    /// zbytečná a město z toho vypadá jako šachovnice.</para>
    /// </summary>
    private const int MaxBlockHops = 2;

    /// <summary>
    /// Rozšíří „napojeno" na blok dotýkajících se budov — ale jen na
    /// <see cref="MaxBlockHops"/> domů daleko.
    ///
    /// <para><b>Tohle byla ta chyba, kvůli které silnice roky „nefungovaly".</b>
    /// Vlna se dřív šířila bez omezení, takže v souvislé zástavbě stačila JEDNA
    /// dlaždice cesty kdekoli ve městě a všech osm set budov se tvářilo jako
    /// napojených. Automat pak neměl co spravovat a nepoložil ani metr dlažby
    /// — ani u hromadné stavby, ani u tažení, ani u guvernéra. Hráč viděl bloky
    /// bez ulic a měl pravdu.</para>
    ///
    /// <para>Vlna je proto teď <b>po vrstvách</b> (BFS) s pevným dosahem: co je
    /// dál než pár domů od skutečné ulice, napojené není a automat tam cestu
    /// dotáhne.</para>
    /// </summary>
    private void SpreadRoadLinkThroughBlocks()
    {
        // Vrstva 0 = budovy, které se silnice dotýkají samy. Každý další průchod
        // přidá sousedy o jeden skok dál; po MaxBlockHops se zastaví.
        for (int hop = 0; hop < MaxBlockHops; hop++)
        {
            bool changed = false;
            for (int i = 0; i < _buildingCount; i++)
            {
                // Šíří se jen z budov, které napojení měly UŽ PŘED touhle vrstvou
                // — jinak by se v jednom průchodu pole prošlo dopředu a vlna by
                // se protáhla celým městem, přesně jako dřív.
                if (_roadLinked[i] || !TouchesLinkedBuilding(i, hop))
                {
                    continue;
                }

                _roadLinkHop[i] = (byte)(hop + 1);
                changed = true;
            }

            if (!changed)
            {
                return;
            }

            for (int i = 0; i < _buildingCount; i++)
            {
                if (_roadLinkHop[i] == hop + 1)
                {
                    _roadLinked[i] = true;
                }
            }
        }
    }

    /// <summary>
    /// Dotýká se budova hranou jiné budovy, která má napojení z NIŽŠÍ vrstvy?
    /// Omezení na vrstvu je to, co drží vlnu u ulice místo přes celé město.
    /// </summary>
    private bool TouchesLinkedBuilding(int buildingIndex, int fromHop)
    {
        ref var building = ref _buildings[buildingIndex];
        var def = _content.Buildings[building.DefIndex];

        for (int x = building.X; x < building.X + def.FootprintWidth; x++)
        {
            if (IsLinkedBuildingAt(x, building.Y - 1, fromHop)
                || IsLinkedBuildingAt(x, building.Y + def.FootprintHeight, fromHop))
            {
                return true;
            }
        }

        for (int y = building.Y; y < building.Y + def.FootprintHeight; y++)
        {
            if (IsLinkedBuildingAt(building.X - 1, y, fromHop)
                || IsLinkedBuildingAt(building.X + def.FootprintWidth, y, fromHop))
            {
                return true;
            }
        }

        return false;
    }

    private bool IsLinkedBuildingAt(int x, int y, int fromHop) =>
        TryGetBuildingAt(x, y, out int index)
        && index < _roadLinked.Length
        && _roadLinked[index]
        && _roadLinkHop[index] <= fromHop;

    /// <summary>
    /// Sousedí půdorys budovy s nějakou silnicí? Jen ORTOGONÁLNĚ — roh se nepočítá,
    /// po úhlopříčce se zboží nevozí. Proto řádky nad a pod jdou jen přes šířku
    /// půdorysu a sloupce vlevo a vpravo jen přes jeho výšku.
    /// </summary>
    private bool TouchesRoad(in BuildingInstance building)
    {
        var def = _content.Buildings[building.DefIndex];
        for (int x = building.X; x < building.X + def.FootprintWidth; x++)
        {
            if (IsRoad(x, building.Y - 1) || IsRoad(x, building.Y + def.FootprintHeight))
            {
                return true;
            }
        }

        for (int y = building.Y; y < building.Y + def.FootprintHeight; y++)
        {
            if (IsRoad(building.X - 1, y) || IsRoad(building.X + def.FootprintWidth, y))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Postaví budovu bez zaplacení ceny (testy a nástroje na balanc, kde jde
    /// o chování systému, ne o ekonomiku stavby).
    /// </summary>
    public PlacementResult TryPlaceBuildingFree(int defIndex, int x, int y)
    {
        var result = CanPlace(defIndex, x, y);
        if (result != PlacementResult.Ok && result != PlacementResult.NotEnoughResources)
        {
            return result;
        }

        var def = _content.Buildings[defIndex];
        AddBuilding(defIndex, x, y, progress: 0f);
        if (!def.TakesTimeToBuild)
        {
            ApplyBuildingBonuses(def); // „zdarma" znamená bez ceny, ne okamžitě
        }

        SettlementsDirty = true;
        DistrictsDirty = true; // změna zástavby může vytvořit i rozpadnout čtvrť
        _roadLinksDirty = true;
        return PlacementResult.Ok;
    }

    /// <summary>
    /// Položí silnici na dlaždici. Veřejné kvůli testům a nástrojům — ve hře cesty
    /// staví <see cref="RoadBuilder"/> sám, hráč je nekreslí.
    /// </summary>
    public void AddRoadTileForTest(int x, int y) => AddRoadTile(x, y);

    /// <summary>
    /// Nastaví počet obyvatel. Veřejné kvůli testům a nástrojům — ve hře populace
    /// jen roste podle jídla, bydlení a spokojenosti, nikdo ji nezadává.
    ///
    /// <para>Existuje proto, aby se daly zkoušet věci, které závisejí na velikosti
    /// města (hustota provozu, agregátní pohled), bez odtikání hodin růstu.</para>
    /// </summary>
    public void SetPopulationForTest(double population) => Population = Math.Max(0, population);

    /// <summary>Označí dlaždici jako silnici (RoadBuilder, načtení savu). Duplicitní volání je no-op.</summary>
    /// <summary>
    /// Je na dlaždici most? Most je silnice vedoucí po vodě — odvozuje se z terénu,
    /// takže se nikde neukládá a po načtení savu vyjde stejně. Pro render (jiný vzhled).
    /// </summary>
    public bool IsBridge(int x, int y) =>
        IsRoad(x, y) && _content.Biomes[Terrain.BiomeAt(x, y)].IsWater;

    internal void AddRoadTile(int x, int y)
    {
        if (_roads.Add(TileKey.Pack(x, y)))
        {
            _roadTiles.Add(new RoadTile(x, y));
            _roadLinksDirty = true;
            ClearGround(x, y, 1, 1); // co leželo v trase, tomu je konec
            ReportVisual(VisualEventKind.RoadBuilt, x, y);
        }
    }

    /// <summary>
    /// Srovná dlaždice pod stavbou: vytěží uzel (strom, balvan) a zruší
    /// zasazený hájek.
    ///
    /// <para>Bez tohohle stál dům <b>ve stromě</b> a silnice vedla skrz balvan —
    /// obojí se pořád kreslilo a pořád šlo těžit. Uzel se srovná běžnou cestou
    /// (vytěžený), takže když se stavba zbourá, krajina se časem vrátí.</para>
    /// </summary>
    private void ClearGround(int x, int y, int width, int height)
    {
        for (int tileY = y; tileY < y + height; tileY++)
        {
            for (int tileX = x; tileX < x + width; tileX++)
            {
                _plantedNodes.Remove(TileKey.Pack(tileX, tileY));

                var yield = _content.Biomes[Terrain.BiomeAt(tileX, tileY)].ClickYield;
                if (yield is { IsExhaustible: true })
                {
                    _nodes.Deplete(tileX, tileY, TickCount);
                }
            }
        }
    }

    // ----- ruční silnice -----

    /// <summary>Lze na dlaždici položit silnici? (Zastavěno, už silnice, nebo vysazený zdroj = ne.)</summary>
    public PlacementResult CanBuildRoad(int x, int y)
    {
        long tile = TileKey.Pack(x, y);
        if (_roads.Contains(tile))
        {
            return PlacementResult.Occupied;
        }

        if (_occupancy.ContainsKey(tile) || _plantedNodes.ContainsKey(tile)
            || _npcTowns.Blocks(x, y))
        {
            return PlacementResult.Occupied;
        }

        // Přes vodu se smí, ale jen jako MOST — tedy když je úsek dost krátký
        // na to, aby ho most překlenul. Dřív hráč vodní dlaždici nedláždil vůbec
        // a most se dal získat jen náhodou přes auto-silnice; přitom přemostit
        // říčku tažením je ta nejpřirozenější věc, kterou od nástroje čeká.
        if (_content.Biomes[Terrain.BiomeAt(x, y)].IsWater && !CanBridge(x, y))
        {
            return PlacementResult.WrongBiome;
        }

        return PlacementResult.Ok;
    }

    /// <summary>
    /// Dá se přes tuhle vodní dlaždici postavit most? Změří souvislou vodu na
    /// obě strany v obou osách a porovná s <c>roads.maxBridgeSpan</c>: přes
    /// říčku nebo úžinu ano, přes oceán ne.
    /// </summary>
    private bool CanBridge(int x, int y)
    {
        int limit = _content.Gameplay.Roads.MaxBridgeSpan;
        return limit > 0 && (WaterRun(x, y, 1, 0) <= limit || WaterRun(x, y, 0, 1) <= limit);
    }

    /// <summary>Délka souvislé vody přes dlaždici v daném směru (včetně jí samé).</summary>
    private int WaterRun(int x, int y, int stepX, int stepY)
    {
        int limit = _content.Gameplay.Roads.MaxBridgeSpan;
        int run = 1;

        for (int sign = -1; sign <= 1; sign += 2)
        {
            for (int i = 1; i <= limit + 1; i++)
            {
                int tileX = x + stepX * i * sign;
                int tileY = y + stepY * i * sign;
                if (!_content.Biomes[Terrain.BiomeAt(tileX, tileY)].IsWater)
                {
                    break;
                }

                run++;
            }
        }

        return run;
    }

    /// <summary>
    /// Příkaz hráče: postaví kus silnice. Auto-silnice řeší jen nutné napojení,
    /// takže tvar sítě má být na hráči — bez tohohle nešlo město srovnat do ulic.
    /// </summary>
    public PlacementResult TryBuildRoad(int x, int y)
    {
        var result = CanBuildRoad(x, y);
        if (result != PlacementResult.Ok)
        {
            return result;
        }

        AddRoadTile(x, y);
        return PlacementResult.Ok;
    }

    /// <summary>Příkaz hráče: strhne silnici z dlaždice. Vrací false, když tam žádná není.</summary>
    public bool TryRemoveRoad(int x, int y)
    {
        long tile = TileKey.Pack(x, y);
        if (!_roads.Remove(tile))
        {
            return false;
        }

        for (int i = 0; i < _roadTiles.Count; i++)
        {
            if (_roadTiles[i].X == x && _roadTiles[i].Y == y)
            {
                // Swap-remove: na pořadí dlaždic v seznamu nikde nezáleží.
                _roadTiles[i] = _roadTiles[^1];
                _roadTiles.RemoveAt(_roadTiles.Count - 1);
                break;
            }
        }

        _roadLinksDirty = true;
        return true;
    }

    /// <summary>
    /// Fronta vizuálních událostí pro render (dokončená výroba, nová budova…).
    /// Simulace sem jen odkládá; render si je vyzvedne a vyprázdní frontu.
    /// </summary>
    public VisualEventQueue VisualEvents => _visualEvents;

    /// <summary>Ohlásí vizuální událost renderu (přeteklá fronta ji tiše zahodí).</summary>
    internal void ReportVisual(VisualEventKind kind, int x, int y, int resourceIndex = -1) =>
        _visualEvents.Add(new VisualEvent(kind, x, y, resourceIndex));

    /// <summary>Budovy pro systémy simulace (mutace progressu výroby).</summary>
    internal Span<BuildingInstance> BuildingsMutable => _buildings.AsSpan(0, _buildingCount);

    /// <summary>Jeden krok simulace. Deterministický — žádná náhoda bez seedu.</summary>
    public void Tick()
    {
        TickCount++;
        TickBlessings(); // požehnání z modliteb dobíhají spolu se slavností
        TickNpcTowns();  // objevené cizí město se postaví ze skutečných budov a ulic
        TickNpcCities(); // dodávky od sousedů a tiché srůstání obestavěných
        if (_boostTicksRemaining > 0)
        {
            _boostTicksRemaining--;
        }

        if (_boostCooldownRemaining > 0)
        {
            _boostCooldownRemaining--;
        }

        // Období napřed: výroba i růst v tomhle tiku už mají počítat s tím,
        // jestli je zima a jestli je čím topit.
        _seasonSystem.Tick(this);
        _constructionSystem.Tick(this); // staveniště napřed: co se dnes dostavělo, dnes i vyrábí
        _production.Tick(this);
        _toolsSystem.Tick(this); // až po výrobě: ohladí se to, čím se právě pracovalo
        _pollutionSystem.Tick(this); // taky po výrobě: dýmá to, co dnes běželo
        _haulSystem.Tick(this);
        _populationSystem.Tick(this);
        _autoBuild.Tick(this);

        // Až po auto-stavbě: nová technologie často odemkne budovu, a je
        // přirozenější postavit ji hned příští tik než ji držet interval navíc.
        _autoResearch.Tick(this);
        _zoneFill.Tick(this);
        _colonySystem.Tick(this); // guvernér: expanze do nových kolonií
        _settlementSystem.Tick(this);
        if (_debugBuildBoostTicks > 0)
        {
            _debugBuildBoostTicks--;
        }

        _reforestSystem.Tick(this);
        _autoTerraformSystem.Tick(this);
        TickScouting();
        _districtSystem.Tick(this);
        _milestoneBonuses.Tick(this);
        _historySystem.Tick(this);
        _happinessSystem.Tick(this);
        _contractSystem.Tick(this);
        _citizenSystem.Tick(this);
        _questSystem.Tick(this);
        _tutorialSystem.Tick(this);
        _challengeSystem.Tick(this);
        _electionSystem.Tick(this);
        _milestoneSystem.Tick(this);
        _achievementSystem.Tick(this);

        // Těžiště města a UFO nejsou hot path — stačí je řešit jednou za čas.
        if (TickCount % CityCenterIntervalTicks == 0)
        {
            UpdateCityCenter();
        }

        UpdateUfo();
    }

    /// <summary>Jak často se přepočítá těžiště města (tiky) — pomalý systém, ne každý tik.</summary>
    private const int CityCenterIntervalTicks = 50;

    /// <summary>O kolik dlaždic se musí těžiště posunout, aby stálo za přepočet svozu.</summary>
    private const int CityCenterHaulShift = 4;

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

        if (def.NeedsSettlementRank && NearestSettlementRank(x, y) < def.MinSettlementRank)
        {
            return PlacementResult.SettlementTooSmall;
        }

        for (int tileY = y; tileY < y + def.FootprintHeight; tileY++)
        {
            for (int tileX = x; tileX < x + def.FootprintWidth; tileX++)
            {
                long key = TileKey.Pack(tileX, tileY);
                // Cizí město je na mapě stejně skutečné jako hráčova zástavba —
                // stavět skrz cizí domy a ulice nejde.
                if (_occupancy.ContainsKey(key) || _roads.Contains(key)
                    || _npcTowns.Blocks(tileX, tileY))
                {
                    return PlacementResult.Occupied;
                }

                if (!def.IsBiomeAllowed(Terrain.BiomeAt(tileX, tileY)))
                {
                    return PlacementResult.WrongBiome;
                }
            }
        }

        // Přístav a rybolov musí stát na břehu — jinak by „pobřežní" budovy ztratily smysl.
        if (def.NeedsWaterAccess && !HasAdjacentWater(def, x, y))
        {
            return PlacementResult.NeedsWaterAccess;
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

    /// <summary>Dotýká se půdorys budovy aspoň jednou stranou vody (moře, jezera či řeky)?</summary>
    private bool HasAdjacentWater(BuildingDef def, int x, int y)
    {
        for (int tileX = x; tileX < x + def.FootprintWidth; tileX++)
        {
            if (IsWaterTile(tileX, y - 1) || IsWaterTile(tileX, y + def.FootprintHeight))
            {
                return true;
            }
        }

        for (int tileY = y; tileY < y + def.FootprintHeight; tileY++)
        {
            if (IsWaterTile(x - 1, tileY) || IsWaterTile(x + def.FootprintWidth, tileY))
            {
                return true;
            }
        }

        return false;
    }

    private bool IsWaterTile(int x, int y) => _content.Biomes[Terrain.BiomeAt(x, y)].IsWater;

    /// <summary>
    /// Kolik vyhovujících dlaždic má budova daného typu v okolí místa (x, y).
    /// Veřejné kvůli náhledu při stavbě — hráč má bonus vidět dřív, než položí,
    /// jinak by se o pravidle nikdy nedozvěděl.
    /// </summary>
    public int CountAdjacencyTiles(int defIndex, int x, int y)
    {
        var def = _content.Buildings[defIndex];
        return def.Adjacency is { } rule ? CountAdjacencyTiles(def, rule, x, y) : 0;
    }

    /// <summary>
    /// Násobič výroby ze svozu, který by budova na místě (x, y) dostala. Veřejné
    /// kvůli náhledu při stavbě — „tady bude výroba na 60 %" je informace, kterou
    /// hráč potřebuje před kliknutím, ne po něm.
    /// </summary>
    public double HaulMultiplierAt(int x, int y) => _haulSystem.MultiplierAt(x, y);

    /// <summary>
    /// Násobič výroby, který by budova daného typu na místě (x, y) dostala za okolí.
    /// 1.0 = budova bez pravidla nebo místo bez vyhovujícího terénu.
    /// </summary>
    public double AdjacencyMultiplierAt(int defIndex, int x, int y) =>
        AdjacencyMultiplier(_content.Buildings[defIndex], x, y);

    private double AdjacencyMultiplier(BuildingDef def, int x, int y) =>
        def.Adjacency is { } rule ? rule.Multiplier(CountAdjacencyTiles(def, rule, x, y)) : 1.0;

    /// <summary>
    /// Projde obdélník kolem půdorysu do vzdálenosti <c>rule.Radius</c> a spočítá
    /// dlaždice vyhovujícího biomu. Dlaždice pod budovou se nepočítají — bonus je
    /// za okolí, ne za to, na čem budova stojí (to už řeší <c>BiomeMult</c>).
    /// </summary>
    private int CountAdjacencyTiles(BuildingDef def, AdjacencyRule rule, int x, int y)
    {
        int count = 0;
        int minX = x - rule.Radius;
        int maxX = x + def.FootprintWidth - 1 + rule.Radius;
        int minY = y - rule.Radius;
        int maxY = y + def.FootprintHeight - 1 + rule.Radius;

        for (int tileY = minY; tileY <= maxY; tileY++)
        {
            bool insideRows = tileY >= y && tileY < y + def.FootprintHeight;
            for (int tileX = minX; tileX <= maxX; tileX++)
            {
                if (insideRows && tileX >= x && tileX < x + def.FootprintWidth)
                {
                    continue; // vlastní půdorys
                }

                if (rule.Counts(Terrain.BiomeAt(tileX, tileY)))
                {
                    count++;
                }
            }
        }

        return count;
    }

    /// <summary>Běží hromadná stavba, u které se dláždí až na konci?</summary>
    private bool _batchPlacement;

    /// <summary>Indexy budov postavených uvnitř dávky — na konci se napojí.</summary>
    private readonly List<int> _batchPlaced = new();

    /// <summary>
    /// Začne dávku staveb, uvnitř které se auto-silnice odloží.
    ///
    /// <para>Bez odkladu si první ulice ukousla dlaždici, na které měl podle
    /// plánu stát další dům — a z „postav 25" jich vyšlo 23. Uvnitř dávky se
    /// tedy jen staví; ulice se dotáhnou, až je čtvrť celá.</para>
    ///
    /// <para>Volá se v páru s <see cref="EndBatchPlacement"/>; vnořovat se nedá
    /// a nepotřebuje to nikdo — dávku spouští jen hromadná stavba.</para>
    /// </summary>
    internal void BeginBatchPlacement()
    {
        _batchPlacement = true;
        _batchPlaced.Clear();
    }

    /// <summary>Ukončí dávku a napojí všechno, co v ní vzniklo.</summary>
    internal void EndBatchPlacement()
    {
        _batchPlacement = false;

        // Napřed ulice kolem všech dotčených bloků, teprve pak dopojování.
        // V opačném pořadí by hledání cesty vedlo skrz místa, kudy za chvíli
        // stejně povede ulice, a zbyly by po něm slepé útržky.
        foreach (int index in _batchPlaced)
        {
            if (index < _buildingCount)
            {
                _streetPaver.PaveBlockAround(this, _buildings[index].X, _buildings[index].Y);
            }
        }

        foreach (int index in _batchPlaced)
        {
            _roadBuilder.ConnectBuilding(this, index);
        }

        _batchPlaced.Clear();
    }

    /// <summary>
    /// Ulice kolem bloku a pak napojení na zbytek sítě. Samotný obvod bloku
    /// z něj ještě nedělá součást města — k síti se musí dostat cesta.
    /// </summary>
    private void PaveAndConnect(int index, int x, int y)
    {
        _streetPaver.PaveBlockAround(this, x, y);
        _roadBuilder.ConnectBuilding(this, index);
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
        if (!def.TakesTimeToBuild)
        {
            ApplyBuildingBonuses(def); // staveniště nic nedává, dokud nestojí
        }

        ReportVisual(VisualEventKind.BuildingPlaced, x, y);
        if (_batchPlacement)
        {
            // Hromadná stavba dláždí až po sobě — viz BeginBatchPlacement.
            _batchPlaced.Add(_buildingCount - 1);
        }
        else
        {
            PaveAndConnect(_buildingCount - 1, x, y);
        }

        SettlementsDirty = true;
        DistrictsDirty = true; // změna zástavby může vytvořit i rozpadnout čtvrť
        _roadLinksDirty = true;
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
    /// Jako <see cref="TryHarvest(int,int,out int,out int)"/>, ale navíc hlásí, jak
    /// sběr dopadl (krit, úlovek života) — deterministicky ze seedu a pořadí sběru.
    /// </summary>
    public bool TryHarvest(int x, int y, out int resourceIndex, out int amount, out HarvestOutcome outcome)
    {
        resourceIndex = 0;
        amount = 0;
        outcome = HarvestOutcome.Normal;

        // Kam hráč dosáhne rukou, tam taky vidí — ruční sběr je ten druhý
        // (a v rané hře jediný) způsob, jak se svět otevírá.
        Fog.Reveal(x, y, FogRevealRadius);

        long tile = TileKey.Pack(x, y);
        if (_occupancy.ContainsKey(tile))
        {
            return false;
        }

        // Přednost: zasazený uzel → landmark (stádo, háj, žíla) → přírodní výnos biomu.
        var yield = YieldAt(x, y);
        if (yield is null)
        {
            return false;
        }

        // Trvalý bonus Vzestupu + slavnost zvedají výnos (nejmíň původní hodnota).
        // Roční období sem patří taky: podzim je čas sbírat, v zimě toho v krajině
        // moc není. Podlaha na původní hodnotě drží ruční sběr užitečný i v zimě.
        // Kombo: série rychlých sběrů zvedá výnos. Počítá se z tiků, ne z reálného
        // času — deterministické jako všechno ostatní.
        AdvanceCombo();

        int gained = Math.Max(yield.Amount,
            (int)Math.Round(yield.Amount * _bonuses.HarvestMult * BoostMultiplier
                * ElectionHarvestMult * SeasonHarvestMult * ToolHarvestMult * ComboMultiplier));

        // Deterministický krit (aktivní klikání se vyplatí). Nejdřív se zkouší
        // vzácný „úlovek života" — má přednost, aby se s kritem nesčítal do absurdna.
        var harvestConfig = _content.Gameplay.Harvest;
        double roll = CritRoll(_harvestCounter);
        double critChance = harvestConfig.CritChance + _bonuses.CritChanceBonus;
        if (_bonuses.JackpotChance > 0 && roll < _bonuses.JackpotChance)
        {
            gained = (int)Math.Round(gained * harvestConfig.JackpotMultiplier);
            outcome = HarvestOutcome.Jackpot;
        }
        else if (critChance > 0 && roll < critChance)
        {
            gained = (int)Math.Round(gained * harvestConfig.CritMultiplier);
            outcome = HarvestOutcome.Crit;
        }

        // Plný sklad = žádný sběr (a žádný lživý popup v UI).
        if (_resources[yield.ResourceIndex] + gained > _storageCaps[yield.ResourceIndex])
        {
            outcome = HarvestOutcome.Normal;
            return false;
        }

        // Uzel se sběrem ubývá. Až TADY, po všech důvodech, proč se sběr nekoná —
        // jinak by hráč přišel o strom za kliknutí, které mu nic nedalo.
        if (!_nodes.TryConsume(x, y, yield, TickCount))
        {
            outcome = HarvestOutcome.Normal;
            return false;
        }

        _resources[yield.ResourceIndex] += gained;
        _harvestedTotals[yield.ResourceIndex] += gained;
        _harvestCounter++;
        resourceIndex = yield.ResourceIndex;
        amount = gained;
        return true;
    }

    /// <summary>
    /// Posune sérii sběrů. Rychlý sběr ji prodlouží, pomalý ji začne od jedničky.
    /// Volá se u KAŽDÉHO pokusu o sběr, i toho neúspěšného kvůli plnému skladu —
    /// jinak by hráči série zhasla za něco, co neudělal.
    /// </summary>
    private void AdvanceCombo()
    {
        var combo = _content.Gameplay.Combo;
        if (!combo.IsEnabled)
        {
            return;
        }

        _comboStreak = TickCount - _lastHarvestTick <= combo.WindowTicks ? _comboStreak + 1 : 1;
        _lastHarvestTick = TickCount;
    }

    /// <summary>
    /// Kolik sběrů má rozjetá série. 0 = série doběhla; UI podle toho ukazuje
    /// „×3" nad kurzorem.
    /// </summary>
    public int ComboStreak =>
        _content.Gameplay.Combo.IsEnabled && TickCount - _lastHarvestTick <= _content.Gameplay.Combo.WindowTicks
            ? _comboStreak
            : 0;

    /// <summary>Násobič výnosu ze série (1.0 bez série), včetně bonusu <c>combo_power</c>.</summary>
    public double ComboMultiplier => _content.Gameplay.Combo.Multiplier(ComboStreak, _bonuses.ComboPower);

    /// <summary>Kolik sekund série ještě vydrží, než zhasne (0 = neběží).</summary>
    public double ComboSecondsLeft
    {
        get
        {
            var combo = _content.Gameplay.Combo;
            if (ComboStreak == 0)
            {
                return 0;
            }

            long left = combo.WindowTicks - (TickCount - _lastHarvestTick);
            return Math.Max(0, left / TicksPerSecond);
        }
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

        var species = SelectedPlantSpecies;
        if (species is null)
        {
            return PlacementResult.NotUnlocked;
        }

        for (int i = 0; i < species.Cost.Count; i++)
        {
            if (_resources[species.Cost[i].ResourceIndex] < species.Cost[i].Amount)
            {
                return PlacementResult.NotEnoughResources;
            }
        }

        return PlacementResult.Ok;
    }

    /// <summary>
    /// Který druh se zrovna sází. Index do <see cref="PlantingConfig.Species"/>;
    /// neodemčený nebo neplatný se tiše sveze na první dostupný, ať se hráč
    /// nemůže zaseknout na něčem, co nemá.
    /// </summary>
    public int PlantSpeciesIndex { get; private set; }

    /// <summary>Vybraný druh k zasazení; <c>null</c> = není co sázet.</summary>
    public PlantSpecies? SelectedPlantSpecies
    {
        get
        {
            var all = _content.Gameplay.Planting.Species;
            if (PlantSpeciesIndex >= 0 && PlantSpeciesIndex < all.Count && IsPlantSpeciesUnlocked(PlantSpeciesIndex))
            {
                return all[PlantSpeciesIndex];
            }

            for (int i = 0; i < all.Count; i++)
            {
                if (IsPlantSpeciesUnlocked(i))
                {
                    return all[i];
                }
            }

            return null;
        }
    }

    /// <summary>Je druh odemčený? (Výzkum je v datech, ne v kódu.)</summary>
    public bool IsPlantSpeciesUnlocked(int speciesIndex)
    {
        var all = _content.Gameplay.Planting.Species;
        if (speciesIndex < 0 || speciesIndex >= all.Count)
        {
            return false;
        }

        int tech = all[speciesIndex].RequiredTechIndex;
        return tech < 0 || _techLevel[tech] > 0;
    }

    /// <summary>
    /// Vybere druh přímo. Zamčený druh se ignoruje — nabídka ho neukazuje, ale
    /// simulace se na UI nespoléhá.
    /// </summary>
    /// <returns>Přijala se volba?</returns>
    public bool SelectPlantSpecies(int speciesIndex)
    {
        if (!IsPlantSpeciesUnlocked(speciesIndex))
        {
            return false;
        }

        PlantSpeciesIndex = speciesIndex;
        return true;
    }

    /// <summary>Přepne na další odemčený druh (klávesová zkratka a gamepad).</summary>
    public void CyclePlantSpecies()
    {
        var all = _content.Gameplay.Planting.Species;
        for (int step = 1; step <= all.Count; step++)
        {
            int candidate = (PlantSpeciesIndex + step) % all.Count;
            if (IsPlantSpeciesUnlocked(candidate))
            {
                PlantSpeciesIndex = candidate;
                return;
            }
        }
    }

    /// <summary>Příkaz hráče: zasadí obnovitelný zdroj (háj) — pak se dá těžit klikem jako přírodní.</summary>
    public PlacementResult TryPlant(int x, int y)
    {
        var result = CanPlant(x, y);
        if (result != PlacementResult.Ok)
        {
            return result;
        }

        var species = SelectedPlantSpecies!;
        for (int i = 0; i < species.Cost.Count; i++)
        {
            _resources[species.Cost[i].ResourceIndex] -= species.Cost[i].Amount;
        }

        _plantedNodes[TileKey.Pack(x, y)] = new ClickYield(species.ResourceIndex, species.Amount);
        _nodes.Restore(x, y); // zasazený háj stojí na plném uzlu, i když se tu předtím těžilo
        return PlacementResult.Ok;
    }

    /// <summary>
    /// Kolik sběrů dlaždici ještě zbývá (0 = vytěženo a zatím nedorostlo).
    /// Render podle toho kreslí plný strom, nakousnutý, nebo pařez.
    /// </summary>
    public int NodeChargesLeft(int x, int y)
    {
        var yield = YieldAt(x, y);
        return yield is null ? 0 : _nodes.ChargesLeft(x, y, yield, TickCount);
    }

    /// <summary>Kolik sběrů uzel na dlaždici pojme, když je plný.</summary>
    public int NodeMaxCharges(int x, int y) => YieldAt(x, y)?.Charges ?? 0;

    /// <summary>Co dlaždice dává ručnímu sběru — zasazený uzel, landmark, nebo biom.</summary>
    private ClickYield? YieldAt(int x, int y)
    {
        if (_plantedNodes.TryGetValue(TileKey.Pack(x, y), out var planted))
        {
            return planted;
        }

        int landmark = LandmarkAt(x, y);
        return landmark >= 0 && _content.Landmarks[landmark].IsHarvestable
            ? _content.Landmarks[landmark].ClickYield
            : _content.Biomes[Terrain.BiomeAt(x, y)].ClickYield;
    }

    /// <summary>Evidence vytěžených dlaždic — pro sav a testy.</summary>
    public NodeLedger Nodes => _nodes;

    /// <summary>
    /// Budova si vezme z okolí jednu dávku suroviny. Vrací false, když v dosahu
    /// není co brát — výrobna pak stojí, dokud něco nedoroste (nebo dokud ji hráč
    /// nepřesune či nezasadí nový háj).
    ///
    /// <para>Prochází se spirálou od budovy ven a pozice se drží na instanci, takže
    /// se okolí neprohledává znovu od nuly při každém cyklu. Kurzor se posune,
    /// teprve když dlaždice dojde.</para>
    /// </summary>
    internal bool TryConsumeTerrain(ref BuildingInstance building, BuildingDef def)
    {
        int radius = def.TerrainHarvestRadius;
        int side = radius * 2 + 1;
        int tiles = side * side;

        for (int step = 0; step < tiles; step++)
        {
            int index = (building.HarvestCursor + step) % tiles;
            int tx = building.X + index % side - radius;
            int ty = building.Y + index / side - radius;

            var yield = YieldAt(tx, ty);
            if (yield is null || !_nodes.TryConsume(tx, ty, yield, TickCount))
            {
                continue;
            }

            building.HarvestCursor = index;
            building.OutOfResources = false;
            return true;
        }

        building.OutOfResources = true;
        return false;
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

    // ----- počasí (živá mapa) -----

    /// <summary>
    /// Biom, ve kterém město stojí (podle první budovy) — počasí je vázané na něj.
    /// Bez budov padne na biom v počátku souřadnic.
    /// </summary>
    public int CityBiome => _buildingCount > 0
        ? Terrain.BiomeAt(_buildings[0].X, _buildings[0].Y)
        : Terrain.BiomeAt(0, 0);

    /// <summary>Index aktuálního jevu počasí, nebo −1 (žádné počasí v datech / pro biom).</summary>
    public int CurrentWeatherIndex
    {
        get
        {
            int index = _weatherSystem.CurrentWeather(CityBiome, TickCount);
            return _weatherSystem.IsActive(index, TickCount) ? index : -1;
        }
    }

    /// <summary>Kolik sekund zbývá do konce aktuálního jevu (0 = žádný neběží).</summary>
    public double WeatherSecondsRemaining => _weatherSystem.SecondsRemaining(CurrentWeatherIndex, TickCount);

    /// <summary>
    /// Násobič výroby od počasí. Extrémní jev (tornádo, vánice…) flow dočasně sníží,
    /// ambientní počasí ho nechá být. Nikdy nic neničí — jen zpomalí (soft pressure).
    /// </summary>
    public double WeatherProductionMult
    {
        get
        {
            int index = CurrentWeatherIndex;
            return index < 0 ? 1.0 : _content.Weather[index].ProductionMult;
        }
    }

    /// <summary>
    /// Spokojenost města 0–1 (1 = ideál). Nízká brzdí růst populace, nikdy nikoho
    /// nezabíjí. Počítá <see cref="HappinessSystem"/> na nízké frekvenci.
    /// </summary>
    public double Happiness { get; internal set; } = 1.0;

    /// <summary>Násobič růstu populace daný spokojeností (pro populaci i pro HUD).</summary>
    public double HappinessGrowthFactor => _content.Gameplay.Happiness.GrowthFactor(Happiness);

    /// <summary>Probíhá právě extrémní jev (katastrofa)? Pro HUD a varování.</summary>
    public bool IsExtremeWeather
    {
        get
        {
            int index = CurrentWeatherIndex;
            return index >= 0 && _content.Weather[index].Extreme;
        }
    }

    // ----- UFO (mapa dělá věci sama od sebe) -----

    /// <summary>Visí právě UFO nad mapou? Render podle toho kreslí talíř a paprsek.</summary>
    public bool IsUfoVisible => _ufoSystem.IsVisible(TickCount);

    /// <summary>Kam UFO míří (má smysl jen když <see cref="IsUfoVisible"/>).</summary>
    public (int X, int Y) UfoTarget => _ufoSystem.TargetTile(_ufoSystem.WindowAt(TickCount), CityCenterX, CityCenterY);

    /// <summary>Střed města (těžiště zástavby) — kolem něj se dějí věci, které mají hráče zajímat.</summary>
    public int CityCenterX { get; private set; }

    /// <summary>Střed města (těžiště zástavby).</summary>
    public int CityCenterY { get; private set; }

    /// <summary>
    /// Doletí UFO a provede svůj zásah, jakmile návštěva skončí. Zásah proběhne
    /// nejvýš jednou za okno — proto se poslední vyřízené okno ukládá do savu.
    /// </summary>
    private void UpdateUfo()
    {
        if (!_content.Ufo.IsEnabled)
        {
            return;
        }

        long window = _ufoSystem.WindowAt(TickCount);
        if (window <= _lastUfoWindow || _ufoSystem.IsVisible(TickCount))
        {
            return; // ještě letí (nebo tohle okno už bylo vyřízené)
        }

        _lastUfoWindow = window;
        int actionIndex = _ufoSystem.ActionIn(window);
        if (actionIndex < 0)
        {
            return;
        }

        var (x, y) = _ufoSystem.TargetTile(window, CityCenterX, CityCenterY);
        ApplyUfoAction(_content.Ufo.Actions[actionIndex], x, y);
    }

    /// <summary>
    /// Behavior-ID hook: řetězec z <c>ufo.json</c> → konkrétní zásah do světa.
    /// Neznámé chování se tiše přeskočí (data smí předběhnout kód).
    /// </summary>
    private void ApplyUfoAction(UfoActionDef action, int x, int y)
    {
        switch (action.Behavior)
        {
            case "abduct":
                // Únos: pár lidí zmizí. Nikdy do záporu — z prázdného města není koho unést.
                double taken = Math.Min(Population, action.Magnitude);
                if (taken <= 0) return;
                Population -= taken;
                break;

            case "demolish":
                if (!DemolishNearest(x, y)) return;
                break;

            case "plant":
                // Kruh v obilí: pár dlaždic se promění v sklizitelný porost.
                if (!PlantUfoPatch(x, y, (int)action.Magnitude)) return;
                break;

            case "terraform":
                if (!TerraformPatch(x, y, (int)action.Magnitude)) return;
                break;

            case "gift":
                // Mimozemská pozornost — do první známé suroviny, ať je co slavit.
                int resource = FirstKnownResource();
                if (resource < 0) return;
                AddResource(resource, action.Magnitude);
                break;

            default:
                return; // „flyby" i neznámé chování: UFO se jen ukázalo
        }

        EnqueueNotification(new GameNotification(NotificationKind.WorldEvent, "toast.ufo", action.MessageKey));
    }

    /// <summary>Sestřelí budovu nejblíž zásahu. Vrací false, když ve městě nic nestojí.</summary>
    private bool DemolishNearest(int x, int y)
    {
        int best = -1;
        long bestDistance = long.MaxValue;
        for (int i = 0; i < _buildingCount; i++)
        {
            long dx = _buildings[i].X - x, dy = _buildings[i].Y - y;
            long distance = dx * dx + dy * dy;
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = i;
            }
        }

        return best >= 0 && TryDemolish(best) == PlacementResult.Ok;
    }

    /// <summary>Zaseje sklizitelný porost na volné dlaždice kolem zásahu (kruh v obilí).</summary>
    private bool PlantUfoPatch(int x, int y, int tiles)
    {
        // UFO seje ten nejzákladnější druh — kruh v obilí není odměna za výzkum.
        var species = _content.Gameplay.Planting.Species.Count > 0
            ? _content.Gameplay.Planting.Species[0]
            : null;
        if (species is null)
        {
            return false;
        }

        bool any = false;
        for (int i = 0; i < tiles; i++)
        {
            int tx = x + i % 3 - 1;
            int ty = y + i / 3 - 1;
            long tile = TileKey.Pack(tx, ty);
            if (_occupancy.ContainsKey(tile) || _roads.Contains(tile) || _plantedNodes.ContainsKey(tile)
                || _content.Biomes[BiomeAt(tx, ty)].IsWater)
            {
                continue;
            }

            _plantedNodes[tile] = new ClickYield(species.ResourceIndex, species.Amount);
            any = true;
        }

        return any;
    }

    /// <summary>
    /// Přemaluje kus krajiny na jiný biom. Vybere se biom stejného druhu (souš zůstane
    /// souší), ať terraformace nezatopí město ani nezanechá budovy ve vodě.
    /// </summary>
    private bool TerraformPatch(int x, int y, int tiles)
    {
        byte current = BiomeAt(x, y);
        bool water = _content.Biomes[current].IsWater;

        byte target = current;
        for (byte i = 0; i < _content.Biomes.Count; i++)
        {
            if (_content.Biomes[i].IsWater == water && i != current)
            {
                target = i;
                break;
            }
        }

        if (target == current)
        {
            return false;
        }

        for (int i = 0; i < tiles; i++)
        {
            SetBiomeOverride(x + i % 3 - 1, y + i / 3 - 1, target);
        }

        return true;
    }

    private int FirstKnownResource()
    {
        for (int i = 0; i < _resourceKnown.Length; i++)
        {
            if (_resourceKnown[i])
            {
                return i;
            }
        }

        return _resourceKnown.Length > 0 ? 0 : -1;
    }

    /// <summary>Přepočítá těžiště zástavby (levné, běží na nízké frekvenci).</summary>
    private void UpdateCityCenter()
    {
        if (_buildingCount == 0)
        {
            CityCenterX = CityCenterY = 0;
            return;
        }

        long sumX = 0, sumY = 0;
        for (int i = 0; i < _buildingCount; i++)
        {
            sumX += _buildings[i].X;
            sumY += _buildings[i].Y;
        }

        int centerX = (int)(sumX / _buildingCount);
        int centerY = (int)(sumY / _buildingCount);

        // Těžiště je jedno ze sběrných míst svozu. Přepočítávat celé město po
        // každém posunu o dlaždici je zbytečné — až znatelný posun stojí za to.
        if (Math.Abs(centerX - CityCenterX) + Math.Abs(centerY - CityCenterY) >= CityCenterHaulShift)
        {
            HaulDirty = true;
        }

        CityCenterX = centerX;
        CityCenterY = centerY;
    }

    // ----- odemykatelné funkce (postupné odhalování UI) -----

    /// <summary>
    /// Je funkce odemčená? Dokud není, UI ji NEUKAZUJE — hráč tak nedostane na
    /// začátku patnáct tlačítek, kterým nerozumí. Bez definic (prázdná data) je
    /// vše dostupné, aby se hra nedala „zamknout" chybějícím obsahem.
    /// </summary>
    public bool IsFeatureUnlocked(string featureId)
    {
        if (!_content.Features.TryIndexOf(featureId, out int index))
        {
            return true; // funkce bez definice = bez omezení
        }

        var unlock = _content.Features[index].Unlock;
        return EvaluateMetric(unlock.Kind, unlock.Param) >= unlock.Target;
    }

    /// <summary>
    /// Smí hráč těžit orbitálním laserem? Je to pozdní podoba ručního sběru:
    /// místo klikání na jednotlivé stromy táhne paprsek přes krajinu.
    ///
    /// <para>Vypnuté v datech i nezpřístupněné funkcí = false, takže starší
    /// data i začínající hra se chovají jako dřív.</para>
    /// </summary>
    public bool LaserUnlocked =>
        _content.Gameplay.Laser.IsEnabled && IsFeatureUnlocked(_content.Gameplay.Laser.FeatureId);

    /// <summary>Kolik funkcí je odemčeno — UI podle změny pozná, že má přestavět lištu.</summary>
    public int UnlockedFeatureCount
    {
        get
        {
            int count = 0;
            foreach (var feature in _content.Features.All)
            {
                if (EvaluateMetric(feature.Unlock.Kind, feature.Unlock.Param) >= feature.Unlock.Target)
                {
                    count++;
                }
            }

            return count;
        }
    }

    // ----- guvernér: automatické vylepšování budov -----

    /// <summary>Nejvyšší míra, na kterou jde guvernérovo vylepšování nastavit.</summary>
    public const int MaxAutoUpgradeLevel = 3;

    /// <summary>ID technologie, která guvernérovu správu vylepšení odemyká (data-driven odkaz).</summary>
    public const string GovernorTechId = "municipal_administration";

    private int _autoUpgradeLevel;

    /// <summary>
    /// Je guvernérova správa vylepšení odemčená? Automatizace se ODEMYKÁ, není
    /// výchozí (living-city.md §4 — jinak by hráč neměl co dělat).
    /// </summary>
    public bool IsGovernorUnlocked =>
        _content.Techs.TryIndexOf(GovernorTechId, out int index) && _techLevel[index] > 0;

    /// <summary>
    /// Jak moc si guvernér vylepšuje budovy sám: 0 = vůbec, 1 = jen bydlení,
    /// 2 = bydlení i výroba, 3 = vše a svižně. Zároveň to je počet vylepšení,
    /// která smí provést za jeden interval — vyšší stupeň = agresivnější správa.
    /// </summary>
    public int AutoUpgradeLevel => IsGovernorUnlocked ? _autoUpgradeLevel : 0;

    /// <summary>ID technologií, které postupně otevírají vyšší stupně správy.</summary>
    public const string GovernorLevel2TechId = "civil_service";

    /// <summary>Technologie pro nejvyšší stupeň guvernérovy správy.</summary>
    public const string GovernorLevel3TechId = "city_planning_office";

    /// <summary>
    /// Nejvyšší stupeň, na který jde guvernéra právě teď nastavit.
    ///
    /// <para>Stupně se ODEMYKAJÍ výzkumem, nejsou to volné posuvníky: automatizace
    /// je odměna za postup, ne přepínač dostupný od začátku (living-city.md §4).
    /// Bez toho by hráč hned na začátku přepnul na „vše a svižně" a přišel o celou
    /// střední hru.</para>
    /// </summary>
    public int MaxUnlockedAutoUpgradeLevel
    {
        get
        {
            if (!IsGovernorUnlocked)
            {
                return 0;
            }

            if (IsTechResearchedById(GovernorLevel3TechId))
            {
                return MaxAutoUpgradeLevel;
            }

            return IsTechResearchedById(GovernorLevel2TechId) ? 2 : 1;
        }
    }

    /// <summary>Je technologie daného ID vyzkoumaná? (Neznámé ID = ne.)</summary>
    private bool IsTechResearchedById(string techId) =>
        _content.Techs.TryIndexOf(techId, out int index) && _techLevel[index] > 0;

    /// <summary>Příkaz hráče: nastaví míru automatického vylepšování (ořízne se do odemčeného rozsahu).</summary>
    public void SetAutoUpgradeLevel(int level) =>
        _autoUpgradeLevel = Math.Clamp(level, 0, MaxUnlockedAutoUpgradeLevel);

    // ----- guvernér: rezerva surovin -----

    /// <summary>Technologie, která odemyká nastavení rezervy.</summary>
    public const string GovernorReserveTechId = "works_department";

    private double _governorReserve;

    /// <summary>Umí guvernér šetřit (je rezerva odemčená)?</summary>
    public bool IsGovernorReserveUnlocked =>
        IsGovernorUnlocked && IsTechResearchedById(GovernorReserveTechId);

    /// <summary>
    /// Podíl zásob (0–0.9), který guvernér nesmí utratit.
    ///
    /// <para>Bez rezervy sebere automatika i to, co si hráč schovával na konkrétní
    /// stavbu — a to je nejrychlejší cesta, jak si hráč automatizaci zase vypne.
    /// Tohle je ta pojistka, díky které ji nechá zapnutou.</para>
    /// </summary>
    public double GovernorReserve => IsGovernorReserveUnlocked ? _governorReserve : 0.0;

    /// <summary>Příkaz hráče: nastaví rezervu (ořízne se na 0–90 %).</summary>
    public void SetGovernorReserve(double fraction) =>
        _governorReserve = Math.Clamp(fraction, 0.0, 0.9);

    /// <summary>Surová rezerva pro save (bez ohledu na odemčení).</summary>
    internal double GovernorReserveRaw => _governorReserve;

    /// <summary>Obnoví rezervu ze savu.</summary>
    internal void RestoreGovernorReserve(double fraction) => SetGovernorReserve(fraction);

    /// <summary>
    /// Smí automatika utratit tuhle cenu, aniž by sáhla do rezervy? Počítá se
    /// proti kapacitě skladu, ne proti aktuálnímu stavu — jinak by se rezerva
    /// sama snižovala tím, jak zásoby ubývají.
    /// </summary>
    internal bool AutomationCanSpend(IReadOnlyList<ResourceAmount> cost)
    {
        double reserve = GovernorReserve;
        if (reserve <= 0)
        {
            return true;
        }

        for (int i = 0; i < cost.Count; i++)
        {
            int index = cost[i].ResourceIndex;
            if (_resources[index] - cost[i].Amount < _storageCaps[index] * reserve)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Plán guvernéra: kam tlačit (velikost vs. kvalita) a co smí stavět.
    /// Automatizace bez kontroly není pomocník, ale spolubydlící, který
    /// přestavuje byt podle svého.
    /// </summary>
    public GovernorPlan Plan { get; } = new();

    /// <summary>Obnoví plán guvernéra ze savu.</summary>
    internal void RestorePlan(GovernorFocus focus, IEnumerable<string> blockedCategories) =>
        Plan.Restore(focus, blockedCategories);

    /// <summary>ID technologie, po které umí guvernér i slučovat bloky (data-driven odkaz).</summary>
    public const string GovernorMergeTechId = "urban_planning";

    private bool _autoMerge;

    /// <summary>
    /// Umí guvernér slučovat bloky 2×2 sám? Odemyká se vlastní technologií —
    /// slučování mění půdorys města, takže se nemá zapnout nepozorovaně spolu
    /// s běžným vylepšováním.
    /// </summary>
    public bool IsAutoMergeUnlocked =>
        IsGovernorUnlocked
        && _content.Techs.TryIndexOf(GovernorMergeTechId, out int index) && _techLevel[index] > 0;

    /// <summary>Slučuje guvernér bloky sám? (Bez technologie vždy ne.)</summary>
    public bool AutoMerge => IsAutoMergeUnlocked && _autoMerge;

    /// <summary>Příkaz hráče: zapne/vypne automatické slučování bloků.</summary>
    public void SetAutoMerge(bool enabled) => _autoMerge = enabled;

    /// <summary>Surová volba pro save (bez ohledu na odemčení).</summary>
    internal bool AutoMergeRaw => _autoMerge;

    /// <summary>Obnoví volbu automatického slučování ze savu.</summary>
    internal void RestoreAutoMerge(bool enabled) => _autoMerge = enabled;

    /// <summary>Smí guvernér na téhle úrovni vylepšit budovu dané kategorie?</summary>
    internal bool AutoUpgradeCovers(string category) => AutoUpgradeLevel switch
    {
        <= 0 => false,
        1 => category == "housing",
        2 => category is "housing" or "production",
        _ => true,
    };

    /// <summary>Míra vylepšování pro serializaci savu.</summary>
    internal int AutoUpgradeLevelRaw => _autoUpgradeLevel;

    /// <summary>Obnoví míru vylepšování při načtení savu.</summary>
    internal void RestoreAutoUpgradeLevel(int level) => SetAutoUpgradeLevel(level);

    // ----- politiky růstu (automatizace, stupeň 4) -----

    /// <summary>Kolik budov smí auto-stavba i plnění zón položit za interval (výchozí 1; politika „build_pace" zvedá).</summary>
    public int BuildsPerInterval { get; private set; } = 1;

    /// <summary>
    /// Nejkratší možný interval auto-stavby. Pod tímhle už by se stavělo skoro
    /// každý tik a město by před očima „vybuchlo" místo aby rostlo — a hráč by
    /// z toho neměl radost, protože by neviděl, co se děje.
    /// </summary>
    private const int MinAutoBuildInterval = 6;

    /// <summary>
    /// Jak často se auto-stavba pokusí stavět, po započtení bonusu
    /// <c>autobuild_speed</c>.
    ///
    /// <para>Tenhle bonus byl doteď mrtvý: počítal se do <see cref="Bonuses"/>,
    /// ale nikdo ho nečetl. Přitom je to jeden z mála trvalých bonusů, který je
    /// opravdu <b>vidět na mapě</b> — po Vzestupu se město rozrůstá znatelně
    /// rychleji, což je přesně ta odměna, kvůli které se resetuje.</para>
    /// </summary>
    public int AutoBuildInterval
    {
        get
        {
            int baseInterval = _content.Gameplay.AutoBuild.IntervalTicks;

            // Dno smí bonus jen zastavit, ne PŘEBÍT data: když si obsah řekne
            // o rychlejší tempo, než je dno, platí obsah. Jinak by dno tiše
            // zpomalilo hru proti tomu, co je v datech napsané.
            int floor = Math.Min(baseInterval, MinAutoBuildInterval);
            return Math.Max(floor, (int)Math.Round(baseInterval / AutoBuildSpeed()));
        }
    }

    /// <summary>
    /// Strop staveb za jeden interval. Existuje kvůli snímkové frekvenci, ne
    /// kvůli balancu: každá stavba hledá místo a napojuje se na síť, takže
    /// neomezená dávka by hru na okamžik zastavila. Při dně intervalu (6 tiků)
    /// je to pořád přes tři tisíce budov za sekundu — na suroviny narazí hráč
    /// dřív než na tenhle strop.
    /// </summary>
    private const int MaxAutoBuildBudget = 2048;

    /// <summary>
    /// Kolik staveb se za interval zkusí. Když už interval narazil na dno,
    /// přeteče zbytek zrychlení sem — jinak by se bonus nad určitou úroveň
    /// přestal projevovat a další upgrady by byly k ničemu.
    ///
    /// <para>Počítá se v <c>double</c> a ořezává až na konci. Dřív se výsledek
    /// přetypovával na <c>int</c> uprostřed výpočtu — jenže bonus se skládá
    /// násobně přes desítky úrovní, takže na maximu vyšlo číslo mimo rozsah
    /// <c>int</c>, přeteklo do záporu a <c>Math.Max(1, …)</c> ho srazil na
    /// <b>jednu</b> stavbu za interval. Čím víc měl hráč vylepšení, tím pomaleji
    /// se stavělo.</para>
    /// </summary>
    public int AutoBuildBudget
    {
        get
        {
            double baseInterval = Math.Max(1, _content.Gameplay.AutoBuild.IntervalTicks);

            // Co se nevešlo do zkrácení intervalu, jde do počtu staveb za interval.
            double perInterval = BuildsPerInterval * AutoBuildSpeed() * (AutoBuildInterval / baseInterval);

            // Zaměření plánu rozpočet váží. Vyvážený plán má váhu 1, takže se
            // nic nemění pro toho, kdo si nic nenastavil.
            perInterval *= Plan.GrowthWeight;
            if (!Plan.BuildsAtAll)
            {
                return 0; // „jen kvalita" znamená, že město nemá růst do šířky
            }

            return (int)Math.Clamp(Math.Round(perInterval), 1, MaxAutoBuildBudget);
        }
    }

    /// <summary>
    /// Kolik budov guvernér za interval vylepší. Stupeň říká <b>co</b> smí
    /// vylepšovat, tempo Vzestupu <b>jak rychle</b> — bez druhé půlky zůstalo
    /// vylepšování na třech budovách za interval i s vymaxovanými upgrady,
    /// což u města o tisících domů znamenalo čekat hodiny.
    /// </summary>
    public int AutoUpgradeBudget
    {
        get
        {
            int level = AutoUpgradeLevel;
            if (level <= 0)
            {
                return 0;
            }

            if (!Plan.UpgradesAtAll)
            {
                return 0; // „jen růst" znamená, že se nemá vylepšovat
            }

            // Rozpočet se odvozuje od tempa stavby, ne od jejího VÁŽENÉHO
            // výsledku — jinak by „jen kvalita" (kde se nestaví) nevylepšovala
            // vůbec, protože by násobila nulou.
            double perInterval = (double)level * UnweightedBuildBudget / Math.Max(1, BuildsPerInterval);
            perInterval *= Plan.QualityWeight;
            return (int)Math.Clamp(Math.Round(perInterval), 1, MaxAutoBuildBudget);
        }
    }

    /// <summary>
    /// Tempo stavby <b>bez</b> vážení plánem. Slouží jako společný základ pro
    /// obě strany plánu: kdyby se vylepšování odvozovalo od váženého tempa
    /// stavby, „jen kvalita" by násobila nulou a nevylepšovala by nic.
    /// </summary>
    private int UnweightedBuildBudget
    {
        get
        {
            double baseInterval = Math.Max(1, _content.Gameplay.AutoBuild.IntervalTicks);
            double perInterval = BuildsPerInterval * AutoBuildSpeed() * (AutoBuildInterval / baseInterval);
            return (int)Math.Clamp(Math.Round(perInterval), 1, MaxAutoBuildBudget);
        }
    }

    /// <summary>Násobič tempa auto-stavby (nikdy pod 1 — zpomalovat ho nic neumí).</summary>
    private double AutoBuildSpeed() =>
        Math.Max(1.0, _bonuses.AutoBuildSpeed) * (_debugBuildBoostTicks > 0 ? _debugBuildBoost : 1.0);

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

    // ----- landmarky (živá mapa) -----

    /// <summary>
    /// Který landmark stojí na dlaždici (−1 = žádný)? Výskyt je ČISTÁ FUNKCE pozice
    /// a seedu — nic se negeneruje dopředu ani neukládá, takže po načtení savu je
    /// mapa bodů zájmu stejná. Zastavěná dlaždice landmark překryje.
    /// </summary>
    public int LandmarkAt(int x, int y)
    {
        var landmarks = _content.Landmarks;
        if (landmarks.Count == 0)
        {
            return -1;
        }

        // POŘADÍ TESTŮ JE VÝKONNOSTNÍ ROZHODNUTÍ: hash je pár násobení, kdežto
        // BiomeAt vzorkuje fBm šum. Landmarky jsou vzácné (rarity ve stovkách),
        // takže levný hash odmítne drtivou většinu dlaždic dřív, než se sáhne
        // na terén. Render se ptá na desítky tisíc dlaždic za snímek — obrácené
        // pořadí sráželo FPS.
        int biome = -1;
        for (int i = 0; i < landmarks.Count; i++)
        {
            // Sůl z indexu → každý typ má vlastní rozmístění, ne všechny na stejných místech.
            if (LandmarkHash(x, y, i) % (ulong)landmarks[i].Rarity != 0)
            {
                continue;
            }

            if (biome < 0)
            {
                if (_occupancy.ContainsKey(TileKey.Pack(x, y)))
                {
                    return -1; // zástavba landmark překryje
                }

                biome = Terrain.BiomeAt(x, y);
            }

            if (landmarks[i].AppliesTo(biome))
            {
                return i;
            }
        }

        return -1;
    }

    private ulong LandmarkHash(int x, int y, int landmarkIndex)
    {
        var rng = new WorldGen.SplitMix64(unchecked(
            (ulong)Seed
            ^ ((ulong)(uint)x * 0x9E3779B97F4A7C15UL)
            ^ ((ulong)(uint)y * 0xC2B2AE3D27D4EB4FUL)
            ^ ((ulong)(uint)landmarkIndex * 0x165667B19E3779F9UL)));
        return rng.Next();
    }

    // ----- objevování mapy (skrýše) -----

    private const int DiscoveryRate = 500; // zhruba každá N-tá suchá dlaždice skrývá skrýš

    /// <summary>
    /// Je na (suché) dlaždici skrýš k objevení? Deterministické z pozice (nekonečná mapa).
    /// Štěstí z Vzestupu zahušťuje síť skrýší — dělitel se zmenšuje, poloha zůstává
    /// funkcí souřadnic, takže se pořád nic neukládá.
    /// </summary>
    public bool IsDiscoveryTile(int x, int y)
    {
        if (_content.Biomes[Terrain.BiomeAt(x, y)].IsWater)
        {
            return false;
        }

        int rate = Math.Max(20, (int)Math.Round(DiscoveryRate / Math.Max(0.1, _bonuses.DiscoveryLuck)));
        return DiscoveryHash(x, y) % (ulong)rate == 0;
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

        // Losuje se JEN ze surovin, které hráč zná — skrýš nesmí vysypat nanomateriál
        // dřív, než se k němu hráč vůbec dostal.
        int known = KnownResourceCount;
        if (known == 0)
        {
            return false;
        }

        int pick = (int)(roll % (ulong)known);
        resourceIndex = 0;
        for (int i = 0, seen = 0; i < _resourceKnown.Length; i++)
        {
            if (!_resourceKnown[i])
            {
                continue;
            }

            if (seen++ == pick)
            {
                resourceIndex = i;
                break;
            }
        }

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
        MetricKind.Population => Numbers.ToLong(Population),
        MetricKind.HousingCapacity => Numbers.ToLong(HousingCapacity),
        MetricKind.Harvested => _harvestedTotals[param],
        MetricKind.ResourceStock => (long)_resources[param],
        MetricKind.TotalBuildings => _buildingCount,
        MetricKind.BuildingOfType => CountBuildingsOfType(param),
        MetricKind.ResearchedTech => _techLevel[param] > 0 ? 1 : 0,
        MetricKind.AscensionLevel => AscensionLevel,
        MetricKind.DayNumber => DayNumber,
        MetricKind.PlantedNodes => _plantedNodes.Count,
        MetricKind.TerraformedTiles => TerraformedTiles,
        MetricKind.MergedBuildings => MergedBuildings,
        MetricKind.WondersCompleted => WondersCompleted,
        MetricKind.Prayers => PrayerCount,
        MetricKind.CitiesJoined => CitiesJoined,
        MetricKind.Explored => Fog.ExploredChunks,
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

    /// <summary>
    /// Postavilo tohle město někdy na daném biomu? Podklad pro kroniku —
    /// „kde všude jsi stavěl" je sběratelský cíl, který přesahuje jednu hru.
    /// </summary>
    public bool HasSettledBiome(int biomeIndex) => _settledBiomes[biomeIndex];

    // ----- milníky -----

    /// <summary>Byl milník už oslaven? (Každý se spustí jen jednou za hru.)</summary>
    public bool IsMilestoneReached(int index) => _milestonesReached[index];

    /// <summary>Označí milník za oslavený (volá systém milníků po oznámení).</summary>
    internal void MarkMilestoneReached(int index) => _milestonesReached[index] = true;

    /// <summary>Indexy dosažených milníků (pro serializaci savu).</summary>
    internal IEnumerable<int> ReachedMilestoneIndices()
    {
        for (int i = 0; i < _milestonesReached.Length; i++)
        {
            if (_milestonesReached[i])
            {
                yield return i;
            }
        }
    }

    /// <summary>Dosažené milníky pro testy (vnitřní seznam je jen pro save).</summary>
    public IReadOnlyList<int> ReachedMilestoneIndicesForTest() => ReachedMilestoneIndices().ToList();

    /// <summary>Obnoví dosažený milník ze savu (bez oslavy).</summary>
    internal void RestoreMilestone(int index)
    {
        if (index >= 0 && index < _milestonesReached.Length)
        {
            _milestonesReached[index] = true;
        }
    }

    // ----- volby -----

    /// <summary>Kolikáté volební období běží; −1 = volby se ještě nekonaly.</summary>
    public long ElectionTerm { get; private set; } = -1;

    /// <summary>Index zvoleného programu do <see cref="ElectionConfig.Candidates"/>; −1 = zatím nikdo.</summary>
    public int ElectedCandidate { get; private set; } = -1;

    /// <summary>Kolik programů je na aktuální kandidátce.</summary>
    public int BallotSize => _content.Elections.IsEnabled ? _content.Elections.BallotSize : 0;

    /// <summary>Program na daném místě kandidátky (index do fondu programů).</summary>
    public int BallotAt(int slot) => _ballot[slot];

    /// <summary>Vybral už hráč (nebo automat) program pro tohle období?</summary>
    public bool HasElected => ElectedCandidate >= 0;

    /// <summary>Kolik herních dní zbývá do dalších voleb.</summary>
    public long DaysUntilElection => !_content.Elections.IsEnabled
        ? 0
        : Math.Max(0, (ElectionTerm + 1) * _content.Elections.TermDays - DayNumber);

    /// <summary>
    /// Příkaz hráče: zvolí program z kandidátky. Volba platí do konce období.
    /// </summary>
    public void ElectCandidate(int candidateIndex)
    {
        if (candidateIndex >= 0 && candidateIndex < _content.Elections.Candidates.Count)
        {
            ElectedCandidate = candidateIndex;
        }
    }

    /// <summary>
    /// Otevře nové volební období: sestaví kandidátku a zruší předchozí volbu.
    /// Kandidátka je odvozená z čísla období a seedu, aby po načtení savu vyšla stejná.
    /// </summary>
    internal void BeginElectionTerm(long term)
    {
        ElectionTerm = term;
        FillBallot(term);

        // Vláda nastoupí hned: prázdné období by znamenalo, že hráč, který si
        // nevybral, přijde o bonus úplně — a to není relaxační, to je trest.
        ElectedCandidate = _ballot[0];
    }

    /// <summary>Obnoví stav voleb ze savu (bez oznámení a bez nové kandidátky).</summary>
    internal void RestoreElection(long term, int elected)
    {
        ElectionTerm = term;
        ElectedCandidate = elected;
        if (term >= 0)
        {
            FillBallot(term);
        }
    }

    private void FillBallot(long term)
    {
        var candidates = _content.Elections.Candidates;
        if (candidates.Count == 0)
        {
            return;
        }

        // Bez opakování: postupně se losuje z těch, které ještě na kandidátce nejsou.
        Span<int> pool = candidates.Count <= 64 ? stackalloc int[candidates.Count] : new int[candidates.Count];
        for (int i = 0; i < candidates.Count; i++)
        {
            pool[i] = i;
        }

        var rng = new CivDle.Core.WorldGen.SplitMix64(unchecked((ulong)Seed ^ ((ulong)term * 0x9E3779B97F4A7C15UL)));
        int remaining = candidates.Count;
        for (int slot = 0; slot < _ballot.Length; slot++)
        {
            int pick = (int)(rng.Next() % (ulong)remaining);
            _ballot[slot] = pool[pick];
            pool[pick] = pool[--remaining];
        }
    }

    /// <summary>Účinek zvoleného programu daného druhu (0 = nikdo takový nevládne).</summary>
    private double ElectionBonus(ElectionEffect effect)
    {
        if (ElectedCandidate < 0)
        {
            return 0.0;
        }

        var candidate = _content.Elections.Candidates[ElectedCandidate];
        return candidate.Effect == effect ? candidate.Magnitude : 0.0;
    }

    /// <summary>Násobič výroby ze zvoleného programu (1.0 = bez vlivu).</summary>
    public double ElectionProductionMult => 1.0 + ElectionBonus(ElectionEffect.Production);

    /// <summary>Násobič růstu populace ze zvoleného programu.</summary>
    public double ElectionGrowthMult => 1.0 + ElectionBonus(ElectionEffect.Growth);

    /// <summary>Násobič ručního sběru ze zvoleného programu.</summary>
    public double ElectionHarvestMult => 1.0 + ElectionBonus(ElectionEffect.Harvest);

    /// <summary>Sleva na výzkum ze zvoleného programu (podíl ceny).</summary>
    public double ElectionResearchDiscount => ElectionBonus(ElectionEffect.Research);

    /// <summary>Přídavek ke spokojenosti ze zvoleného programu.</summary>
    public double ElectionHappinessBonus => ElectionBonus(ElectionEffect.Happiness);

    // ----- denní výzvy -----

    /// <summary>Den, pro který platí aktuální sada výzev (UTC, <c>yyyy-MM-dd</c>); prázdné = žádná.</summary>
    public string ChallengeDay { get; private set; } = string.Empty;

    /// <summary>Indexy dnešních výzev do fondu <see cref="GameContent.Challenges"/>.</summary>
    public IReadOnlyList<int> ActiveChallenges => _activeChallenges;

    /// <summary>Je výzva na daném místě dnešní sady splněná?</summary>
    public bool IsChallengeDone(int slot) => _challengesDone[slot];

    /// <summary>
    /// Řekne simulaci, jaký je dnes den. Simulace si na hodiny nesahá sama —
    /// musí zůstat deterministická — takže datum vkládá aplikační vrstva.
    /// Změna dne vydá novou sadu výzev a zapamatuje si výchozí hodnoty metrik.
    /// </summary>
    public void SetChallengeDay(string dayKey)
    {
        if (!_content.Challenges.IsEnabled || dayKey == ChallengeDay || dayKey.Length == 0)
        {
            return;
        }

        ChallengeDay = dayKey;
        _activeChallenges.Clear();
        _challengeBaselines.Clear();
        _challengesDone.Clear();

        var catalog = _content.Challenges;
        foreach (int index in DailyChallenges.Select(catalog.Challenges.Count, catalog.DailyCount, dayKey))
        {
            var condition = catalog.Challenges[index].Condition;
            _activeChallenges.Add(index);
            _challengeBaselines.Add(EvaluateMetric(condition.Kind, condition.Param));
            _challengesDone.Add(false);
        }
    }

    /// <summary>Kolik z dnešní výzvy je hotovo (u kumulativních metrik jen dnešní přírůstek).</summary>
    public long ChallengeProgress(int slot)
    {
        var condition = _content.Challenges.Challenges[_activeChallenges[slot]].Condition;
        return DailyChallenges.Progress(
            condition.Kind, EvaluateMetric(condition.Kind, condition.Param), _challengeBaselines[slot]);
    }

    /// <summary>Označí výzvu za splněnou (volá systém výzev po udělení odměny).</summary>
    internal void MarkChallengeDone(int slot) => _challengesDone[slot] = true;

    /// <summary>Obnoví sadu výzev ze savu (bez vydávání nové a bez odměn).</summary>
    internal void RestoreChallenges(string dayKey, IReadOnlyList<int> indices, IReadOnlyList<long> baselines, IReadOnlyList<bool> done)
    {
        // Fond se mohl mezi verzemi zmenšit — neplatné indexy se zahodí, ať save
        // z novějšího obsahu nikdy neshodí načtení.
        ChallengeDay = dayKey;
        _activeChallenges.Clear();
        _challengeBaselines.Clear();
        _challengesDone.Clear();
        for (int i = 0; i < indices.Count; i++)
        {
            if (indices[i] >= 0 && indices[i] < _content.Challenges.Challenges.Count)
            {
                _activeChallenges.Add(indices[i]);
                _challengeBaselines.Add(i < baselines.Count ? baselines[i] : 0);
                _challengesDone.Add(i < done.Count && done[i]);
            }
        }
    }

    /// <summary>Výchozí hodnoty metrik dnešních výzev (pro serializaci savu).</summary>
    internal IReadOnlyList<long> ChallengeBaselines => _challengeBaselines;

    /// <summary>Splněnost dnešních výzev (pro serializaci savu).</summary>
    internal IReadOnlyList<bool> ChallengeDoneFlags => _challengesDone;

    // ----- průvodce prvními kroky -----

    /// <summary>
    /// Kolikátý krok průvodce je na řadě (0 = první). Rovná se počtu kroků,
    /// když je průvodce dokončený; větší hodnota znamená ručně přeskočený.
    /// </summary>
    public int TutorialStep { get; internal set; }

    /// <summary>Nemá už průvodce co říct (dokončený nebo přeskočený)?</summary>
    public bool IsTutorialFinished => TutorialStep >= _content.Tutorial.Count;

    /// <summary>Aktivní krok průvodce, nebo <c>null</c>, když je hotovo.</summary>
    public TutorialStepDef? CurrentTutorialStep =>
        IsTutorialFinished ? null : _content.Tutorial[TutorialStep];

    /// <summary>
    /// Vypne průvodce natrvalo (tlačítko „Přeskočit"). Hráč, který ví, co dělá,
    /// nemá být nucen odklikat devět kroků.
    /// </summary>
    public void SkipTutorial() => TutorialStep = _content.Tutorial.Count;

    /// <summary>Obnoví postup průvodce ze savu (bez oznámení).</summary>
    internal void RestoreTutorialStep(int step) => TutorialStep = Math.Max(0, step);

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

    // ----- slučování bloků 2×2 -----

    /// <summary>
    /// Najde blok 2×2 stejných budov, jehož součástí je budova na dané dlaždici.
    /// Zkouší všechny čtyři polohy, ve kterých může dlaždice v bloku ležet —
    /// hráč klikne kamkoli do čtverce, ne nutně do jeho levého horního rohu.
    /// </summary>
    public bool TryFindMergeGroup(int x, int y, out MergeGroup group)
    {
        group = default;
        if (!TryGetBuildingAt(x, y, out int clicked))
        {
            return false;
        }

        int defIndex = _buildings[clicked].DefIndex;
        if (!_content.Buildings[defIndex].CanMergeIntoBigger)
        {
            return false;
        }

        for (int dy = 0; dy <= 1; dy++)
        {
            for (int dx = 0; dx <= 1; dx++)
            {
                if (TryBlockAt(x - dx, y - dy, defIndex, out group))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>Leží na dlaždicích (x,y)…(x+1,y+1) čtyři budovy téhož typu?</summary>
    private bool TryBlockAt(int x, int y, int defIndex, out MergeGroup group)
    {
        group = default;
        if (!IsSameBuilding(x, y, defIndex, out int a)
            || !IsSameBuilding(x + 1, y, defIndex, out int b)
            || !IsSameBuilding(x, y + 1, defIndex, out int c)
            || !IsSameBuilding(x + 1, y + 1, defIndex, out int d))
        {
            return false;
        }

        group = new MergeGroup(x, y, defIndex, a, b, c, d);
        return true;
    }

    private bool IsSameBuilding(int x, int y, int defIndex, out int buildingIndex) =>
        TryGetBuildingAt(x, y, out buildingIndex) && _buildings[buildingIndex].DefIndex == defIndex;

    /// <summary>
    /// Lze blok 2×2 na dané dlaždici sloučit? Cíl musí být odemčený technologií
    /// (proto se slučování nedá zneužít hned na začátku) a hráč musí mít na doplatek.
    /// </summary>
    public PlacementResult CanMerge(int x, int y)
    {
        if (!TryFindMergeGroup(x, y, out var group))
        {
            return PlacementResult.Occupied;
        }

        return CanMerge(group);
    }

    /// <summary>Lze konkrétní nalezený blok sloučit?</summary>
    public PlacementResult CanMerge(MergeGroup group)
    {
        var def = _content.Buildings[group.DefIndex];
        int targetIndex = def.MergesToIndex;
        if (targetIndex < 0 || !_buildingUnlocked[targetIndex])
        {
            return PlacementResult.NotUnlocked;
        }

        // Cílová budova musí na dané místo vůbec smět — biom se pod blokem může lišit.
        var target = _content.Buildings[targetIndex];
        for (int tileY = group.Y; tileY < group.Y + 2; tileY++)
        {
            for (int tileX = group.X; tileX < group.X + 2; tileX++)
            {
                if (!target.IsBiomeAllowed(Terrain.BiomeAt(tileX, tileY)))
                {
                    return PlacementResult.WrongBiome;
                }
            }
        }

        var cost = def.MergeCost;
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
    /// Příkaz hráče: sloučí blok 2×2 stejných budov v jednu velkou. Čtyři budovy
    /// zmizí (bez vrácení ceny — nebourá se, přestavuje) a na jejich místě vznikne
    /// cílová 2×2 budova.
    ///
    /// <para>Existuje kvůli tomu, že velké město je jinak koberec identických
    /// domečků; sloučení dá hráči důvod stavět úhledně a odmění ho siluetou,
    /// kterou jinak nezíská.</para>
    /// </summary>
    public PlacementResult TryMerge(int x, int y)
    {
        if (!TryFindMergeGroup(x, y, out var group))
        {
            return PlacementResult.Occupied;
        }

        var result = CanMerge(group);
        if (result != PlacementResult.Ok)
        {
            return result;
        }

        var def = _content.Buildings[group.DefIndex];
        var cost = def.MergeCost;
        for (int i = 0; i < cost.Count; i++)
        {
            _resources[cost[i].ResourceIndex] -= cost[i].Amount;
        }

        // Od nejvyššího indexu: odebrání přesouvá poslední budovu na uvolněné
        // místo, takže při mazání odspodu by se zbylé indexy posunuly pod rukama.
        var (first, second, third, fourth) = group.DescendingIndices();
        RemoveBuildingSilently(first);
        RemoveBuildingSilently(second);
        RemoveBuildingSilently(third);
        RemoveBuildingSilently(fourth);

        // Sloučení není nová stavba: čtyři domy už stojí, jen se přestaví.
        AddBuilding(def.MergesToIndex, group.X, group.Y, progress: 0f, asConstructionSite: false);
        ApplyBuildingBonuses(_content.Buildings[def.MergesToIndex]);
        ReportVisual(VisualEventKind.BuildingMerged, group.X, group.Y);
        _roadLinksDirty = true; // napojení se musí přepočítat, než se zeptáme na cestu
        _roadBuilder.ConnectLastBuilding(this);
        MergedBuildings++;
        SettlementsDirty = true;
        DistrictsDirty = true; // změna zástavby může vytvořit i rozpadnout čtvrť
        _roadLinksDirty = true;
        EnqueueNotification(new GameNotification(
            NotificationKind.BuildingMerged, "toast.merged", _content.Buildings[def.MergesToIndex].NameKey));
        return PlacementResult.Ok;
    }

    /// <summary>Kolik sloučení hráč provedl (metrika pro cíle a achievementy).</summary>
    public long MergedBuildings { get; internal set; }

    /// <summary>Obnoví počet sloučení ze savu.</summary>
    internal void RestoreMergedBuildings(long count) => MergedBuildings = count;

    /// <summary>
    /// Odebere budovu bez vracení surovin a bez oznámení — slučování není bourání,
    /// materiál jde do nové stavby.
    /// </summary>
    private void RemoveBuildingSilently(int buildingIndex)
    {
        var def = _content.Buildings[_buildings[buildingIndex].DefIndex];
        int x = _buildings[buildingIndex].X, y = _buildings[buildingIndex].Y;
        for (int tileY = y; tileY < y + def.FootprintHeight; tileY++)
        {
            for (int tileX = x; tileX < x + def.FootprintWidth; tileX++)
            {
                _occupancy.Remove(TileKey.Pack(tileX, tileY));
            }
        }

        ForgetBuilding(buildingIndex, def);

        int last = _buildingCount - 1;
        if (buildingIndex != last)
        {
            _buildings[buildingIndex] = _buildings[last];
            RemapOccupancy(buildingIndex);
        }

        _buildingCount--;
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

        return HasRoomToGrow(buildingIndex, _content.Buildings[def.UpgradesToIndex])
            ? PlacementResult.Ok
            : PlacementResult.Occupied;
    }

    /// <summary>
    /// Vejde se vyšší stupeň na místo toho současného?
    ///
    /// <para>Vylepšení dlouho jen vyměnilo definici na místě, protože všechny
    /// stupně měly stejný půdorys. Jakmile ho ale některý stupeň zvětší
    /// (chalupa 1×1 → arkologie 3×3), musí se nové dlaždice napřed uvolnit —
    /// jinak by budova stála na políčkách, o kterých mapa obsazenosti neví,
    /// a stavělo by se do ní.</para>
    ///
    /// <para>Roste se doprava a dolů od původního rohu. Nové dlaždice musí být
    /// volné a na povoleném biomu; ty původní se nekontrolují, ty už budova má.</para>
    /// </summary>
    private bool HasRoomToGrow(int buildingIndex, BuildingDef target)
    {
        ref readonly var instance = ref _buildings[buildingIndex];
        var current = _content.Buildings[instance.DefIndex];
        if (target.FootprintWidth <= current.FootprintWidth
            && target.FootprintHeight <= current.FootprintHeight)
        {
            return true;
        }

        for (int tileY = instance.Y; tileY < instance.Y + target.FootprintHeight; tileY++)
        {
            for (int tileX = instance.X; tileX < instance.X + target.FootprintWidth; tileX++)
            {
                bool inside = tileX < instance.X + current.FootprintWidth
                    && tileY < instance.Y + current.FootprintHeight;
                if (inside)
                {
                    continue;
                }

                if (IsOccupied(tileX, tileY) || !target.IsBiomeAllowed(Terrain.BiomeAt(tileX, tileY)))
                {
                    return false;
                }
            }
        }

        return true;
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

        // Vyšší stupeň může mít větší půdorys (arkologie je 3×3). Nové dlaždice
        // se musí zabrat a srovnat, jinak by na nich zůstal strom a dalo by se
        // do budovy stavět. Že jsou volné, ověřil CanUpgrade.
        var newDef = _content.Buildings[instance.DefIndex];
        if (newDef.FootprintWidth != oldDef.FootprintWidth
            || newDef.FootprintHeight != oldDef.FootprintHeight)
        {
            ClearGround(instance.X, instance.Y, newDef.FootprintWidth, newDef.FootprintHeight);
            RemapOccupancy(buildingIndex);
        }

        ReportVisual(VisualEventKind.BuildingUpgraded, instance.X, instance.Y);
        ApplyBuildingBonuses(newDef);
        SettlementsDirty = true;
        DistrictsDirty = true; // změna zástavby může vytvořit i rozpadnout čtvrť
        _roadLinksDirty = true;
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

        ForgetBuilding(buildingIndex, def);
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
        DistrictsDirty = true; // změna zástavby může vytvořit i rozpadnout čtvrť
        _roadLinksDirty = true;
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
        // Přesun mění biom pod budovou i její okolí → cachované násobiče jdou s ní.
        building.BiomeMult = (float)_content.Biomes[Terrain.BiomeAt(x, y)].Production;
        building.AdjacencyMult = (float)AdjacencyMultiplier(def, x, y);
        building.HaulMult = (float)_haulSystem.MultiplierAt(x, y);
        building.PollutionMult = (float)_pollutionSystem.MultiplierAt(this, building.DefIndex, x, y);
        building.DistrictMult = 1f; // přesunutá budova ze čtvrti vypadla, než se pozná nová
        building.DistrictIndex = -1;
        if (def.StorageBonus.Count > 0)
        {
            HaulDirty = true; // přesunutý sklad mění svoz na obou koncích
        }
        for (int tileY = y; tileY < y + def.FootprintHeight; tileY++)
        {
            for (int tileX = x; tileX < x + def.FootprintWidth; tileX++)
            {
                _occupancy[TileKey.Pack(tileX, tileY)] = buildingIndex + 1;
            }
        }

        SettlementsDirty = true;
        DistrictsDirty = true; // změna zástavby může vytvořit i rozpadnout čtvrť
        _roadLinksDirty = true;
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

    /// <summary>
    /// Zná hráč tuhle technologii, nebo je pro něj zatím jen tečka v mlze?
    ///
    /// <para>Známé je hotové a to, co jde vyzkoumat hned — tedy přesně jeden krok
    /// dopředu. Dál se strom nečte: kdyby hráč viděl celou budoucnost, ubral by si
    /// tím to nejlepší, co strom nabízí — zvědavost, co je za dalším uzlem.</para>
    /// </summary>
    public bool IsTechKnown(int techIndex) =>
        _techLevel[techIndex] > 0 || CanResearch(techIndex) != PlacementResult.NotUnlocked;

    /// <summary>
    /// Kolikátou úroveň opakovatelné technologie hráč má (0 = nevyzkoumaná).
    /// U jednorázových je to 0 nebo 1.
    /// </summary>
    public int TechLevel(int techIndex) => _techLevel[techIndex];

    /// <summary>Je technologie na svém stropu (další úroveň už není)?</summary>
    public bool IsTechMaxed(int techIndex) => _techLevel[techIndex] >= _content.Techs[techIndex].MaxLevel;

    /// <summary>
    /// Skutečná cena výzkumu — se škálováním podle počtu hotových technologií
    /// i se slevou z Vzestupu.
    ///
    /// <para>UI <b>musí</b> ukazovat tohle číslo. Dokud vypisovalo základ z dat,
    /// hráč viděl „40 vědy", měl 50, klikl a nestalo se nic: skutečná cena byla
    /// dávno jinde. Přesně to hlásil jako „často mi něco nejde vyzkoumat, i když
    /// mám suroviny".</para>
    /// </summary>
    public IReadOnlyList<ResourceAmount> ScaledResearchCost(int techIndex)
    {
        var cost = _content.Techs[techIndex].Cost;
        var scaled = new ResourceAmount[cost.Count];
        for (int i = 0; i < cost.Count; i++)
        {
            scaled[i] = new ResourceAmount(cost[i].ResourceIndex, ResearchCost(cost[i].Amount, _techLevel[techIndex]));
        }

        return scaled;
    }

    /// <summary>
    /// Lze technologii vyzkoumat (prerekvizity splněny, dost surovin, není na stropu)?
    ///
    /// <para>U opakovatelných technologií se ptáme na <b>další úroveň</b>: hotová je
    /// teprve ta, která došla na <see cref="TechDef.MaxLevel"/>.</para>
    /// </summary>
    public PlacementResult CanResearch(int techIndex)
    {
        if (IsTechBeyondDemo(techIndex))
        {
            return PlacementResult.NotUnlocked;
        }

        var tech = _content.Techs[techIndex];
        int level = _techLevel[techIndex];
        if (level >= tech.MaxLevel)
        {
            return PlacementResult.Occupied; // už hotová
        }

        foreach (int prereq in tech.PrerequisiteIndices)
        {
            if (_techLevel[prereq] == 0)
            {
                return PlacementResult.NotUnlocked;
            }
        }

        var cost = tech.Cost;
        for (int i = 0; i < cost.Count; i++)
        {
            if (_resources[cost[i].ResourceIndex] < ResearchCost(cost[i].Amount, level))
            {
                return PlacementResult.NotEnoughResources;
            }
        }

        return PlacementResult.Ok;
    }

    /// <summary>Nejvyšší podíl surovin, který jde přes Vzestup přenést.</summary>
    private const double MaxKeptResourceShare = 0.8;

    /// <summary>Kolik dlaždic silnic Vzestup přežije (dědictví Odkazu).</summary>
    public int InheritedRoads => (int)Math.Floor(_bonuses.KeptRoads);

    /// <summary>Jaký podíl surovin Vzestup přežije (0–0,8).</summary>
    public double InheritedResourceShare => _bonuses.KeptResourceShare;

    /// <summary>Kolik divů a megastruktur Vzestup přežije nad rámec běžných budov.</summary>
    public int InheritedWonders => (int)Math.Floor(_bonuses.KeptWonders);

    /// <summary>Přežije časosběr celou civilizaci, ne jen jeden běh?</summary>
    public bool InheritsHistory => _bonuses.KeepsHistory >= 1;

    /// <summary>
    /// O kolik dlaždic se při novém běhu navíc odhalí okolí středu.
    ///
    /// <para>První úroveň Oka věků drží mapu, každá další ji <b>rozšiřuje</b> —
    /// jinak by to bylo jediné dědictví, které se nedá stupňovat, a hráč by na
    /// něm po jedné koupi uvízl.</para>
    /// </summary>
    public int InheritedRevealRadius =>
        Math.Max(0, (int)Math.Floor(_bonuses.KeepsMap) - 1) * MapRevealPerLevel;

    /// <summary>Kolik dlaždic přidá jedna úroveň Oka věků nad rámec první.</summary>
    private const int MapRevealPerLevel = 24;

    /// <summary>Kolik technologií Vzestup přežije (dědictví Odkazu).</summary>
    public int InheritedTechs => (int)Math.Floor(_bonuses.KeptTechs);

    /// <summary>Kolik budov Vzestup přežije (dědictví Odkazu).</summary>
    public int InheritedBuildings => (int)Math.Floor(_bonuses.KeptBuildings);

    /// <summary>Zůstává po Vzestupu odhalená mapa? (Dědictví Odkazu.)</summary>
    public bool InheritsMap => _bonuses.KeepsMap >= 1;

    /// <summary>Dědí se vůbec něco? (Pro cedulku před Vzestupem.)</summary>
    public bool HasInheritance =>
        InheritedTechs > 0 || InheritedBuildings > 0 || InheritsMap
        || InheritedRoads > 0 || InheritedResourceShare > 0 || InheritedWonders > 0
        || InheritsHistory;

    /// <summary>
    /// Je budova div nebo megastruktura?
    ///
    /// <para>Bere se z kategorie v datech, ne z pevného seznamu — mod, který
    /// přidá vlastní monument, se tím do dědictví dostane sám.</para>
    /// </summary>
    internal bool IsWonder(int defIndex) =>
        _content.Buildings[defIndex].Category is "monument" or "megastructure";

    /// <summary>
    /// Nastaví zásobu při dědictví. Ořízne se stropem skladu — zděděné zlato
    /// nesmí přetéct přes kapacitu, kterou nový svět ještě nemá.
    /// </summary>
    internal void SetResourceForInheritance(int resourceIndex, double amount) =>
        _resources[resourceIndex] = Math.Min(amount, _storageCaps[resourceIndex]);

    /// <summary>Jsou splněné předpoklady technologie? (Cenu neřeší.)</summary>
    internal bool PrerequisitesMet(int techIndex)
    {
        foreach (int prereq in _content.Techs[techIndex].PrerequisiteIndices)
        {
            if (_techLevel[prereq] == 0)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Odemkne technologii bez placení. Jen pro dědictví Odkazu — dědictví se
    /// nekupuje, hráč za něj zaplatil body Odkazu už dřív.
    /// </summary>
    internal void GrantTechFree(int techIndex) => UnlockTech(techIndex);

    /// <summary>Běží tenhle svět v demoverzi? (Pro testy a UI.)</summary>
    public bool ContentIsDemoForTests => _content.IsDemo;

    /// <summary>
    /// Maska technologií dostupných v ukázce. Počítá se až při prvním dotazu
    /// a pak se drží — je odvozená z obsahu, takže se během hry nemění.
    /// </summary>
    private bool[]? _demoTechs;

    /// <summary>
    /// Je technologie mimo výřez, který ukázka nabízí?
    ///
    /// <para>Výřez je <b>uzávěr přes předpoklady</b> (viz
    /// <see cref="DemoTechSelection"/>), ne prostě prvních N v pořadí dat.
    /// Strom totiž není psaný striktně od kořene, takže by prostý řez nechal
    /// v nabídce uzly, ke kterým v ukázce nevede cesta — a to vypadá jako
    /// chyba, ne jako hranice dema.</para>
    /// </summary>
    public bool IsTechBeyondDemo(int techIndex)
    {
        if (!_content.IsDemo)
        {
            return false;
        }

        _demoTechs ??= DemoTechSelection.Build(
            _content.Techs.All, _content.Demo.TechCountFor(_content.Techs.Count));

        return !_demoTechs[techIndex];
    }

    /// <summary>Kolik technologií obsah nabízí.</summary>
    public int TechCount => _content.Techs.Count;

    /// <summary>
    /// Kolik surovin dohromady stojí další úroveň technologie. Slouží
    /// automatickému výzkumu k výběru „nejlevnější dostupné".
    ///
    /// <para>Sčítá se přes suroviny bez vážení: cílem není ekonomicky přesné
    /// srovnání, ale stabilní pořadí, ve kterém se strom prochází zhruba tak,
    /// jak by šel hráč.</para>
    /// </summary>
    public double TotalResearchCost(int techIndex)
    {
        var tech = _content.Techs[techIndex];
        int level = _techLevel[techIndex];

        double total = 0;
        for (int i = 0; i < tech.Cost.Count; i++)
        {
            total += ResearchCost(tech.Cost[i].Amount, level);
        }

        return total;
    }

    /// <summary>
    /// Kolik uzlů za interval zvládne automatický výzkum (0 = vypnutý).
    /// Odemyká se vylepšením Odkazu.
    /// </summary>
    public double AutoResearchLevel => _bonuses.AutoResearchLevel;

    /// <summary>Násobič tempa automatického výzkumu (nikdy pod 1).</summary>
    public double ResearchSpeed => Math.Max(1.0, _bonuses.ResearchSpeed);

    /// <summary>Běží automatický výzkum? (Pro cedulku v UI.)</summary>
    public bool AutoResearchActive => AutoResearchLevel >= 1;

    /// <summary>Příkaz hráče: vyzkoumat technologii — odečte cenu a odemkne její budovy.</summary>
    public PlacementResult TryResearch(int techIndex)
    {
        var result = CanResearch(techIndex);
        if (result != PlacementResult.Ok)
        {
            return result;
        }

        var tech = _content.Techs[techIndex];
        int level = _techLevel[techIndex];
        for (int i = 0; i < tech.Cost.Count; i++)
        {
            _resources[tech.Cost[i].ResourceIndex] -= ResearchCost(tech.Cost[i].Amount, level);
        }

        UnlockTech(techIndex);
        return PlacementResult.Ok;
    }

    /// <summary>
    /// Cena výzkumu: základ z dat × škálování × sleva z Vzestupu.
    ///
    /// <para>Škálování roste s počtem už hotových technologií — první uzly jsou
    /// svižné, pozdější stojí za rozmyšlenou. Bez toho měl celý strom prakticky
    /// stejnou cenu a v druhé půlce hry se proklikal za pár minut.</para>
    ///
    /// <para>Nikdy neklesne pod 1 — technologie zadarmo by rozbila progresi.</para>
    /// </summary>
    public int ResearchCost(int baseAmount) => ResearchCost(baseAmount, 0);

    /// <summary>
    /// Cena <paramref name="level"/>-té další úrovně opakovatelné technologie
    /// (0 = první výzkum). Nad rámec obecného škálování se přidává mocnina
    /// z <see cref="ResearchConfig.ScaleForLevel"/>.
    /// </summary>
    public int ResearchCost(int baseAmount, int level)
    {
        var research = _content.Gameplay.Research;
        double scaled = baseAmount * research.ScaleAfter(TechsResearched) * research.ScaleForLevel(level);
        double discount = Math.Min(0.9, _bonuses.ResearchDiscount + ElectionResearchDiscount);
        return Math.Max(1, (int)Math.Round(scaled * (1.0 - discount)));
    }

    /// <summary>
    /// Kolik technologií je už hotových (vstupuje do ceny další).
    ///
    /// <para>Drží se jako číslo, ne jako průchod polem: cena výzkumu se počítá
    /// při každém překreslení stromu pro každý uzel a každou surovinu, a procházet
    /// při tom stovku příznaků by bylo zbytečné.</para>
    /// </summary>
    public int TechsResearched { get; private set; }

    private void UnlockTech(int techIndex)
    {
        if (_techLevel[techIndex] == 0)
        {
            TechsResearched++; // počítají se uzly, ne úrovně — jinak by statistika lhala
        }

        // Strop hlídá i obnova ze savu: kdyby v datech ubyla úroveň, načtená hra
        // se přiškrtí na to, co data dovolují, místo aby dál dávala bonus navíc.
        _techLevel[techIndex] = Math.Min(_techLevel[techIndex] + 1, _content.Techs[techIndex].MaxLevel);
        foreach (int buildingIndex in _content.Techs[techIndex].UnlockedBuildingIndices)
        {
            _buildingUnlocked[buildingIndex] = true;
        }

        // Pasivní bonus technologie se musí hned promítnout do násobičů i do
        // odvozeného stavu (bydlení/sklady se počítají z bonusů).
        if (_content.Techs[techIndex].HasPassiveEffect)
        {
            RecomputeBonuses();
            RecomputeDerivedState();
        }
    }

    // ----- obnova ze savu (jen pro SaveGameSerializer, obchází cenu a validaci biomů) -----

    /// <summary>Označí technologii jako vyzkoumanou při načtení savu (bez ceny).</summary>
    internal void RestoreTech(int techIndex) => UnlockTech(techIndex);

    /// <summary>Nastaví globální stav načtený ze savu (zásoby se přiškrtí na aktuální kapacity).</summary>
    internal void RestoreState(double[] resourceAmounts, double population, long tickCount)
    {
        RestoreResources(resourceAmounts);
        RestoreCore(population, tickCount);
    }

    /// <summary>
    /// Obnoví jen zásoby surovin. Sekční save načítá části nezávisle na pořadí,
    /// takže potřebuje obnovu po částech, ne jedno velké „nastav všechno".
    ///
    /// <para>Zásoby se schválně NEOŘEZÁVAJÍ na kapacitu: sklady zvedají až budovy
    /// a bonusy Vzestupu, které přijdou v jiné sekci. Ořez proběhne jednou na konci
    /// ve <see cref="FinalizeLoad"/> — jinak by hráč s plným skladem o zásoby přišel
    /// jen kvůli pořadí sekcí.</para>
    /// </summary>
    internal void RestoreResources(double[] resourceAmounts)
    {
        for (int i = 0; i < _resources.Length; i++)
        {
            _resources[i] = resourceAmounts[i];
        }
    }

    /// <summary>Obnoví populaci a odtikaný čas (zbytek stavu nesou další sekce).</summary>
    internal void RestoreCore(double population, long tickCount)
    {
        Population = population;
        TickCount = tickCount;
    }

    /// <summary>
    /// Přidá budovu ze savu bez ceny a kontroly biomu — data se od uložení mohla
    /// změnit a už postavené budovy hráči nemažeme.
    /// </summary>
    internal void RestoreBuilding(int defIndex, int x, int y, float progress)
    {
        AddBuilding(defIndex, x, y, progress, asConstructionSite: false);
        ApplyBuildingBonuses(_content.Buildings[defIndex]);
        SettlementsDirty = true;
        DistrictsDirty = true; // změna zástavby může vytvořit i rozpadnout čtvrť
        _roadLinksDirty = true; // silnice ze savu chodí zvlášť, přepočet osad ale spustit musíme
    }

    /// <summary>
    /// Vloží budovu do plochého pole. <paramref name="asConstructionSite"/> říká,
    /// jestli se teprve staví (nová stavba), nebo už stojí (obnova ze savu) —
    /// odpočet stavby je stav budovy, ne vlastnost jejího typu.
    /// </summary>
    private void AddBuilding(int defIndex, int x, int y, float progress, bool asConstructionSite = true)
    {
        var def = _content.Buildings[defIndex];
        if (_buildingCount == _buildings.Length)
        {
            Array.Resize(ref _buildings, _buildings.Length * 2);
        }

        // Kronika: biom, na kterém město stavělo. Zaznamenává se tady, protože
        // tudy prochází i obnova ze savu — jinak by se po načtení zapomněl.
        _settledBiomes[Terrain.BiomeAt(x, y)] = true;

        // Co postavíš, na to i vidíš. Je to tady ze stejného důvodu jako řádek
        // výš: touhle cestou jde i obnova ze savu, takže i starý save (bez sekce
        // mlhy) dostane odhalené aspoň okolí svých budov.
        Fog.Reveal(x, y, FogRevealRadius);

        // Staveniště se srovná: strom, balvan ani hájek pod domem nezůstanou.
        ClearGround(x, y, def.FootprintWidth, def.FootprintHeight);

        // Cache napojení na silnice mluví o polích, která se právě mění — zneplatni
        // ji TADY, ne až po zavolání RoadBuilderu. Dřív to bylo až na konci
        // TryPlaceBuilding, takže se stihl někdo zeptat na napojení budovy, kterou
        // cache ještě neznala, a sáhnout za konec pole (pád při stavbě).
        _roadLinksDirty = true;

        _buildings[_buildingCount] = new BuildingInstance
        {
            DefIndex = defIndex,
            X = x,
            Y = y,
            Progress = progress,
            // Ekonomická identita biomu se cachuje při položení — v tikové smyčce
            // už se terén nevzorkuje (viz BuildingInstance.BiomeMult).
            BiomeMult = (float)_content.Biomes[Terrain.BiomeAt(x, y)].Production,
            AdjacencyMult = (float)AdjacencyMultiplier(def, x, y),
            HaulMult = (float)_haulSystem.MultiplierAt(x, y),
            // Čistá 1.0, ne 0 — jinak by nová budova nevyráběla nic, dokud kolem ní
            // poprvé neproběhne pomalý přepočet znečištění.
            PollutionMult = (float)_pollutionSystem.MultiplierAt(this, defIndex, x, y),
            // Čistá 1.0: čtvrť se pozná až při nejbližším přepočtu, a do té doby
            // (i navždy, když jsou čtvrti vypnuté) musí budova vyrábět normálně.
            DistrictMult = 1f,
            DistrictIndex = -1,
            // Milník typu už platí — nová budova ho zdědí hned, ať výroba
            // nezačne na 1.0 a po pár ticích neposkočila.
            MilestoneMult = (float)_milestoneBonuses.MultiplierOf(defIndex),
            BuildTicksRemaining = asConstructionSite ? def.BuildTicks : 0,
        };
        _buildingCount++;
        if (asConstructionSite && def.TakesTimeToBuild)
        {
            BuildingsUnderConstruction++;
        }

        // Nový sklad mění svoz i budovám, které stojí dávno — ty se přepočítají
        // rozloženě, tahle jedna to má správně hned.
        if (def.StorageBonus.Count > 0)
        {
            HaulDirty = true;
        }

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
        HousingCapacity += def.HousingCapacity * _bonuses.HousingMult;
        TotalWorkerSlots += def.WorkerSlots;
        TotalPowerSupply += def.PowerSupply;
        TotalPowerDemand += def.PowerDemand;
        for (int i = 0; i < def.StorageBonus.Count; i++)
        {
            _storageCaps[def.StorageBonus[i].ResourceIndex] += def.StorageBonus[i].Amount * _bonuses.StorageMult;
        }
    }

    /// <summary>
    /// Odepíše budovu ze všech evidencí, než zmizí z pole. Rozestavěná budova
    /// žádné bonusy nedostala, takže se jí ani neodebírají — jen se odečte
    /// ze staveniště, jinak by počítadlo zůstalo viset a stavební systém by
    /// nadarmo procházel celé město.
    /// </summary>
    private void ForgetBuilding(int buildingIndex, BuildingDef def)
    {
        if (_buildings[buildingIndex].IsComplete)
        {
            RemoveBuildingBonuses(def);
            return;
        }

        BuildingsUnderConstruction = Math.Max(0, BuildingsUnderConstruction - 1);
    }

    /// <summary>Odebere globální bonusy budovy (vylepšení nahrazuje starou úroveň novou).</summary>
    private void RemoveBuildingBonuses(BuildingDef def)
    {
        HousingCapacity -= def.HousingCapacity * _bonuses.HousingMult;
        TotalWorkerSlots -= def.WorkerSlots;
        TotalPowerSupply -= def.PowerSupply;
        TotalPowerDemand -= def.PowerDemand;
        for (int i = 0; i < def.StorageBonus.Count; i++)
        {
            _storageCaps[def.StorageBonus[i].ResourceIndex] -= def.StorageBonus[i].Amount * _bonuses.StorageMult;
        }

        // Zbouraný (nebo vylepšený) sklad zmizel ze sběrných míst — svoz kolem
        // něj se musí přepočítat. Sedí to tady, protože tímhle jediným místem
        // prochází bourání i slučování.
        if (def.StorageBonus.Count > 0)
        {
            HaulDirty = true;
        }
    }

    // ----- Vzestup (prestige) -----

    /// <summary>
    /// Práh pro DALŠÍ Vzestup. Roste s každým dosaženým stupněm, jinak by druhý
    /// Vzestup přišel hned po prvním a měřítko by přestalo něco znamenat.
    /// </summary>
    public long AscensionRequirement()
    {
        // Demo: první Vzestup zůstává normální, aby si hráč prestiž osahal
        // celou. Od druhého platí práh z dat — a ten je zároveň cíl, na kterém
        // ukázka končí.
        if (_content.IsDemo && AscensionLevel >= 1)
        {
            return Math.Max(1, _content.Demo.AscensionRequirement);
        }

        var requirement = _content.Prestige.Requirement;
        double scaled = requirement.Target * Math.Pow(_content.Prestige.RequirementGrowth, AscensionLevel);

        // Sleva z Odkazu. Je omezená zdola (viz LegacySystem), takže i po hodně
        // úrovních zůstane Vzestup krok, ne formalita.
        scaled *= _legacy.AscensionRequirementShare();
        return (long)Math.Max(1, Math.Min(scaled, long.MaxValue / 2));
    }

    /// <summary>Metrika, kterou práh Vzestupu měří (UI ukazuje pokrok).</summary>
    public long AscensionProgress() =>
        EvaluateMetric(_content.Prestige.Requirement.Kind, _content.Prestige.Requirement.Param);

    /// <summary>Je splněná podmínka pro Vzestup?</summary>
    public bool CanAscend() => AscensionProgress() >= AscensionRequirement();

    /// <summary>Kolik bodů Vzestupu by teď Vzestup udělil.</summary>
    public long PendingAscensionPoints()
    {
        var prestige = _content.Prestige;
        long metric = EvaluateMetric(prestige.PointsMetric, prestige.PointsParam);
        if (metric <= 0 || prestige.PointsDivisor <= 0)
        {
            return 0;
        }

        // Klesající výnos (odmocnina) místo lineárního. Při lineárním se vždycky
        // vyplatilo hrát dál a rozhodnutí „resetnout teď, nebo ještě vydržet?"
        // vůbec nevzniklo — přitom právě to je celý smysl prestiže.
        double ratio = metric / (double)prestige.PointsDivisor;
        double points = Math.Pow(ratio, prestige.PointsExponent) * _legacy.AscensionPointsMult();
        return (long)Math.Min(points, long.MaxValue / 2);
    }

    /// <summary>
    /// Co přesně Vzestup udělá — pro obrazovku, která to má hráči říct dřív,
    /// než klikne. Vzestup je jediná nevratná akce ve hře; překvapení vlastním
    /// rozhodnutím je ta nejhorší možná zpětná vazba.
    /// </summary>
    public AscensionPreview PreviewAscension()
    {
        long points = PendingAscensionPoints();
        int levelAfter = AscensionLevel + 1;
        double nextScaled = _content.Prestige.Requirement.Target
            * Math.Pow(_content.Prestige.RequirementGrowth, levelAfter);

        int techs = 0;
        for (int i = 0; i < _techLevel.Length; i++)
        {
            if (_techLevel[i] > 0)
            {
                techs++;
            }
        }

        // Do bilance se počítají ÚROVNĚ, ne uzly: u opakovatelného upgradu je
        // desátá úroveň stejný kus postupu jako desátý různý upgrade.
        int upgrades = 0;
        for (int i = 0; i < _upgradeLevels.Length; i++)
        {
            upgrades += _upgradeLevels[i];
        }

        return new AscensionPreview(
            points,
            PrestigePoints + points,
            levelAfter,
            (long)Math.Min(nextScaled, long.MaxValue / 2),
            _buildingCount,
            (long)Population,
            _roadTiles.Count,
            _zones.Count,
            techs,
            _districts.Count,
            EvaluateMetric(MetricKind.WondersCompleted, -1),
            upgrades);
    }

    /// <summary>
    /// Nejvyšší populace, jaké město v tomhle běhu dosáhlo. Vzestup ji resetuje —
    /// je to vrchol jedné kapitoly, ne celoživotní rekord.
    /// </summary>
    public long PeakPopulation { get; internal set; }

    /// <summary>Nejlidnatější běh, jaký hráč kdy dohrál. Přežívá Vzestup i restart.</summary>
    public long BestRunPopulation { get; internal set; }

    /// <summary>
    /// Bilance posledního doběhnutého běhu; <see cref="RunSummary.Exists"/> je
    /// false, dokud hráč poprvé nevzestoupí. Neukládá se — je to zpráva
    /// o okamžiku, ne stav světa.
    /// </summary>
    public RunSummary LastRun { get; private set; } = RunSummary.None;

    /// <summary>Je trvalý upgrade Vzestupu koupený?</summary>
    public bool IsUpgradePurchased(int upgradeIndex) => _upgradeLevels[upgradeIndex] > 0;

    /// <summary>
    /// Jeden zdroj síly do rozpisu: odkud násobič je a kolik dělá.
    /// </summary>
    /// <param name="LabelKey">Lokalizační klíč popisku.</param>
    /// <param name="Multiplier">Násobič z tohohle zdroje (1,0 = nic).</param>
    public readonly record struct PowerSource(string LabelKey, double Multiplier);

    /// <summary>
    /// Celková síla výroby a odkud je.
    ///
    /// <para>Tohle je číslo, kvůli kterému se žánr hraje: <b>jedno velké ×N</b>,
    /// na které se hráč dívá a sleduje, jak roste. Doteď nikde nebylo — hráč
    /// kupoval upgrady a musel věřit, že to k něčemu je.</para>
    ///
    /// <para>Rozpis je odvozený stav, počítá se na požádání z UI (ne v tiku),
    /// takže alokace seznamu nevadí.</para>
    /// </summary>
    public IReadOnlyList<PowerSource> PowerBreakdown()
    {
        double prestige = PrestigeProductionMult();
        double grandWork = GrandWorkProductionMult();
        double legacy = LegacyProductionMult();

        // Výzkum se dopočítá zbytkem. Trvalé vrstvy se počítají přímo, protože
        // každá je vlastní kategorie a hráč chce vidět, která z nich táhne.
        double permanent = prestige * grandWork * legacy;

        return new List<PowerSource>
        {
            new("power.prestige", prestige),
            new("power.legacy", legacy),
            new("power.grandWork", grandWork),
            new("power.research", _bonuses.ProductionMult / Math.Max(1e-9, permanent)),
            new("power.milestones", _milestoneBonuses.AverageMultiplier(this)),
            new("power.festival", BoostMultiplier),
        };
    }

    /// <summary>Celkový násobič výroby — součin všech zdrojů z rozpisu.</summary>
    public double TotalPower()
    {
        double total = 1.0;
        var sources = PowerBreakdown();
        for (int i = 0; i < sources.Count; i++)
        {
            total *= sources[i].Multiplier;
        }

        return total;
    }

    /// <summary>Kolik z násobiče výroby pochází z trvalých upgradů Vzestupu.</summary>
    private double PrestigeProductionMult()
    {
        double mult = 1.0;
        for (int i = 0; i < _upgradeLevels.Length; i++)
        {
            if (_upgradeLevels[i] > 0 && _content.PrestigeUpgrades[i].Effect == "production_mult")
            {
                mult *= _content.PrestigeUpgrades[i].MultiplierAtLevel(_upgradeLevels[i]);
            }
        }

        return mult;
    }

    /// <summary>Kolik z násobiče výroby pochází z upgradů Odkazu.</summary>
    private double LegacyProductionMult()
    {
        double mult = 1.0;
        var upgrades = _content.LegacyUpgrades.All;
        for (int i = 0; i < upgrades.Count; i++)
        {
            if (upgrades[i].Effect == "production_mult" && _legacy.Level(i) > 0)
            {
                mult *= upgrades[i].MultiplierAtLevel(_legacy.Level(i));
            }
        }

        return mult;
    }

    /// <summary>Kolik z násobiče výroby pochází z dokončených stupňů Velkého díla.</summary>
    private double GrandWorkProductionMult()
    {
        double mult = 1.0;
        for (int i = 0; i < _grandWorkDone.Count; i++)
        {
            if (_grandWorkDone[i].Effect == "production_mult")
            {
                mult *= 1.0 + _grandWorkDone[i].Magnitude;
            }
        }

        return mult;
    }

    // ----- Velké dílo: kam jdou přebytky -----

    /// <summary>
    /// Je Velké dílo k dispozici? Odemyká se Vzestupem — dřív hráč nemá co
    /// přebývat a nabídka „sypej sem" by byla jen matoucí.
    /// </summary>
    public bool GrandWorkAvailable
    {
        get
        {
            var config = _content.GrandWork;
            if (!config.IsEnabled || AscensionLevel < config.UnlockAscensionLevel)
            {
                return false;
            }

            // Výzkum a stavba: dílo přestalo být položkou v menu, která se
            // jednou objeví. Napřed se vyzkoumá, pak vykope — a teprve do
            // vykopané jámy je kam sypat.
            if (config.NeedsTech && _techLevel[config.UnlockTechIndex] == 0)
            {
                return false;
            }

            return !config.NeedsBuilding || CountBuildingsOfType(config.BuildingIndex) > 0;
        }
    }

    /// <summary>Kolikátý stupeň Velkého díla se právě staví.</summary>
    public int GrandWorkStage => _grandWork.Stage;

    /// <summary>Postup rozestavěného stupně 0–1.</summary>
    public double GrandWorkProgress01() => _grandWork.Progress01();

    /// <summary>Kolik dané suroviny stupni ještě chybí.</summary>
    public double GrandWorkRemaining(int resourceIndex) => _grandWork.Remaining(resourceIndex);

    /// <summary>
    /// Kolik čeho stupeň Velkého díla spotřebuje.
    ///
    /// <para>Vlastní typ s <c>double</c>, ne <see cref="ResourceAmount"/>:
    /// ceny rostou geometricky a po pár desítkách stupňů by se do <c>int</c>
    /// prostě nevešly.</para>
    /// </summary>
    public readonly record struct GrandWorkNeed(int ResourceIndex, double Amount);

    /// <summary>Suroviny, které rozestavěný stupeň spotřebuje.</summary>
    public IReadOnlyList<GrandWorkNeed> GrandWorkCost()
    {
        var pattern = _content.GrandWork.StageAt(_grandWork.Stage);
        var cost = new GrandWorkNeed[pattern.Cost.Count];
        for (int i = 0; i < pattern.Cost.Count; i++)
        {
            int resource = pattern.Cost[i].ResourceIndex;
            cost[i] = new GrandWorkNeed(resource, _content.GrandWork.CostOf(_grandWork.Stage, resource));
        }

        return cost;
    }

    /// <summary>
    /// Příkaz hráče: vloží do díla, co má po ruce (nejvýš tolik, kolik stupni
    /// chybí). Vrací množství, které se opravdu vložilo.
    ///
    /// <para>Vkládá se ručně a jen do plné výše potřeby: automatický odběr by
    /// z díla udělal neviditelnou daň z produkce, a přesypat víc, než stupeň
    /// spotřebuje, by suroviny jen spálilo.</para>
    /// </summary>
    public double InvestInGrandWork(int resourceIndex)
    {
        if (!GrandWorkAvailable)
        {
            return 0;
        }

        double amount = Math.Min(_resources[resourceIndex], _grandWork.Remaining(resourceIndex));
        if (amount <= 0)
        {
            return 0;
        }

        _resources[resourceIndex] -= amount;
        if (_grandWork.Invest(resourceIndex, amount))
        {
            CompleteGrandWorkStage();
        }

        return amount;
    }

    /// <summary>Dokončený stupeň přidá trvalý bonus a dílo se pustí do dalšího.</summary>
    private void CompleteGrandWorkStage()
    {
        _grandWorkDone.Add(_grandWork.AdvanceStage());
        RecomputeBonuses();
        RecomputeDerivedState();

        EnqueueNotification(new GameNotification(
            NotificationKind.Milestone, "toast.grandWork", string.Empty));
        ReportVisual(VisualEventKind.MilestoneReached, CityCenterX, CityCenterY);
    }

    /// <summary>Dokončené stupně (pro sav).</summary>
    internal int GrandWorkCompletedCount => _grandWorkDone.Count;

    /// <summary>Vklady do rozestavěného stupně (pro sav).</summary>
    internal IReadOnlyList<double> GrandWorkInvested() => _grandWork.InvestedAll();

    /// <summary>Obnova Velkého díla ze savu.</summary>
    internal void RestoreGrandWork(int stage, IReadOnlyList<double> invested)
    {
        _grandWork.Restore(stage, invested);

        // Bonusy dokončených stupňů se dopočítají z jejich počtu — vzor stupňů
        // se cyklí, takže stačí vědět kolik jich je.
        _grandWorkDone.Clear();
        for (int i = 0; i < stage; i++)
        {
            _grandWorkDone.Add(_content.GrandWork.StageAt(i));
        }

        RecomputeBonuses();
        RecomputeDerivedState();
    }

    // ----- Odkaz: druhá prestižní vrstva -----

    /// <summary>
    /// Je Odkaz v datech zapnutý? Nabídka se ukazuje, až když má smysl —
    /// hráč před prvním Vzestupem netuší, co Vzestup je, natož vrstva nad ním.
    /// </summary>
    public bool LegacyAvailable => _content.Legacy.IsEnabled && AscensionLevel > 0;

    /// <summary>Kolikrát už hráč Odkaz zanechal.</summary>
    public int LegacyDepth => _legacy.Depth;

    /// <summary>Nevyužité body Odkazu.</summary>
    public long LegacyPoints => _legacy.Points;

    /// <summary>Práh dalšího Odkazu (roste s hloubkou).</summary>
    public long LegacyRequirement() => _legacy.Requirement();

    /// <summary>Metrika, kterou práh Odkazu měří.</summary>
    public long LegacyProgress() =>
        EvaluateMetric(_content.Legacy.Requirement.Kind, _content.Legacy.Requirement.Param);

    /// <summary>Je splněná podmínka pro zanechání Odkazu?</summary>
    public bool CanLeaveLegacy() => LegacyAvailable && LegacyProgress() >= LegacyRequirement();

    /// <summary>Kolik bodů Odkazu by teď jeho zanechání udělilo.</summary>
    public long PendingLegacyPoints() =>
        _legacy.PendingPoints(EvaluateMetric(_content.Legacy.PointsMetric, _content.Legacy.PointsParam));

    /// <summary>Kolikátou úroveň upgradu Odkazu hráč koupil.</summary>
    public int LegacyLevel(int upgradeIndex) => _legacy.Level(upgradeIndex);

    /// <summary>Cena další úrovně upgradu Odkazu.</summary>
    public long LegacyCost(int upgradeIndex) => _legacy.Cost(upgradeIndex);

    /// <summary>Je upgrade Odkazu vykoupený na doraz?</summary>
    public bool IsLegacyUpgradeMaxed(int upgradeIndex) => _legacy.IsMaxed(upgradeIndex);

    /// <summary>Lze upgrade Odkazu koupit?</summary>
    public PlacementResult CanBuyLegacyUpgrade(int upgradeIndex) => _legacy.CanBuy(upgradeIndex);

    /// <summary>Koupí úroveň upgradu Odkazu a přepočítá bonusy.</summary>
    public PlacementResult TryBuyLegacyUpgrade(int upgradeIndex)
    {
        var result = _legacy.CanBuy(upgradeIndex);
        if (result != PlacementResult.Ok || !_legacy.TryBuy(upgradeIndex))
        {
            return result == PlacementResult.Ok ? PlacementResult.NotEnoughResources : result;
        }

        RecomputeBonuses();
        RecomputeDerivedState();
        return PlacementResult.Ok;
    }

    /// <summary>
    /// Zanechá Odkaz: udělí body Odkazu a smaže <b>i Vzestupy</b> — jejich
    /// úroveň, body i všechny koupené upgrady. Zůstává jen Velké dílo (stavba
    /// napříč věky) a to, co si hráč koupí za body Odkazu.
    ///
    /// <para>Je to hlubší řez než Vzestup a schválně: kdyby si hráč nechal
    /// upgrady Vzestupu, byl by Odkaz jen bonus zdarma a rozhodnutí „udělat ho
    /// teď, nebo ještě ne" by nevzniklo.</para>
    /// </summary>
    public PlacementResult TryLeaveLegacy()
    {
        if (!CanLeaveLegacy())
        {
            return PlacementResult.NotEnoughResources;
        }

        _historySystem.Capture(this);

        long points = PendingLegacyPoints();
        _legacy.Leave(points);

        // Vzestup jde k zemi celý: úroveň, body i strom upgradů.
        AscensionLevel = 0;
        PrestigePoints = 0;
        ScaleCapAnnounced = false; // nový strop, nová novinka
        Array.Clear(_upgradeLevels);

        PeakPopulation = 0;
        ResetEra();
        RecomputeBonuses();
        RecomputeDerivedState();

        EnqueueNotification(new GameNotification(
            NotificationKind.Ascended, "toast.legacyLeft", "legacy.leftSubject"));
        ReportVisual(VisualEventKind.MilestoneReached, CityCenterX, CityCenterY);
        return PlacementResult.Ok;
    }

    /// <summary>Úrovně upgradů Odkazu pro sav — jednou za každou koupenou úroveň.</summary>
    internal IEnumerable<int> LegacyPurchasedLevels() => _legacy.PurchasedLevels();

    /// <summary>Obnova Odkazu ze savu: hloubka a body (úrovně přijdou zvlášť).</summary>
    internal void RestoreLegacy(int depth, long points) => _legacy.Restore(depth, points);

    /// <summary>Obnova jedné úrovně upgradu Odkazu ze savu.</summary>
    internal void RestoreLegacyLevel(int upgradeIndex) => _legacy.RestoreLevel(upgradeIndex);

    /// <summary>
    /// Kolik technologií je vyzkoumaných. Počítá se na požádání (statistiky a
    /// bilance běhu), ne v tiku — pole je krátké a cache by se musela hlídat.
    /// </summary>
    public int ResearchedTechCount
    {
        get
        {
            int count = 0;
            for (int i = 0; i < _techLevel.Length; i++)
            {
                if (_techLevel[i] > 0)
                {
                    count++;
                }
            }

            return count;
        }
    }

    /// <summary>Kolikátou úroveň upgradu už hráč koupil (0 = žádnou).</summary>
    public int UpgradeLevel(int upgradeIndex) => _upgradeLevels[upgradeIndex];

    /// <summary>Cena další úrovně upgradu — roste s každou koupenou.</summary>
    public long UpgradeCost(int upgradeIndex) =>
        _content.PrestigeUpgrades[upgradeIndex].CostAtLevel(_upgradeLevels[upgradeIndex]);

    /// <summary>Je upgrade na maximu?</summary>
    public bool IsUpgradeMaxed(int upgradeIndex) =>
        _upgradeLevels[upgradeIndex] >= _content.PrestigeUpgrades[upgradeIndex].MaxLevel;

    /// <summary>Lze upgrade koupit (splněné prereky, dost bodů, ještě nekoupený)?</summary>
    public PlacementResult CanBuyUpgrade(int upgradeIndex)
    {
        if (IsUpgradeMaxed(upgradeIndex))
        {
            return PlacementResult.Occupied; // vykoupený až na doraz
        }

        var upgrade = _content.PrestigeUpgrades[upgradeIndex];
        foreach (int prereq in upgrade.PrerequisiteIndices)
        {
            if (_upgradeLevels[prereq] <= 0)
            {
                return PlacementResult.NotUnlocked;
            }
        }

        return PrestigePoints < UpgradeCost(upgradeIndex)
            ? PlacementResult.NotEnoughResources
            : PlacementResult.Ok;
    }

    /// <summary>Koupí trvalý upgrade Vzestupu — odečte body a přepočítá bonusy i odvozený stav.</summary>
    public PlacementResult TryBuyUpgrade(int upgradeIndex)
    {
        var result = CanBuyUpgrade(upgradeIndex);
        if (result != PlacementResult.Ok)
        {
            return result;
        }

        PrestigePoints -= UpgradeCost(upgradeIndex);
        _upgradeLevels[upgradeIndex]++;
        RecomputeBonuses();
        RecomputeDerivedState();
        return PlacementResult.Ok;
    }

    /// <summary>
    /// Kolik úrovní upgradu si hráč koupí hned teď, nejvýš <paramref name="max"/>.
    ///
    /// <para>Pravidlo o rostoucí ceně patří sem, ne do UI. Obrazovka Vzestupu
    /// z toho jen vypíše popisek tlačítka („Koupit ×5") — kdyby si ceny sčítala
    /// sama, měla by hra dvě místa, kde se počítá totéž.</para>
    /// </summary>
    public int AffordableUpgradeLevels(int upgradeIndex, int max)
    {
        if (max <= 0 || CanBuyUpgrade(upgradeIndex) != PlacementResult.Ok)
        {
            return 0;
        }

        var upgrade = _content.PrestigeUpgrades[upgradeIndex];
        int level = _upgradeLevels[upgradeIndex];
        long budget = PrestigePoints;
        int count = 0;
        while (count < max && level + count < upgrade.MaxLevel)
        {
            long cost = upgrade.CostAtLevel(level + count);
            if (cost > budget)
            {
                break;
            }

            budget -= cost;
            count++;
        }

        return count;
    }

    /// <summary>Kolik bodů stojí <paramref name="count"/> dalších úrovní dohromady.</summary>
    public long UpgradeBatchCost(int upgradeIndex, int count)
    {
        var upgrade = _content.PrestigeUpgrades[upgradeIndex];
        int level = _upgradeLevels[upgradeIndex];
        long total = 0;
        for (int i = 0; i < count && level + i < upgrade.MaxLevel; i++)
        {
            total += upgrade.CostAtLevel(level + i);
        }

        return total;
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

        long points = PendingAscensionPoints();

        // Poslední podoba města patří do časosběru — jinak by přehrávka končila
        // někde uprostřed, u snímku, který padl náhodou na správný tik.
        _historySystem.Capture(this);

        // Bilance se sbírá TEĎ, ještě před resetem — po něm už není z čeho.
        int techs = 0;
        for (int i = 0; i < _techLevel.Length; i++)
        {
            if (_techLevel[i] > 0)
            {
                techs++;
            }
        }

        long peak = Math.Max(PeakPopulation, (long)Population);
        long previousBest = BestRunPopulation;
        LastRun = new RunSummary(
            AscensionLevel + 1,
            TickCount,
            peak,
            _buildingCount,
            techs,
            EvaluateMetric(MetricKind.WondersCompleted, -1),
            points,
            peak > previousBest,
            previousBest);

        BestRunPopulation = Math.Max(previousBest, peak);
        PeakPopulation = 0; // vrchol patří k běhu, ne k hráči

        PrestigePoints += points;
        AscensionLevel++;
        ScaleCapAnnounced = false; // nový strop, nová novinka

        // Dědictví se musí sebrat TEĎ, dokud město ještě stojí.
        LegacyInheritance.Capture(this, InheritedBuildings, InheritedWonders, _inheritedBuildings);
        LegacyInheritance.CaptureRoads(this, InheritedRoads, _inheritedRoads);
        LegacyInheritance.CaptureResources(this, _inheritedResources);

        ResetEra(); // uvnitř i RefreshTierUnlocks — nové měřítko může odemknout megastruktury

        // Technologie napřed, budovy až po nich: zděděná budova, jejíž
        // technologie se nevrátila, by stála, ale hráč by ji neuměl postavit
        // znovu. Takhle sedí zděděné město na znalostech, které k němu patří.
        LastInheritedTechs = LegacyInheritance.GrantTechs(this, InheritedTechs);
        LastInheritedBuildings = LegacyInheritance.Restore(this, _inheritedBuildings);
        LastInheritedRoads = LegacyInheritance.RestoreRoads(this, _inheritedRoads);
        LegacyInheritance.RestoreResources(this, _inheritedResources, InheritedResourceShare);

        _inheritedBuildings.Clear();
        _inheritedRoads.Clear();

        // Oko věků nad první úrovní rozšiřuje výhled kolem středu.
        if (InheritedRevealRadius > 0)
        {
            Fog.Reveal(CityCenterX, CityCenterY, InheritedRevealRadius);
        }

        if (LastInheritedBuildings > 0)
        {
            // Zděděné budovy mění kapacity i odvozený stav; bez přepočtu by
            // město začalo s domy, které „nejsou vidět" ve statistikách.
            RecomputeDerivedState();
        }
        EnqueueNotification(new GameNotification(NotificationKind.Ascended, "toast.ascended", "prestige.ascendedSubject"));

        // Největší okamžik ve hře si zaslouží ohňostroj nad novým světem.
        ReportVisual(VisualEventKind.MilestoneReached, CityCenterX, CityCenterY);
        return PlacementResult.Ok;
    }

    /// <summary>
    /// Přepočítá trvalé násobiče.
    ///
    /// <para>Pravidlo skládání: <b>uvnitř kategorie se sčítá, mezi kategoriemi
    /// násobí</b>. Upgrady Vzestupu jsou jedna kategorie, výzkum druhá — a jejich
    /// výsledky se pronásobí. Dřív padalo všechno do jednoho součtu, takže dvacet
    /// upgradů po +30 % dohromady dalo ×7 a strop celého stromu byl někde kolem
    /// ×4. Tohle je ta věc, kvůli které v žánru čísla vůbec rostou.</para>
    ///
    /// <para>Jednotlivý upgrade navíc skládá <b>mocninou podle úrovně</b>: deset
    /// úrovní po +30 % je ×13,8, ne ×4.</para>
    /// </summary>
    private void RecomputeBonuses()
    {
        // Kategorie 1: trvalé upgrady Vzestupu (násobí se mezi sebou).
        double production = 1.0, harvest = 1.0, growth = 1.0, housing = 1.0, storage = 1.0;
        double start = 1.0, offline = 1.0, discovery = 1.0, festival = 1.0, autoBuild = 1.0;
        double combo = 1.0, researchSpeed = 1.0;
        double critChance = 0.0, jackpot = 0.0, research = 0.0, autoResearch = 0.0;
        double keptTechs = 0.0, keptBuildings = 0.0, keepsMap = 0.0;
        double keptRoads = 0.0, keptResources = 0.0, keptWonders = 0.0, keepsHistory = 0.0;

        for (int i = 0; i < _upgradeLevels.Length; i++)
        {
            if (_upgradeLevels[i] <= 0)
            {
                continue;
            }

            var upgrade = _content.PrestigeUpgrades[i];
            Scale(upgrade.Effect, upgrade.MultiplierAtLevel(_upgradeLevels[i]), upgrade.Magnitude * _upgradeLevels[i]);
        }

        // Velké dílo je třetí kategorie — a schválně: jeho stupně přežívají
        // Vzestup, takže je to jediná osa, která roste napříč všemi běhy.
        for (int i = 0; i < _grandWorkDone.Count; i++)
        {
            Scale(_grandWorkDone[i].Effect, 1.0 + _grandWorkDone[i].Magnitude, _grandWorkDone[i].Magnitude);
        }

        // Čtvrtá kategorie: Odkaz. Jeho dva vlastní efekty (body Vzestupu, sleva
        // na práh) se sem záměrně nepromítají — nepatří do násobiče výroby a
        // sahá si na ně přímo AscensionRequirement / PendingAscensionPoints.
        var legacyUpgrades = _content.LegacyUpgrades.All;
        for (int i = 0; i < legacyUpgrades.Count; i++)
        {
            int level = _legacy.Level(i);
            if (level > 0)
            {
                Scale(legacyUpgrades[i].Effect, legacyUpgrades[i].MultiplierAtLevel(level), legacyUpgrades[i].Magnitude * level);
            }
        }

        // Kategorie 2: výzkum. Platí jen v rámci běhu (Vzestup ho resetuje),
        // proto se sčítá zvlášť a teprve výsledek se do násobičů promítne.
        double techProduction = 1.0, techHarvest = 1.0, techGrowth = 1.0, techHousing = 1.0;
        double techStorage = 1.0, techOffline = 1.0, techDiscovery = 1.0, techFestival = 1.0, techAutoBuild = 1.0;
        double techCombo = 1.0, techResearchSpeed = 1.0;

        // Cílené efekty jdou do vlastního pole; bez toho by „+5 % dřeva"
        // zvedlo i výrobu oceli.
        Array.Fill(_resourceProductionMult, 1.0);
        for (int i = 0; i < _techLevel.Length; i++)
        {
            if (_techLevel[i] == 0)
            {
                continue;
            }

            var tech = _content.Techs[i];

            // Opakovatelná technologie dává svůj bonus za KAŽDOU úroveň. Jednorázová
            // má úroveň 1, takže se pro ni nic nemění.
            double magnitude = tech.Magnitude * _techLevel[i];
            if (tech.TargetResourceIndex >= 0)
            {
                if (tech.Effect == "production_mult")
                {
                    _resourceProductionMult[tech.TargetResourceIndex] += magnitude;
                }

                continue;
            }

            switch (tech.Effect)
            {
                case "production_mult": techProduction += magnitude; break;
                case "harvest_mult": techHarvest += magnitude; break;
                case "growth_mult": techGrowth += magnitude; break;
                case "housing_mult": techHousing += magnitude; break;
                case "storage_mult": techStorage += magnitude; break;
                case "offline_mult": techOffline += magnitude; break;
                case "discovery_luck": techDiscovery += magnitude; break;
                case "festival_power": techFestival += magnitude; break;
                case "autobuild_speed": techAutoBuild += magnitude; break;
                case "combo_power": techCombo += magnitude; break;
                case "crit_chance": critChance += magnitude; break;
                case "jackpot_chance": jackpot += magnitude; break;
                case "research_discount": research += magnitude; break;
                case "research_speed": techResearchSpeed += magnitude; break;
            }
        }

        _bonuses = new PrestigeBonuses(
            production * techProduction,
            harvest * techHarvest,
            growth * techGrowth,
            housing * techHousing,
            storage * techStorage,
            start,
            offline * techOffline,
            critChance,
            jackpot,
            discovery * techDiscovery,
            festival * techFestival,
            Math.Min(research, 0.9),
            autoBuild * techAutoBuild,
            combo * techCombo,
            autoResearch,
            researchSpeed * techResearchSpeed,
            keptTechs,
            keptBuildings,
            keepsMap,
            keptRoads,

            // Podíl surovin má strop: sto procent by z Vzestupu udělalo jen
            // změnu čísla nahoře a přestal by být rozhodnutím.
            Math.Min(keptResources, MaxKeptResourceShare),
            keptWonders,
            keepsHistory);

        // Násobičové efekty se skládají mocninou; ty, které se sčítají do
        // pravděpodobnosti (kritický sběr) nebo do slevy, přirozeně součtem.
        void Scale(string effect, double multiplier, double sum)
        {
            switch (effect)
            {
                case "production_mult": production *= multiplier; break;
                case "harvest_mult": harvest *= multiplier; break;
                case "growth_mult": growth *= multiplier; break;
                case "housing_mult": housing *= multiplier; break;
                case "storage_mult": storage *= multiplier; break;
                case "start_resources": start *= multiplier; break;
                case "offline_mult": offline *= multiplier; break;
                case "discovery_luck": discovery *= multiplier; break;
                case "festival_power": festival *= multiplier; break;
                case "autobuild_speed": autoBuild *= multiplier; break;
                case "combo_power": combo *= multiplier; break;
                case "crit_chance": critChance += sum; break;
                case "jackpot_chance": jackpot += sum; break;
                case "research_discount": research += sum; break;
                case "research_speed": researchSpeed *= multiplier; break;

                // Automatický výzkum je POČET uzlů za interval, ne násobič —
                // proto se úrovně sčítají. Násobení by z první koupené úrovně
                // udělalo nulu krát cokoli.
                case "auto_research": autoResearch += sum; break;

                // Dědictví jsou POČTY, ne násobiče — proto součet úrovní.
                case "keep_techs": keptTechs += sum; break;
                case "keep_buildings": keptBuildings += sum; break;
                case "keep_map": keepsMap += sum; break;
                case "keep_roads": keptRoads += sum; break;
                case "keep_resources": keptResources += sum; break;
                case "keep_wonders": keptWonders += sum; break;
                case "keep_history": keepsHistory += sum; break;
            }
        }
    }

    /// <summary>
    /// Přepočítá odvozený stav (bydlení, pracovní místa, kapacity skladů) z nuly
    /// podle aktuálních budov a bonusů Vzestupu. Volá se po koupi upgradu, po Vzestupu
    /// a po načtení savu — drží stav konzistentní bez ohledu na pořadí změn.
    /// </summary>
    /// <summary>Přepočet odvozeného stavu pro testy (jinak interní, volá ho simulace sama).</summary>
    public void RecomputeDerivedStateForTests() => RecomputeDerivedState();

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
            if (_buildings[i].IsComplete)
            {
                ApplyBuildingBonuses(_content.Buildings[_buildings[i].DefIndex]);
            }
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
        _nodes.Clear(); // nový svět má nedotčenou krajinu, ne vytěžené paseky po předchůdcích
        _pollution.Clear(); // ani smog po továrnách, které v novém měřítku ještě nestojí
        _districts.Clear(); // čtvrti se poznají znovu, až nová zástavba doroste
        HighestSettlementRank = -1; // v novém měřítku je i první osada zas událost
        _founders.Clear(); // zakladatelé patří ke světu, který právě skončil
        // Kronika: bez Nepřerušené kroniky patří časosběr k jednomu běhu.
        if (!InheritsHistory)
        {
            History.Clear();   // nový svět začíná prázdným listem
        }
        _npcStates.Clear(); // cizí města nového měřítka hráče ještě neznají
        _npcTowns.Clear();  // a jejich zástavba se postaví znovu, až je hráč najde
        // Mlha: kdo si koupil Oko věků, svět znovu neobjevuje.
        if (!InheritsMap)
        {
            Fog.Clear();       // nový svět se musí objevit znovu
            Fog.Reveal(0, 0, FogRevealRadius * 2);
        }
        PendingCitizenRequest = CitizenRequest.None;
        CitizenCooldownTicks = 0;
        ResetContractBoard(); // zákazníci z minulého měřítka na novou nástěnku nepatří
        ContractsCompleted = 0; // a v novém měřítku začínají objednávky zas malé
        _buildingCount = 0;

        Array.Clear(_techLevel);
        TechsResearched = 0; // nové měřítko se zkoumá od nuly, i cenami
        Array.Fill(_buildingUnlocked, true);
        foreach (var tech in _content.Techs.All)
        {
            foreach (int buildingIndex in tech.UnlockedBuildingIndices)
            {
                _buildingUnlocked[buildingIndex] = false;
            }
        }

        RefreshTierUnlocks(); // dosažené měřítko přetrvává i po resetu éry
        RecomputeBonuses();
        RecomputeDerivedState(); // bez budov → základní kapacity × StorageMult
        for (int i = 0; i < _resources.Length; i++)
        {
            _resources[i] = Math.Min(_content.Resources[i].StartAmount * _bonuses.StartResourceMult, _storageCaps[i]);
        }

        Population = _content.Gameplay.StartingPopulation;
        TickCount = 0;
        SettlementsDirty = true;
        DistrictsDirty = true; // změna zástavby může vytvořit i rozpadnout čtvrť
        _roadLinksDirty = true;
    }

    /// <summary>Indexy vyzkoumaných technologií (pro serializaci savu).</summary>
    internal IEnumerable<int> ResearchedTechIndices()
    {
        for (int i = 0; i < _techLevel.Length; i++)
        {
            if (_techLevel[i] > 0)
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
    /// <summary>
    /// Obnoví jednu úroveň upgradu ze savu. Sav zapisuje ID <b>jednou za každou
    /// úroveň</b>, takže starý sav (bez úrovní) se načte jako úroveň 1 a formát
    /// se nemusel měnit.
    /// </summary>
    internal void RestoreUpgrade(int upgradeIndex)
    {
        if (_upgradeLevels[upgradeIndex] < _content.PrestigeUpgrades[upgradeIndex].MaxLevel)
        {
            _upgradeLevels[upgradeIndex]++;
        }
    }

    /// <summary>Indexy koupených upgradů Vzestupu (pro serializaci savu).</summary>
    internal IEnumerable<int> PurchasedUpgradeIndices()
    {
        for (int i = 0; i < _upgradeLevels.Length; i++)
        {
            // Jednou za každou koupenou úroveň — čtení je pak prostý součet
            // a starý formát savu zůstal beze změny.
            for (int level = 0; level < _upgradeLevels[i]; level++)
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
        RefreshTierUnlocks();     // obnovená úroveň Vzestupu → odemčené megastruktury

        // Milníky se neukládají (odvodí se z počtu budov), ale platit musí hned
        // po načtení — jinak by hráč prvních pár tiků vyráběl pod svou úrovní.
        // Bez ohlašování: dosažené prahy hráč oslavil už minule.
        _milestoneBonuses.Recompute(this, announce: false);
    }
}
