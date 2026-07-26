using CivDle.Core.Sim;

namespace CivDle.Core.Content;

/// <summary>
/// Odemykatelná herní funkce z <c>data/features.json</c> — sázení, bourání, zóny,
/// guvernér, slavnost… Dokud není odemčená, UI ji vůbec NEUKAZUJE.
///
/// <para>Proč: nasypat hráči na začátku patnáct tlačítek znamená, že netuší, co
/// dělají a čemu se věnovat. Funkce se proto odemykají postupně (living-city.md §4
/// — „automatizace se odemyká, není výchozí") a hra tím zároveň učí sama sebe.</para>
///
/// <para>Podmínka je sdílená <see cref="GoalCondition"/> — stejný mechanismus jako
/// úkoly, achievementy a Vzestup, takže odemčení jde ladit v datech bez zásahu do kódu.</para>
/// </summary>
/// <param name="Id">Stabilní ID; kód se na funkci odkazuje přes něj.</param>
/// <param name="Unlock">Podmínka odemčení (splněná = funkce je dostupná).</param>
public sealed record FeatureDef(string Id, GoalCondition Unlock)
{
    /// <summary>Lokalizační klíč jména funkce (pro hlášku „odemčeno").</summary>
    public string NameKey => $"feature.{Id}";
}
