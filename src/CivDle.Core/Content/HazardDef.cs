namespace CivDle.Core.Content;

/// <summary>
/// Chování přírodního jevu (svety-design.md 7.3). Každé je malá třída
/// v simulaci; data říkají jen <b>jak moc</b> a <b>jak často</b>. Mapuje se
/// z řetězce v <c>hazards.json</c> (<c>behavior</c>) — behavior-ID hook z CLAUDE.md.
/// </summary>
public enum HazardBehavior
{
    /// <summary>
    /// <c>weather_burial</c>: pás bouře přejde přes město a nechráněné budovy
    /// zasype (písek na Duně, sníh na Mrazu, příboj v Souostroví).
    /// </summary>
    WeatherBurial,

    /// <summary>
    /// <c>tides</c>: hladina v cyklu stoupá a klesá; přílivová mělčina se
    /// zaplaví podle své výšky a budova bez kůlů na ní vypadne (Souostroví).
    /// </summary>
    Tides,
}

/// <summary>
/// Jak se chová bouře, která zasypává (<see cref="HazardBehavior.WeatherBurial"/>).
/// Rozvrh je čistá funkce času a seedu — stejný save dá stejné bouře.
/// </summary>
/// <param name="FirstAfterSeconds">Kdy přijde první bouře (herní sekundy od založení).</param>
/// <param name="IntervalSeconds">Průměrný rozestup bouří.</param>
/// <param name="IntervalJitter">Jak moc se začátek v rámci rozestupu posouvá (0–0,9).</param>
/// <param name="WarningSeconds">Jak dlouho předem hra varuje (obloha zhnědne).</param>
/// <param name="SweepSeconds">Jak dlouho pás přechází přes město.</param>
/// <param name="BandTiles">Šířka pásu v dlaždicích.</param>
/// <param name="BurySeconds">Na jak dlouho budova vypadne, než ji lidé vyhrabou.</param>
/// <param name="WeatherIndex">Počasí, které během varování a bouře vidí hráč; −1 = žádné.</param>
/// <param name="SolarDim">Kolik slunce za bouře projde (sluneční zrcadla); 1 = nic neubere.</param>
/// <param name="MinBuildings">Od kolika budov se bouře počítá do statistiky (prázdná poušť bouři „nepřečkává").</param>
/// <param name="CoastTiles">
/// Jen pobřeží: bouře zasáhne budovu, jen když má do tolika dlaždic vodu
/// (příboj v Souostroví); 0 = celý pás.
/// </param>
/// <param name="MoundColor">Čím je budova zasypaná (písek, sníh, naplavenina); <c>null</c> = písek.</param>
public sealed record BurialRule(
    double FirstAfterSeconds,
    double IntervalSeconds,
    double IntervalJitter,
    double WarningSeconds,
    double SweepSeconds,
    int BandTiles,
    double BurySeconds,
    int WeatherIndex,
    double SolarDim,
    int MinBuildings,
    int CoastTiles = 0,
    RgbColor? MoundColor = null);

/// <summary>
/// Příliv a odliv (<see cref="HazardBehavior.Tides"/>). Hladina je čistá funkce
/// času: <c>(1 − cos(2π t / perioda)) / 2</c> — hra začíná odlivem. Dlaždice
/// přílivové mělčiny má „výšku" 0–1 z výšky terénu mezi
/// <paramref name="LowOffset"/> a <paramref name="HighOffset"/> (vůči hladině
/// moře) a je pod vodou, když hladina výšku přesáhne. Níž položené mělčiny
/// tak zaplaví dřív a čára přílivu po mapě opravdu putuje.
/// </summary>
/// <param name="PeriodSeconds">Délka celého cyklu příliv–odliv (herní sekundy).</param>
/// <param name="FloodBiomeIndex">Biom, který příliv zaplavuje (přílivová mělčina).</param>
/// <param name="LowOffset">Výška terénu vůči hladině moře, kde mělčina zaplaví první (výška 0).</param>
/// <param name="HighOffset">Výška terénu vůči hladině moře, kam dosáhne jen nejvyšší příliv (výška 1).</param>
public sealed record TideRule(double PeriodSeconds, int FloodBiomeIndex, double LowOffset, double HighOffset)
{
    /// <summary>Hladina v čase <paramref name="seconds"/>: 0 = nejhlubší odliv, 1 = nejvyšší příliv.</summary>
    public double LevelAt(double seconds) => (1 - Math.Cos(2 * Math.PI * seconds / PeriodSeconds)) / 2;

