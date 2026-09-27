using CivDle.Core.Content;
using CivDle.Core.World;

namespace CivDle.Core.Sim;

/// <summary>
/// V jaké fázi je přírodní jev.
/// </summary>
public enum HazardPhase
{
    /// <summary>Klid.</summary>
    Calm,

    /// <summary>Blíží se — hra varuje, obloha hnědne.</summary>
    Warning,

    /// <summary>Probíhá.</summary>
    Active,
}

/// <summary>
/// Co se právě děje s jedním přírodním jevem — pro render a HUD. Jen ke čtení.
/// </summary>
/// <param name="HazardIndex">Index jevu v <see cref="HazardCatalog"/>; −1 = žádný.</param>
/// <param name="Phase">Fáze.</param>
/// <param name="SecondsToStart">Za kolik sekund jev začne (při varování).</param>
/// <param name="Progress">Jak daleko je bouře (0–1) při <see cref="HazardPhase.Active"/>.</param>
/// <param name="DirectionIndex">Odkud jev přichází (0 = od severu, po směru hodin po 45°).</param>
/// <param name="CenterX">Střed města, přes který pás jde.</param>
/// <param name="CenterY">Střed města, přes který pás jde.</param>
/// <param name="Radius">Poloměr, který pás přejde (dlaždice).</param>
/// <param name="BandTiles">Šířka pásu.</param>
public readonly record struct HazardView(
    int HazardIndex, HazardPhase Phase, double SecondsToStart, double Progress,
    int DirectionIndex, int CenterX, int CenterY, double Radius, int BandTiles)
{
    /// <summary>Žádný jev.</summary>
    public static HazardView None => new(-1, HazardPhase.Calm, 0, 0, 0, 0, 0, 0, 0);

    /// <summary>Směr pohybu pásu (od <see cref="DirectionIndex"/> pryč), jednotkový vektor.</summary>
    public (double X, double Y) Movement => HazardSystem.MovementOf(DirectionIndex);

    /// <summary>Vzdálenost středu pásu od středu města podél pohybu (dlaždice).</summary>
    public double BandOffset => -Radius + 2 * Radius * Progress;
}

/// <summary>
/// Přírodní jevy světa (svety-design.md 7.3) — písečné bouře na Duně, vánice
/// na Mrazu. Každý jev je <b>chování za behavior-ID</b> s parametry z dat.
///
/// <para><b>Rozvrh je čistá funkce času a seedu.</b> Bouře k začíná
/// v <c>první + k × rozestup + posun(k)</c>; posun je hash seedu a čísla bouře.
/// Nic se neplánuje dopředu a načtená hra potká tytéž bouře. Uloží se jen
/// rozběhnutá bouře (střed a poloměr, přes který jde) a statistika.</para>
///
/// <para><b>Měkký tlak.</b> Zasypaná budova jen vypadne z provozu (stejný
/// odpočet jako po zásahu v režimu obrany) a lidé ji sami vyhrabou. Nic se
/// nezboří.</para>
///
/// <para><b>Příliv</b> (Souostroví) je taky funkce času: hladina stoupá
/// a klesá v cyklu, dlaždice přílivové mělčiny je pod vodou, když hladina
/// přesáhne její výšku (z výšky terénu). Budova bez kůlů na zaplavené
/// dlaždici vypadne a s odlivem se sama vrátí; nic se neukládá.</para>
///
/// <para>Běží jednou za sekundu, a jen na světě, který jev má. Bez alokací
/// v tiku — seznam ochran se jen čistí.</para>
/// </summary>
internal sealed class HazardSystem
{
    private readonly GameContent _content;
    private readonly long _seed;
    private readonly BurialState[] _states;

    // Ochrany kolem budovy (x, y, r²) — plní se jednou za sekundu bouře.
    private readonly List<(int X, int Y, int RadiusSquared)> _shelters = new();

    // Ochrany před lávou (chladicí věž) — sbírají se každou sekundu erupce.
    private readonly List<(int X, int Y, int RadiusSquared)> _lavaShelters = new();

    // Ochrany před přílivem (chrám přílivu) — drží se mezi koly, čte je i render.
    private readonly List<(int X, int Y, int RadiusSquared)> _tideShelters = new();

