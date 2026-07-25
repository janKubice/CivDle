namespace CivDle.Core.Content;

/// <summary>
/// Stupeň měřítka z <c>data/ascension-tiers.json</c> (progression-prestige.md §3):
/// každý Vzestup zvedne strop o řád a odemkne obsah. Stupeň je „dosažený", když
/// úroveň Vzestupu dosáhne jeho <see cref="Order"/>.
///
/// <para>Prestige tu není trest, ale ZVĚTŠENÍ PLÁTNA: strop populace je měkký cíl,
/// který láká k dalšímu Vzestupu (§6 „soft-lock, ne hard-lock"), a odemčené budovy
/// (megastruktury) jsou odměna za dosažené měřítko.</para>
/// Jméno v jazycích pod <c>tier.&lt;Id&gt;</c>.
/// </summary>
/// <param name="Id">Stabilní ID.</param>
/// <param name="Order">Úroveň Vzestupu, od které stupeň platí (0 = první běh).</param>
/// <param name="PopulationCap">Strop populace tohoto měřítka.</param>
/// <param name="UnlockedBuildingIndices">Budovy, které stupeň odemyká (megastruktury).</param>
public sealed record AscensionTierDef(
    string Id,
    int Order,
    double PopulationCap,
    IReadOnlyList<int> UnlockedBuildingIndices)
{
    /// <summary>Lokalizační klíč jména stupně měřítka.</summary>
    public string NameKey => $"tier.{Id}";
}
