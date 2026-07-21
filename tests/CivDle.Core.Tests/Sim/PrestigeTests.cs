using CivDle.Core.Content;
using CivDle.Core.Save;
using CivDle.Core.Sim;
using CivDle.Core.Tests.Support;
using CivDle.Core.World;
using Xunit;

namespace CivDle.Core.Tests.Sim;

/// <summary>
/// Vzestup (prestige): udělení bodů a reset éry, koupě trvalých upgradů a jejich
/// bonusy, prerekvizity a přežití savem (v5). Používá syntetický obsah s nízkou
/// podmínkou Vzestupu, ať se dá odtestovat bez dlouhého růstu populace.
/// </summary>
public class PrestigeTests
{
    // Vzestup dostupný hned (podmínka = startovní populace), body = populace ÷ 5.
    private static readonly PrestigeConfig EarlyAscension =
        new(new GoalCondition(MetricKind.Population, -1, 5), MetricKind.Population, -1, 5);

    private static PrestigeUpgradeDef Upgrade(string id, string effect, double magnitude, int cost, params int[] prereqs) =>
        new(id, effect, magnitude, cost, prereqs);

    private static GameContent PrestigeContent(params PrestigeUpgradeDef[] upgrades) =>
        TestContent.Build(prestige: EarlyAscension, prestigeUpgrades: upgrades);

    private static Simulation NewSim(GameContent content) =>
        new(content, new UniformTerrain((byte)1)); // biom 1 = pevnina

    [Fact]
    public void Ascend_GrantsPoints_IncrementsLevel_ResetsEra()
    {
        var content = PrestigeContent();
        var sim = NewSim(content);
        sim.TryPlaceBuilding(0, 2, 2); // něco postavíme, ať je co resetovat

        Assert.True(sim.CanAscend());
        Assert.Equal(1, sim.PendingAscensionPoints()); // pop 5 ÷ 5

        Assert.Equal(PlacementResult.Ok, sim.TryAscend());

        Assert.Equal(1, sim.AscensionLevel);
        Assert.Equal(1, sim.PrestigePoints);
        Assert.Equal(0, sim.Buildings.Length); // éra se zresetovala
    }

    [Fact]
    public void BuyUpgrade_DeductsPoints_RaisesBonus()
    {
        var content = PrestigeContent(Upgrade("industrious", "production_mult", 0.30, 1));
        var sim = NewSim(content);
        sim.TryAscend(); // 1 bod

        Assert.Equal(PlacementResult.Ok, sim.TryBuyUpgrade(0));
        Assert.Equal(0, sim.PrestigePoints);
        Assert.True(sim.IsUpgradePurchased(0));
        Assert.Equal(1.30, sim.Bonuses.ProductionMult, 3);
    }

    [Fact]
    public void BuyUpgrade_BlockedByPrerequisite()
    {
        var content = PrestigeContent(
            Upgrade("fertile", "growth_mult", 0.35, 1),
            Upgrade("spacious", "housing_mult", 0.40, 1, 0)); // vyžaduje fertile (index 0)
        var sim = NewSim(content);
        sim.TryAscend();

        Assert.Equal(PlacementResult.NotUnlocked, sim.CanBuyUpgrade(1));
        sim.TryBuyUpgrade(0);
        // po koupi prereku (a s dost body z dalšího Vzestupu) už jde koupit
        sim.TryAscend();
        Assert.Equal(PlacementResult.Ok, sim.CanBuyUpgrade(1));
    }

    [Fact]
    public void Harvest_ScalesWithHarvestBonus()
    {
        var biomes = new[]
        {
            TestContent.WaterBiome(),
            new Biome("forest", new RgbColor(20, 80, 20), 0f, IsWater: false,
                DepthRange: ValueRange.Full, ElevationRange: ValueRange.Full, MoistureRange: ValueRange.Full,
                ClickYield: new ClickYield(0, 2)),
        };
        var content = TestContent.Build(
            biomes: biomes,
            resources: new[] { new Resource("wood", new RgbColor(140, 90, 40), 0, 1000) },
            prestige: EarlyAscension,
            prestigeUpgrades: new[] { Upgrade("sharp", "harvest_mult", 0.50, 1) });
        var sim = NewSim(content);
        sim.TryAscend();
        sim.TryBuyUpgrade(0); // +50 % sběr

        Assert.True(sim.TryHarvest(0, 0, out _, out int amount));
        Assert.Equal(3, amount); // round(2 × 1.5)
    }

    [Fact]
    public void SaveRoundtrip_PersistsPrestige()
    {
        var content = PrestigeContent(Upgrade("industrious", "production_mult", 0.30, 1));
        var sim = NewSim(content);
        sim.TryAscend();
        sim.TryBuyUpgrade(0);

        var metadata = new SaveMetadata(7, "s", "test", DateTime.UtcNow);
        var stream = new MemoryStream();
        new SaveGameSerializer().Write(stream, sim, metadata);
        stream.Position = 0;
        var (loaded, _) = new SaveGameSerializer().Read(stream, content);

        Assert.Equal(1, loaded.AscensionLevel);
        Assert.Equal(0, loaded.PrestigePoints);
        Assert.True(loaded.IsUpgradePurchased(0));
        Assert.Equal(1.30, loaded.Bonuses.ProductionMult, 3);
    }
}
