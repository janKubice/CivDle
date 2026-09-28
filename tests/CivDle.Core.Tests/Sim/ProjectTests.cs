using CivDle.Core.Content;
using CivDle.Core.Save;
using CivDle.Core.Sim;
using CivDle.Core.Tests.Support;
using CivDle.Core.World;
using Xunit;

namespace CivDle.Core.Tests.Sim;

/// <summary>
/// Projekty: stavby, které nerostou časem, ale vkládáním surovin po stupních
/// (Hvězdná brána).
///
/// <para>Hlídá se, na čem to tiše selhává: čas s projektem nesmí hnout (ani
/// v tiku, ani ve skoku dohánění), stupeň se nesmí dokončit o haléř dřív,
/// vklad nesmí sáhnout na rezervu guvernéra, dokončení musí spustit efekt
/// z dat — a rozestavěný stupeň musí přežít save.</para>
/// </summary>
public class ProjectTests
{
    private const int Wood = 0;
    private const int Stone = 1;

    [Fact]
    public void AProjectSiteWaitsForInvestmentNotForTime()
    {
        var sim = NewSim();
        int gate = PlaceGate(sim);

        for (int i = 0; i < 6000; i++)
        {
            sim.Tick();
        }

        // Dvanáct hodin offline: dohánění tiká přesně a zbytek přeskočí — ani
        // jedno nesmí projekt posunout.
        var start = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc);
        var catchUp = new OfflineCatchUp(sim, start, start.AddHours(12));
        while (!catchUp.IsDone)
        {
            catchUp.Advance(100_000);
        }

