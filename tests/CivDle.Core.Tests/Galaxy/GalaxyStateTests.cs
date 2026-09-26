using CivDle.Core.Content;
using CivDle.Core.Galaxy;
using CivDle.Core.Sim;
using CivDle.Core.Tests.Support;
using CivDle.Core.World;
using Xunit;

namespace CivDle.Core.Tests.Galaxy;

/// <summary>
/// Stav galaxie (svety-design.md 2.4, 2.6, 7.5): jedny hodiny, hvězdy
/// a odemykání světů.
/// </summary>
public class GalaxyStateTests
{
    [Fact]
    public void TheClockFollowsTheActiveWorldsTicks()
    {
        var sim = World();
        var galaxy = GalaxyState.NewWithHome(sim);
        double start = galaxy.NowSeconds(sim);

        for (int i = 0; i < 50; i++)
        {
            sim.Tick();
        }

        Assert.Equal(start + 50 / Simulation.TicksPerSecond, galaxy.NowSeconds(sim), 9);
    }

    [Fact]
    public void AnAscensionDoesNotTurnTheClockBack()
    {
        // Vzestup začíná éru od tiku nula; galaktický čas musí běžet dál.
        var before = World();
        var galaxy = GalaxyState.NewWithHome(before);
        for (int i = 0; i < 300; i++)
        {
            before.Tick();
        }

        galaxy.Observe(before);
        double atAscension = galaxy.NowSeconds(before);

        var after = World(); // stejný svět po resetu éry: tiky od nuly
        galaxy.Observe(after); // hra se dívá každý snímek — reset pozná hned
        for (int i = 0; i < 20; i++)
        {
            after.Tick();
        }

        galaxy.Observe(after);

        Assert.Equal(atAscension + 20 / Simulation.TicksPerSecond, galaxy.NowSeconds(after), 6);
    }

    [Fact]
    public void WithoutTheGateTheGalaxyIsClosed()
    {
        var galaxy = GalaxyState.NewWithHome(World());

        Assert.Equal(WorldAvailability.Colony, galaxy.AvailabilityOf(Def("home", 0)));
        Assert.Equal(WorldAvailability.Locked, galaxy.AvailabilityOf(Def("dune", 0, gate: true)));
    }

    [Fact]
    public void TheGateOpensTheFirstWorldAndStarsTheRest()
    {
        var galaxy = GalaxyState.NewWithHome(World());
        galaxy.GateOpened = true;
        var dune = galaxy.Add(new WorldRecord("dune", 7));

        Assert.Equal(WorldAvailability.Colony, galaxy.AvailabilityOf(Def("dune", 0, gate: true)));
        Assert.Equal(WorldAvailability.Locked, galaxy.AvailabilityOf(Def("frost", 2)));

        dune.Stars.Add("dune_pop");
        dune.Stars.Add("dune_rule");

        Assert.Equal(2, galaxy.TotalStars());
        Assert.Equal(WorldAvailability.Available, galaxy.AvailabilityOf(Def("frost", 2)));
        Assert.Equal(WorldAvailability.Locked, galaxy.AvailabilityOf(Def("archipelago", 4)));
    }

    [Fact]
    public void StarsAreTheCompletedStarQuestsOfTheActiveWorld()
    {
        var sim = World(withStars: true);
        var galaxy = GalaxyState.NewWithHome(sim);
        for (int i = 0; i < 20; i++)
        {
            sim.Tick();
        }

        galaxy.Refresh(sim);

        // Hvězda i mistrovská hvězda se počítají, obyčejný úkol ne.
        Assert.Equal(new[] { "master", "star" }, galaxy.Active.Stars.OrderBy(s => s));
        Assert.Equal(2, galaxy.TotalStars());
    }

    [Fact]
    public void RefreshingTheHomeWorldNoticesTheOpenGate()
    {
        var sim = WorldWithGate();
        var galaxy = GalaxyState.NewWithHome(sim);
        Assert.False(galaxy.GateOpened);

        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuildingFree(1, 2, 2));
        sim.DebugCompleteConstruction(); // brána se otevře stejnou cestou jako po vkladech
        galaxy.Refresh(sim);

        Assert.True(galaxy.GateOpened);
    }

    [Fact]
    public void TheEstimateOfAnAbsentWorldIncludesTrade()
    {
        var record = new WorldRecord("dune", 1)
        {
            LeftAtSeconds = 100,
            Summary = new WorldSummary(new[] { "glass" }, new[] { 10.0 }, new[] { 1.0 }, new[] { 1_000.0 }, 50, 80, 0.1),
        };
        record.PendingDelta["glass"] = -25;

        Assert.Equal(10 + 50 - 25, record.EstimatedStock("glass", 150), 6);
        Assert.Equal(55, record.EstimatedPopulation(150), 6);
    }

    private static WorldDef Def(string id, int stars, bool gate = false) => new(
        id, 0, stars, gate, Array.Empty<ProjectStage>(), 1, id,
        new PlanetLook(new RgbColor(1, 1, 1), new RgbColor(1, 1, 1), 1, false, false, false));

    private static Simulation WorldWithGate()
    {
        var biomes = new[] { TestContent.WaterBiome(), TestContent.LandBiome("plain") };
        var project = new ProjectRule(
            new[] { new ProjectStage(new[] { new ResourceAmount(0, 10) }) }, ProjectRule.GateOpened);
        var gate = TestContent.SimpleBuilding("gate", biomes.Length) with
        {
            BuildCost = Array.Empty<ResourceAmount>(),
            BuildTicks = project.TotalUnits,
            ProjectOrNull = project,
        };
        var content = TestContent.Build(biomes, 1, buildings: new[] { TestContent.SimpleBuilding("hut", biomes.Length), gate });
        return new Simulation(content, new UniformTerrain(1), 7);
    }

    private static Simulation World(bool withStars = false)
    {
        var quests = withStars
            ? new[]
            {
                new QuestDef("star", new GoalCondition(MetricKind.Population, -1, 1), Array.Empty<ResourceAmount>(), Group: QuestGroup.Star),
                new QuestDef("master", new GoalCondition(MetricKind.Population, -1, 1), Array.Empty<ResourceAmount>(), Group: QuestGroup.StarMaster),
                new QuestDef("plain", new GoalCondition(MetricKind.Population, -1, 1), Array.Empty<ResourceAmount>()),
            }
            : Array.Empty<QuestDef>();
        var gameplay = TestContent.DefaultGameplay with { StartingPopulation = 5, FoodPerPersonPerSecond = 0 };
        var content = TestContent.Build(gameplay: gameplay, quests: quests);
        return new Simulation(content, new UniformTerrain(1), 7);
    }
}
