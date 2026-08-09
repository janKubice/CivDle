using CivDle.Core.Content;
using CivDle.Core.Sim;
using CivDle.Core.Tests.Support;
using CivDle.Core.World;
using Xunit;

namespace CivDle.Core.Tests.Sim;

/// <summary>
/// Tempo guvernéra: stavba i vylepšování s vymaxovanými bonusy Vzestupu.
///
/// <para>Tady byla chyba, kvůli které si hráč stěžoval, že „auto building je
/// i s vymaxovanými upgrady extrémně pomalý". Rozpočet staveb na interval se
/// počítal přes <c>(int)Math.Round(...)</c> uprostřed výpočtu — jenže bonus se
/// skládá násobně přes desítky úrovní, takže na maximu vyšlo číslo mimo rozsah
/// <c>int</c>. Po přetečení ho <c>Math.Max(1, …)</c> srazil na jednu jedinou
/// stavbu za interval: <b>čím víc vylepšení, tím pomaleji se stavělo.</b></para>
/// </summary>
public class GovernorPaceTests
{
    private static readonly Resource[] Planks =
    {
        new("planks", new RgbColor(200, 170, 110), StartAmount: 10_000_000, BaseStorage: 100_000_000),
    };

    private static readonly PrestigeConfig EarlyAscension =
        new(new GoalCondition(MetricKind.Population, -1, 5), MetricKind.Population, -1, 5);

    /// <param name="speedLevels">Kolik úrovní tempa auto-stavby se dá koupit.</param>
    private static GameContent Content(int speedLevels)
    {
        var house = new BuildingDef(
            "house", "housing", new RgbColor(180, 100, 60), 1, 1,
            WorkerSlots: 0, HousingCapacity: 4,
            BuildCost: new[] { new ResourceAmount(0, 1) },
            Recipe: null,
            AllowedBiomes: new[] { false, true },
            StorageBonus: Array.Empty<ResourceAmount>(),
            AutoBuild: true, Buildable: true,
            UpgradesToIndex: -1, UpgradeCost: Array.Empty<ResourceAmount>(),
            PowerSupply: 0, PowerDemand: 0);

        var faster = new PrestigeUpgradeDef(
            "master_builders", "autobuild_speed", 0.25, 1, Array.Empty<int>(),
            MaxLevel: speedLevels, CostGrowth: 1.0);

        // Guvernérova správa se odemyká výzkumem; bez těchhle tří technologií
        // je stupeň vylepšování vždycky nula a o tempu se nedá nic zjistit.
        var techs = new[]
        {
            Tech(Simulation.GovernorTechId),
            Tech(Simulation.GovernorLevel2TechId),
            Tech(Simulation.GovernorLevel3TechId),
        };

        return TestContent.Build(
            biomes: new[] { TestContent.WaterBiome(), TestContent.LandBiome("grass") },
            resources: Planks,
            buildings: new[] { house },
            techs: techs,
            prestige: EarlyAscension,
            prestigeUpgrades: new[] { faster });
    }

    private static TechDef Tech(string id) => new(
        id, new[] { new ResourceAmount(0, 1) }, Array.Empty<int>(), Array.Empty<int>(), string.Empty, 0);

    /// <summary>Vyzkoumá guvernéra, ať se dá stupeň vylepšování vůbec nastavit.</summary>
    private static void UnlockGovernor(Simulation sim, GameContent content)
    {
        for (int i = 0; i < content.Techs.Count; i++)
        {
            Assert.Equal(PlacementResult.Ok, sim.TryResearch(i));
        }
    }

    private static Simulation WithSpeed(int levels, out GameContent content)
    {
        content = Content(Math.Max(1, levels));
        var sim = new Simulation(content, new UniformTerrain((byte)1));
        sim.DebugGrantPrestigePoints(10_000);
        for (int i = 0; i < levels; i++)
        {
            Assert.Equal(PlacementResult.Ok, sim.TryBuyUpgrade(0));
        }

        return sim;
    }

    [Fact]
    public void MaxedSpeedDoesNotCollapseTheBudget()
    {
        // Jádro chyby: na maximu se stavělo POMALEJI než bez bonusu.
        var slow = WithSpeed(0, out _);
        var fast = WithSpeed(60, out _);

        Assert.True(fast.AutoBuildBudget > slow.AutoBuildBudget,
            $"s vymaxovaným tempem je rozpočet {fast.AutoBuildBudget}, bez bonusu {slow.AutoBuildBudget}");
        Assert.True(fast.AutoBuildBudget >= 10,
            $"vymaxované tempo dalo jen {fast.AutoBuildBudget} staveb za interval");
    }

    [Fact]
    public void SpeedFirstShortensTheIntervalThenRaisesTheBudget()
    {
        var plain = WithSpeed(0, out _);
        var quick = WithSpeed(10, out _);

        Assert.True(quick.AutoBuildInterval < plain.AutoBuildInterval, "interval se má zkrátit jako první");
        Assert.True(quick.AutoBuildBudget >= plain.AutoBuildBudget);
    }

    [Fact]
    public void TheBudgetIsCappedSoAFrameCannotStall()
    {
        var sim = WithSpeed(60, out _);

        Assert.InRange(sim.AutoBuildBudget, 1, 2048);
    }

    [Fact]
    public void MaxedSpeedActuallyBuildsFaster()
    {
        // Nejen číslo v property — opravdu postavené domy.
        var slow = WithSpeed(0, out var slowContent);
        var fast = WithSpeed(30, out var fastContent);
        slow.TryPlaceBuilding(0, 0, 0);
        fast.TryPlaceBuilding(0, 0, 0);

        for (int i = 0; i < 600; i++)
        {
            slow.Tick();
            fast.Tick();
        }

        Assert.True(fast.Buildings.Length > slow.Buildings.Length,
            $"rychlý guvernér postavil {fast.Buildings.Length}, pomalý {slow.Buildings.Length}");
    }

    [Fact]
    public void UpgradeBudgetGrowsWithTheSameBonus()
    {
        // Vylepšování stálo na třech budovách za interval bez ohledu na bonusy.
        var slow = WithSpeed(0, out var slowContent);
        var fast = WithSpeed(60, out var fastContent);
        UnlockGovernor(slow, slowContent);
        UnlockGovernor(fast, fastContent);
        slow.SetAutoUpgradeLevel(3);
        fast.SetAutoUpgradeLevel(3);

        Assert.True(fast.AutoUpgradeBudget > slow.AutoUpgradeBudget,
            $"vylepšování: rychlý {fast.AutoUpgradeBudget}, pomalý {slow.AutoUpgradeBudget}");
    }

    [Fact]
    public void WithoutTheGovernorNothingIsUpgraded()
    {
        var sim = WithSpeed(60, out var content);
        UnlockGovernor(sim, content);
        sim.SetAutoUpgradeLevel(0);

        Assert.Equal(0, sim.AutoUpgradeBudget);
    }
}