    /// <summary>Dráha lávy rozběhnuté erupce (po jevech; prázdné u jiných).</summary>
    private readonly List<long>[] _paths;

    /// <summary>Předpověď dráhy příští lávy a kdy se naposledy počítala.</summary>
    private readonly List<long> _predicted = new();
    private long _predictedLayout = long.MinValue;
    private int _predictedTerrain = int.MinValue;
    private (int X, int Y) _predictedCenter = (int.MinValue, int.MinValue);
    private long _predictedTick = -1_000_000; // „dávno" — bez přetečení při odečtu

    /// <summary>Poslední ztuhlá láva (jen vzhled: kůra chvíli dohasíná).</summary>
    private readonly List<long> _cooling = new();
    private double _cooledAt = double.NegativeInfinity;

    /// <summary>Příliv světa; −1 = svět příliv nemá.</summary>
    private readonly int _tide;

    /// <summary>Hladina moře světa (výška terénu, kde začíná souš).</summary>
    private readonly double _seaLevel;

    public HazardSystem(GameContent content, long seed)
    {
        _content = content;
        _seed = seed;
        _states = new BurialState[content.Hazards.Count];
        _paths = new List<long>[content.Hazards.Count];
        for (int i = 0; i < _states.Length; i++)
        {
            _states[i] = new BurialState();
            _paths[i] = new List<long>();
        }

        _tide = content.Hazards.TideIndex;
        int preset = content.World.PresetIndex;
        _seaLevel = preset >= 0 ? content.WorldGen.Presets[preset].SeaLevel : 0.5;
    }

    /// <summary>Pravidlo přílivu světa; <c>null</c> = svět příliv nemá.</summary>
    public TideRule? Tide => _tide >= 0 ? _content.Hazards.Hazards[_tide].Tide : null;

    /// <summary>Index přílivu v katalogu jevů; −1 = žádný.</summary>
    public int TideIndex => _tide;

    /// <summary>Hladina moře světa (pro výšku mělčin).</summary>
    public double SeaLevel => _seaLevel;

    /// <summary>Kolik jevů přešlo, aniž by něco zasypaly (a město už stálo).</summary>
    public int Calm { get; private set; }

    /// <summary>
    /// Kolik jevů přešlo přes stojící město. Erupce jen ty, jejichž láva
    /// k městu dotekla (<see cref="ThreatensCity"/>).
    /// </summary>
    public int Weathered { get; private set; }

    /// <summary>Stav jednoho jevu pro save.</summary>
    internal BurialState StateOf(int hazard) => _states[hazard];

    /// <summary>Po načtení: statistika ze savu.</summary>
    internal void Restore(int weathered, int calm)
    {
        Weathered = weathered;
        Calm = calm;
    }

    public void Tick(Simulation sim)
    {
        if (_states.Length == 0 || sim.TickCount % (long)Simulation.TicksPerSecond != 0)
        {
            return;
        }

        double now = sim.TickCount / Simulation.TicksPerSecond;
        var hazards = _content.Hazards.Hazards;
        for (int h = 0; h < hazards.Count; h++)
        {
            if (hazards[h].Burial is { } rule)
            {
                TickBurial(sim, h, rule, now);
            }
            else if (hazards[h].Tide is { } tide)
            {
                TickTide(sim, h, tide, now);
            }
            else if (hazards[h].Eruption is { } eruption)
            {
                TickEruption(sim, h, eruption, now);
            }
        }
    }

    // ----- erupce -----

