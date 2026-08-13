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
