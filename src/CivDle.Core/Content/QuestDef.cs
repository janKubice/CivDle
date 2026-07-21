using CivDle.Core.Sim;

namespace CivDle.Core.Content;

/// <summary>
/// Jeden pevný úkol z <c>data/quests.json</c>: splní se, když metrika dosáhne
/// prahu (<see cref="Condition"/>), a udělí odměnu v surovinách. Jednorázový.
/// Jméno a popis v jazycích pod <c>quest.&lt;Id&gt;</c> / <c>.desc</c>.
/// </summary>
/// <param name="Id">Stabilní ID (do savu i lokalizace).</param>
/// <param name="Condition">Podmínka splnění (metrika ≥ práh).</param>
/// <param name="Reward">Odměna v surovinách (smí být prázdná).</param>
public sealed record QuestDef(
    string Id,
    GoalCondition Condition,
    IReadOnlyList<ResourceAmount> Reward)
{
    /// <summary>Lokalizační klíč jména úkolu.</summary>
    public string NameKey => $"quest.{Id}";

    /// <summary>Lokalizační klíč popisu úkolu.</summary>
    public string DescriptionKey => $"quest.{Id}.desc";
}

/// <summary>
/// Nastavení dynamických (nekonečně se opakujících) úkolů z <c>data/quests.json</c>.
/// Po splnění se práh i odměna vynásobí růstem — vždy je co plnit, škáluje to
/// s hrou (viz „hodně úkolů, i dynamické"). Jméno se skládá z <c>quest.dynamic</c>.
/// </summary>
/// <param name="BaseCondition">Výchozí podmínka (tier 0).</param>
/// <param name="TargetGrowth">Násobič prahu za každý splněný tier (&gt; 1).</param>
/// <param name="BaseReward">Výchozí odměna (tier 0).</param>
/// <param name="RewardGrowth">Násobič odměny za každý splněný tier (≥ 1).</param>
public sealed record DynamicQuestConfig(
    GoalCondition BaseCondition,
    double TargetGrowth,
    IReadOnlyList<ResourceAmount> BaseReward,
    double RewardGrowth);
