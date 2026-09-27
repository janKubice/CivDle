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
    int MinBuildings);

/// <summary>
/// Přírodní jev světa. Jména a hlášky jsou v jazycích pod <c>hazard.&lt;id&gt;…</c>.
/// </summary>
/// <param name="Id">ID jevu (<c>sandstorm</c>).</param>
/// <param name="Behavior">Chování (viz <see cref="HazardBehavior"/>).</param>
/// <param name="Burial">Parametry zasypávání; <c>null</c> u jiných chování.</param>
public sealed record HazardDef(string Id, HazardBehavior Behavior, BurialRule? Burial)
{
    /// <summary>Jméno jevu.</summary>
    public string NameKey => $"hazard.{Id}";

    /// <summary>Hláška „blíží se…" (nadpis toastu; předmětem je směr).</summary>
    public string WarningKey => $"hazard.{Id}.warning";

    /// <summary>Hláška „přešla" (nadpis toastu; předmětem je výsledek).</summary>
    public string PassedKey => $"hazard.{Id}.passed";

    /// <summary>Klíče, které musí mít každý jazyk.</summary>
    public IEnumerable<string> TextKeys => new[] { NameKey, WarningKey, PassedKey };
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
}
