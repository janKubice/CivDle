namespace CivDle.Core.Content;

/// <summary>
/// Nastavení auto-stavby (civilizace roste sama — jádro idle smyčky, fáze 2 roadmapy).
/// Běží na nízké frekvenci, ne každý tik (CLAUDE.md, výkon).
/// </summary>
/// <param name="IntervalTicks">Jak často se auto-stavba pokusí stavět.</param>
/// <param name="SearchRadius">V jaké vzdálenosti (dlaždice) od existující zástavby hledá místo.</param>
/// <param name="PopulationHeadroom">Staví se, když populace ≥ kapacita − headroom (poptávka po bydlení).</param>
public sealed record AutoBuildConfig(int IntervalTicks, int SearchRadius, int PopulationHeadroom);

/// <summary>
/// Globální parametry herní smyčky z <c>data/gameplay.json</c> — čísla balancu
/// patří do dat, ne do kódu. Hodnoty „za sekundu" si systémy přepočítávají
/// na tiky přes <c>Simulation.TicksPerSecond</c>.
/// </summary>
/// <param name="StartingPopulation">Počáteční populace nové hry.</param>
/// <param name="BaseHousingCapacity">Kapacita bydlení bez domů (výchozí tábor).</param>
/// <param name="PopulationGrowthPerSecond">Přírůstek populace za sekundu (když je jídlo a kapacita).</param>
/// <param name="FoodPerPersonPerSecond">Spotřeba jídla na osobu za sekundu (soft pressure).</param>
/// <param name="FoodResourceIndex">Která surovina je „jídlo" (odkaz z dat, ne natvrdo).</param>
/// <param name="AutoBuild">Nastavení automatického růstu zástavby.</param>
public sealed record GameplayConfig(
    double StartingPopulation,
    int BaseHousingCapacity,
    double PopulationGrowthPerSecond,
    double FoodPerPersonPerSecond,
    int FoodResourceIndex,
    AutoBuildConfig AutoBuild);
