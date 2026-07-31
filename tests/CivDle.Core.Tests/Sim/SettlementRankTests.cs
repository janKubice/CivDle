using CivDle.Core.Content;
using CivDle.Core.Sim;
using CivDle.Core.Tests.Support;
using CivDle.Core.World;
using Xunit;

namespace CivDle.Core.Tests.Sim;

/// <summary>
/// Hierarchie sídel: osada → vesnice → městečko.
///
/// <para>Proč to stojí za testy: všechny milníky hry byly do teď globální čísla,
/// nic nebylo vázané na místo. Tady se ověřuje, že stupeň roste se shlukem,
/// že se povýšení hlásí <b>jednou</b> (a ne u každého sídla znovu) a že prázdný
/// žebříček nechá hru jako dřív.</para>
/// </summary>
public class SettlementRankTests
{
    private static SettlementRankLadder Ladder() => new(new[]
    {
        new SettlementRankDef("hamlet", 3),
        new SettlementRankDef("village", 8),
        new SettlementRankDef("town", 20),
    });

    private static Simulation NewSim(SettlementRankLadder? ladder = null)
    {
        var biomes = new[] { TestContent.WaterBiome(), TestContent.LandBiome("plain") };
        var resources = new[]
        {
            new Resource("wood", new RgbColor(1, 1, 1), StartAmount: 10_000, BaseStorage: 1_000_000),
        };

        var gameplay = TestContent.DefaultGameplay with
        {
            FoodPerPersonPerSecond = 0,
            PopulationGrowthPerSecond = 0,
            Settlements = new SettlementConfig(MinBuildings: 3, ClusterDistance: 2, UpdateIntervalTicks: 10),
        };

        // Vzestup hned od začátku — jeden z testů ověřuje, že nový svět zase
        // považuje první osadu za událost.
        var prestige = new PrestigeConfig(
            new GoalCondition(MetricKind.Population, -1, 1), MetricKind.Population, -1, 5);

        var content = TestContent.Build(
            biomes, 1, resources, gameplay: gameplay, prestige: prestige,
            settlementRanks: ladder ?? Ladder());
        return new Simulation(content, new UniformTerrain(1));
    }

    private static void Tick(Simulation sim, int ticks)
    {
        for (int i = 0; i < ticks; i++)
        {
            sim.Tick();
        }
    }

    /// <summary>Postaví shluk budov v mřížce, aby drželi pohromadě.</summary>
    private static void BuildCluster(Simulation sim, int count, int originX = 0, int originY = 0)
    {
        int placed = 0;
        for (int y = 0; placed < count; y++)
        {
            for (int x = 0; x < 10 && placed < count; x++)
            {
                if (sim.TryPlaceBuilding(0, originX + x, originY + y) == PlacementResult.Ok)
                {
                    placed++;
                }
            }
        }
    }

    private static int DrainRankNotifications(Simulation sim)
    {
        int count = 0;
        while (sim.TryDequeueNotification(out var note))
        {
            if (note.TitleKey == "toast.settlementRank")
            {
                count++;
            }
        }

        return count;
    }

    [Fact]
    public void ASmallClusterIsOnlyAHamlet()
    {
        var sim = NewSim();
        BuildCluster(sim, 4);

        Tick(sim, 30);

        var settlement = Assert.Single(sim.Settlements);
        Assert.Equal(0, settlement.RankIndex); // hamlet
    }

    [Fact]
    public void GrowingTheClusterPromotesIt()
    {
        var sim = NewSim();
        BuildCluster(sim, 4);
        Tick(sim, 30);
        Assert.Equal(0, sim.Settlements[0].RankIndex);

        BuildCluster(sim, 6, originY: 1); // dohromady 10 → vesnice
        Tick(sim, 30);

        Assert.Equal(1, sim.Settlements[0].RankIndex);
    }

