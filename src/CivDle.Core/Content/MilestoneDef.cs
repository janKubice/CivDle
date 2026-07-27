using CivDle.Core.Sim;

namespace CivDle.Core.Content;

/// <summary>
/// Milník z <c>data/milestones.json</c>: okamžik, který si zaslouží oslavu —
/// první střecha, stovka obyvatel, tisící budova, Vzestup.
///
/// <para>Proti achievementům je rozdíl v účelu, ne v mechanice: achievement je
/// sbírka pro hráče, který je sbírá, milník je <b>zpětná vazba za postup</b>
/// a hlásí se hlasitěji. Proto jsou to samostatná data — jinak by se buď
/// slavilo úplně všechno, nebo nic.</para>
///
/// <para>Text v jazycích pod <c>milestone.&lt;Id&gt;</c>.</para>
/// </summary>
/// <param name="Id">Stabilní ID (do savu i lokalizace).</param>
/// <param name="Condition">Kdy se milník spustí.</param>
public sealed record MilestoneDef(string Id, GoalCondition Condition)
{
    /// <summary>Lokalizační klíč názvu milníku.</summary>
    public string NameKey => $"milestone.{Id}";
}
