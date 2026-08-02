namespace CivDle.Core.Content;

/// <summary>
/// Definice technologie z <c>data/tech.json</c> (tech tree). Výzkum stojí suroviny
/// a vyžaduje splněné prerekvizity; po odemčení zpřístupní nové budovy a/nebo dá
/// trvalý pasivní bonus. Jméno a popis v jazycích pod <c>tech.&lt;Id&gt;</c> /
/// <c>tech.&lt;Id&gt;.desc</c>. Indexy (prereky, odemčené budovy) se překládají z ID
/// při načtení.
///
/// <para><see cref="Effect"/> je behavior-ID (data = co, kód = jak) sdílené
/// s upgrady Vzestupu — např. <c>production_mult</c>. Prázdný efekt = technologie
/// jen odemyká budovy. Neznámý efekt se tiše ignoruje (data smí předběhnout kód).</para>
/// </summary>
/// <param name="Id">Stabilní ID.</param>
/// <param name="Cost">Cena výzkumu v surovinách.</param>
/// <param name="PrerequisiteIndices">Indexy technologií, které musí předcházet.</param>
/// <param name="UnlockedBuildingIndices">Indexy budov, které tech zpřístupní.</param>
/// <param name="Effect">Behavior-ID trvalého pasivního bonusu (prázdné = žádný).</param>
/// <param name="Magnitude">Síla pasivního bonusu.</param>
/// <param name="TargetResourceIndex">
/// Surovina, na kterou efekt míří; −1 = na všechny.
///
/// <para>Díky tomu může strom obsahovat i drobnosti typu „+5 % těžby dřeva"
/// vedle globálních skoků. Bez cíle by každé druhé vylepšení muselo být
/// celoplošné, a strom by byl jen dlouhý seznam téhož.</para>
/// </param>
public sealed record TechDef(
    string Id,
    IReadOnlyList<ResourceAmount> Cost,
    IReadOnlyList<int> PrerequisiteIndices,
    IReadOnlyList<int> UnlockedBuildingIndices,
    string Effect = "",
    double Magnitude = 0.0,
    int TargetResourceIndex = -1)
{
    /// <summary>Dává technologie trvalý pasivní bonus (ne jen odemčení budov)?</summary>
    public bool HasPassiveEffect => !string.IsNullOrEmpty(Effect);

    /// <summary>Lokalizační klíč jména technologie.</summary>
    public string NameKey => $"tech.{Id}";

    /// <summary>Lokalizační klíč popisu technologie.</summary>
    public string DescriptionKey => $"tech.{Id}.desc";
}
