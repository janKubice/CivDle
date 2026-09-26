using CivDle.Core.Sim;

namespace CivDle.Core.Content;

/// <summary>Na co dočasný efekt volby působí. Behavior-ID: data řeknou co, kód jak.</summary>
public enum EventEffectKind
{
    /// <summary>Výroba jedné suroviny (nebo všech) — povodeň podemele pole, stávka zastaví linku.</summary>
    Production,

    /// <summary>Růst populace — karavana, která se tu usadí, nebo smutek po ztraceném dítěti.</summary>
    Growth,
}

/// <summary>
/// Dočasný efekt volby: násobič na čas.
///
/// <para><b>Proč to existuje:</b> všech třicet událostí byla tatáž volba „zaplať X,
/// dostaneš Y — nebo nic". U povodně nebo krys v sýpce přitom „nic nedělat"
/// nemělo žádný následek: text sliboval problém, který nepřišel. A u darů
/// („Karavana: přijmout / poslat pryč") bylo odmítnutí prostě horší volba.
/// Efekt dává druhé volbě váhu — ignorovat povodeň stojí úrodu, poslat karavanu
/// pryč znamená, že se pár lidí usadí.</para>
/// </summary>
/// <param name="Kind">Na co působí.</param>
/// <param name="ResourceIndex">Surovina (jen u výroby); −1 = všechny.</param>
/// <param name="Multiplier">Násobič (0,75 = −25 %).</param>
/// <param name="Seconds">Jak dlouho platí (herní sekundy).</param>
public sealed record EventEffectDef(EventEffectKind Kind, int ResourceIndex, double Multiplier, double Seconds);

/// <summary>
/// Jedna volba v události: popisek + volitelná cena (co zaplatíš), zisk (co dostaneš)
/// a dočasný efekt. Např. „Přijmout: +40 jídla" nebo „Obchod: −20 dřeva, +30 kamene".
/// Data = co, kód = jak. Bez ceny = volba zdarma (třeba „Odmítnout").
/// </summary>
/// <param name="LabelKey">Lokalizační klíč popisku volby.</param>
/// <param name="Cost">Co volba stojí (může být prázdné).</param>
/// <param name="Gain">Co volba dá (může být prázdné).</param>
/// <param name="Effect">Dočasný efekt; <c>null</c> = žádný.</param>
public sealed record EventChoiceDef(
    string LabelKey,
    IReadOnlyList<ResourceAmount> Cost,
    IReadOnlyList<ResourceAmount> Gain,
    EventEffectDef? Effect = null);

/// <summary>
/// Náhodná událost s volbami (mikro-rozhodnutí) z <c>data/events.json</c>: kupec,
/// učenec, slavnost… Občas vyskočí a hráč vybere jednu z možností. Přidává agenci
/// a variabilitu, ladí s relaxačním tónem (žádný trest, jen nabídky). Jméno a popis
/// v jazycích pod <c>event.&lt;Id&gt;</c> / <c>.desc</c>.
///
/// <para>Tón zůstává relaxační: nic se nezničí a nikdo neumře. Dočasný efekt
/// (<see cref="EventChoiceDef.Effect"/>) je nejhorší, co se může stát — a vždy
/// má konec.</para>
///
/// <para>Volitelná <see cref="Requirement"/> drží události v jejich době: kupec
/// s ocelí nemá co nabízet osadě, která ještě neumí bronz. Bez podmínky je
/// událost dostupná od začátku.</para>
/// </summary>
/// <param name="Id">Stabilní ID.</param>
/// <param name="Choices">Nabízené volby (1–4).</param>
/// <param name="Requirement">Kdy se událost smí objevit; <c>null</c> = vždy.</param>
public sealed record EventDef(
    string Id,
    IReadOnlyList<EventChoiceDef> Choices,
    GoalCondition? Requirement = null)
{
    /// <summary>Lokalizační klíč jména události.</summary>
    public string NameKey => $"event.{Id}";

    /// <summary>Lokalizační klíč popisu události.</summary>
    public string DescriptionKey => $"event.{Id}.desc";
}
