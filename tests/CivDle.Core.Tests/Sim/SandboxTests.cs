using CivDle.Core.Content;
using CivDle.Core.Platform;
using CivDle.Core.Save;
using CivDle.Core.Sim;
using CivDle.Core.Tests.Support;
using CivDle.Core.World;
using Xunit;

namespace CivDle.Core.Tests.Sim;

/// <summary>
/// Pískoviště: stavět zadarmo a nic se nikam nepočítá.
///
/// <para>Testuje se hlavně to druhé. „Zadarmo" pozná hráč sám během minuty,
/// ale kdyby se z pískoviště dostal achievement nebo záznam v žebříčku, nikdo
/// si toho nevšimne — a přesně tím ztratí achievementy smysl pro všechny
/// ostatní. Proto je tu na každou cestu ven ze hry vlastní test.</para>
/// </summary>
public class SandboxTests
{
    [Fact]
    public void BuildingIsFreeAndTakesNothingFromStorage()
    {
        var (sim, content) = Sandbox();
        int house = Index(content, "house");

        double[] before = Snapshot(sim);
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuilding(house, 4, 4));

        for (int i = 0; i < sim.ResourceCount; i++)
        {
            Assert.Equal(before[i], sim.GetResource(i));
        }
    }

    [Fact]
    public void EmptyStorageIsNoObstacle()
    {
        var (sim, content) = Sandbox();
        int house = Index(content, "house");
        Drain(sim);

        Assert.Equal(PlacementResult.Ok, sim.CanPlace(house, 6, 6));
        Assert.True(sim.CanAfford(house));
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuilding(house, 6, 6));
    }

    [Fact]
    public void ResearchIsFreeToo()
    {
        var (sim, content) = Sandbox();
        Drain(sim);

        int tech = FirstAvailableTech(sim, content);
        Assert.Equal(PlacementResult.Ok, sim.CanResearch(tech));
        Assert.Equal(PlacementResult.Ok, sim.TryResearch(tech));
        Assert.True(sim.IsTechKnown(tech));
    }

    [Fact]
    public void ANormalGameStillPaysForEverything()
    {
        // Kontrolní test: kdyby se příznak někde přehodil na „vždycky zadarmo",
        // ostatní testy by to nechytily — všechny mají pískoviště zapnuté.
        var (sim, content) = Normal();
        int house = Index(content, "house");
        Drain(sim);

        Assert.Equal(PlacementResult.NotEnoughResources, sim.CanPlace(house, 4, 4));
        Assert.False(sim.CanAfford(house));
    }

    [Fact]
    public void NormalGameSpendsResourcesOnResearch()
    {
        var (sim, content) = Normal();
        sim.DebugFillStorages();

        int tech = FirstAvailableTech(sim, content);
        var cost = sim.ScaledResearchCost(tech);
        double before = sim.GetResource(cost[0].ResourceIndex);

        Assert.Equal(PlacementResult.Ok, sim.TryResearch(tech));

        Assert.Equal(before - cost[0].Amount, sim.GetResource(cost[0].ResourceIndex), 3);
    }

    [Fact]
    public void ResearchShowsUpInTheFlowLedger()
    {
        // Výzkum dřív odečítal suroviny mimo společný trychtýř, takže se
        // v bilanci toků neobjevil a hráči „mizely samy od sebe".
        var (sim, content) = Normal();
        sim.DebugFillStorages();

        int tech = FirstAvailableTech(sim, content);
        var cost = sim.ScaledResearchCost(tech);
        sim.Ledger.Reset();

        Assert.Equal(PlacementResult.Ok, sim.TryResearch(tech));

        // Evidence se uzavírá na konci tiku — bez něj by se zapsaná útrata
        // ještě nestihla promítnout do hlášeného toku.
        sim.Tick();

        Assert.True(
            sim.Ledger.ConsumedPerSecond(cost[0].ResourceIndex) > 0,
            "spotřeba na výzkum se v účtování toků neobjevila");
    }

    [Fact]
    public void NoAchievementsUnlockInSandbox()
    {
        var (sandbox, content) = Sandbox();
        var (normal, _) = Normal();

        // Obě hry udělají totéž — deset domů je achievement „stavitel".
        // Bez toho by test tvrdil jen „za dvacet vteřin se nestalo nic",
        // což by platilo i kdyby pískoviště žádný vliv nemělo.
        BuildTen(sandbox, content);
        BuildTen(normal, content);

        TickAWhile(sandbox);
        TickAWhile(normal);

        Assert.Equal(0, UnlockedCount(sandbox, content));
        Assert.True(
            UnlockedCount(normal, content) > 0,
            "v normální hře se za tu dobu musí odemknout aspoň něco");
    }

    [Fact]
    public void NoScoresAndNoStatsLeaveSandbox()
    {
        var (sim, _) = Sandbox();
        var platform = new RecordingPlatform();

        PlatformCatalog.PushScores(platform, sim);
        PlatformCatalog.PushStats(platform, sim);

        Assert.Empty(platform.Scores);
        Assert.Empty(platform.Stats);
    }

    [Fact]
    public void ANormalGameDoesSendScoresAndStats()
    {
        var (sim, _) = Normal();
        var platform = new RecordingPlatform();

        PlatformCatalog.PushScores(platform, sim);
        PlatformCatalog.PushStats(platform, sim);

        Assert.NotEmpty(platform.Scores);
        Assert.NotEmpty(platform.Stats);
    }

    [Fact]
    public void TheFlagSurvivesSaveAndLoad()
    {
        var (sim, _) = Sandbox();
        var content = TestData.LoadRealContent();

        var restored = SaveAndLoad(sim, content);

        Assert.True(restored.Sandbox);
    }

    [Fact]
    public void ANormalGameDoesNotBecomeSandboxByLoading()
    {
        var (sim, _) = Normal();
        var content = TestData.LoadRealContent();

        var restored = SaveAndLoad(sim, content);

        Assert.False(restored.Sandbox);
    }

    private static int Index(GameContent content, string id)
    {
        Assert.True(content.Buildings.TryIndexOf(id, out int index), id);
        return index;
    }

    /// <summary>Uloží a zase načte — jinak se nedá tvrdit, že příznak přežije.</summary>
    private static Simulation SaveAndLoad(Simulation sim, GameContent content)
    {
        var stream = new MemoryStream();
        new SaveGameSerializer().Write(
            stream, sim, new SaveMetadata(sim.Seed, "medium", "continents", DateTime.UtcNow));
        stream.Position = 0;

        var (loaded, _) = new SaveGameSerializer().Read(stream, content);
        return loaded;
    }

    private static (Simulation Sim, GameContent Content) Sandbox()
    {
        var (sim, content) = Normal();
        sim.MarkAsSandbox();
        return (sim, content);
    }

    private static (Simulation Sim, GameContent Content) Normal()
    {
        var content = TestData.LoadRealContent();
        var sim = new Simulation(content, new UniformTerrain(content.Biomes.IndexOf("grassland")));
        sim.SkipTutorial();
        return (sim, content);
    }

    private static void Drain(Simulation sim)
    {
        for (int i = 0; i < sim.ResourceCount; i++)
        {
            sim.AddResource(i, -sim.GetResource(i));
        }
    }

    private static double[] Snapshot(Simulation sim)
    {
        var values = new double[sim.ResourceCount];
        for (int i = 0; i < values.Length; i++)
        {
            values[i] = sim.GetResource(i);
        }

        return values;
    }

    /// <summary>Postaví deset domů — tolik chce achievement „stavitel".</summary>
    private static void BuildTen(Simulation sim, GameContent content)
    {
        int house = Index(content, "house");
        for (int i = 0; i < 10; i++)
        {
            Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuildingFree(house, 4 + i * 2, 40));
        }
    }

    private static void TickAWhile(Simulation sim)
    {
        for (int i = 0; i < 200; i++)
        {
            sim.Tick();
        }
    }

    private static int UnlockedCount(Simulation sim, GameContent content)
    {
        int count = 0;
        for (int i = 0; i < content.Achievements.Count; i++)
        {
            if (sim.IsAchievementUnlocked(i))
            {
                count++;
            }
        }

        return count;
    }

    /// <summary>
    /// První nevyzkoumaná technologie, na kterou hráč dosáhne. Pozor na
    /// <c>IsTechKnown</c>: to je true i pro tu, která teprve jde vyzkoumat —
    /// filtrovat se musí podle úrovně.
    /// </summary>
    private static int FirstAvailableTech(Simulation sim, GameContent content)
    {
        for (int i = 0; i < content.Techs.Count; i++)
        {
            if (sim.TechLevel(i) == 0
                && sim.CanResearch(i) is PlacementResult.Ok or PlacementResult.NotEnoughResources
                    or PlacementResult.ExceedsStorage)
            {
                return i;
            }
        }

        throw new InvalidOperationException("nová hra musí mít aspoň jednu dostupnou technologii");
    }

    /// <summary>Platforma, která si jen zapisuje, co jí kdo poslal.</summary>
    private sealed class RecordingPlatform : IPlatformServices
    {
        public List<string> Scores { get; } = new();

        public List<string> Stats { get; } = new();

        public bool IsAvailable => true;

        public bool HasOnlineLeaderboards => false;

        public string PlayerName => "test";

        public bool LeaderboardsAllowed => true;

        public void UnlockAchievement(string apiName) { }

        public bool IsAchievementUnlocked(string apiName) => false;

        public void SetStat(string apiName, long value) => Stats.Add(apiName);

        public void SetStat(string apiName, double value) => Stats.Add(apiName);

        public double GetStat(string apiName) => 0;

        public void SubmitScore(string leaderboardId, long score) => Scores.Add(leaderboardId);

        public IReadOnlyList<LeaderboardEntry> TopScores(string leaderboardId, int count) =>
            Array.Empty<LeaderboardEntry>();

        public long? PersonalBest(string leaderboardId) => null;

        public IReadOnlyList<WorkshopItem> WorkshopItems() => Array.Empty<WorkshopItem>();

        public IReadOnlyList<string> SubscribedModDirectories() => Array.Empty<string>();

        public void Flush() { }
    }
}
