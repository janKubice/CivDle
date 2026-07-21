using CivDle.Core.Content;

namespace CivDle.Core.Sim;

/// <summary>Výsledek vyhodnocení denní odměny.</summary>
/// <param name="Due">Náleží dnes odměna (ještě nebyla vyzvednuta)?</param>
/// <param name="Streak">Nová série po sobě jdoucích dní.</param>
/// <param name="Reward">Odměna (základ × série do stropu).</param>
public readonly record struct DailyRewardResult(bool Due, int Streak, IReadOnlyList<ResourceAmount> Reward);

/// <summary>
/// Denní odměna za návrat (retenční háček): jednou za reálný den dá surovinovou
/// odměnu, která roste se sérií po sobě jdoucích dní (do stropu). Čistá funkce —
/// hodiny přes parametr, aby šla testovat; perzistenci data řeší volající (profil).
/// </summary>
public static class DailyReward
{
    private const string DateFormat = "yyyy-MM-dd";

    /// <summary>
    /// Vyhodnotí, zda dnes náleží odměna, a spočítá ji. <paramref name="lastDateUtc"/>
    /// a <paramref name="currentStreak"/> jsou z profilu; při <c>Due</c> je volající uloží.
    /// </summary>
    public static DailyRewardResult Evaluate(DailyRewardConfig config, string lastDateUtc, int currentStreak, DateTime todayUtc)
    {
        string today = todayUtc.ToString(DateFormat);
        if (lastDateUtc == today)
        {
            return new DailyRewardResult(false, currentStreak, Array.Empty<ResourceAmount>());
        }

        string yesterday = todayUtc.AddDays(-1).ToString(DateFormat);
        int streak = lastDateUtc == yesterday ? currentStreak + 1 : 1; // přerušená série začíná od 1
        int factor = Math.Min(streak, Math.Max(1, config.StreakCap));

        var reward = new List<ResourceAmount>(config.BaseReward.Count);
        for (int i = 0; i < config.BaseReward.Count; i++)
        {
            reward.Add(new ResourceAmount(config.BaseReward[i].ResourceIndex, config.BaseReward[i].Amount * factor));
        }

        return new DailyRewardResult(true, streak, reward);
    }

    /// <summary>Dnešní datum ve formátu, který se ukládá do profilu.</summary>
    public static string TodayKey(DateTime todayUtc) => todayUtc.ToString(DateFormat);
}
