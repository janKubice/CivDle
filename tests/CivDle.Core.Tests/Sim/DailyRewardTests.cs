using CivDle.Core.Content;
using CivDle.Core.Sim;
using Xunit;

namespace CivDle.Core.Tests.Sim;

/// <summary>
/// Denní odměna: náleží jednou za den, série roste za dny v řadě, přeruší se při
/// vynechání a odměna neroste nad strop série.
/// </summary>
public class DailyRewardTests
{
    private static readonly DailyRewardConfig Config =
        new(new[] { new ResourceAmount(0, 10) }, StreakCap: 3);

    private static readonly DateTime Today = new(2026, 7, 21, 8, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void FirstTime_StartsStreakAtOne()
    {
        var result = DailyReward.Evaluate(Config, lastDateUtc: "", currentStreak: 0, Today);
        Assert.True(result.Due);
        Assert.Equal(1, result.Streak);
        Assert.Equal(10, result.Reward[0].Amount);
    }

    [Fact]
    public void SameDay_NotDue()
    {
        var result = DailyReward.Evaluate(Config, "2026-07-21", currentStreak: 4, Today);
        Assert.False(result.Due);
    }

    [Fact]
    public void ConsecutiveDay_IncrementsStreak()
    {
        var result = DailyReward.Evaluate(Config, "2026-07-20", currentStreak: 2, Today);
        Assert.True(result.Due);
        Assert.Equal(3, result.Streak);
        Assert.Equal(30, result.Reward[0].Amount); // 10 × série 3
    }

    [Fact]
    public void MissedDay_ResetsStreak()
    {
        var result = DailyReward.Evaluate(Config, "2026-07-18", currentStreak: 9, Today);
        Assert.True(result.Due);
        Assert.Equal(1, result.Streak);
        Assert.Equal(10, result.Reward[0].Amount);
    }

    [Fact]
    public void Reward_CappedByStreakCap()
    {
        var result = DailyReward.Evaluate(Config, "2026-07-20", currentStreak: 8, Today);
        Assert.Equal(9, result.Streak);          // série roste dál
        Assert.Equal(30, result.Reward[0].Amount); // ale odměna jen ×3 (strop)
    }
}
