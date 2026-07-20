namespace CivDle.Core.Content;

/// <summary>
/// Definice technologie z <c>data/tech.json</c> (tech tree). Výzkum stojí suroviny
/// a vyžaduje splněné prerekvizity; po odemčení zpřístupní nové budovy. Jméno
/// a popis v jazycích pod <c>tech.&lt;Id&gt;</c> / <c>tech.&lt;Id&gt;.desc</c>.
/// Indexy (prereky, odemčené budovy) se překládají z ID při načtení.
/// </summary>
/// <param name="Id">Stabilní ID.</param>
/// <param name="Cost">Cena výzkumu v surovinách.</param>
/// <param name="PrerequisiteIndices">Indexy technologií, které musí předcházet.</param>
/// <param name="UnlockedBuildingIndices">Indexy budov, které tech zpřístupní.</param>
public sealed record TechDef(
    string Id,
    IReadOnlyList<ResourceAmount> Cost,
    IReadOnlyList<int> PrerequisiteIndices,
    IReadOnlyList<int> UnlockedBuildingIndices)
{
    /// <summary>Lokalizační klíč jména technologie.</summary>
    public string NameKey => $"tech.{Id}";

    /// <summary>Lokalizační klíč popisu technologie.</summary>
    public string DescriptionKey => $"tech.{Id}.desc";
}