    /// <summary>
    /// Jednou za sekundu: varování, začátek (průduch nejblíž městu a dráha
    /// z kopce), postup lávy po dráze a na konci ztuhnutí v novou zem.
    ///
    /// <para>Stav erupce drží tentýž <see cref="BurialState"/> jako bouře:
    /// místo středu pásu průduch, místo poloměru kam láva dotekla. Dráha se
    /// neukládá — z průduchu se po načtení spočítá znovu.</para>
    /// </summary>
    private void TickEruption(Simulation sim, int hazard, EruptionRule rule, double now)
    {
        var state = _states[hazard];
        var path = _paths[hazard];
        var (phase, eruption) = PhaseAt(hazard, rule, now);

        if (state.ActiveStorm >= 0 && (phase != HazardPhase.Active || eruption != state.ActiveStorm))
        {
            FinishEruption(sim, hazard, rule, state, path, now);
        }

        if (phase == HazardPhase.Warning && state.WarnedStorm != eruption)
        {
            state.WarnedStorm = eruption;
            sim.EnqueueNotification(new GameNotification(
                NotificationKind.Hazard, _content.Hazards.Hazards[hazard].WarningKey, "hazard.lava.soon"));
        }

        if (phase != HazardPhase.Active)
        {
            return;
        }

        if (state.ActiveStorm != eruption)
        {
            BeginEruption(sim, rule, state, path, eruption);
        }
        else if (path.Count == 0 && state.Radius >= 0)
        {
            TracePath(sim, rule, state.CenterX, state.CenterY, path); // po načtení savu
        }

        if (state.Radius < 0 || path.Count < 2)
        {
            return; // průduch v dosahu není (nebo láva nemá kam téct)
        }

        double progress = Math.Clamp((now - StartOf(hazard, rule, eruption)) / rule.FlowSeconds, 0, 1);
        int front = Math.Min(path.Count - 1, (int)Math.Ceiling(progress * (path.Count - 1)));
        int reached = (int)state.Radius;
        int lavaTicks = (int)Math.Ceiling(rule.LavaSeconds * Simulation.TicksPerSecond / sim.Bonuses.HazardResistance);

        // Chladicí věž a Kovadlina světa: v jejich okruhu láva budovy
        // nezalije (mlha a kanály ji zchladí dřív).
        CollectShelters(sim.BuildingsMutable, hazard, _lavaShelters);
        for (int i = reached + 1; i <= front; i++)
        {
            Scorch(sim, path[i], lavaTicks, state);
        }

        state.Radius = Math.Max(reached, front);
    }

    /// <summary>Erupce začíná: průduch nejblíž městu a dráha lávy z něj.</summary>
    private void BeginEruption(Simulation sim, EruptionRule rule, BurialState state, List<long> path, int eruption)
    {
        state.ActiveStorm = eruption;
        state.WarnedStorm = eruption;
        state.Buried = 0;
        state.Radius = 0;
        state.CityStood = false;
        path.Clear();
        if (FindVent(sim, rule, out int ventX, out int ventY))
        {
            state.CenterX = ventX;
            state.CenterY = ventY;
            TracePath(sim, rule, ventX, ventY, path);
            state.CityStood = sim.Buildings.Length >= rule.MinBuildings && ThreatensCity(sim, path);
        }
        else
        {
            state.Radius = -1; // v dosahu města žádný průduch — tahle erupce ho mine
        }
    }

    /// <summary>Jak blízko budovy musí láva dotéct, aby erupce „šla na město".</summary>
    private const int ThreatTiles = 2;

