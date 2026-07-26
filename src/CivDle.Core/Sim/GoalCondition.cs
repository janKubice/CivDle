namespace CivDle.Core.Sim;

/// <summary>
/// Druh sledované metriky pro cíle a achievementy. Data (JSON) říkají JEN „co"
/// (metrika + práh), kód říká „jak" (jak metriku přečíst a porovnat) — žádná
/// logika ani vzorce v datech (viz CLAUDE.md, data-driven pravidlo).
/// </summary>
public enum MetricKind
{
    /// <summary>Aktuální počet obyvatel.</summary>
    Population,

    /// <summary>Kapacita bydlení (základna + domy).</summary>
    HousingCapacity,

    /// <summary>Kumulativně nasbíráno suroviny klikáním (<see cref="GoalCondition.Param"/> = index suroviny).</summary>
    Harvested,

    /// <summary>Aktuální zásoba suroviny (<see cref="GoalCondition.Param"/> = index suroviny).</summary>
    ResourceStock,

    /// <summary>Celkový počet postavených budov.</summary>
    TotalBuildings,

    /// <summary>Počet budov daného typu (<see cref="GoalCondition.Param"/> = index definice budovy).</summary>
    BuildingOfType,

    /// <summary>Je technologie vyzkoumaná? (<see cref="GoalCondition.Param"/> = index technologie; práh 1).</summary>
    ResearchedTech,

    /// <summary>Počet Vzestupů (prestige).</summary>
    AscensionLevel,

    /// <summary>Pořadové číslo herního dne.</summary>
    DayNumber,

    /// <summary>Počet zasazených (obnovitelných) uzlů na mapě — sázení jako sledovaná činnost.</summary>
    PlantedNodes,
}

/// <summary>
/// Podmínka cíle/achievementu: metrika ≥ práh. Odkazy (surovina/budova/tech) jsou
/// už přeložené na indexy při načtení dat (definice vs. instance). Neměnná.
/// </summary>
/// <param name="Kind">Kterou metriku sledovat.</param>
/// <param name="Param">Doplňující index (surovina/budova/tech), nebo −1.</param>
/// <param name="Target">Práh, při kterém je podmínka splněná (metrika ≥ práh).</param>
public readonly record struct GoalCondition(MetricKind Kind, int Param, long Target);
