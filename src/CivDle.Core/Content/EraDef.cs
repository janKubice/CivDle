namespace CivDle.Core.Content;

/// <summary>
/// Éra civilizace z <c>data/eras.json</c> (T0 → T6): organizuje progresi obsahu.
/// Éra je „dosažená", když je vyzkoumaná její otevírací technologie
/// (<see cref="UnlockTechId"/>); základní éry ji nemají a jsou dostupné od startu.
/// Aktuální éru simulace odvozuje z vyzkoumaných technologií. Jméno v jazycích
/// pod <c>era.&lt;Id&gt;</c>. Odkaz na tech je STRING (řeší se až za běhu), aby šlo
/// éry definovat dopředu, než jejich technologie vzniknou.
/// </summary>
/// <param name="Id">Stabilní ID.</param>
/// <param name="Order">Pořadí éry (0 = nejstarší); určuje, která je „vyšší".</param>
/// <param name="UnlockTechId">ID technologie, která éru otevírá; prázdné = od startu.</param>
public sealed record EraDef(string Id, int Order, string UnlockTechId)
{
    /// <summary>Lokalizační klíč jména éry.</summary>
    public string NameKey => $"era.{Id}";
}
