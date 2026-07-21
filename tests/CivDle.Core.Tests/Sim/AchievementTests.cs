using CivDle.Core.Content;
using CivDle.Core.Sim;
using CivDle.Core.Tests.Support;
using CivDle.Core.World;
using Xunit;

namespace CivDle.Core.Tests.Sim;

/// <summary>
/// Achievementy: odemknou se při dosažení metriky (jednou, s oznámením), a už
/// odemčené (naseedované z profilu) se v nové hře znovu nespouštějí.
/// </summary>
public class AchievementTests
{
    // Podmínka splněná hned (populace ≥ startovní).
    private static readonly AchievementDef[] EasyAchievement =
    {
        new("a0", new GoalCondition(MetricKind.Population, -1, 5), Hidden: false),
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
    public void Achievement_Unlocks_AndNotifies_WhenConditionMet()
    {
        var content = TestContent.Build(achievements: EasyAchievement);
        var sim = Run(content, 12);

        Assert.True(sim.IsAchievementUnlocked(0));
        Assert.True(sim.TryDequeueNotification(out var note));
        Assert.Equal(NotificationKind.AchievementUnlocked, note.Kind);
        Assert.Equal(content.Achievements[0].NameKey, note.SubjectKey);
    }

    [Fact]
    public void SeededAchievement_DoesNotRefire()
    {
        var content = TestContent.Build(achievements: EasyAchievement);
        var sim = new Simulation(content, new UniformTerrain((byte)1));
        sim.SeedUnlockedAchievements(new[] { "a0" }); // už odemčeno z profilu

        for (int i = 0; i < 12; i++)
        {
            sim.Tick();
        }

        Assert.True(sim.IsAchievementUnlocked(0));
        Assert.False(sim.TryDequeueNotification(out _)); // žádné nové oznámení
    }
}
