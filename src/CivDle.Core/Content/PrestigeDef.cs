using CivDle.Core.Sim;

namespace CivDle.Core.Content;

/// <summary>
/// Nastavení Vzestupu (prestige) z <c>data/prestige.json</c>: kdy je Vzestup
/// dostupný a kolik bodů dá. Vzestup zresetuje éru (mapu), ale trvalé upgrady
/// a body zůstávají — progrese přes měřítko (viz progression-prestige.md).
/// </summary>
/// <param name="Requirement">Podmínka PRVNÍHO Vzestupu (metrika ≥ práh).</param>
/// <param name="PointsMetric">Metrika, ze které se počítají body Vzestupu.</param>
/// <param name="PointsParam">Doplňující index metriky bodů (surovina…), nebo −1.</param>
/// <param name="PointsDivisor">Body = hodnota metriky ÷ tenhle dělitel (celočíselně).</param>
/// <param name="RequirementGrowth">
/// Kolikrát je každý další Vzestup náročnější (1.0 = pořád stejně). Bez růstu by
/// druhý Vzestup přišel hned po prvním a celá progrese by ztratila smysl.
/// </param>
public sealed record PrestigeConfig(
    GoalCondition Requirement,
    MetricKind PointsMetric,
    int PointsParam,
    long PointsDivisor,
    double RequirementGrowth = 1.0);

/// <summary>
/// Jeden trvalý upgrade za body Vzestupu. <see cref="Effect"/> je behavior-ID
/// (řetězec mapovaný v kódu na konkrétní bonus — data-driven pravidlo z CLAUDE.md),
/// <see cref="Magnitude"/> jeho síla (0.30 = +30 %). Odemyká se po prerekvizitách.
/// Jméno a popis v jazycích pod <c>prestige.&lt;Id&gt;</c> / <c>.desc</c>.
/// </summary>
/// <param name="Id">Stabilní ID (do savu i lokalizace).</param>
/// <param name="Effect">Behavior-ID efektu (production_mult, harvest_mult, …).</param>
/// <param name="Magnitude">Síla efektu (přičítá se k násobiči, 0.3 = +30 %).</param>
/// <param name="Cost">Cena v bodech Vzestupu.</param>
/// <param name="PrerequisiteIndices">Indexy upgradů, které musí předcházet.</param>
public sealed record PrestigeUpgradeDef(
    string Id,
    string Effect,
    double Magnitude,
    int Cost,
    IReadOnlyList<int> PrerequisiteIndices)
{
    /// <summary>Lokalizační klíč jména upgradu.</summary>
    public string NameKey => $"prestige.{Id}";

    /// <summary>Lokalizační klíč popisu upgradu.</summary>
    public string DescriptionKey => $"prestige.{Id}.desc";
}
