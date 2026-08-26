using CivDle.Core.Content;
using CivDle.Core.Save;
using CivDle.Core.Sim;
using CivDle.Core.Tests.Support;
using CivDle.Core.World;
using Xunit;

namespace CivDle.Core.Tests.Sim;

/// <summary>
/// Scénáře: svět s pevným zadáním, výhrou a prohrou.
///
/// <para>Hlídají se čtyři věci, na kterých to tiše selhává: cíl se má poznat
/// (a jen jednou), prohra nesmí přebít výhru ve stejném tiku, zvláštní
/// pravidla musí opravdu platit, a <b>výsledek musí přežít save</b> — jinak
/// se prohra dá odklikat načtením hry.</para>
/// </summary>
public class ScenarioTests
{
    /// <summary>Scénář se kontroluje po dávkách, ne každý tik.</summary>
    private const int CheckTicks = 12;

    [Fact]
    public void RealContentHasScenariosWithGoals()
    {
        var content = TestData.LoadRealContent();

        Assert.True(content.Scenarios.IsEnabled);
        foreach (var scenario in content.Scenarios.Scenarios)
        {
            Assert.True(scenario.Goal.Target > 0, $"scénář '{scenario.Id}' nemá co splnit");
            Assert.NotEqual(0, scenario.Seed); // pevný svět pro všechny
        }
    }

    [Fact]
    public void AFreeGameIsNotAScenario()
    {
        var sim = new Simulation(ScenarioContent(), new UniformTerrain(1), seed: 1);

        Assert.False(sim.InScenario);
        Assert.Null(sim.Scenario);
        Assert.Equal(ScenarioOutcome.Running, sim.ScenarioResult);
        Assert.Equal(double.PositiveInfinity, sim.ScenarioSecondsLeft);
    }

    [Fact]
    public void StartingAScenarioHandsOutItsStartingResources()
    {
        var sim = new Simulation(ScenarioContent(), new UniformTerrain(1), seed: 1);
        double before = sim.GetResource(0);

        sim.StartScenario(0);

        Assert.True(sim.InScenario);
        Assert.Equal(before + 50, sim.GetResource(0));
    }

    [Fact]
    public void ReachingTheGoalWinsIt()
    {
        var (sim, content) = Started();

        // Cíl je pět budov; do té doby scénář běží.
        Tick(sim, CheckTicks);
        Assert.Equal(ScenarioOutcome.Running, sim.ScenarioResult);

        for (int i = 0; i < 5; i++)
        {
            Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuilding(0, i * 2, 0));
        }

        Tick(sim, CheckTicks);

