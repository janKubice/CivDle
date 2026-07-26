using CivDle.Core.Content;
using CivDle.Core.Sim;
using CivDle.Core.Tests.Support;
using CivDle.Core.World;
using Xunit;

namespace CivDle.Core.Tests.Sim;

/// <summary>
/// Slavnost (dočasný boost) a kritický sběr: boost se spustí, násobí, vyprší
/// a má cooldown; krit deterministicky násobí výnos sběru.
/// </summary>
public class BoostCritTests
{
    private static Biome[] ForestWorld() => new[]
    {
        TestContent.WaterBiome(),
        new Biome("forest", new RgbColor(20, 80, 20), 0f, IsWater: false,
            DepthRange: ValueRange.Full, ElevationRange: ValueRange.Full, MoistureRange: ValueRange.Full, TemperatureRange: ValueRange.Full,
            ClickYield: new ClickYield(0, 2)),
    };

    private static Resource[] Wood() => new[] { new Resource("wood", new RgbColor(140, 90, 40), 0, 1000) };

    [Fact]
    public void Boost_Starts_Multiplies_Expires_ThenCooldown()
    {
        var sim = new Simulation(TestContent.Build(), new UniformTerrain((byte)1));

        Assert.True(sim.CanStartBoost);
        Assert.True(sim.TryStartBoost());
        Assert.True(sim.IsBoostActive);
        Assert.Equal(2.0, sim.BoostMultiplier, 3);
        Assert.False(sim.TryStartBoost()); // už běží

        for (int i = 0; i < 300; i++) // 30 s × 10 Hz
        {
            sim.Tick();
        }

        Assert.False(sim.IsBoostActive);
        Assert.Equal(1.0, sim.BoostMultiplier, 3);
        Assert.False(sim.CanStartBoost); // cooldown ještě běží
    }

    [Fact]
    public void Boost_ScalesHarvestYield()
    {
        var content = TestContent.Build(biomes: ForestWorld(), resources: Wood());
        var sim = new Simulation(content, new UniformTerrain((byte)1));
        sim.TryStartBoost();

        Assert.True(sim.TryHarvest(0, 0, out _, out int amount));
        Assert.Equal(4, amount); // 2 základ × 2.0 slavnost
    }

    [Fact]
    public void Crit_MultipliesYield_WhenGuaranteed()
    {
        var content = TestContent.Build(
            biomes: ForestWorld(),
            resources: Wood(),
            gameplay: TestContent.DefaultGameplay with { Harvest = new HarvestConfig(1.0, 3.0) });
        var sim = new Simulation(content, new UniformTerrain((byte)1));

        Assert.True(sim.TryHarvest(0, 0, out _, out int amount, out bool crit));
        Assert.True(crit);
        Assert.Equal(6, amount); // 2 základ × 3 krit
    }
}
