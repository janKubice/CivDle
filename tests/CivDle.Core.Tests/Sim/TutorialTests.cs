using CivDle.Core.Content;
using CivDle.Core.Save;
using CivDle.Core.Sim;
using CivDle.Core.Tests.Support;
using CivDle.Core.World;
using Xunit;

namespace CivDle.Core.Tests.Sim;

/// <summary>
/// Průvodce prvními kroky: aktivní je právě jeden krok, splněné kroky se
/// přeskakují najednou, přeskočení je trvalé a postup přežije save.
/// </summary>
public class TutorialTests
{
    private static readonly Resource[] Wood =
    {
        new("wood", new RgbColor(140, 90, 40), StartAmount: 10, BaseStorage: 1000),
    };

    // První krok je splněný hned (startovní populace 5), druhý ne (potřebuje 500).
    private static readonly TutorialStepDef[] TwoSteps =
    {
        new("start", new GoalCondition(MetricKind.Population, -1, 5), FocusHint.None),
        new("far", new GoalCondition(MetricKind.Population, -1, 500), FocusHint.None),
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
    public void FirstStep_IsActive_BeforeAnythingHappens()
    {
        var content = TestContent.Build(resources: Wood, tutorial: TwoSteps);
        var sim = new Simulation(content, new UniformTerrain((byte)1));

        Assert.False(sim.IsTutorialFinished);
        Assert.Equal("start", sim.CurrentTutorialStep!.Id);
    }

    [Fact]
    public void SatisfiedStep_Advances_AndNotifies()
    {
        var content = TestContent.Build(resources: Wood, tutorial: TwoSteps);
        var sim = Run(content, 12);

        Assert.Equal(1, sim.TutorialStep);
        Assert.Equal("far", sim.CurrentTutorialStep!.Id);

        Assert.True(sim.TryDequeueNotification(out var note));
        Assert.Equal(NotificationKind.Milestone, note.Kind);
        Assert.Equal("tutorial.far", note.SubjectKey);
    }

    [Fact]
    public void UnsatisfiedStep_StaysPut()
    {
        var content = TestContent.Build(resources: Wood, tutorial: TwoSteps);
        var sim = Run(content, 60);

        // Populace na 500 za 60 tiků nedoroste — průvodce nesmí přeskočit dopředu.
        Assert.Equal(1, sim.TutorialStep);
        Assert.False(sim.IsTutorialFinished);
    }

    [Fact]
    public void AlreadySatisfiedSteps_AreSkippedAtOnce()
    {
        // Oba kroky jsou splněné startovní populací → průvodce projde na konec
        // v jednom vyhodnocení (hráč nemá odklikávat, co už dávno splnil).
        var steps = new[]
        {
            new TutorialStepDef("a", new GoalCondition(MetricKind.Population, -1, 1), FocusHint.None),
            new TutorialStepDef("b", new GoalCondition(MetricKind.Population, -1, 2), FocusHint.None),
        };
        var content = TestContent.Build(resources: Wood, tutorial: steps);

        var sim = Run(content, 12);

        Assert.True(sim.IsTutorialFinished);
        Assert.Null(sim.CurrentTutorialStep);
        Assert.True(sim.TryDequeueNotification(out var note));
        Assert.Equal("tutorial.done", note.SubjectKey);
    }

    [Fact]
    public void Skip_EndsTheGuide_AndSurvivesTicking()
    {
        var content = TestContent.Build(resources: Wood, tutorial: TwoSteps);
        var sim = new Simulation(content, new UniformTerrain((byte)1));

        sim.SkipTutorial();
        for (int i = 0; i < 30; i++)
        {
            sim.Tick();
        }

        Assert.True(sim.IsTutorialFinished);
        Assert.Null(sim.CurrentTutorialStep);
    }

    [Fact]
    public void SaveRoundtrip_PersistsProgress()
    {
        var content = TestContent.Build(resources: Wood, tutorial: TwoSteps);
        var sim = Run(content, 12);

        var stream = new MemoryStream();
        new SaveGameSerializer().Write(stream, sim, new SaveMetadata(7, "s", "test", System.DateTime.UtcNow));
        stream.Position = 0;
        var (loaded, _) = new SaveGameSerializer().Read(stream, content);

        Assert.Equal(sim.TutorialStep, loaded.TutorialStep);
        Assert.Equal("far", loaded.CurrentTutorialStep!.Id);
    }

    [Fact]
    public void NoSteps_MeansNoGuide()
    {
        var content = TestContent.Build(resources: Wood);
        var sim = Run(content, 12);

        Assert.True(sim.IsTutorialFinished);
        Assert.Null(sim.CurrentTutorialStep);
    }
}
