using CivDle.Core.Content;
using CivDle.Core.Sim;
using CivDle.Core.Tests.Support;
using CivDle.Core.World;
using Xunit;

namespace CivDle.Core.Tests.Sim;

/// <summary>
/// Sázení jako sledovaná činnost: metrika „zasazeno" musí růst se skutečně
/// zasazenými uzly, aby na ni šlo navěsit úkoly i achievementy. Bez toho zůstalo
/// sázení osamělým tlačítkem, které hru nikam neposouvá.
/// </summary>
public sealed class PlantingProgressTests
{
    [Fact]
    public void PlantedMetric_CountsWhatThePlayerPlanted()
    {
        var sim = NewSim();
        Assert.Equal(0, sim.EvaluateMetric(MetricKind.PlantedNodes, -1));

        Assert.Equal(PlacementResult.Ok, sim.TryPlant(3, 3));
        Assert.Equal(PlacementResult.Ok, sim.TryPlant(4, 3));

        Assert.Equal(2, sim.EvaluateMetric(MetricKind.PlantedNodes, -1));
    }

    [Fact]
    public void PlantingTheSameTileTwice_DoesNotInflateTheCount()
    {
        var sim = NewSim();
        Assert.Equal(PlacementResult.Ok, sim.TryPlant(3, 3));
        Assert.NotEqual(PlacementResult.Ok, sim.TryPlant(3, 3));

        Assert.Equal(1, sim.EvaluateMetric(MetricKind.PlantedNodes, -1));
    }

    [Fact]
    public void RealContent_HasQuestsAndAchievementsForPlanting()
    {
        var content = TestData.LoadRealContent();

        Assert.Contains(content.Quests.All, q => q.Condition.Kind == MetricKind.PlantedNodes);
        Assert.Contains(content.Achievements.All, a => a.Condition.Kind == MetricKind.PlantedNodes);
    }

    /// <summary>Svět z pevniny se surovinou na sázení a dost surovinami v zásobě.</summary>
    private static Simulation NewSim()
    {
        var content = TestContent.Build(
            resources: new[] { new Resource("wood", new RgbColor(140, 90, 40), 1000, 10000) });
        return new Simulation(content, new UniformTerrain(1));
    }
}
