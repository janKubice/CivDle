namespace CivDle.Core.Config;

/// <summary>
/// Účet-wide profil hráče (napříč hrami a érami) — zatím odemčené achievementy.
/// Ukládá se mimo herní save (ten je per-hra), aby achievementy přetrvaly i po
/// novém startu. ID achievementů jsou stabilní (i pro budoucí napojení na Steam).
/// </summary>
public sealed class PlayerProfile
{
    /// <summary>ID odemčených achievementů (stabilní stringy).</summary>
    public List<string> UnlockedAchievements { get; set; } = new();

    /// <summary>Datum posledního vyzvednutí denní odměny (UTC, formát <c>yyyy-MM-dd</c>); prázdné = nikdy.</summary>
    public string LastDailyRewardDate { get; set; } = string.Empty;

    /// <summary>Aktuální série po sobě jdoucích dní s vyzvednutím.</summary>
    public int DailyStreak { get; set; }
}
