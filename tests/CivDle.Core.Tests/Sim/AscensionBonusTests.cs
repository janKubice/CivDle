using CivDle.Core.Content;
using CivDle.Core.Sim;
using CivDle.Core.Tests.Support;
using CivDle.Core.World;
using Xunit;

namespace CivDle.Core.Tests.Sim;

/// <summary>
/// Nové trvalé bonusy Vzestupu (krit, úlovek života, štěstí na nálezy, síla
/// slavnosti, sleva na výzkum) a rostoucí práh Vzestupu. Bonusy se nastavují
/// stejnou cestou jako ve hře — koupí upgradu — takže se testuje i to, že je
/// behavior-ID z dat vůbec napojené na simulaci.
/// </summary>
public sealed class AscensionBonusTests
{
    /// <summary>Vzestup dostupný hned (práh = startovní populace), každý další 4× dražší.</summary>
    private static readonly PrestigeConfig GrowingAscension =
        new(new GoalCondition(MetricKind.Population, -1, 5), MetricKind.Population, -1, 1, RequirementGrowth: 4.0);

    [Fact]
    public void AscensionRequirement_GrowsWithEachAscension()
    {
        var sim = new Simulation(TestContent.Build(prestige: GrowingAscension), new UniformTerrain(1));

        long first = sim.AscensionRequirement();
        Assert.Equal(PlacementResult.Ok, sim.TryAscend());
        long second = sim.AscensionRequirement();

        Assert.Equal(5, first);
        Assert.Equal(20, second);
        Assert.False(sim.CanAscend(), "Druhý Vzestup nesmí být dostupný hned po prvním.");
    }

    [Fact]
    public void RealContent_FirstAscensionIsNotTrivial()
    {
        var content = TestData.LoadRealContent();
        var sim = new Simulation(content, new UniformTerrain(1));

        // Vzestup po pár chalupách byl nesmysl — první musí stát za práci.
        Assert.True(sim.AscensionRequirement() >= 200,
            $"První Vzestup je moc levný ({sim.AscensionRequirement()}).");
        Assert.True(content.Prestige.RequirementGrowth > 1.0, "Práh Vzestupu musí růst.");
    }

    [Fact]
    public void JackpotUpgrade_TurnsHarvestsIntoTheCatchOfALifetime()
    {
        var sim = HarvestWorld(Upgrade("leviathan", "jackpot_chance", 1.0));
        BuyFirstUpgrade(sim);

        Assert.True(sim.TryHarvest(0, 0, out _, out int amount, out var outcome));
        Assert.Equal(HarvestOutcome.Jackpot, outcome);
        Assert.Equal(50, amount); // 2 základ × 25 (výchozí násobič úlovku)
    }

    [Fact]
    public void WithoutTheUpgrade_NoHarvestIsEverAJackpot()
    {
        var sim = HarvestWorld(Upgrade("leviathan", "jackpot_chance", 1.0));

        for (int i = 0; i < 200; i++)
        {
            sim.TryHarvest(i, 0, out _, out _, out var outcome);
            Assert.NotEqual(HarvestOutcome.Jackpot, outcome);
        }
    }

    [Fact]
    public void CritChanceBonus_AddsToTheConfiguredChance()
    {
        // Základní šance 0 + bonus 1.0 = krit pokaždé; jinak by šlo o náhodu.
        var sim = HarvestWorld(Upgrade("lucky_hands", "crit_chance", 1.0));
        BuyFirstUpgrade(sim);

        Assert.True(sim.TryHarvest(0, 0, out _, out int amount, out var outcome));
        Assert.Equal(HarvestOutcome.Crit, outcome);
        Assert.Equal(6, amount); // 2 základ × 3 krit
    }

    [Fact]
    public void ResearchDiscount_MakesTechCheaperButNeverFree()
    {
        var sim = new Simulation(
            TestContent.Build(prestige: GrowingAscension,
                prestigeUpgrades: new[] { Upgrade("scholars", "research_discount", 0.25) }),
            new UniformTerrain(1));

        Assert.Equal(100, sim.ResearchCost(100));

        BuyFirstUpgrade(sim);
        Assert.Equal(75, sim.ResearchCost(100));
        Assert.Equal(1, sim.ResearchCost(1)); // zdarma nikdy
    }

    [Fact]
    public void FestivalPower_MultipliesTheBoost()
    {
        var content = TestContent.Build(prestige: GrowingAscension,
            prestigeUpgrades: new[] { Upgrade("carnival", "festival_power", 1.0) });
        var plainSim = new Simulation(content, new UniformTerrain(1));
        plainSim.TryStartBoost();
        double plain = plainSim.BoostMultiplier;

        var boosted = new Simulation(content, new UniformTerrain(1));
        BuyFirstUpgrade(boosted);
        boosted.TryStartBoost();

        Assert.Equal(plain * 2.0, boosted.BoostMultiplier, 6);
    }

    [Fact]
    public void DiscoveryLuck_MakesCachesDenser()
    {
        var content = TestContent.Build(prestige: GrowingAscension,
            prestigeUpgrades: new[] { Upgrade("pathfinders", "discovery_luck", 3.0) });

        var plainSim = new Simulation(content, new UniformTerrain(1));
        int plain = CountDiscoveries(plainSim);

        var luckySim = new Simulation(content, new UniformTerrain(1));
        BuyFirstUpgrade(luckySim);
        int lucky = CountDiscoveries(luckySim);

        Assert.True(lucky > plain, $"Štěstí na nálezy nic nezměnilo ({plain} → {lucky}).");
    }

    /// <summary>Vzestoupí (body z populace) a koupí první upgrade — jako by to udělal hráč.</summary>
    private static void BuyFirstUpgrade(Simulation sim)
    {
        Assert.Equal(PlacementResult.Ok, sim.TryAscend());
        Assert.Equal(PlacementResult.Ok, sim.TryBuyUpgrade(0));
    }

    private static int CountDiscoveries(Simulation sim)
    {
        int found = 0;
        for (int y = 0; y < 80; y++)
        {
            for (int x = 0; x < 80; x++)
            {
                if (sim.IsDiscoveryTile(x, y))
                {
                    found++;
                }
            }
        }

        return found;
    }

    private static PrestigeUpgradeDef Upgrade(string id, string effect, double magnitude) =>
        new(id, effect, magnitude, 1, Array.Empty<int>());

    /// <summary>Svět s jediným lesním biomem (klik = 2 dřevo) a jedním upgradem ke koupi.</summary>
    private static Simulation HarvestWorld(PrestigeUpgradeDef upgrade)
    {
        var biomes = new[]
        {
            TestContent.WaterBiome(),
            new Biome("forest", new RgbColor(20, 80, 20), 0f, IsWater: false,
                DepthRange: ValueRange.Full, ElevationRange: ValueRange.Full,
                MoistureRange: ValueRange.Full, TemperatureRange: ValueRange.Full,
                ClickYield: new ClickYield(0, 2)),
        };

        var content = TestContent.Build(
            biomes: biomes,
            resources: new[] { new Resource("wood", new RgbColor(140, 90, 40), 0, 100000) },
            prestige: GrowingAscension,
            prestigeUpgrades: new[] { upgrade },
            gameplay: TestContent.DefaultGameplay with { Harvest = new HarvestConfig(0.0, 3.0) });

        return new Simulation(content, new UniformTerrain(1));
    }
}