    /// <summary>
    /// Erupce jde na město, když láva doteče k budově (i k hrázi, která ji
    /// zastavila). Jen taková se počítá do statistik a hvězd: láva, která
    /// teče z kopce na druhou stranu, není zvládnuté pravidlo světa.
    /// Jednou za erupci, ne za tik.
    /// </summary>
    private static bool ThreatensCity(Simulation sim, List<long> path)
    {
        for (int i = 1; i < path.Count; i++)
        {
            int x = TileKey.X(path[i]);
            int y = TileKey.Y(path[i]);
            for (int dy = -ThreatTiles; dy <= ThreatTiles; dy++)
            {
                for (int dx = -ThreatTiles; dx <= ThreatTiles; dx++)
                {
                    if (sim.TryGetBuildingAt(x + dx, y + dy, out _))
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    /// <summary>Láva dotekla na dlaždici: budova na ní (kromě hráze a kanálu) vypadne.</summary>
    private void Scorch(Simulation sim, long tile, int lavaTicks, BurialState state)
    {
        if (!sim.TryGetBuildingAt(TileKey.X(tile), TileKey.Y(tile), out int index))
        {
            return;
        }

        ref var building = ref sim.BuildingsMutable[index];
        if (!building.IsComplete || _content.Buildings[building.DefIndex].LavaRole != LavaRole.None
            || IsIn(_lavaShelters, building.X, building.Y))
        {
            return; // staveniště láva jen zalije; hráz a kanál jsou na ni stavěné; v závětří chladicí věže nic
        }

        if (building.DisabledTicks <= 0 || building.DisabledCause != DisableCause.Lava)
        {
            state.Buried++; // budova přes víc dlaždic se počítá jednou
        }

        building.DisabledTicks = Math.Max(building.DisabledTicks, lavaTicks);
        building.DisabledCause = DisableCause.Lava;
        sim.MarkDisabled();
    }

    /// <summary>Láva dotekla: ztuhne v novou zem, statistika a zpráva hráči.</summary>
    private void FinishEruption(Simulation sim, int hazard, EruptionRule rule, BurialState state, List<long> path, double now)
    {
        if (path.Count == 0 && state.Radius >= 0)
        {
            TracePath(sim, rule, state.CenterX, state.CenterY, path); // po načtení savu
        }

        int reached = Math.Min(path.Count - 1, (int)state.Radius);
        for (int i = 1; i <= reached; i++)
        {
            sim.CoverWithLava(TileKey.X(path[i]), TileKey.Y(path[i]), rule.CrustBiomeIndex);
        }

        _cooling.Clear();
        for (int i = 0; i <= reached; i++)
        {
            _cooling.Add(path[i]);
        }

        _cooledAt = now;
        Finish(sim, hazard, state);
        path.Clear();
    }

    /// <summary>
    /// Průduch nejblíž středu města v dosahu <see cref="EruptionRule.SearchRadius"/>
    /// (při shodě ten s menším y, pak x — deterministicky).
    /// </summary>
    private static bool FindVent(Simulation sim, EruptionRule rule, out int ventX, out int ventY)
    {
        int cx = sim.CityCenterX;
        int cy = sim.CityCenterY;
        int radius = rule.SearchRadius;
        long best = long.MaxValue;
        ventX = 0;
        ventY = 0;
        for (int y = cy - radius; y <= cy + radius; y++)
        {
            for (int x = cx - radius; x <= cx + radius; x++)
            {
                if (sim.BiomeAt(x, y) != rule.VentBiomeIndex)
                {
                    continue;
                }

                long d = (long)(x - cx) * (x - cx) + (long)(y - cy) * (y - cy);
                if (d < best)
                {
                    best = d;
                    ventX = x;
                    ventY = y;
                }
            }
        }

        return best != long.MaxValue;
    }

    /// <summary>Dráha lávy z průduchu přes zástavbu, jak je teď (viz <see cref="LavaFlow.Trace"/>).</summary>
    internal static void TracePath(Simulation sim, EruptionRule rule, int ventX, int ventY, List<long> path) =>
        LavaFlow.Trace(new CityLavaGround(sim, rule), rule.FlowLength, ventX, ventY, path);

    /// <summary>Povrch se zástavbou: hráz = zeď, kanál = koryto, ztuhlá láva o kus výš.</summary>
    private readonly struct CityLavaGround : ILavaGround
    {
        private readonly Simulation _sim;
        private readonly int _crust;

        public CityLavaGround(Simulation sim, EruptionRule rule)
        {
            _sim = sim;
            _crust = rule.CrustBiomeIndex;
        }

        public double FlowHeight(int x, int y)
        {
            if (_sim.TryGetBuildingAt(x, y, out int index))
            {
                var role = _sim.Content.Buildings[_sim.Buildings[index].DefIndex].LavaRole;
                if (role == LavaRole.Wall)
                {
                    return LavaFlow.Wall;
                }

                if (role == LavaRole.Channel)
                {
                    return _sim.ElevationAt(x, y) - 1.0;
                }
            }

            return LavaFlow.TerrainHeight(_sim.ElevationAt(x, y), _sim.BiomeAt(x, y) == _crust);
        }

        public bool IsWater(int x, int y) => _sim.IsWaterAt(x, y);
    }

    /// <summary>
    /// Kudy poteče příští láva (pro seismickou stanici a guvernéra): z průduchu
    /// nejblíž městu, se zástavbou, jak je teď. Přepočítá se jen po změně
    /// zástavby nebo terénu, nejvýš dvakrát za sekundu (mezi tím může být
    /// o půl sekundy stará — guvernér pak hráz postaví o kolo později).
    /// </summary>
    public IReadOnlyList<long> PredictedPath(Simulation sim)
    {
        int hazard = _content.Hazards.EruptionIndex;
        if (hazard < 0)
        {
            return Array.Empty<long>();
        }

        // Revize budov (ne rozložení): hráz staveniště drží lávu už rozestavěná
        // a předpověď ji musí vidět hned — jinak by guvernér stavěl hráz za hrází.
        // Střed města rozhoduje, ze kterého průduchu láva poteče.
        var center = (sim.CityCenterX, sim.CityCenterY);
        bool stale = sim.BuildingRevision != _predictedLayout || sim.TerrainRevision != _predictedTerrain
            || center != _predictedCenter;
        if (stale && sim.TickCount - _predictedTick >= Simulation.TicksPerSecond / 2)
        {
            _predictedLayout = sim.BuildingRevision;
            _predictedTerrain = sim.TerrainRevision;
            _predictedCenter = center;
            _predictedTick = sim.TickCount;
            var rule = _content.Hazards.Hazards[hazard].Eruption!;
            if (FindVent(sim, rule, out int ventX, out int ventY))
            {
                TracePath(sim, rule, ventX, ventY, _predicted);
            }
            else
            {
                _predicted.Clear();
            }
        }

        return _predicted;
    }

    /// <summary>Kudy teče láva teď a kam už dotekla (render); prázdné mimo erupci.</summary>
    public (IReadOnlyList<long> Path, int Front) ActiveLava
    {
        get
        {
            int hazard = _content.Hazards.EruptionIndex;
            if (hazard < 0 || _states[hazard].ActiveStorm < 0)
            {
                return (Array.Empty<long>(), -1);
            }

            return (_paths[hazard], (int)_states[hazard].Radius);
        }
    }

    /// <summary>Dráha poslední ztuhlé lávy a kdy ztuhla (render: chladnoucí kůra).</summary>
    public (IReadOnlyList<long> Path, double CooledAtSeconds) CoolingLava => (_cooling, _cooledAt);

    // ----- příliv -----

    /// <summary>
    /// Jednou za sekundu: budovy bez kůlů, pod kterými je teď voda, vypadnou
    /// do příští kontroly. S odlivem už je nikdo neobnoví a samy se vrátí.
    /// </summary>
    private void TickTide(Simulation sim, int hazard, TideRule rule, double now)
    {
        double level = rule.LevelAt(now);
        var buildings = sim.BuildingsMutable;
        CollectShelters(buildings, hazard, _tideShelters);
        int floodTicks = (int)Simulation.TicksPerSecond + 2; // přesah přes příští kontrolu
        for (int i = 0; i < buildings.Length; i++)
        {
            ref var building = ref buildings[i];
            if (!building.IsComplete)
            {
                continue;
            }

            var def = _content.Buildings[building.DefIndex];
            if (def.Stilted || (building.DisabledTicks > 0 && building.DisabledCause != DisableCause.Flood))
            {
                continue; // kůly příliv podteče; zasypanou či poškozenou nechá, čím je
            }

            if (!IsFootprintFlooded(sim, rule, building.X, building.Y, def, level))
            {
                continue;
            }

            building.DisabledTicks = Math.Max(building.DisabledTicks, floodTicks);
            building.DisabledCause = DisableCause.Flood;
            sim.MarkDisabled();
        }
    }

    /// <summary>Stojí některá dlaždice budovy pod přílivem?</summary>
    private bool IsFootprintFlooded(Simulation sim, TideRule rule, int x, int y, BuildingDef def, double level)
    {
        for (int dy = 0; dy < def.FootprintHeight; dy++)
        {
            for (int dx = 0; dx < def.FootprintWidth; dx++)
            {
                if (IsFlooded(sim, rule, x + dx, y + dy, level))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Je dlaždice teď pod přílivem? Jen přílivová mělčina, jen když hladina
    /// přesáhla její výšku a nechrání ji chrám přílivu.
    /// </summary>
    public bool IsFlooded(Simulation sim, TideRule rule, int x, int y, double level) =>
        sim.BiomeAt(x, y) == rule.FloodBiomeIndex
        && sim.TideHeightAt(x, y) < level
        && !IsIn(_tideShelters, x, y);

    /// <summary>Co se právě děje (první jev, který varuje nebo běží).</summary>
    public HazardView View(long tickCount)
    {
        double now = tickCount / Simulation.TicksPerSecond;
        var hazards = _content.Hazards.Hazards;
        for (int h = 0; h < hazards.Count; h++)
        {
            if (hazards[h].Schedule is not { } rule)
            {
                continue;
            }

            var (phase, storm) = PhaseAt(h, rule, now);
            if (phase == HazardPhase.Calm)
            {
                continue;
            }

            double start = StartOf(h, rule, storm);
            var state = _states[h];
            bool known = state.ActiveStorm == storm && hazards[h].Burial is not null; // pás má jen bouře
            return new HazardView(
                h, phase, Math.Max(0, start - now), Math.Clamp((now - start) / rule.DurationSeconds, 0, 1),
                DirectionOf(h, storm), known ? state.CenterX : 0, known ? state.CenterY : 0,
                known ? state.Radius : 0, hazards[h].Burial?.BandTiles ?? 0);
        }

        return HazardView.None;
    }

    /// <summary>
    /// Kolik slunce projde (sluneční zrcadla): v bouři méně. 1 = jasno.
    /// </summary>
    public double SolarDim(long tickCount)
    {
        double now = tickCount / Simulation.TicksPerSecond;
        double dim = 1.0;
        var hazards = _content.Hazards.Hazards;
        for (int h = 0; h < hazards.Count; h++)
        {
            if (hazards[h].Schedule is { } rule && PhaseAt(h, rule, now).Phase == HazardPhase.Active)
            {
                dim = Math.Min(dim, rule.SolarDim);
            }
        }

        return dim;
    }

    /// <summary>
    /// Jde teď přes město bouřkový pás (<see cref="BurialLook.Storm"/>)? Podle
    /// toho dodávají hromosvody.
    /// </summary>
    public bool StormActive(long tickCount)
    {
        double now = tickCount / Simulation.TicksPerSecond;
        var hazards = _content.Hazards.Hazards;
        for (int h = 0; h < hazards.Count; h++)
        {
            if (hazards[h].Burial is { Look: BurialLook.Storm } rule && PhaseAt(h, rule, now).Phase == HazardPhase.Active)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Počasí, které má hráč vidět: během varování a bouře počasí jevu
    /// (obloha zhnědne dřív, než bouře dorazí). −1 = žádné.
    /// </summary>
    public int ForcedWeather(long tickCount)
    {
        double now = tickCount / Simulation.TicksPerSecond;
        var hazards = _content.Hazards.Hazards;
        for (int h = 0; h < hazards.Count; h++)
        {
            if (hazards[h].Schedule is { WeatherIndex: >= 0 } rule && PhaseAt(h, rule, now).Phase != HazardPhase.Calm)
            {
                return rule.WeatherIndex;
            }
        }

        return -1;
    }

    // ----- zasypávání -----

    private void TickBurial(Simulation sim, int hazard, BurialRule rule, double now)
    {
        var state = _states[hazard];
        var (phase, storm) = PhaseAt(hazard, rule, now);

        // Bouře, která běžela, skončila: sečíst a ohlásit.
        if (state.ActiveStorm >= 0 && (phase != HazardPhase.Active || storm != state.ActiveStorm))
        {
            Finish(sim, hazard, state);
        }

        if (phase == HazardPhase.Warning && state.WarnedStorm != storm)
        {
            state.WarnedStorm = storm;
            var def = _content.Hazards.Hazards[hazard];
            sim.EnqueueNotification(new GameNotification(
                NotificationKind.Hazard, def.WarningKey, $"hazard.from.{DirectionOf(hazard, storm)}"));
        }

        if (phase != HazardPhase.Active)
        {
            return;
        }

        if (state.ActiveStorm != storm)
        {
            Begin(sim, state, storm, rule);
        }

        double progress = (now - StartOf(hazard, rule, storm)) / rule.SweepSeconds;
        Sweep(sim, hazard, rule, state, storm, progress);
    }

    /// <summary>Bouře začíná: zapamatuje si město, přes které půjde.</summary>
    private static void Begin(Simulation sim, BurialState state, int storm, BurialRule rule)
    {
        state.ActiveStorm = storm;
        state.WarnedStorm = storm;
        state.Buried = 0;
        state.CenterX = sim.CityCenterX;
        state.CenterY = sim.CityCenterY;
        state.CityStood = sim.Buildings.Length >= rule.MinBuildings;

        double reach = 0;
        var buildings = sim.Buildings;
        for (int i = 0; i < buildings.Length; i++)
        {
            double dx = buildings[i].X - state.CenterX;
            double dy = buildings[i].Y - state.CenterY;
            reach = Math.Max(reach, Math.Sqrt(dx * dx + dy * dy));
        }

        state.Radius = reach + rule.BandTiles;
    }

    /// <summary>
    /// Pás je teď v dané vzdálenosti od středu: co v něm stojí a nic ho
    /// nechrání, zasype.
    /// </summary>
    private void Sweep(Simulation sim, int hazard, BurialRule rule, BurialState state, int storm, double progress)
    {
        var (moveX, moveY) = MovementOf(DirectionOf(hazard, storm));
        double band = -state.Radius + 2 * state.Radius * Math.Clamp(progress, 0, 1);
        double half = rule.BandTiles / 2.0;

        var buildings = sim.BuildingsMutable;
        CollectShelters(buildings, hazard, _shelters);
        // Odolnost ze Vzestupu (pevné větrolamy) zkrátí, jak dlouho budova leží.
        int buryTicks = (int)Math.Ceiling(rule.BurySeconds * Simulation.TicksPerSecond / sim.Bonuses.HazardResistance);
        for (int i = 0; i < buildings.Length; i++)
        {
            ref var building = ref buildings[i];
            if (!building.IsComplete || building.DisabledCause == DisableCause.Burial && building.DisabledTicks > 0)
            {
                continue; // staveniště nic nedrží a zasypané se znovu nezasype
            }

            var def = _content.Buildings[building.DefIndex];
            double centerX = building.X + def.FootprintWidth / 2.0;
            double centerY = building.Y + def.FootprintHeight / 2.0;
            double along = (centerX - state.CenterX) * moveX + (centerY - state.CenterY) * moveY;
            if (Math.Abs(along - band) > half || IsIn(_shelters, building.X, building.Y)
                || (rule.CoastTiles > 0 && !IsNearWater(sim, building.X, building.Y, def, rule.CoastTiles)))
            {
                continue; // mimo pás, v závětří — nebo příboj a budova je daleko od vody
            }

            building.DisabledTicks = Math.Max(building.DisabledTicks, buryTicks);
            building.DisabledCause = DisableCause.Burial;
            state.Buried++;
            sim.MarkDisabled();
        }
    }

    private void CollectShelters(ReadOnlySpan<BuildingInstance> buildings, int hazard, List<(int X, int Y, int RadiusSquared)> shelters)
    {
        shelters.Clear();
        for (int i = 0; i < buildings.Length; i++)
        {
            if (!buildings[i].IsComplete)
            {
                continue;
            }

            int radius = _content.Buildings[buildings[i].DefIndex].ShelterRadius(hazard);
            if (radius > 0)
            {
                shelters.Add((buildings[i].X, buildings[i].Y, radius * radius));
            }
        }
    }

    private static bool IsIn(List<(int X, int Y, int RadiusSquared)> shelters, int x, int y)
    {
        foreach (var (shelterX, shelterY, radiusSquared) in shelters)
        {
            int dx = x - shelterX;
            int dy = y - shelterY;
            if (dx * dx + dy * dy <= radiusSquared)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Má budova do <paramref name="reach"/> dlaždic vodu (příboj bije jen pobřeží)?</summary>
    private static bool IsNearWater(Simulation sim, int x, int y, BuildingDef def, int reach)
    {
        for (int ty = y - reach; ty < y + def.FootprintHeight + reach; ty++)
        {
            for (int tx = x - reach; tx < x + def.FootprintWidth + reach; tx++)
            {
                if (sim.IsWaterAt(tx, ty))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>Bouře přešla: statistika a zpráva hráči.</summary>
    private void Finish(Simulation sim, int hazard, BurialState state)
    {
        var def = _content.Hazards.Hazards[hazard];
        if (state.CityStood)
        {
            Weathered++;
            if (state.Buried == 0)
            {
                Calm++;
            }
        }

        sim.EnqueueNotification(state.Buried == 0
            ? new GameNotification(NotificationKind.Hazard, def.PassedKey, "hazard.calm")
            : new GameNotification(NotificationKind.Hazard, def.PassedKey, "hazard.buried", state.Buried));
        state.ActiveStorm = -1;
        state.Buried = 0;
    }

    // ----- rozvrh (čistá funkce seedu a času) -----

    /// <summary>
    /// Ve které fázi je jev v čase <paramref name="now"/> a o kterou bouři jde.
    /// Posun začátku je menší než rozestup, takže stačí zkusit sousední bouře.
    /// </summary>
    private (HazardPhase Phase, int Storm) PhaseAt(int hazard, IHazardSchedule rule, double now)
    {
        if (now < rule.FirstAfterSeconds - rule.WarningSeconds)
        {
            return (HazardPhase.Calm, -1);
        }

        int slot = (int)Math.Floor((now - rule.FirstAfterSeconds) / rule.IntervalSeconds);
        for (int storm = Math.Max(0, slot - 1); storm <= slot + 1; storm++)
        {
            double start = StartOf(hazard, rule, storm);
            if (now >= start && now < start + rule.DurationSeconds)
            {
                return (HazardPhase.Active, storm);
            }

            if (now >= start - rule.WarningSeconds && now < start)
            {
                return (HazardPhase.Warning, storm);
            }
        }

        return (HazardPhase.Calm, -1);
    }

    /// <summary>Kdy bouře číslo <paramref name="storm"/> začíná (herní sekundy).</summary>
    private double StartOf(int hazard, IHazardSchedule rule, int storm) =>
        rule.FirstAfterSeconds + storm * rule.IntervalSeconds
        + Unit(hazard, storm, 0x51A7) * rule.IntervalJitter * rule.IntervalSeconds;

    /// <summary>Odkud bouře přichází: jeden z osmi směrů, ze seedu.</summary>
    private int DirectionOf(int hazard, int storm) => (int)(Unit(hazard, storm, 0xD1E7) * 8) & 7;

    /// <summary>Jednotkový vektor pohybu pásu: přichází od <paramref name="direction"/>, jde opačně.</summary>
    public static (double X, double Y) MovementOf(int direction)
    {
        double angle = direction * Math.PI / 4; // 0 = sever (−y), po směru hodin
        return (-Math.Sin(angle), Math.Cos(angle));
    }

    /// <summary>Deterministické číslo 0–1 ze seedu, jevu a bouře (SplitMix64).</summary>
    private double Unit(int hazard, int storm, ulong salt)
    {
        ulong z = unchecked((ulong)_seed ^ ((ulong)(hazard + 1) * 0x9E3779B97F4A7C15UL) ^ ((ulong)storm * 0xBF58476D1CE4E5B9UL) ^ salt);
        z = unchecked((z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL);
        z = unchecked((z ^ (z >> 27)) * 0x94D049BB133111EBUL);
        z ^= z >> 31;
        return (z >> 11) * (1.0 / (1UL << 53));
    }

    /// <summary>
    /// Stav jedné zasypávající bouře. Ukládá se jen rozběhnutá bouře —
    /// zbytek je funkce času.
    /// </summary>
    internal sealed class BurialState
    {
        /// <summary>Která bouře právě běží (−1 = žádná).</summary>
        public int ActiveStorm = -1;

        /// <summary>Před kterou bouří už hra varovala.</summary>
        public int WarnedStorm = -1;

        /// <summary>Kolik budov tahle bouře zasypala.</summary>
        public int Buried;

        /// <summary>Stálo město, když bouře začala? (Jinak se do statistiky nepočítá.)</summary>
        public bool CityStood;

        /// <summary>Střed pásu (bouře), nebo průduch, ze kterého teče láva (erupce).</summary>
        public int CenterX;

        /// <summary>Viz <see cref="CenterX"/>.</summary>
        public int CenterY;

        /// <summary>
        /// Poloměr, který pás přejde (bouře), nebo kam po dráze láva dotekla
        /// (erupce; −1 = v dosahu města žádný průduch).
        /// </summary>
        public double Radius;
    }
}
