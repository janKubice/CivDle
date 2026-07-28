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
