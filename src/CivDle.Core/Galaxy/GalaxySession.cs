using CivDle.Core.Content;
using CivDle.Core.Save;
using CivDle.Core.Sim;

namespace CivDle.Core.Galaxy;

/// <summary>Proč teď nejde stavět kolonizační loď.</summary>
public enum ShipBlocker
{
    /// <summary>Nic nebrání.</summary>
    None,

    /// <summary>Galaxie ještě není otevřená (brána) nebo hra galaxii nemá.</summary>
    GalaxyClosed,

    /// <summary>Loď se staví jen na Domovině — kosmodrom je tam.</summary>
    NotHome,

    /// <summary>Na Domovině nestojí kosmodrom.</summary>
    NoSpaceport,

    /// <summary>Svět je zamčený (málo hvězd).</summary>
    WorldLocked,

    /// <summary>Kolonie tam už stojí.</summary>
    AlreadyColony,

    /// <summary>Jiná loď je rozestavěná.</summary>
    ShipInProgress,
}

/// <summary>
/// Vstup na svět: simulace a případné dohánění dlouhé nepřítomnosti, které
/// obrazovka načítání posouvá po krocích. Po doběhnutí se volá <see cref="Complete"/>.
/// </summary>
public sealed class WorldEntry
{
    private readonly Action? _complete;
    private bool _completed;

    internal WorldEntry(Simulation simulation, OfflineCatchUp? catchUp, Action? complete)
    {
        Simulation = simulation;
        CatchUp = catchUp;
        _complete = complete;
        if (catchUp is null)
        {
            Complete();
        }
    }

    /// <summary>Svět, na který se vstupuje.</summary>
    public Simulation Simulation { get; }

    /// <summary>Dohánění dlouhé nepřítomnosti; <c>null</c> = krátká, už dopočítaná souhrnem.</summary>
    public OfflineCatchUp? CatchUp { get; }

    /// <summary>
    /// Dokončí vstup (připíše obchod za nepřítomnost, seřídí hodiny). Po
    /// dohánění ho volá obrazovka; u krátké nepřítomnosti proběhl sám.
    /// </summary>
    public void Complete()
    {
        if (_completed)
        {
            return;
        }

        _completed = true;
        _complete?.Invoke();
    }
}

/// <summary>
/// Galaxie za běhu hry (svety-design.md 2.2–2.4, 7.5–7.6): drží aktivní
/// svět, měří jeho toky, staví kolonizační loď, zakládá kolonie a přepíná
/// světy.
///
/// <para><b>Jedna živá simulace.</b> Při odchodu se svět uloží jako snímek
/// (stávající formát savu) a změří se jeho souhrn; při návratu se snímek
/// načte a posune: krátká nepřítomnost souhrnem (zásoby a lidé), dlouhá
/// poctivým doháněním (<see cref="OfflineCatchUp"/> — guvernér za tu dobu
/// opravdu stavěl). Pak krátké přesné dotikání, ať se výrobny rozjedou.</para>
///
/// <para>Vrstva: jádro (OOP nad simulací). Nezná obrazovky — obrazovka si
/// po přepnutí postaví novou hru nad <see cref="Active"/>.</para>
/// </summary>
public sealed class GalaxySession
{
    /// <summary>Do kolika sekund nepřítomnosti stačí souhrn (a přepnutí je okamžité).</summary>
    public const double ShortAbsenceSeconds = 30 * 60;

    /// <summary>Krátké přesné dotikání po návratu (svety-design.md 2.4: 60–120 tiků).</summary>
    public const int WarmUpTicks = 100;

    /// <summary>Jak často se přepisují hvězdy aktivního světa (tiky).</summary>
    private const long RefreshTicks = 50;