    /// <summary>Stoupá hladina (první půlka cyklu)?</summary>
    public bool IsRising(double seconds) => seconds % PeriodSeconds < PeriodSeconds / 2;

    /// <summary>Výška dlaždice 0–1 z výšky terénu a hladiny moře světa.</summary>
    public double HeightOf(double elevation, double seaLevel) =>
        Math.Clamp((elevation - seaLevel - LowOffset) / (HighOffset - LowOffset), 0, 1);
}

/// <summary>
/// Přírodní jev světa. Jména a hlášky jsou v jazycích pod <c>hazard.&lt;id&gt;…</c>.
/// </summary>
/// <param name="Id">ID jevu (<c>sandstorm</c>).</param>
/// <param name="Behavior">Chování (viz <see cref="HazardBehavior"/>).</param>
/// <param name="Burial">Parametry zasypávání; <c>null</c> u jiných chování.</param>
/// <param name="Tide">Parametry přílivu; <c>null</c> u jiných chování.</param>
public sealed record HazardDef(string Id, HazardBehavior Behavior, BurialRule? Burial, TideRule? Tide = null)
{
    /// <summary>Jméno jevu.</summary>
    public string NameKey => $"hazard.{Id}";

    /// <summary>Hláška „blíží se…" (nadpis toastu; předmětem je směr).</summary>
    public string WarningKey => $"hazard.{Id}.warning";

    /// <summary>Hláška „přešla" (nadpis toastu; předmětem je výsledek).</summary>
    public string PassedKey => $"hazard.{Id}.passed";

    /// <summary>Stav přílivu v HUD: „příliv stoupá".</summary>
    public string RisingKey => $"hazard.{Id}.rising";

    /// <summary>Stav přílivu v HUD: „odliv".</summary>
    public string EbbingKey => $"hazard.{Id}.ebbing";

    /// <summary>Klíče, které musí mít každý jazyk (podle chování).</summary>
    public IEnumerable<string> TextKeys => Behavior == HazardBehavior.Tides
        ? new[] { NameKey, RisingKey, EbbingKey }
        : new[] { NameKey, WarningKey, PassedKey };
}

/// <summary>Ochrana před jevem: budova chrání okruh (větrolam před pískem).</summary>
/// <param name="HazardIndex">Index jevu v <see cref="HazardCatalog"/>.</param>
/// <param name="Radius">Poloměr ochrany v dlaždicích.</param>
public readonly record struct Shelter(int HazardIndex, int Radius);

/// <summary>Přírodní jevy světa (z <c>hazards.json</c> ve složce světa).</summary>
public sealed class HazardCatalog
{
    private readonly List<HazardDef> _hazards;

    public HazardCatalog(IReadOnlyList<HazardDef> hazards) => _hazards = hazards.ToList();

    /// <summary>Svět bez přírodních jevů (Domovina).</summary>
    public static HazardCatalog Empty { get; } = new(Array.Empty<HazardDef>());

    /// <summary>Jevy v pořadí souboru.</summary>
    public IReadOnlyList<HazardDef> Hazards => _hazards;

    /// <summary>Počet jevů.</summary>
    public int Count => _hazards.Count;

    /// <summary>Index jevu podle ID; −1 = neexistuje.</summary>
    public int IndexOf(string id) => _hazards.FindIndex(h => h.Id == id);

    /// <summary>Index přílivu světa; −1 = svět příliv nemá.</summary>
    public int TideIndex => _hazards.FindIndex(h => h.Tide is not null);
}
