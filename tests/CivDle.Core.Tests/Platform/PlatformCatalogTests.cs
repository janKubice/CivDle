using CivDle.Core.Platform;
using CivDle.Core.Sim;
using CivDle.Core.Tests.Support;
using CivDle.Core.World;
using Xunit;

namespace CivDle.Core.Tests.Platform;

/// <summary>
/// Most mezi hrou a platformou.
///
/// <para>Steamworks jména jsou řetězce, které překladač nezkontroluje — překlep
/// znamená achievement, který se tiše nikdy neodemkne, a to se pozná až po
/// vydání. Testy proto ověřují, že jména, kterými hra volá, sedí s tím, co je
/// vygenerované do <c>docs/steam/generated/</c> a zadané ve Steamworks.</para>
/// </summary>
public class PlatformCatalogTests
{
    [Fact]
    public void ApiNamesFollowTheConventionTheGeneratedCsvUses()
    {
        // Generátor CSV dělá totéž; kdyby se tyhle dva rozešly, hra by odemykala
        // achievementy, které ve Steamworks neexistují.
        Assert.Equal("ACH_WOODCUTTER", PlatformCatalog.AchievementApiName("woodcutter"));
        Assert.Equal("ACH_FIRST_STEPS", PlatformCatalog.AchievementApiName("first_steps"));
    }

    [Fact]
    public void EveryRealAchievementGetsAUniqueApiName()
    {
        var content = TestData.LoadRealContent();
        var names = new HashSet<string>(StringComparer.Ordinal);

        foreach (var achievement in content.Achievements.All)
        {
            Assert.True(
                names.Add(PlatformCatalog.AchievementApiName(achievement.Id)),
                $"dvě achievement ID dávají stejné API jméno: {achievement.Id}");
        }

        Assert.Equal(content.Achievements.Count, names.Count);
    }

    [Fact]
    public void LeaderboardIdsAreUnique()
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var board in PlatformCatalog.Leaderboards)
        {
            Assert.True(ids.Add(board.Id), $"duplicitní ID žebříčku: {board.Id}");
        }
    }

    [Fact]
    public void OnlySpeedLeaderboardsAreAscending()
    {
        // Špatný směr řazení znamená žebříček, kde vyhrává nejhorší výsledek —
        // a opravit se to po vydání nedá bez smazání všech skóre.
        foreach (var board in PlatformCatalog.Leaderboards)
        {
            bool speed = board.Id.Contains("FASTEST", StringComparison.Ordinal);
            Assert.Equal(speed, board.Ascending);
            Assert.Equal(speed, board.IsTime);
        }
    }

    [Fact]
    public void PushingStatsAndScoresFromAFreshGameDoesNotThrow()
    {
        var sim = new Simulation(TestContent.Build(), new UniformTerrain(1));
        var platform = new LocalPlatformServices(Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()));

        PlatformCatalog.PushStats(platform, sim);
        PlatformCatalog.PushScores(platform, sim);

        Assert.Equal(sim.Buildings.Length, platform.GetStat(PlatformCatalog.StatTotalBuildings));
        Assert.Equal(1.0, platform.GetStat(PlatformCatalog.StatTotalPower), 3);
    }

    [Fact]
    public void ScoresAreNotSubmittedWhenLeaderboardsAreBlocked()
    {
        // S modem se do sdílených žebříčků posílat nesmí — čísla z upravených
        // dat by je znehodnotila a zpětně se to vzít nedá.
        var sim = new Simulation(TestContent.Build(), new UniformTerrain(1));
        var platform = new BlockedPlatform();

        PlatformCatalog.PushScores(platform, sim);

        Assert.Empty(platform.Submitted);
    }

    /// <summary>Platforma, která žebříčky zakazuje — jinak se chová jako lokální.</summary>
    private sealed class BlockedPlatform : IPlatformServices
    {
        public List<string> Submitted { get; } = new();

        public bool IsAvailable => true;

        public string PlayerName => "test";

        public bool LeaderboardsAllowed => false;

        public void UnlockAchievement(string apiName) { }

        public bool IsAchievementUnlocked(string apiName) => false;

        public void SetStat(string apiName, long value) { }

        public void SetStat(string apiName, double value) { }

        public double GetStat(string apiName) => 0;

        public void SubmitScore(string leaderboardId, long score) => Submitted.Add(leaderboardId);

        public IReadOnlyList<LeaderboardEntry> TopScores(string leaderboardId, int count) =>
            Array.Empty<LeaderboardEntry>();

        public long? PersonalBest(string leaderboardId) => null;

        public IReadOnlyList<WorkshopItem> WorkshopItems() => Array.Empty<WorkshopItem>();

        public IReadOnlyList<string> SubscribedModDirectories() => Array.Empty<string>();

        public void Flush() { }
    }
}