    /// <summary>Datum, od kterého se počítá dohánění — na skutečných hodinách nezáleží.</summary>
    private static readonly DateTime Epoch = new(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    /// <summary>Jak často se počítá obchod (galaktické sekundy).</summary>
    private const double TradeStepSeconds = 1.0;

    private readonly SaveGameSerializer _serializer = new();
    private readonly SessionTradeWorlds _tradeWorlds;
    private long _lastRefreshTick = long.MinValue;

    /// <summary>
    /// Probíhá vstup na svět s doháněním? Pak se obchod nepočítá — aktivní
    /// simulace už je nová, ale galaktické hodiny ještě patří světu, ze kterého
    /// hráč odešel.
    /// </summary>
    private bool _entering;

    public GalaxySession(GalaxyContent contents, GalaxyState state, Simulation active)
    {
        Contents = contents;
        State = state;
        Active = active;
        _tradeWorlds = new SessionTradeWorlds(this);
    }

    /// <summary>
    /// Galaxie z načteného savu. Save bez galaxie (starší hra) dostane galaxii
    /// s jedinou Domovinou — hra vypadá a chová se přesně jako dřív.
    /// </summary>
    /// <param name="contents">Obsah světů.</param>
    /// <param name="save">Načtený save (nebo nová hra: galaxie <c>null</c>).</param>
    public static GalaxySession Resume(GalaxyContent contents, LoadedSave save)
    {
        var state = save.Galaxy ?? GalaxyState.NewWithHome(save.Simulation);
        var active = state.Active;
        if (active.PresetId.Length == 0)
        {
            active.PresetId = save.Metadata.PresetId;
            active.SizeId = save.Metadata.SizeId;
        }

        state.LastSeenTick = save.Simulation.TickCount; // dohánění offline tiky posune, hodiny s nimi
        return new GalaxySession(contents, state, save.Simulation);
    }

    /// <summary>Obsah všech světů.</summary>
    public GalaxyContent Contents { get; }

    /// <summary>Stav galaxie (ukládá se).</summary>
    public GalaxyState State { get; }

    /// <summary>Svět, na který se hráč dívá.</summary>
    public Simulation Active { get; private set; }

    /// <summary>Měřič toků aktivního světa.</summary>
    public FlowMeter Meter { get; } = new();

    /// <summary>Světy galaxie.</summary>
    public WorldCatalog Catalog => Contents.Catalog;

    /// <summary>Je galaxie přístupná (hra ji má a brána je otevřená)?</summary>
    public bool IsOpen => Catalog.IsEnabled && State.GateOpened;

    /// <summary>Galaktický čas teď.</summary>
    public double NowSeconds => State.NowSeconds(Active);

    /// <summary>
    /// Každý snímek: hodiny (Vzestup), měřič toků a jednou za čas hvězdy
    /// a brána. Levné — porovnání a občas průchod úkoly.
    /// </summary>
    public void Update()
    {
        State.Observe(Active);
        Meter.Sample(Active);
        if (Active.TickCount - _lastRefreshTick >= RefreshTicks || Active.TickCount < _lastRefreshTick)
        {
            _lastRefreshTick = Active.TickCount;
            State.Refresh(Active);
        }

        var trade = State.Trade;
        if (!_entering && (double.IsNaN(trade.LastAdvancedAt) || NowSeconds - trade.LastAdvancedAt >= TradeStepSeconds))
        {
            AdvanceTrade();
        }
    }

    // ----- obchod -----

    /// <summary>Přístup obchodu ke světům (aktivní ze simulace, ostatní ze souhrnu).</summary>
    public ITradeWorlds TradeWorlds => _tradeWorlds;

    /// <summary>Posune obchod do teď (vyloží, co dorazilo, a naloží, co trasy unesou).</summary>
    public void AdvanceTrade()
    {
        if (!_entering)
        {
            State.Trade.Advance(NowSeconds, _tradeWorlds, Catalog.Trade, Catalog.StepsBetween);
        }
    }

    /// <summary>
    /// Co brání založit trasu: oba konce musí být kolonie, zdroj musí surovinu
    /// vyvážet (artikly světa) a cíl ji musí znát.
    /// </summary>
    public TradeBlocker CanOpenRoute(string fromWorldId, string toWorldId, string resourceId)
    {
        if (fromWorldId == toWorldId)
        {
            return TradeBlocker.SameWorld;
        }

        if (!State.Records.ContainsKey(fromWorldId) || !State.Records.ContainsKey(toWorldId))
        {
            return TradeBlocker.NotColony;
        }

        var from = Contents.For(fromWorldId);
        bool exported = false;
        foreach (int r in from.World.ExportIndices)
        {
            exported |= from.Resources[r].Id == resourceId;
        }

        if (!exported)
        {
            return TradeBlocker.NotExported;
        }

        if (!Contents.For(toWorldId).Resources.TryIndexOf(resourceId, out _))
        {
            return TradeBlocker.UnknownAtDestination;
        }

        if (State.Trade.Exists(fromWorldId, toWorldId, resourceId))
        {
            return TradeBlocker.Duplicate;
        }

        return State.Trade.Routes.Count >= TradeRouteSystem.MaxRoutes ? TradeBlocker.TooMany : TradeBlocker.None;
    }

    /// <summary>Založí trasu.</summary>
    /// <exception cref="InvalidOperationException">Trasu nejde založit (viz <see cref="CanOpenRoute"/>).</exception>
    public TradeRoute OpenRoute(string fromWorldId, string toWorldId, string resourceId)
    {
        var blocker = CanOpenRoute(fromWorldId, toWorldId, resourceId);
        if (blocker != TradeBlocker.None)
        {
            throw new InvalidOperationException($"Trasu {fromWorldId}→{toWorldId} ({resourceId}) nejde založit: {blocker}.");
        }

        AdvanceTrade(); // nová trasa nemá dostat náklad za čas, kdy neexistovala
        return State.Trade.Open(fromWorldId, toWorldId, resourceId);
    }

    /// <summary>Zruší trasu (co pluje, dopluje).</summary>
    public bool CloseRoute(int routeId)
    {
        AdvanceTrade();
        return State.Trade.Close(routeId);
    }

    /// <summary>Kapacita trasy teď (jednotek za sekundu).</summary>
    public double RouteCapacity(TradeRoute route) => State.Trade.CapacityOf(route, _tradeWorlds);

    /// <summary>Doba cesty mezi dvěma světy (galaktické sekundy).</summary>
    public double TravelSeconds(string fromWorldId, string toWorldId) =>
        Catalog.StepsBetween(fromWorldId, toWorldId) * Catalog.Trade.TravelSecondsPerStep;

    /// <summary>
    /// Svět v obchodu: aktivní odpovídá ze simulace, neaktivní ze souhrnu
    /// posunutého do teď a z nevyrovnaného obchodu. Neaktivní svět tak za
    /// nepřítomnosti posílá jen to, co by opravdu měl — a po návratu se mu
    /// odeslané odečte a přivezené připíše (<see cref="WorldRecord.PendingDelta"/>).
    /// </summary>
    private sealed class SessionTradeWorlds : ITradeWorlds
    {
        private readonly GalaxySession _session;

        public SessionTradeWorlds(GalaxySession session) => _session = session;

        public double Available(string worldId, string resourceId)
        {
            if (IsActive(worldId, resourceId, out int r))
            {
                return _session.Active.TradeAvailable(r);
            }

            return Record(worldId) is { } record ? record.EstimatedStock(resourceId, _session.NowSeconds) : 0;
        }

        public double Room(string worldId, string resourceId)
        {
            if (IsActive(worldId, resourceId, out int r))
            {
                return _session.Active.TradeRoom(r);
            }

            if (Record(worldId) is not { Summary: { } summary } record)
            {
                return 0;
            }

            return Math.Max(0, summary.CapOf(resourceId) - record.EstimatedStock(resourceId, _session.NowSeconds));
        }

        public double Take(string worldId, string resourceId, double amount)
        {
            if (IsActive(worldId, resourceId, out int r))
            {
                return _session.Active.ExportResource(r, amount);
            }

            if (Record(worldId) is not { } record)
            {
                return 0;
            }

            double taken = Math.Min(amount, record.EstimatedStock(resourceId, _session.NowSeconds));
            if (taken > 0)
            {
                record.PendingDelta[resourceId] = record.PendingDelta.GetValueOrDefault(resourceId) - taken;
            }

            return Math.Max(0, taken);
        }

        public double Put(string worldId, string resourceId, double amount)
        {
            if (IsActive(worldId, resourceId, out int r))
            {
                return _session.Active.ImportResource(r, amount);
            }

            if (Record(worldId) is not { } record)
            {
                return 0;
            }

            double put = Math.Min(amount, Room(worldId, resourceId));
            if (put > 0)
            {
                record.PendingDelta[resourceId] = record.PendingDelta.GetValueOrDefault(resourceId) + put;
            }

            return Math.Max(0, put);
        }

        public double PortCapacity(string worldId)
        {
            if (worldId == _session.State.ActiveWorldId)
            {
                return _session.Active.PortCapacity();
            }

            return Record(worldId)?.PortCapacity ?? 0;
        }

        private bool IsActive(string worldId, string resourceId, out int resource)
        {
            resource = -1;
            return worldId == _session.State.ActiveWorldId
                && _session.Active.Content.Resources.TryIndexOf(resourceId, out resource);
        }

        private WorldRecord? Record(string worldId) =>
            _session.State.Records.TryGetValue(worldId, out var record) && worldId != _session.State.ActiveWorldId
                ? record
                : null;
    }

    // ----- kolonizační loď -----

    /// <summary>
    /// Cena stupně lodi k danému světu: cena z dat × růst za každou už
    /// založenou kolonii (svety-design.md 2.2). V surovinách Domoviny.
    /// </summary>
    public IReadOnlyList<ResourceAmount> ShipStageCost(WorldDef world, int stage)
    {
        double scale = Math.Pow(world.ColonyCostGrowth, State.ColonyCount);
        var cost = world.ColonyCost[stage].Cost;
        var result = new ResourceAmount[cost.Count];
        for (int i = 0; i < cost.Count; i++)
        {
            result[i] = new ResourceAmount(cost[i].ResourceIndex, (int)Math.Min(int.MaxValue, Math.Ceiling(cost[i].Amount * scale)));
        }

        return result;
    }

    /// <summary>Co brání postavit loď k tomuhle světu.</summary>
    public ShipBlocker CanStartShip(string worldId)
    {
        if (!IsOpen)
        {
            return ShipBlocker.GalaxyClosed;
        }

        if (State.ActiveWorldId != WorldScope.HomeId)
        {
            return ShipBlocker.NotHome;
        }

        if (!HasSpaceport(Active))
        {
            return ShipBlocker.NoSpaceport;
        }

        if (State.Ship is not null)
        {
            return ShipBlocker.ShipInProgress;
        }

        var world = Catalog.Find(worldId);
        return world is null ? ShipBlocker.WorldLocked : State.AvailabilityOf(world) switch
        {
            WorldAvailability.Colony => ShipBlocker.AlreadyColony,
            WorldAvailability.Locked => ShipBlocker.WorldLocked,
            _ => ShipBlocker.None,
        };
    }

    /// <summary>Začne stavět loď k danému světu.</summary>
    /// <exception cref="InvalidOperationException">Loď teď stavět nejde (viz <see cref="CanStartShip"/>).</exception>
    public void StartShip(string worldId)
    {
        var blocker = CanStartShip(worldId);
        if (blocker != ShipBlocker.None)
        {
            throw new InvalidOperationException($"Loď k '{worldId}' teď nejde stavět: {blocker}.");
        }

        State.Ship = new ColonyShipState(worldId);
    }

    /// <summary>Svět, ke kterému se loď staví; <c>null</c> = žádná loď.</summary>
    public WorldDef? ShipTarget => State.Ship is { } ship ? Catalog.Find(ship.TargetWorldId) : null;

    /// <summary>Je loď hotová a čeká na výběr místa přistání?</summary>
    public bool IsShipReady => ShipTarget is { } world && State.Ship!.StageIndex >= world.ColonyCost.Count;

    /// <summary>Kolik je vloženo do rozestavěného stupně (surovina Domoviny podle indexu).</summary>
    public double ShipInvested(int homeResourceIndex) =>
        State.Ship is { } ship
            ? ship.Invested.GetValueOrDefault(Contents.Home.Resources[homeResourceIndex].Id)
            : 0;

    /// <summary>
    /// Vloží přebytky Domoviny do rozestavěného stupně lodi (nad rezervou
    /// guvernéra). Zaplacený stupeň se uzavře a začne další.
    /// </summary>
    /// <returns>Kolik se vložilo.</returns>
    public double InvestInShip()
    {
        if (State.ActiveWorldId != WorldScope.HomeId || ShipTarget is not { } world || IsShipReady)
        {
            return 0;
        }

        var ship = State.Ship!;
        var resources = Contents.Home.Resources;
        var invested = new double[resources.Count];
        foreach (var (id, amount) in ship.Invested)
        {
            if (resources.TryIndexOf(id, out int r))
            {
                invested[r] = amount;
            }
        }

        var cost = ShipStageCost(world, ship.StageIndex);
        double total = Active.InvestSurplus(cost, invested);

        bool paid = true;
        foreach (var item in cost)
        {
            paid &= invested[item.ResourceIndex] >= item.Amount - 1e-9;
        }

        ship.Invested.Clear();
        if (paid)
        {
            ship.StageIndex++;
        }
        else
        {
            for (int r = 0; r < invested.Length; r++)
            {
                if (invested[r] > 0)
                {
                    ship.Invested[resources[r].Id] = invested[r];
                }
            }
        }

        return total;
    }

    // ----- přistání -----

    /// <summary>
    /// Místa, kam může hotová loď přistát. Terén cílového světa se jen
    /// přečte — kolonie ještě nevznikla.
    /// </summary>
    public IReadOnlyList<(int X, int Y)> LandingSites(string worldId)
    {
        var content = Contents.For(worldId);
        long seed = ColonySeed(worldId);
        var preset = content.WorldGen.Presets[content.World.PresetIndex];
        return LandingSiteFinder.Find(content, WorldTerrain.Create(content, preset, seed));
    }

    /// <summary>
    /// Založí kolonii: Domovina se uloží jako snímek, na cílovém světě přistane
    /// modul s nákladem lodi, kolonisté si přivezou společné znalosti a Odkaz.
    /// Nová kolonie je od téhle chvíle aktivní svět.
    /// </summary>
    /// <exception cref="InvalidOperationException">Loď není hotová nebo modul na místo nesedne.</exception>
    public Simulation Colonize(int x, int y)
    {
        if (!IsShipReady)
        {
            throw new InvalidOperationException("Kolonizační loď není hotová.");
        }

        var world = ShipTarget!;
        var content = Contents.For(world.Id);
        long seed = ColonySeed(world.Id);
        var preset = content.WorldGen.Presets[content.World.PresetIndex];
        var colony = new Simulation(content, WorldTerrain.Create(content, preset, seed), seed);
        var landed = colony.Land(x, y);
        if (landed != PlacementResult.Ok)
        {
            throw new InvalidOperationException($"Přistávací modul na {x},{y} nesedne: {landed}.");
        }

        var home = Active;
        AdvanceTrade();
        double now = Leave();
        colony.GrantKnownTechs(home.ResearchedTechIds());
        colony.CopyLegacyFrom(home);

        var record = State.Add(new WorldRecord(world.Id, seed)
        {
            PresetId = preset.Id,
            SizeId = content.WorldGen.Sizes[content.WorldGen.DefaultSizeIndex].Id,
            FoundedAtSeconds = now,
            LeftAtSeconds = now,
            LandingX = x,
            LandingY = y,
        });

        State.Ship = null;
        Enter(record, colony, now);
        return colony;
    }

    /// <summary>
    /// Seed kolonie: z Domoviny a ID světa. Každá galaxie má jiné planety,
    /// ale stejný save dá vždy stejnou planetu (terén se neukládá).
    /// </summary>
    public long ColonySeed(string worldId)
    {
        ulong hash = 14695981039346656037UL;
        foreach (char c in worldId)
        {
            hash = (hash ^ c) * 1099511628211UL;
        }

        return (long)(hash ^ (ulong)State.Records[WorldScope.HomeId].Seed);
    }

    /// <summary>
    /// Načte svět ze snímku jen ke čtení — bez přepnutí (přehrání konce
    /// kapitoly nad Domovinou, když hráč zrovna stojí v kolonii). Aktivní svět
    /// vrátí rovnou.
    /// </summary>
    public Simulation PeekWorld(string worldId)
    {
        if (worldId == State.ActiveWorldId)
        {
            return Active;
        }

        if (!State.Records.TryGetValue(worldId, out var record) || record.Snapshot is null)
        {
            throw new InvalidOperationException($"Svět '{worldId}' nemá snímek.");
        }

        return _serializer.Read(new MemoryStream(record.Snapshot), Contents).Simulation;
    }

    // ----- přepínání -----

    /// <summary>
    /// Přepne na jiný založený svět. Aktivní svět se uloží jako snímek
    /// a změří; cílový se načte a posune o dobu nepřítomnosti.
    /// </summary>
    /// <exception cref="InvalidOperationException">Svět není kolonie.</exception>
    public WorldEntry SwitchTo(string worldId)
    {
        if (worldId == State.ActiveWorldId)
        {
            return new WorldEntry(Active, null, null);
        }

        if (!State.Records.TryGetValue(worldId, out var target) || target.Snapshot is null)
        {
            throw new InvalidOperationException($"Svět '{worldId}' není založená kolonie.");
        }

        var previous = Active;
        var sim = _serializer.Read(new MemoryStream(target.Snapshot), Contents).Simulation;
        AdvanceTrade(); // vyrovnat obchod se světem, dokud je živý
        double now = Leave();
        sim.CopyLegacyFrom(previous);
        target.Snapshot = null;

        double absence = Math.Max(0, now - target.LeftAtSeconds);
        if (absence <= ShortAbsenceSeconds || target.Summary is null)
        {
            if (target.Summary is { } summary)
            {
                sim.ResumeFrom(summary.Advance(absence));
            }

            ApplyPending(target, sim);
            for (int i = 0; i < WarmUpTicks; i++)
            {
                sim.Tick();
            }

            Enter(target, sim, now);
            return new WorldEntry(sim, null, null);
        }

        // Dlouhá nepřítomnost: poctivé dohánění, které posouvá obrazovka
        // načítání (s ukazatelem a stropem práce). Vstup se dokončí po něm.
        var catchUp = new OfflineCatchUp(sim, Epoch, Epoch.AddSeconds(absence));
        Active = sim;
        _entering = true;
        return new WorldEntry(sim, catchUp, () =>
        {
            _entering = false;
            ApplyPending(target, sim);
            Enter(target, sim, now);
        });
    }

    /// <summary>Uloží aktivní svět jako snímek se souhrnem; vrací galaktický čas odchodu.</summary>
    private double Leave()
    {
        State.Observe(Active);
        double now = State.NowSeconds(Active);
        var record = State.Active;
        State.Refresh(Active);
        record.Summary = WorldSummary.Measure(Active, Meter);
        record.PortCapacity = Active.PortCapacity();
        record.LeftAtSeconds = now;
        record.Snapshot = Snapshot(Active, record);
        return now;
    }

    /// <summary>Udělá ze světa aktivní svět a seřídí galaktické hodiny.</summary>
    private void Enter(WorldRecord record, Simulation sim, double now)
    {
        record.Snapshot = null;
        State.ActiveWorldId = record.WorldId;
        State.ActiveEnteredAtTick = sim.TickCount;
        State.ActiveEnteredAtSeconds = now;
        State.LastSeenTick = sim.TickCount;
        Active = sim;
        Meter.Reset();
        _lastRefreshTick = long.MinValue;
        State.Refresh(sim);
    }

    /// <summary>Připíše, co obchod za nepřítomnosti přivezl a odvezl.</summary>
    private static void ApplyPending(WorldRecord record, Simulation sim)
    {
        foreach (var (id, amount) in record.PendingDelta)
        {
            if (sim.Content.Resources.TryIndexOf(id, out int r))
            {
                sim.AddResource(r, amount);
            }
        }

        record.PendingDelta.Clear();
    }

    private byte[] Snapshot(Simulation sim, WorldRecord record)
    {
        using var stream = new MemoryStream();
        _serializer.Write(stream, sim, new SaveMetadata(sim.Seed, record.SizeId, record.PresetId, Epoch));
        return stream.ToArray();
    }

    private static bool HasSpaceport(Simulation home)
    {
        var buildings = home.Buildings;
        var defs = home.Content.Buildings;
        if (!defs.TryIndexOf("spaceport", out int spaceport))
        {
            return true; // data bez kosmodromu (mody, testy) — loď se staví bez něj
        }

        for (int i = 0; i < buildings.Length; i++)
        {
            if (buildings[i].DefIndex == spaceport && buildings[i].IsComplete)
            {
                return true;
            }
        }

        return false;
    }
}
