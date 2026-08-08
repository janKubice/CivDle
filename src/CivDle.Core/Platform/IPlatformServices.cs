namespace CivDle.Core.Platform;

/// <summary>
/// Jeden záznam v žebříčku.
/// </summary>
/// <param name="Rank">Pořadí (1 = první).</param>
/// <param name="PlayerName">Jméno hráče; u lokálního žebříčku je to hráč sám.</param>
/// <param name="Score">Hodnota, podle které se řadí.</param>
/// <param name="IsLocalPlayer">Zvýraznit řádek jako „to jsi ty".</param>
public readonly record struct LeaderboardEntry(int Rank, string PlayerName, long Score, bool IsLocalPlayer);

/// <summary>
/// Jeden mod nabízený k odběru (z Workshopu nebo z lokální složky).
/// </summary>
/// <param name="Id">Identifikátor u zdroje (u Steamu PublishedFileId).</param>
/// <param name="Title">Jméno pro hráče.</param>
/// <param name="Description">Popis od autora.</param>
/// <param name="Author">Autor.</param>
/// <param name="Subscribed">Má ho hráč odebraný (= stažený)?</param>
/// <param name="Directory">Kde leží na disku, nebo prázdné, pokud stažený není.</param>
public sealed record WorkshopItem(
    string Id,
    string Title,
    string Description,
    string Author,
    bool Subscribed,
    string Directory);

/// <summary>
/// Vše, co hra potřebuje od herní platformy (Steam) — a nic víc.
///
/// <para><b>Proč rozhraní a ne přímo Steamworks:</b> Steam se nedá vyzkoušet
/// bez zaplaceného App ID a nainstalovaného klienta. Kdyby na něm hra visela
/// přímo, nešla by spustit na stroji bez Steamu, nešla by testovat v CI a
/// nešla by vydat nikde jinde. Takhle běží úplně bez něj a Steam je jen jedna
/// z implementací — ta se doplní, až bude App ID.</para>
///
/// <para>Rozhraní je schválně <b>úzké</b>: čtyři věci (achievementy, statistiky,
/// žebříčky, mody). Všechno ostatní, co Steam umí, hra nepotřebuje.</para>
///
/// <para>Vrstva: jádro. Nezná render ani UI.</para>
/// </summary>
public interface IPlatformServices
{
    /// <summary>Je platforma opravdu k dispozici? (Steam běží, hra je přes něj spuštěná.)</summary>
    bool IsAvailable { get; }

    /// <summary>
    /// Jsou žebříčky sdílené s ostatními hráči?
    ///
    /// <para>Odděleno od <see cref="IsAvailable"/> schválně: lokální platforma
    /// je vždycky „k dispozici" (achievementy i rekordy fungují), ale sdílená
    /// není. Bez tohohle rozlišení tvrdila obrazovka žebříčků „připojeno ke
    /// Steamu" i tomu, kdo Steam vůbec nemá.</para>
    /// </summary>
    bool HasOnlineLeaderboards { get; }

    /// <summary>Jméno hráče pro žebříčky a sdílení.</summary>
    string PlayerName { get; }

    /// <summary>
    /// Smí se teď posílat do žebříčků?
    ///
    /// <para>Falešné, když běží mod: čísla z upravených dat by žebříčky
    /// znehodnotila. Achievementy se s modem odemykat smí — zákaz by hráče
    /// jen otravoval a idle hra nemá kompetitivní integritu, kterou by to
    /// poškodilo.</para>
    /// </summary>
    bool LeaderboardsAllowed { get; }

    /// <summary>Odemkne achievement. Opakované volání je bez následku.</summary>
    void UnlockAchievement(string apiName);

    /// <summary>Je achievement odemčený?</summary>
    bool IsAchievementUnlocked(string apiName);

    /// <summary>Nastaví celočíselnou statistiku.</summary>
    void SetStat(string apiName, long value);

    /// <summary>Nastaví desetinnou statistiku (velká čísla se do <c>long</c> nevejdou).</summary>
    void SetStat(string apiName, double value);

    /// <summary>Přečte statistiku (0, když ještě nebyla nastavená).</summary>
    double GetStat(string apiName);

    /// <summary>
    /// Pošle skóre do žebříčku. Nižší skóre se u vzestupných žebříčků
    /// nepřepisuje horším — o to se stará implementace.
    /// </summary>
    void SubmitScore(string leaderboardId, long score);

    /// <summary>Vrátí špičku žebříčku (nejvýš <paramref name="count"/> záznamů).</summary>
    IReadOnlyList<LeaderboardEntry> TopScores(string leaderboardId, int count);

    /// <summary>Nejlepší skóre tohoto hráče, nebo <c>null</c>, když ještě žádné nemá.</summary>
    long? PersonalBest(string leaderboardId);

    /// <summary>Mody, které má hráč k dispozici (odebrané i nabízené).</summary>
    IReadOnlyList<WorkshopItem> WorkshopItems();

    /// <summary>Složky se staženými mody, které má hra načíst navíc ke své <c>mods/</c>.</summary>
    IReadOnlyList<string> SubscribedModDirectories();

    /// <summary>Zapíše, co se změnilo (Steam ukládá dávkově, ne po jednom).</summary>
    void Flush();
}
