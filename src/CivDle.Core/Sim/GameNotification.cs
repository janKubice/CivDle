namespace CivDle.Core.Sim;

/// <summary>Druh vyskakovací zprávy (toast) — určuje ikonu/barvu v renderu.</summary>
public enum NotificationKind
{
    /// <summary>Splněný úkol.</summary>
    QuestCompleted,

    /// <summary>Odemčený achievement.</summary>
    AchievementUnlocked,

    /// <summary>Milník (osada → město…).</summary>
    Milestone,

    /// <summary>Proběhl Vzestup.</summary>
    Ascended,

    /// <summary>Zásah do světa, který nespustil hráč (návštěva UFO).</summary>
    WorldEvent,

    /// <summary>Na nástěnku přibyla nová zakázka.</summary>
    ContractOffered,

    /// <summary>Zakázka vypršela, aniž ji hráč splnil (žádný trest, jen odešla).</summary>
    ContractExpired,

    /// <summary>Hráč zakázku odevzdal a dostal zaplaceno.</summary>
    ContractFulfilled,

    /// <summary>Ozval se obyvatel s prosbou.</summary>
    CitizenAsks,

    /// <summary>Hráč obyvateli pomohl a vznikla jeho živnost.</summary>
    CitizenHelped,

    /// <summary>Obyvatel to vzdal a odešel (bez trestu).</summary>
    CitizenLeft,
}

/// <summary>
/// Událost k zobrazení jako toast. Simulace ji jen VYROBÍ (data: druh + lokalizační
/// klíče), render vrstva ji přeloží a vykreslí — sim nezná obrazovku (viz vrstvy
/// v CLAUDE.md). <see cref="SubjectKey"/> je konkrétní věc (jméno úkolu/achievementu),
/// <see cref="TitleKey"/> je nadpis druhu.
/// </summary>
/// <param name="Kind">Druh zprávy (barva/ikona).</param>
/// <param name="TitleKey">Lokalizační klíč nadpisu (např. <c>toast.quest</c>).</param>
/// <param name="SubjectKey">Lokalizační klíč předmětu (jméno úkolu/achievementu/milníku).</param>
/// <param name="SubjectArg">
/// Číslo do jména předmětu, když ho jméno potřebuje (dynamický úkol se jmenuje
/// „Rozrůstání: {0} obyvatel"). −1 = jméno je bez zástupného symbolu.
///
/// <para>Bez tohohle se v toastu ukazovalo doslova „{0}" — jméno se totiž skládá
/// až v UI a simulace mu k němu musí dodat i to číslo.</para>
/// </param>
public readonly record struct GameNotification(
    NotificationKind Kind,
    string TitleKey,
    string SubjectKey,
    long SubjectArg = -1)
{
    /// <summary>Potřebuje jméno předmětu doplnit číslo?</summary>
    public bool HasSubjectArg => SubjectArg >= 0;
}