        Assert.Equal(ScenarioOutcome.Won, sim.ScenarioResult);
        Assert.Equal(content.Scenarios[0].Id, sim.Scenario!.Id);
    }

    [Fact]
    public void RunningOutOfTimeLosesIt()
    {
        var (sim, _) = Started();

        // Limit je 5 sekund herního času; nic se nepostaví, takže cíl nepadne.
        Tick(sim, (int)Simulation.TicksPerSecond * 6);

        Assert.Equal(ScenarioOutcome.Lost, sim.ScenarioResult);
        Assert.Equal(0, sim.ScenarioSecondsLeft);
    }

    [Fact]
    public void FinishingOnTheLastTickStillCounts()
    {
        // Dohnat zadání na poslední chvíli je ta nejlepší část scénáře a nemá
        // ji sebrat pořadí ifů: v tiku, kdy vyprší čas i padne cíl, se vyhrálo.
        var (sim, _) = Started();
        for (int i = 0; i < 5; i++)
        {
            Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuilding(0, i * 2, 0));
        }

        Tick(sim, (int)Simulation.TicksPerSecond * 6);

        Assert.Equal(ScenarioOutcome.Won, sim.ScenarioResult);
    }

    [Fact]
    public void OnceItIsOverItStaysOver()
    {
        var (sim, _) = Started();
        Tick(sim, (int)Simulation.TicksPerSecond * 6);
        Assert.Equal(ScenarioOutcome.Lost, sim.ScenarioResult);

        // I když se cíl splní dodatečně, prohra platí.
        for (int i = 0; i < 5; i++)
        {
            Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuilding(0, i * 2, 0));
        }

        Tick(sim, CheckTicks * 3);

        Assert.Equal(ScenarioOutcome.Lost, sim.ScenarioResult);
    }

    [Fact]
    public void ScenarioRulesActuallyApply()
    {
        var (sim, _) = Started();

        Assert.True(sim.ScenarioRuleActive(ScenarioRule.NoAscension));
        Assert.False(sim.ScenarioRuleActive(ScenarioRule.NoAutoBuild));

        // Vzestup je zakázaný, i kdyby na něj hráč měl.
        sim.DebugGrantPrestigePoints(1_000_000);
        Assert.Equal(PlacementResult.NotUnlocked, sim.TryAscend());
    }

    [Fact]
    public void GameplayOverrideChangesTheNumbers_WithoutTouchingTheOriginal()
    {
        var content = TestData.LoadRealContent();
        var over = new GameplayOverride(StartingPopulation: 42, FoodPerPersonPerSecond: 0.5);

        var changed = over.Apply(content.Gameplay);

        Assert.Equal(42, changed.StartingPopulation);
        Assert.Equal(0.5, changed.FoodPerPersonPerSecond);
        Assert.Equal(content.Gameplay.BaseHousingCapacity, changed.BaseHousingCapacity); // co se nepřepsalo, zůstalo
        Assert.NotEqual(42, content.Gameplay.StartingPopulation); // původní obsah je nedotčený
    }

    [Fact]
    public void TheOutcomeSurvivesSaveAndLoad()
    {
        // Bez toho by se prohra dala „vyzkoušet znovu" prostým načtením hry —
        // a zadání by přestalo něco znamenat.
        var content = ScenarioContent();
        var sim = new Simulation(content, new UniformTerrain(1), seed: 1);
        sim.StartScenario(0);
        Tick(sim, (int)Simulation.TicksPerSecond * 6);
        Assert.Equal(ScenarioOutcome.Lost, sim.ScenarioResult);

        var serializer = new SaveGameSerializer();
        using var stream = new MemoryStream();
        serializer.Write(stream, sim, new SaveMetadata(1, "s", "test", DateTime.UtcNow));
        stream.Position = 0;
        var (loaded, _) = serializer.Read(stream, content);

        Assert.True(loaded.InScenario);
        Assert.Equal("test_scenario", loaded.Scenario!.Id);
        Assert.Equal(ScenarioOutcome.Lost, loaded.ScenarioResult);
    }

    [Fact]
    public void AFreeGameSaveCarriesNoScenario()
    {
        var content = ScenarioContent();
        var sim = new Simulation(content, new UniformTerrain(1), seed: 1);

        var serializer = new SaveGameSerializer();
        using var stream = new MemoryStream();
        serializer.Write(stream, sim, new SaveMetadata(1, "s", "test", DateTime.UtcNow));
        stream.Position = 0;
        var (loaded, _) = serializer.Read(stream, content);

        Assert.False(loaded.InScenario);
    }

    // ----- pomocné -----

    private static void Tick(Simulation sim, int ticks)
    {
        for (int i = 0; i < ticks; i++)
        {
            sim.Tick();
        }
    }

    private static (Simulation Sim, GameContent Content) Started()
    {
        var content = ScenarioContent();
        var sim = new Simulation(content, new UniformTerrain(1), seed: 1);
        sim.StartScenario(0);
        return (sim, content);
    }

    private static GameContent ScenarioContent()
    {
        var biomes = new[] { TestContent.WaterBiome(), TestContent.LandBiome("grass") };
        var buildings = new[] { TestContent.SimpleBuilding("hut", biomes.Length) };

        return TestContent.Build(
            biomes: biomes,
            buildings: buildings,
            scenarios: new ScenarioCatalog(new[]
            {
                new ScenarioDef(
                    "test_scenario",
                    Seed: 7,
                    PresetIndex: -1,
                    GameplayOverride.None,
                    new[] { new ResourceAmount(0, 50) },
                    new GoalCondition(MetricKind.TotalBuildings, -1, 5),
                    FailBelow: null,
                    TimeLimitSeconds: 5,
                    new[] { ScenarioRule.NoAscension }),
            }));
    }
}
