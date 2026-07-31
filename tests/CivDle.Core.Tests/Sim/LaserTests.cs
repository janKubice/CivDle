using CivDle.Core.Content;
using CivDle.Core.Sim;
using CivDle.Core.Tests.Support;
using CivDle.Core.World;
using Xunit;

namespace CivDle.Core.Tests.Sim;

/// <summary>
/// Orbitální těžba: pozdní podoba ručního sběru. Laser tu činnost nezruší, jen
/// ji promění — proto je za bránou a proto má sazbu v datech.
///
/// <para>Testuje se hlavně to, co by tiše rozbilo balanc nebo začátek hry:
/// bez odemčení nesmí být dostupná, bez dat musí zůstat úplně vypnutá, a příliš
/// rychlý paprsek musí loader odmítnout.</para>
/// </summary>
public class LaserTests
{
    private static GameContent Content(LaserConfig? laser, params FeatureDef[] features)
    {
        var gameplay = TestContent.DefaultGameplay with { LaserOrNull = laser };
        return TestContent.Build(gameplay: gameplay, features: features);
    }

    private static Simulation NewSim(GameContent content) => new(content, new UniformTerrain(1));

    [Fact]
    public void WithoutDataTheLaserStaysOff()
    {
        // Starší data laser neznají — hra se musí chovat jako dřív.
        var sim = NewSim(Content(laser: null));

        Assert.False(sim.LaserUnlocked);
    }

    [Fact]
    public void ALockedFeatureKeepsTheLaserOff()
    {
        // Bez brány by paprsek platil od první minuty a z ručního sběru by
        // nezbylo nic k odemykání.
        var gate = new FeatureDef("laser", new GoalCondition(MetricKind.TotalBuildings, -1, 3));
        var sim = NewSim(Content(new LaserConfig(8, 1, "laser"), gate));

        Assert.False(sim.LaserUnlocked);
    }

    [Fact]
    public void MeetingTheConditionUnlocksTheLaser()
    {
        var gate = new FeatureDef("laser", new GoalCondition(MetricKind.TotalBuildings, -1, 2));
        var sim = NewSim(Content(new LaserConfig(8, 1, "laser"), gate));

        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuilding(0, 2, 2));
        Assert.False(sim.LaserUnlocked);
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuilding(0, 6, 6));

        Assert.True(sim.LaserUnlocked);
    }

    [Fact]
    public void ARateOfZeroMeansTheLayerIsOff()
    {
        var sim = NewSim(Content(LaserConfig.Disabled));

        Assert.False(sim.LaserUnlocked);
    }

    [Fact]
    public void TheRateTranslatesToATickInterval()
    {
        var config = new LaserConfig(8, 1, "laser");

        Assert.Equal(0.125, config.SecondsPerHarvest, 6);
        Assert.True(config.IsEnabled);
    }

    [Fact]
    public void RealDataGatesTheLaserBehindResearch()
    {
        // Laser má být odměna za dojití daleko, ne startovní vybavení.
        var content = TestData.LoadRealContent();
        var sim = new Simulation(content, new UniformTerrain((byte)content.Biomes.IndexOf("grassland")));

        Assert.True(content.Gameplay.Laser.IsEnabled, "Herní data mají laser nabízet.");
        Assert.False(sim.LaserUnlocked, "Na začátku hry laser dostupný být nesmí.");
    }
}
