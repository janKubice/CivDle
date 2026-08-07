using CivDle.Core.Platform;
using Xunit;

namespace CivDle.Core.Tests.Platform;

/// <summary>
/// Lokální platforma — pro hráče mimo Steam je to ta jediná, kterou uvidí,
/// takže musí achievementy i rekordy držet stejně spolehlivě jako Steam.
/// </summary>
public sealed class LocalPlatformServicesTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".json");

    public void Dispose()
    {
        if (File.Exists(_path))
        {
            File.Delete(_path);
        }
    }

    [Fact]
    public void AchievementsAndStatsSurviveARestart()
    {
        var first = new LocalPlatformServices(_path);
        first.UnlockAchievement("ACH_WOODCUTTER");
        first.SetStat(PlatformCatalog.StatTotalPower, 1234.5);
        first.Flush();

        var second = new LocalPlatformServices(_path);

        Assert.True(second.IsAchievementUnlocked("ACH_WOODCUTTER"));
        Assert.False(second.IsAchievementUnlocked("ACH_SETTLER"));
        Assert.Equal(1234.5, second.GetStat(PlatformCatalog.StatTotalPower), 3);
    }

    [Fact]
    public void DescendingBoardKeepsTheHighestScore()
    {
        var platform = new LocalPlatformServices(_path);

        platform.SubmitScore("LB_PEAK_POPULATION", 500);
        platform.SubmitScore("LB_PEAK_POPULATION", 200); // horší se nesmí zapsat

        Assert.Equal(500, platform.PersonalBest("LB_PEAK_POPULATION"));
    }

    [Fact]
    public void AscendingBoardKeepsTheLowestScore()
    {
        // U rychlostního žebříčku je lepší nižší. Kdyby se to spletlo, rekord
        // by se „zlepšoval" každým pomalejším pokusem.
        var platform = new LocalPlatformServices(_path);

        platform.SubmitScore("LB_FASTEST_ASCENSION", 90_000);
        platform.SubmitScore("LB_FASTEST_ASCENSION", 60_000);
        platform.SubmitScore("LB_FASTEST_ASCENSION", 120_000);

        Assert.Equal(60_000, platform.PersonalBest("LB_FASTEST_ASCENSION"));
    }

    [Fact]
    public void UnknownBoardHasNoPersonalBestYet()
    {
        var platform = new LocalPlatformServices(_path);

        Assert.Null(platform.PersonalBest("LB_TOTAL_POWER"));
        Assert.Empty(platform.TopScores("LB_TOTAL_POWER", 10));
    }

    [Fact]
    public void ACorruptedFileDoesNotStopTheGame()
    {
        // Achievementy nejsou stav světa — za poškozený soubor nemá hráč přijít
        // o možnost hrát.
        File.WriteAllText(_path, "{ tohle není JSON");

        var platform = new LocalPlatformServices(_path);

        Assert.False(platform.IsAchievementUnlocked("ACH_WOODCUTTER"));
        platform.UnlockAchievement("ACH_WOODCUTTER");
        platform.Flush();

        Assert.True(new LocalPlatformServices(_path).IsAchievementUnlocked("ACH_WOODCUTTER"));
    }

    [Fact]
    public void UnlockingTwiceIsHarmless()
    {
        var platform = new LocalPlatformServices(_path);
        platform.UnlockAchievement("ACH_SETTLER");
        platform.UnlockAchievement("ACH_SETTLER");
        platform.Flush();

        Assert.True(new LocalPlatformServices(_path).IsAchievementUnlocked("ACH_SETTLER"));
    }
}
