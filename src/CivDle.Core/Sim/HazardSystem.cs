using CivDle.Core.Content;

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

    public HazardSystem(GameContent content, long seed)
    {
        _content = content;
        _seed = seed;
        _states = new BurialState[content.Hazards.Count];
        for (int i = 0; i < _states.Length; i++)
        {
            _states[i] = new BurialState();
        }
    }

    /// <summary>Kolik jevů přešlo, aniž by něco zasypaly (a město už stálo).</summary>
    public int Calm { get; private set; }

    /// <summary>Kolik jevů přešlo přes stojící město.</summary>
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
        }
    }

    /// <summary>Co se právě děje (první jev, který varuje nebo běží).</summary>
    public HazardView View(long tickCount)
    {
        double now = tickCount / Simulation.TicksPerSecond;
        var hazards = _content.Hazards.Hazards;
        for (int h = 0; h < hazards.Count; h++)
        {
            if (hazards[h].Burial is not { } rule)
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
            bool known = state.ActiveStorm == storm;
            return new HazardView(
                h, phase, Math.Max(0, start - now), Math.Clamp((now - start) / rule.SweepSeconds, 0, 1),
                DirectionOf(h, storm), known ? state.CenterX : 0, known ? state.CenterY : 0,
                known ? state.Radius : 0, rule.BandTiles);
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
            if (hazards[h].Burial is { } rule && PhaseAt(h, rule, now).Phase == HazardPhase.Active)
            {
                dim = Math.Min(dim, rule.SolarDim);
            }
        }

        return dim;
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
            if (hazards[h].Burial is { WeatherIndex: >= 0 } rule && PhaseAt(h, rule, now).Phase != HazardPhase.Calm)
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
        CollectShelters(buildings, hazard);
        int buryTicks = (int)Math.Ceiling(rule.BurySeconds * Simulation.TicksPerSecond);
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
            if (Math.Abs(along - band) > half || IsSheltered(building.X, building.Y))
            {
                continue;
            }

            building.DisabledTicks = Math.Max(building.DisabledTicks, buryTicks);
            building.DisabledCause = DisableCause.Burial;
            state.Buried++;
            sim.MarkDisabled();
        }
    }

    private void CollectShelters(ReadOnlySpan<BuildingInstance> buildings, int hazard)
    {
        _shelters.Clear();
        for (int i = 0; i < buildings.Length; i++)
        {
            if (!buildings[i].IsComplete)
            {
                continue;
            }

            int radius = _content.Buildings[buildings[i].DefIndex].ShelterRadius(hazard);
            if (radius > 0)
            {
                _shelters.Add((buildings[i].X, buildings[i].Y, radius * radius));
            }
        }
    }

    private bool IsSheltered(int x, int y)
    {
        foreach (var (shelterX, shelterY, radiusSquared) in _shelters)
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
    private (HazardPhase Phase, int Storm) PhaseAt(int hazard, BurialRule rule, double now)
    {
        if (now < rule.FirstAfterSeconds - rule.WarningSeconds)
        {
            return (HazardPhase.Calm, -1);
        }

        int slot = (int)Math.Floor((now - rule.FirstAfterSeconds) / rule.IntervalSeconds);
        for (int storm = Math.Max(0, slot - 1); storm <= slot + 1; storm++)
        {
            double start = StartOf(hazard, rule, storm);
            if (now >= start && now < start + rule.SweepSeconds)
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
    private double StartOf(int hazard, BurialRule rule, int storm) =>
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

        public int CenterX;
        public int CenterY;
        public double Radius;
    }
}
