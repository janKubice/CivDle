using CivDle.Core.Content;
using CivDle.Core.Save;
using CivDle.Core.Sim;
using CivDle.Core.Tests.Support;
using CivDle.Core.World;
using Xunit;

namespace CivDle.Core.Tests.Sim;

/// <summary>
/// Úkoly: pevný úkol se splní při dosažení metriky (odměna + oznámení),
/// dynamický úkol se posune na vyšší tier, a stav přežije save (v6).
/// </summary>
public class QuestTests
{
    private static readonly Resource[] FoodAndGold =
    {
        new("food", new RgbColor(220, 180, 60), StartAmount: 50, BaseStorage: 1000),
        new("gold", new RgbColor(230, 200, 80), StartAmount: 0, BaseStorage: 1000),
    };

    // Podmínka splněná hned (populace ≥ startovní), odměna 7 zlata.
    private static readonly QuestDef[] EasyQuest =
    {
        new("q0", new GoalCondition(MetricKind.Population, -1, 5), new[] { new ResourceAmount(1, 7) }),
    };

    private static Simulation Run(GameContent content, int ticks)
    {
        var sim = new Simulation(content, new UniformTerrain((byte)1));
        for (int i = 0; i < ticks; i++)
        {
            sim.Tick();
        }

        return sim;
    }

    [Fact]
    public void FixedQuest_Completes_GrantsReward_AndNotifies()
    {
        var content = TestContent.Build(resources: FoodAndGold, quests: EasyQuest);
        var sim = Run(content, 12);

        Assert.True(sim.IsQuestCompleted(0));
        Assert.Equal(7, sim.GetResource(1)); // odměna zlata

        Assert.True(sim.TryDequeueNotification(out var note));
        Assert.Equal(NotificationKind.QuestCompleted, note.Kind);
        Assert.Equal(content.Quests[0].NameKey, note.SubjectKey);
    }

    [Fact]
    public void AnOutgrownQuest_ClosesWithoutReward()
    {
        // „Nasbírej ručně 10 zlata" (sklizeň) se nikdy nesplní, když surovinu
        // dělá automatika — a jakmile je město dál, než úkol počítal, zavře se
        // bez odměny a z panelu zmizí.
        var outgrown = new QuestDef(
            "gather_by_hand",
            new GoalCondition(MetricKind.Harvested, 1, 10),
            new[] { new ResourceAmount(1, 7) },
            RetireWhen: new GoalCondition(MetricKind.Population, -1, 5));
        var content = TestContent.Build(resources: FoodAndGold, quests: new[] { outgrown });

        var sim = Run(content, 12);

        Assert.True(sim.IsQuestRetired(0));
        Assert.False(sim.IsQuestActive(0));
        Assert.False(sim.IsQuestCompleted(0));
        Assert.Equal(0, sim.GetResource(1));
    }

    [Fact]
    public void AQuestComesBack_WhenTheCityIsSmallAgain()
    {
        // Uzavření se neukládá, počítá se ze stavu: kdo začne znovu od vesnice
        // (Odkaz), tomu se úvodní úkol vrátí, protože zase dává smysl.
        var quest = new QuestDef(
            "gather_by_hand",
            new GoalCondition(MetricKind.Harvested, 1, 10),
            System.Array.Empty<ResourceAmount>(),
            RetireWhen: new GoalCondition(MetricKind.Population, -1, 20));
        var content = TestContent.Build(resources: FoodAndGold, quests: new[] { quest });
        var sim = new Simulation(content, new UniformTerrain((byte)1));

        sim.SetPopulationForTest(25);
        Assert.True(sim.IsQuestRetired(0));

        sim.SetPopulationForTest(5);
        Assert.False(sim.IsQuestRetired(0));
        Assert.True(sim.IsQuestActive(0));
    }

    [Fact]
    public void AGreatGoal_StaysHidden_UntilItsTime()
    {
        // Velký cíl („miliarda obyvatel") by v první hodině jen strašil.
        var late = new QuestDef(
            "big_city",
            new GoalCondition(MetricKind.Population, -1, 5),
            new[] { new ResourceAmount(1, 3) },
            ActiveWhen: new GoalCondition(MetricKind.Population, -1, 50),
            Group: QuestGroup.Late);
        var content = TestContent.Build(resources: FoodAndGold, quests: new[] { late });

        var sim = Run(content, 12);

        Assert.False(sim.IsQuestActive(0));
        Assert.False(sim.IsQuestCompleted(0)); // skrytý cíl se ani nesplní

        sim.SetPopulationForTest(60);
        for (int i = 0; i < 12; i++)
        {
            sim.Tick();
        }

        Assert.True(sim.IsQuestCompleted(0));
        Assert.Equal(3, sim.GetResource(1));
    }

    [Fact]
    public void DynamicQuest_AdvancesTier_WhenTargetReached()
    {
        var dynamic = new DynamicQuestConfig(
            new GoalCondition(MetricKind.Population, -1, 5), 2.0, System.Array.Empty<ResourceAmount>(), 1.0);
        var content = TestContent.Build(resources: FoodAndGold, questsDynamic: dynamic);

        var sim = Run(content, 12);

        // Práh 5 splněn (start pop 5) → tier 1; další práh 10 zatím ne.
        Assert.Equal(1, sim.DynamicQuestTier);
        Assert.Equal(10, sim.DynamicQuestTarget);
    }

    [Fact]
    public void SaveRoundtrip_PersistsQuestState()
    {
        var dynamic = new DynamicQuestConfig(
            new GoalCondition(MetricKind.Population, -1, 5), 2.0, System.Array.Empty<ResourceAmount>(), 1.0);
        var content = TestContent.Build(resources: FoodAndGold, quests: EasyQuest, questsDynamic: dynamic);
        var sim = Run(content, 12);

        var metadata = new SaveMetadata(7, "s", "test", System.DateTime.UtcNow);
        var stream = new MemoryStream();
        new SaveGameSerializer().Write(stream, sim, metadata);
        stream.Position = 0;
        var (loaded, _) = new SaveGameSerializer().Read(stream, content);

        Assert.True(loaded.IsQuestCompleted(0));
        Assert.Equal(sim.DynamicQuestTier, loaded.DynamicQuestTier);
    }
}
