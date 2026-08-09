using CivDle.Core.Content;
using CivDle.Core.Sim;
using CivDle.Core.Tests.Support;
using CivDle.Core.World;
using Xunit;

namespace CivDle.Core.Tests.Sim;

/// <summary>
/// Nákup upgradů Vzestupu po dávkách.
///
/// <para>U opakovatelných upgradů hráč utrácí stovky bodů a kupovat je po
/// jedné je jen klikání. Obrazovka Vzestupu se ptá simulace, kolik úrovní
/// na body opravdu vyjde a co dohromady stojí — počítat to v UI by znamenalo
/// mít pravidlo o rostoucí ceně na dvou místech.</para>
/// </summary>
public class PrestigeBatchTests
{
    private static readonly PrestigeConfig EarlyAscension =
        new(new GoalCondition(MetricKind.Population, -1, 5), MetricKind.Population, -1, 5);

    /// <summary>Opakovatelný upgrade: základ 10 bodů, každá další úroveň ×2.</summary>
    private static PrestigeUpgradeDef Doubling(string id = "industrious", int maxLevel = 10) =>
        new(id, "production_mult", 0.1, 10, Array.Empty<int>(), maxLevel, 2.0);

    private static Simulation NewSim(GameContent content) =>
        new(content, new UniformTerrain((byte)1));

    private static Simulation WithPoints(long points, out GameContent content, int maxLevel = 10)
    {
        content = TestContent.Build(prestige: EarlyAscension, prestigeUpgrades: new[] { Doubling(maxLevel: maxLevel) });
        var sim = NewSim(content);
        sim.DebugGrantPrestigePoints(points);
        return sim;
    }

    [Fact]
    public void AffordableLevels_StopsWhenPointsRunOut()
    {
        // 10 + 20 + 40 = 70; na čtvrtou úroveň (80) už nezbývá.
        var sim = WithPoints(75, out _);

        Assert.Equal(3, sim.AffordableUpgradeLevels(0, 5));
    }

    [Fact]
    public void AffordableLevels_RespectsTheRequestedBatch()
    {
        var sim = WithPoints(1_000_000, out _);

        Assert.Equal(5, sim.AffordableUpgradeLevels(0, 5));
    }

    [Fact]
    public void AffordableLevels_NeverExceedsMaxLevel()
    {
        // I s nekonečnem bodů se nekoupí víc, než kolik má upgrade úrovní.
        var sim = WithPoints(long.MaxValue / 4, out _, maxLevel: 3);

        Assert.Equal(3, sim.AffordableUpgradeLevels(0, int.MaxValue));
    }

    [Fact]
    public void AffordableLevels_IsZeroWithoutPoints()
    {
        var sim = WithPoints(5, out _);

        Assert.Equal(0, sim.AffordableUpgradeLevels(0, 5));
    }

    [Fact]
    public void BatchCost_MatchesWhatBuyingActuallyTakes()
    {
        // Cena na tlačítku musí sedět na to, co se hráči strhne — jinak je to lež.
        var sim = WithPoints(1000, out _);
        int levels = sim.AffordableUpgradeLevels(0, 4);
        long quoted = sim.UpgradeBatchCost(0, levels);
        long before = sim.PrestigePoints;

        for (int i = 0; i < levels; i++)
        {
            Assert.Equal(PlacementResult.Ok, sim.TryBuyUpgrade(0));
        }

        Assert.Equal(quoted, before - sim.PrestigePoints);
        Assert.Equal(levels, sim.UpgradeLevel(0));
    }

    [Fact]
    public void BatchCost_CountsFromTheCurrentLevel()
    {
        var sim = WithPoints(1000, out _);
        sim.TryBuyUpgrade(0); // za 10

        // Další dvě úrovně stojí 20 + 40, ne znovu 10 + 20.
        Assert.Equal(60, sim.UpgradeBatchCost(0, 2));
    }

    [Fact]
    public void MaxedUpgrade_OffersNothing()
    {
        var sim = WithPoints(long.MaxValue / 4, out _, maxLevel: 2);
        sim.TryBuyUpgrade(0);
        sim.TryBuyUpgrade(0);

        Assert.True(sim.IsUpgradeMaxed(0));
        Assert.Equal(0, sim.AffordableUpgradeLevels(0, int.MaxValue));
    }
}
