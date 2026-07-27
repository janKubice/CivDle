using CivDle.Core.Content;
using CivDle.Core.Save;
using CivDle.Core.Sim;
using CivDle.Core.Tests.Support;
using CivDle.Core.World;
using Xunit;

namespace CivDle.Core.Tests.Sim;

/// <summary>
/// Milníky: každý se oslaví jen jednou za hru a stav přežije save. Ukládají se
/// podle ID, takže přeskládání dat nezpůsobí opakovanou oslavu.
/// </summary>
public class MilestoneTests
{
    private static readonly Resource[] Wood =
    {
        new("wood", new RgbColor(140, 90, 40), StartAmount: 50, BaseStorage: 1000),
    };

    private static readonly MilestoneDef[] Two =
    {
        new("start", new GoalCondition(MetricKind.Population, -1, 5)),
        new("far", new GoalCondition(MetricKind.Population, -1, 5000)),
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
    public void ReachedMilestone_IsAnnouncedOnce()
    {
        var content = TestContent.Build(resources: Wood, milestones: Two);
        var sim = Run(content, 40);

        Assert.True(sim.IsMilestoneReached(0));
        Assert.False(sim.IsMilestoneReached(1));

        int announcements = 0;
        while (sim.TryDequeueNotification(out var note))
        {
            if (note.SubjectKey == "milestone.start")
            {
                announcements++;
            }
        }

        Assert.Equal(1, announcements);
    }

    [Fact]
    public void UnreachedMilestone_StaysQuiet()
    {
        var content = TestContent.Build(resources: Wood, milestones: Two);
        var sim = Run(content, 60);

        Assert.False(sim.IsMilestoneReached(1));
    }

    [Fact]
    public void SaveRoundtrip_DoesNotCelebrateAgain()
    {
        var content = TestContent.Build(resources: Wood, milestones: Two);
        var sim = Run(content, 40);

        var stream = new MemoryStream();
        new SaveGameSerializer().Write(stream, sim, new SaveMetadata(1, "s", "test", DateTime.UtcNow));
        stream.Position = 0;
        var (loaded, _) = new SaveGameSerializer().Read(stream, content);

        Assert.True(loaded.IsMilestoneReached(0));

        // Odtikej dál — už dosažený milník se nesmí ohlásit znovu.
        for (int i = 0; i < 40; i++)
        {
            loaded.Tick();
        }

        while (loaded.TryDequeueNotification(out var note))
        {
            Assert.NotEqual("milestone.start", note.SubjectKey);
        }
    }

    [Fact]
    public void WithoutMilestoneData_NothingBreaks()
    {
        var sim = Run(TestContent.Build(resources: Wood), 30);

        Assert.Empty(sim.ReachedMilestoneIndicesForTest());
    }
}
