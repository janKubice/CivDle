using CivDle.Core.Content;
using CivDle.Core.Sim;
using CivDle.Core.Tests.Support;
using CivDle.Core.World;
using Xunit;

namespace CivDle.Core.Tests.Sim;

/// <summary>
/// Velké cíle pozdní hry (bod A2 v docs/endgame.md): nové metriky a odměna,
/// kterou jde ukázat — pomník odemčený splněným cílem.
/// </summary>
public class GreatGoalsTests
{
    private static readonly Resource[] Wood =
    {
        new("wood", new RgbColor(140, 90, 40), StartAmount: 1000, BaseStorage: 10_000),
    };

    [Fact]
    public void AMonument_UnlocksOnlyWithItsGoal()
    {
        // Pomník za „100 obyvatel" se nedá postavit dřív, než cíl padne.
        var monument = TestContent.SimpleBuilding("pillar", 2) with { UnlockedBy = "quest:big" };
        var goal = new QuestDef("big", new GoalCondition(MetricKind.Population, -1, 100), Array.Empty<ResourceAmount>(),
            Group: QuestGroup.Late);
        var content = TestContent.Build(resources: Wood, buildings: new[] { monument }, quests: new[] { goal });
        var sim = new Simulation(content, new UniformTerrain(1));

        Assert.False(sim.IsBuildingBuildable(0));
        Assert.Equal(PlacementResult.NotUnlocked, sim.CanPlace(0, 3, 3));

        sim.SetPopulationForTest(120);
        for (int i = 0; i < 12; i++)
        {
            sim.Tick();
        }

        Assert.True(sim.IsQuestCompleted(0));
        Assert.True(sim.IsBuildingBuildable(0));
    }

    [Fact]
    public void AChallengeReward_ComesFromTheProfile()
    {
        // Výzva platí pro hráče napříč hrami — simulace ji dostane z profilu.
        var reward = TestContent.SimpleBuilding("stilt_house", 2) with { UnlockedBy = "challenge:flood" };
        var content = TestContent.Build(resources: Wood, buildings: new[] { reward });
        var sim = new Simulation(content, new UniformTerrain(1));

        Assert.False(sim.IsBuildingBuildable(0));

        sim.SetProfileUnlocks(new[] { "challenge:flood" });

        Assert.True(sim.IsBuildingBuildable(0));
    }

    [Fact]
    public void CleanAirIsAHundred_AndDefenceWavesZero_InAFreshCity()
    {
        var sim = new Simulation(TestContent.Build(resources: Wood), new UniformTerrain(1));

        Assert.Equal(100, sim.EvaluateMetric(MetricKind.AirQuality, -1));
        Assert.Equal(0, sim.EvaluateMetric(MetricKind.DefenceWaves, -1)); // bez režimu obrany žádné vlny
        Assert.Equal(0, sim.EvaluateMetric(MetricKind.Megastructures, -1));
        Assert.Equal(0, sim.EvaluateMetric(MetricKind.Satellites, -1));
    }

    [Fact]
    public void Megastructures_CountDistinctCompletedKinds()
    {
        // Dvě stejné megastruktury jsou pořád jeden div; rozestavěná se nepočítá.
        var a = TestContent.SimpleBuilding("spire", 2) with { Category = "megastructure", BuildCost = Array.Empty<ResourceAmount>() };
        var b = TestContent.SimpleBuilding("ring", 2) with { Category = "megastructure", BuildCost = Array.Empty<ResourceAmount>(), BuildTicks = 500 };
        var content = TestContent.Build(resources: Wood, buildings: new[] { a, b });
        var sim = new Simulation(content, new UniformTerrain(1));

        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuilding(0, 2, 2));
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuilding(0, 6, 2));
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuilding(1, 10, 2)); // rozestavěná

        Assert.Equal(1, sim.EvaluateMetric(MetricKind.Megastructures, -1));
    }

    /// <summary>
    /// Cesta ke hvězdám musí být vidět předem: cíle „Sedm divů techniky"
    /// a „Otevřít bránu" se ukážou spolu s měřítkem, které odemyká první
    /// megastruktury. Dřív byla brána vidět až po dostavění všech sedmi divů
    /// a hráč o ní neměl jak vědět.
    /// </summary>
    [Fact]
    public void RealContent_TheRoadToTheStarsShowsUpWithTheFirstMegastructures()
    {
        var content = TestData.LoadRealContent();
        int firstMegaTier = content.AscensionTiers.All
            .Where(t => t.UnlockedBuildingIndices.Any(b => content.Buildings[b].Category == "megastructure"))
            .Min(t => t.Order);
        int wonders = content.Quests.IndexOf("great_megastructures");
        int gate = content.Quests.IndexOf("open_the_gate");

        var sim = new Simulation(content, new UniformTerrain((byte)content.Biomes.IndexOf("grassland")), 3);
        sim.DebugGrantAscensionLevels(firstMegaTier - 1);
        Assert.False(sim.IsQuestActive(wonders));
        Assert.False(sim.IsQuestActive(gate));

        sim.DebugGrantAscensionLevels(1);
        Assert.True(sim.IsQuestActive(wonders), "Sedm divů má být vidět s prvními megastrukturami");
        Assert.True(sim.IsQuestActive(gate), "brána má být vidět jako další krok, ne až po sedmi divech");
    }

    [Fact]
    public void RealContent_EveryGreatGoal_HasAMonument()
    {
        // Velký cíl bez pomníku by byl jen další procento — přesně to, co
        // pozdní hra nepotřebuje. Jediná výjimka je cíl, jehož odměnou je
        // dokončený projekt sám (Otevřít bránu → konec kapitoly).
        var content = TestData.LoadRealContent();
        foreach (var quest in content.Quests.All.Where(q => q.Group == QuestGroup.Late))
        {
            if (quest.Condition.Kind == MetricKind.ProjectsCompleted)
            {
                continue;
            }

            Assert.Contains(content.Buildings.All, b => b.UnlockedBy == $"quest:{quest.Id}");
        }
    }
}
