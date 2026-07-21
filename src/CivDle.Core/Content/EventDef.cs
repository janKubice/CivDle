namespace CivDle.Core.Content;

/// <summary>
/// Jedna volba v události: popisek + volitelná cena (co zaplatíš) a zisk (co dostaneš).
/// Např. „Přijmout: +40 jídla" nebo „Obchod: −20 dřeva, +30 kamene". Data = co,
/// kód = jak (odečtení/přičtení). Bez ceny = volba zdarma (třeba „Odmítnout").
/// </summary>
/// <param name="LabelKey">Lokalizační klíč popisku volby.</param>
/// <param name="Cost">Co volba stojí (může být prázdné).</param>
/// <param name="Gain">Co volba dá (může být prázdné).</param>
public sealed record EventChoiceDef(
    string LabelKey,
    IReadOnlyList<ResourceAmount> Cost,
    IReadOnlyList<ResourceAmount> Gain);

/// <summary>
/// Náhodná událost s volbami (mikro-rozhodnutí) z <c>data/events.json</c>: kupec,
/// učenec, slavnost… Občas vyskočí a hráč vybere jednu z možností. Přidává agenci
/// a variabilitu, ladí s relaxačním tónem (žádný trest, jen nabídky). Jméno a popis
/// v jazycích pod <c>event.&lt;Id&gt;</c> / <c>.desc</c>.
/// </summary>
/// <param name="Id">Stabilní ID.</param>
/// <param name="Choices">Nabízené volby (1–4).</param>
public sealed record EventDef(string Id, IReadOnlyList<EventChoiceDef> Choices)
{
    /// <summary>Lokalizační klíč jména události.</summary>
    public string NameKey => $"event.{Id}";

    /// <summary>Lokalizační klíč popisu události.</summary>
    public string DescriptionKey => $"event.{Id}.desc";
}