        Assert.True(sim.IsProjectSite(gate));
        Assert.Equal(0, sim.ConstructionProgress01(gate), 6);
        Assert.Equal(1, sim.ProjectSiteCount);
    }

    [Fact]
    public void InvestingBuildsStageByStageAndOpensTheGate()
    {
        var sim = NewSim();
        int gate = PlaceGate(sim);

        Assert.Equal(100, sim.TryInvestInProject(gate));
        Assert.Equal(1, sim.ProjectStageIndex(gate));
        Assert.Equal(0.5, sim.ConstructionProgress01(gate), 3);
        Assert.False(sim.IsGateOpened);

        Assert.Equal(50, sim.TryInvestInProject(gate));

        Assert.False(sim.IsProjectSite(gate));
        Assert.True(sim.Buildings[gate].IsComplete);
        Assert.True(sim.IsGateOpened);
        Assert.Equal(0, sim.ProjectSiteCount);
        Assert.Equal(1, sim.EvaluateMetric(MetricKind.ProjectsCompleted, -1));
        Assert.Equal(1, sim.EvaluateMetric(MetricKind.ProjectsCompleted, 1));
    }

    [Fact]
    public void APartialInvestmentShowsProgressButNeverFinishesTheStageEarly()
    {
        var sim = NewSim(wood: 99.999, stone: 0);
        int gate = PlaceGate(sim);

        sim.TryInvestInProject(gate);

        Assert.Equal(0, sim.ProjectStageIndex(gate));
        Assert.True(sim.ProjectStageProgress01(gate) > 0.99);
        Assert.True(sim.ConstructionProgress01(gate) < 0.5, "stupeň se nesmí dokončit o haléř dřív");
        Assert.Equal(0, sim.TryInvestInProject(gate)); // není z čeho
    }

    [Fact]
    public void InvestingLeavesTheGovernorsReserveAlone()
    {
        var sim = NewSim(wood: 150, stone: 200);
        int gate = PlaceGate(sim);
        sim.SetClaimForTest(2); // guvernér šetří na chatu za 80 dřeva

        double invested = sim.TryInvestInProject(gate);

        Assert.Equal(70, invested, 6);
        Assert.Equal(80, sim.GetResource(Wood), 6);
    }

    [Fact]
    public void DemolishingASiteForgetsItsInvestment()
    {
        var sim = NewSim(wood: 40);
        int gate = PlaceGate(sim);
        sim.TryInvestInProject(gate);

        Assert.Equal(PlacementResult.Ok, sim.TryDemolish(gate));

        Assert.Equal(0, sim.ProjectSiteCount);
        int again = PlaceGate(sim);
        Assert.Equal(0, sim.ProjectInvested(again, Wood));
    }

    [Fact]
    public void AHalfPaidStageSurvivesASave()
    {
        var sim = NewSim(wood: 1000, stone: 20, out var content);
        int gate = PlaceGate(sim);
        sim.TryInvestInProject(gate); // první stupeň celý (dřevo)
        sim.TryInvestInProject(gate); // druhý jen z části (20 z 50 kamene)
        double progress = sim.ConstructionProgress01(gate);

        var loaded = RoundTrip(sim, content);
        int loadedGate = FindBuilding(loaded, 1);

        Assert.True(loaded.IsProjectSite(loadedGate));
        Assert.Equal(1, loaded.ProjectStageIndex(loadedGate));
        Assert.Equal(progress, loaded.ConstructionProgress01(loadedGate), 6);
        Assert.Equal(20, loaded.ProjectInvested(loadedGate, Stone), 6);
        Assert.Equal(1, loaded.ProjectSiteCount);
        Assert.False(loaded.IsGateOpened);

        // A dostavět jde i po načtení.
        loaded.AddResource(Stone, 100);
        Assert.Equal(30, loaded.TryInvestInProject(loadedGate), 6);
        Assert.True(loaded.IsGateOpened);
    }

    [Fact]
    public void AnOpenGateSurvivesASave()
    {
        var sim = NewSim(1000, 1000, out var content);
        int gate = PlaceGate(sim);
        sim.TryInvestInProject(gate);
        sim.TryInvestInProject(gate);
        Assert.True(sim.IsGateOpened);

        var loaded = RoundTrip(sim, content);

        Assert.True(loaded.IsGateOpened);
        Assert.Equal(sim.GateOpenedAtTick, loaded.GateOpenedAtTick);
        Assert.Equal(0, loaded.ProjectSiteCount);
    }

    [Fact]
    public void TheDebugFinishOpensTheGateThroughTheSamePath()
    {
        var sim = NewSim();
        PlaceGate(sim);

        sim.DebugCompleteConstruction();

        Assert.True(sim.IsGateOpened);
        Assert.Equal(0, sim.ProjectSiteCount);
    }

    [Fact]
    public void TheGalacticWonderUnitesTheGalaxyAndLeavesTheGateAlone()
    {
        var sim = NewSim(1000, 1000, out _, ProjectRule.GalaxyUnited);
        int wonder = PlaceGate(sim);
        Drain(sim);
        Assert.False(sim.IsGalaxyUnited);

        sim.TryInvestInProject(wonder);
        Assert.False(sim.IsGalaxyUnited); // první stupeň ještě ne
        sim.TryInvestInProject(wonder);

        Assert.True(sim.IsGalaxyUnited);
        Assert.False(sim.IsGateOpened); // Souhvězdí bránu neotevírá
        Assert.Contains(Drain(sim), n => n.TitleKey == "toast.galaxyUnited");
    }

    [Fact]
    public void AUnitedGalaxySurvivesASaveBecauseTheWonderStands()
    {
        var sim = NewSim(1000, 1000, out var content, ProjectRule.GalaxyUnited);
        PlaceGate(sim);
        sim.DebugCompleteConstruction();

        var loaded = RoundTrip(sim, content);

        Assert.True(loaded.IsGalaxyUnited);
    }

    [Fact]
    public void TheDebugPlacementSkipsTheUnlocksButNotTheGround()
    {
        // Smoke staví Souhvězdí na Domovině bez výzkumu, brány i metropole;
        // volnou zem ale ladicí cesta ctít musí, jinak by stavěla přes domy.
        var content = TestData.LoadRealContent();
        var sim = new Simulation(content, new UniformTerrain((byte)content.Biomes.IndexOf("grassland")), 7);
        int wonder = content.Buildings.IndexOf("constellation");
        Assert.NotEqual(PlacementResult.Ok, sim.CanPlace(wonder, 0, 0));

        Assert.Equal(PlacementResult.Ok, sim.DebugPlaceBuilding(wonder, 0, 0));
        Assert.Equal(PlacementResult.Occupied, sim.DebugPlaceBuilding(wonder, 3, 3));

        sim.DebugCompleteConstruction();
        Assert.True(sim.IsGalaxyUnited);
    }

    [Fact]
    public void TheDebugBuildOpensTheRealGateAndFinishesNothingElse()
    {
        // Páka „Otevřít Hvězdnou bránu“: brána bez výzkumu, divů a metropole,
        // dostavěná cestou stavebního systému (hlášení spustí průlet a konec
        // kapitoly) — ale rozestavěný kosmodrom ve městě zůstane rozestavěný.
        var content = TestData.LoadRealContent();
        var sim = new Simulation(content, new UniformTerrain((byte)content.Biomes.IndexOf("grassland")), 7);
        int spaceport = content.Buildings.IndexOf("spaceport");
        Assert.Equal(PlacementResult.Ok, sim.DebugPlaceBuilding(spaceport, 60, 60));
        Drain(sim);

        int gate = sim.DebugBuildNearCity(content.Buildings.IndexOf("star_gate"));

        Assert.True(gate >= 0);
        Assert.True(sim.Buildings[gate].IsComplete);
        Assert.True(sim.IsGateOpened);
        Assert.Contains(Drain(sim), n => n.TitleKey == "toast.gateOpened");
        Assert.False(sim.Buildings[0].IsComplete); // kosmodrom se staví dál
    }

    [Fact]
    public void TheDebugBuildUnitesTheGalaxyWithTheRealConstellation()
    {
        var content = TestData.LoadRealContent();
        var sim = new Simulation(content, new UniformTerrain((byte)content.Biomes.IndexOf("grassland")), 7);

        Assert.True(sim.DebugBuildNearCity(content.Buildings.IndexOf("constellation")) >= 0);

        Assert.True(sim.IsGalaxyUnited);
        Assert.Contains(Drain(sim), n => n.TitleKey == "toast.galaxyUnited");
    }

    [Fact]
    public void TheDebugBuildSaysSoWhenThereIsNoGround()
    {
        // Menu pak hráči řekne, že se projekt nevešel — nesmí stavět do moře.
        var sim = new Simulation(TestContent.Build(
            new[] { TestContent.WaterBiome(), TestContent.LandBiome("plain") }, 1,
            new[] { new Resource("wood", new RgbColor(1, 1, 1), 0, BaseStorage: 100) },
            new[] { TestContent.SimpleBuilding("gate", 2) with { AllowedBiomes = new[] { false, true } } }),
            new UniformTerrain(0));

        Assert.Equal(-1, sim.DebugBuildNearCity(0, searchRadius: 12));
        Assert.Equal(0, sim.Buildings.Length);
    }

    [Fact]
    public void TheRealGateIsAProjectThatOpensTheGate()
    {
        var content = TestData.LoadRealContent();
        var gate = content.Buildings[content.Buildings.IndexOf("star_gate")];

        Assert.True(gate.IsProject);
        Assert.Equal(ProjectRule.GateOpened, gate.ProjectOrNull!.OnComplete);
        Assert.Equal("megastructure", gate.Category);
        Assert.False(gate.AutoBuild); // guvernér divy nestaví
        Assert.True(gate.ProjectOrNull.Stages.Count >= 4);
    }

    [Fact]
    public void TheEndingSummaryOnlyReadsTheGame()
    {
        // Sekvence se dá pustit znovu z menu kolikrát chceš — nesmí na hru sáhnout.
        var sim = NewSim();
        int gate = PlaceGate(sim);
        sim.TryInvestInProject(gate);
        sim.TryInvestInProject(gate);
        for (int i = 0; i < 50; i++)
        {
            sim.Tick();
        }

        long tick = sim.TickCount;
        double wood = sim.GetResource(Wood);
        int buildings = sim.Buildings.Length;
        long revision = sim.BuildingRevision;

        var summary = EndingSummary.Of(sim);
        var again = EndingSummary.Of(sim);

        Assert.Equal(tick, sim.TickCount);
        Assert.Equal(wood, sim.GetResource(Wood));
        Assert.Equal(buildings, sim.Buildings.Length);
        Assert.Equal(revision, sim.BuildingRevision);
        Assert.Equal(summary.Buildings, again.Buildings);
        Assert.Equal(buildings, summary.Buildings);
        Assert.Equal(tick / Simulation.TicksPerSecond, summary.GameSeconds, 6);
        Assert.True(summary.WondersCompleted >= 1); // brána se počítá mezi divy
    }

    // ----- pomocníci -----

    private static Simulation NewSim(double wood = 1000, double stone = 1000) => NewSim(wood, stone, out _);

    private static Simulation NewSim(double wood, double stone, out GameContent content, string onComplete = ProjectRule.GateOpened)
    {
        var biomes = new[] { TestContent.WaterBiome(), TestContent.LandBiome("plain") };
        var resources = new[]
        {
            new Resource("wood", new RgbColor(1, 1, 1), wood, BaseStorage: 100_000),
            new Resource("stone", new RgbColor(1, 1, 1), stone, BaseStorage: 100_000),
        };

        var project = new ProjectRule(
            new[]
            {
                new ProjectStage(new[] { new ResourceAmount(Wood, 100) }),
                new ProjectStage(new[] { new ResourceAmount(Stone, 50) }),
            },
            onComplete);

        var hut = TestContent.SimpleBuilding("hut", biomes.Length);
        var gate = TestContent.SimpleBuilding("gate", biomes.Length) with
        {
            BuildCost = Array.Empty<ResourceAmount>(),
            BuildTicks = project.TotalUnits,
            ProjectOrNull = project,
        };
        var cabin = TestContent.SimpleBuilding("cabin", biomes.Length) with
        {
            BuildCost = new[] { new ResourceAmount(Wood, 80) },
        };

        var gameplay = TestContent.DefaultGameplay with
        {
            FoodPerPersonPerSecond = 0,
            PopulationGrowthPerSecond = 0,
        };

        content = TestContent.Build(biomes, 1, resources, new[] { hut, gate, cabin }, gameplay);
        return new Simulation(content, new UniformTerrain(1), 42);
    }

    private static List<GameNotification> Drain(Simulation sim)
    {
        var notes = new List<GameNotification>();
        while (sim.TryDequeueNotification(out var note))
        {
            notes.Add(note);
        }

        return notes;
    }

    private static Simulation RoundTrip(Simulation sim, GameContent content)
    {
        var serializer = new SaveGameSerializer();
        using var stream = new MemoryStream();
        serializer.Write(stream, sim, new SaveMetadata(42, "medium", "test", DateTime.UtcNow));
        stream.Position = 0;
        return serializer.Read(stream, content).Simulation;
    }

    private static int PlaceGate(Simulation sim)
    {
        for (int x = 0; x < 30; x++)
        {
            if (sim.TryPlaceBuilding(1, x, 0) == PlacementResult.Ok)
            {
                return sim.Buildings.Length - 1;
            }
        }

        throw new InvalidOperationException("Bránu nešlo postavit.");
    }

    private static int FindBuilding(Simulation sim, int defIndex)
    {
        for (int i = 0; i < sim.Buildings.Length; i++)
        {
            if (sim.Buildings[i].DefIndex == defIndex)
            {
                return i;
            }
        }

        return -1;
    }
}
