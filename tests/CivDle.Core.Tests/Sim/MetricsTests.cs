using CivDle.Core.Sim;
using CivDle.Core.Tests.Support;
using CivDle.Core.World;
using Xunit;

namespace CivDle.Core.Tests.Sim;

/// <summary>
/// Metriky pro cíle a achievementy: čtou skutečný stav simulace (populace,
/// budovy, kumulativní sběr). Základ, na kterém stojí úkoly i achievementy.
/// </summary>
public class MetricsTests
{
    [Fact]
    public void Metric_Population_MatchesSimulation()
    {
        var content = TestData.LoadRealContent();
        var sim = new Simulation(content, new UniformTerrain((byte)content.Biomes.IndexOf("grassland")));

        Assert.Equal((long)sim.Population, sim.EvaluateMetric(MetricKind.Population, -1));
    }

    [Fact]
    public void Metric_BuildingCounts_ReflectPlacedBuildings()
    {
        var content = TestData.LoadRealContent();
        var sim = new Simulation(content, new UniformTerrain((byte)content.Biomes.IndexOf("grassland")));
        int house = content.Buildings.IndexOf("house");

        Assert.Equal(0, sim.EvaluateMetric(MetricKind.TotalBuildings, -1));

        sim.TryPlaceBuilding(house, 2, 2);
        sim.TryPlaceBuilding(house, 5, 5);

        Assert.Equal(2, sim.EvaluateMetric(MetricKind.TotalBuildings, -1));
        Assert.Equal(2, sim.EvaluateMetric(MetricKind.BuildingOfType, house));
    }

    [Fact]
    public void Metric_Harvested_AccumulatesAcrossClicks()
    {
        var content = TestData.LoadRealContent();
        var sim = new Simulation(content, new UniformTerrain((byte)content.Biomes.IndexOf("forest")));
        int wood = content.Resources.IndexOf("wood");

        Assert.Equal(0, sim.EvaluateMetric(MetricKind.Harvested, wood));

        sim.TryHarvest(0, 0, out _, out int a);
        sim.TryHarvest(1, 0, out _, out int b);

        Assert.Equal(a + b, sim.EvaluateMetric(MetricKind.Harvested, wood));
        Assert.Equal(a + b, sim.GetHarvestedTotal(wood));
    }
}
