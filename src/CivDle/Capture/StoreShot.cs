namespace CivDle.Capture;

/// <summary>Co má snímek ukázat — určuje, kam se postaví kamera a co se předtím zapne.</summary>
public enum ShotSubject
{
    /// <summary>Rozrostlé město za bílého dne.</summary>
    City,

    /// <summary>Totéž město v noci (rozsvícená okna).</summary>
    Night,

    /// <summary>Zimní město — modravý nádech scény a zima v HUD.</summary>
    Winter,

    /// <summary>Seznam achievementů — kolik toho hra nabízí.</summary>
    Achievements,

    /// <summary>Strom technologií.</summary>
    Tech,

    /// <summary>
    /// Oběžná dráha s vypuštěnými družicemi. Koncová meta hry, kterou v běžném
    /// pohledu není vidět vůbec — družice nemá dlaždici, takže bez vlastního
    /// záběru by o ní zákazník z obrázků nevěděl.
    /// </summary>
    Orbit,

    /// <summary>Odzoomovaný pohled na aglomeraci.</summary>
    Scale,

    /// <summary>
    /// Město na pobřeží za podvečerního světla. Pěna u břehu, hloubka vody
    /// a odlesky na hladině jsou to nejhezčí, co hra kreslí — a uprostřed
    /// města nejsou vidět vůbec.
    /// </summary>
    Coast,

    /// <summary>
    /// Podvečer: modrofialové světlo, dlouhé stíny, první rozsvícená okna.
    /// Nejsilnější obrázek, co hra má, a na poledním záběru z něj není nic.
    /// </summary>
    GoldenHour,

    /// <summary>
    /// Aglomerace z výšky v noci — z hustoty zástavby se stane světelná mapa.
    /// </summary>
    NightScale,

    /// <summary>
    /// Rozestavěný div světa. Megastruktura je největší věc, kterou hráč
    /// postaví, a na běžném záběru na město z ní není nic — staveniště přitom
    /// samo vypráví, že se tu dělá něco velkého.
    /// </summary>
    Wonder,

    /// <summary>
    /// Rozbřesk: studené světlo, mlha v údolích, okna ještě svítí. Protipól
    /// <see cref="GoldenHour"/> — tentýž trik s nízkým sluncem, ale opačný konec dne.
    /// </summary>
    Dawn,

    /// <summary>
    /// Přehled výrobních řetězců. Jádro hry je řetězec „co z čeho" a na fotce
    /// města ho nepozná nikdo — tahle obrazovka je jediné místo, kde je vidět celý.
    /// </summary>
    Chains,

    /// <summary>Vzestup: prestiž a to, co se za něj dá koupit. Konec smyčky.</summary>
    Ascension,

    /// <summary>Obrana hranice — že se hra dá i prohrát.</summary>
    Frontier,

    /// <summary>Kronika města: co všechno se cestou stalo.</summary>
    Chronicle,

    /// <summary>Statistiky a grafy růstu — pro toho, kdo si rád kouká na čísla.</summary>
    Stats,
}

/// <summary>
/// Jeden snímek do obchodu: co se má nasimulovat, na co se dívat a jak blízko.
///
/// <para>Popis je záměrně data, ne kód — přidat další záběr znamená přidat řádek
/// do seznamu v <see cref="CaptureDirector"/>, ne psát novou metodu.</para>
/// </summary>
/// <param name="FileName">Jméno výsledného PNG (bez přípony).</param>
/// <param name="Subject">Co se má ukázat.</param>
/// <param name="Minutes">Kolik herních minut se před snímkem odsimuluje.</param>
/// <param name="Zoom">Přiblížení kamery (1 = dlaždice v základní velikosti).</param>
/// <param name="Seed">Seed světa — stejný seed dá vždy stejný záběr.</param>
public sealed record StoreShot(
    string FileName,
    ShotSubject Subject,
    double Minutes,
    float Zoom,
    long Seed);