    [Fact]
    public void PromotionIsAnnouncedOnlyTheFirstTime()
    {
        // Kdyby se hlásilo u každého sídla zvlášť, hráč by při rozrůstání dostal
        // deset stejných hlášek za sebou — a z události by byl šum.
        var sim = NewSim();
        BuildCluster(sim, 4, originX: 0);
        Tick(sim, 30);
        Assert.Equal(1, DrainRankNotifications(sim)); // první osada = událost

        BuildCluster(sim, 4, originX: 40); // druhá osada, stejný stupeň
        Tick(sim, 30);

        Assert.Equal(2, sim.Settlements.Count);
        Assert.Equal(0, DrainRankNotifications(sim)); // podruhé už ne
    }

    [Fact]
    public void TheHighestRankIsRemembered()
    {
        var sim = NewSim();
        BuildCluster(sim, 10);
        Tick(sim, 30);

        Assert.Equal(1, sim.HighestSettlementRank);
        Assert.True(DrainRankNotifications(sim) >= 1);
    }

    [Fact]
    public void AscendingMakesTheFirstHamletAnEventAgain()
    {
        var sim = NewSim();
        BuildCluster(sim, 10);
        Tick(sim, 30);
        Assert.True(sim.HighestSettlementRank >= 0);

        sim.TryAscend();

        Assert.Equal(-1, sim.HighestSettlementRank);
    }

    [Fact]
    public void AnEmptyLadderLeavesSettlementsAsTheyWere()
    {
        var sim = NewSim(SettlementRankLadder.Empty);
        BuildCluster(sim, 10);

        Tick(sim, 30);

        var settlement = Assert.Single(sim.Settlements);
        Assert.Equal(-1, settlement.RankIndex);
        Assert.Equal(0, DrainRankNotifications(sim));
    }

    [Fact]
    public void ABigBuildingNeedsABigSettlement()
    {
        // Letiště nepatří do osady o třech chalupách — a jakmile město doroste,
        // jde postavit bez dalšího zásahu. Roste to samo se zástavbou.
        var biomes = new[] { TestContent.WaterBiome(), TestContent.LandBiome("plain") };
        var resources = new[]
        {
            new Resource("wood", new RgbColor(1, 1, 1), StartAmount: 10_000, BaseStorage: 1_000_000),
        };

        var hut = TestContent.SimpleBuilding("hut", biomes.Length);
        var airport = TestContent.SimpleBuilding("airport", biomes.Length) with { MinSettlementRank = 1 };

        var gameplay = TestContent.DefaultGameplay with
        {
            FoodPerPersonPerSecond = 0,
            PopulationGrowthPerSecond = 0,
            Settlements = new SettlementConfig(MinBuildings: 3, ClusterDistance: 2, UpdateIntervalTicks: 10),
        };

        var content = TestContent.Build(
            biomes, 1, resources, new[] { hut, airport }, gameplay, settlementRanks: Ladder());
        var sim = new Simulation(content, new UniformTerrain(1));

        // Čtyři chalupy = osada (stupeň 0), na letiště (stupeň 1) to nestačí.
        for (int i = 0; i < 4; i++)
        {
            sim.TryPlaceBuilding(0, i, 0);
        }

        Tick(sim, 30);
        Assert.Equal(PlacementResult.SettlementTooSmall, sim.CanPlace(1, 2, 3));

        // Dorosteme na vesnici (8 budov) → letiště projde.
        for (int i = 0; i < 6; i++)
        {
            sim.TryPlaceBuilding(0, i, 1);
        }

        Tick(sim, 30);
        Assert.Equal(PlacementResult.Ok, sim.CanPlace(1, 2, 3));
    }

    [Fact]
    public void TheLadderPicksTheHighestReachedStep()
    {
        var ladder = Ladder();

        Assert.Equal(-1, ladder.RankFor(2));
        Assert.Equal(0, ladder.RankFor(3));
        Assert.Equal(0, ladder.RankFor(7));
        Assert.Equal(1, ladder.RankFor(8));
        Assert.Equal(2, ladder.RankFor(500)); // nad nejvyšším prahem zůstává nejvyšší
    }
}
