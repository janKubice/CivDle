namespace CivDle.Core.Content;

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
public sealed record GameplayConfig(
    double StartingPopulation,
    int BaseHousingCapacity,
    double PopulationGrowthPerSecond,
    double FoodPerPersonPerSecond,
    int FoodResourceIndex);
