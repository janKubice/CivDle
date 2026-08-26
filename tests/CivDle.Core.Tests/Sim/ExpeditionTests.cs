using CivDle.Core.Content;
using CivDle.Core.Save;
using CivDle.Core.Sim;
using CivDle.Core.Tests.Support;
using CivDle.Core.World;
using Xunit;

namespace CivDle.Core.Tests.Sim;

/// <summary>
/// Anomálie a výpravy.
///
/// <para>Hlídají se tři věci. <b>Determinismus</b>: tentýž seed musí dát tatáž
/// místa i tytéž odměny — jinak by šlo uložit hru před výpravou a losovat,
/// dokud nepadne relikvie. <b>Save</b>: běžící výprava i vybraná místa musí
/// přežít načtení. A <b>relikvie v násobiči</b> — bonus, který se nikam
/// nepromítne, je jen řádek v seznamu.</para>
/// </summary>
public class ExpeditionTests
{
    [Fact]
    public void RealContentHasAnomaliesWithRewards()
    {
        var content = TestData.LoadRealContent();

        Assert.True(content.PointsOfInterest.IsEnabled);
        foreach (var kind in content.PointsOfInterest.Kinds)
        {
            Assert.NotEmpty(kind.Rewards);
            Assert.True(kind.TotalWeight > 0);
            Assert.True(kind.DurationTicks > 0);
        }
    }

    [Fact]
    public void TheSameSeedGivesTheSameAnomalies()
    {
        var content = PoiContent();
        var first = new List<PointOfInterest>();
        var second = new List<PointOfInterest>();

        World(content, seed: 99).PointsOfInterest.InRange(-500, -500, 500, 500, first);
        World(content, seed: 99).PointsOfInterest.InRange(-500, -500, 500, 500, second);

        Assert.NotEmpty(first);
        Assert.Equal(first, second);
    }

    [Fact]
    public void ADifferentSeedGivesADifferentWorld()
    {
        var content = PoiContent();
        var first = new List<PointOfInterest>();
        var second = new List<PointOfInterest>();

        World(content, seed: 1).PointsOfInterest.InRange(-500, -500, 500, 500, first);
        World(content, seed: 2).PointsOfInterest.InRange(-500, -500, 500, 500, second);

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void NothingLiesRightNextToWhereTheCityStarted()
    {
        // První hodina hry je o stavbě, ne o výpravách.
        var sim = World(PoiContent(), seed: 7);
        var found = new List<PointOfInterest>();
        sim.PointsOfInterest.InRange(-500, -500, 500, 500, found);

        Assert.All(found, poi => Assert.True(
            ((long)poi.X * poi.X) + ((long)poi.Y * poi.Y) >= 40L * 40,
            $"anomálie na {poi.X},{poi.Y} je moc blízko startu"));
    }

    [Fact]
    public void SendingAnExpeditionCostsAndTakesTime()
    {
        var (sim, poi) = WorldWithAnomaly();
        sim.DebugFillStorages();
        double before = sim.GetResource(0);

        Assert.Equal(PlacementResult.Ok, sim.TrySendExpedition(poi.X, poi.Y));

        Assert.True(sim.ExpeditionRunning);
        Assert.True(sim.GetResource(0) < before, "výprava nic nestála");

        // Vybere se hned při vypravení: jinak by šlo poslat dvě výpravy na
        // totéž místo a ta druhá by našla prázdno.
        Assert.True(sim.PointsOfInterest.IsClaimed(poi.X, poi.Y));
    }

    [Fact]
    public void OnlyOneExpeditionAtATime()
    {
        var (sim, poi) = WorldWithAnomaly();
        sim.DebugFillStorages();
        Assert.Equal(PlacementResult.Ok, sim.TrySendExpedition(poi.X, poi.Y));

        var others = new List<PointOfInterest>();
        sim.PointsOfInterest.InRange(-2000, -2000, 2000, 2000, others);
        Assert.NotEmpty(others);

        Assert.Equal(PlacementResult.NotUnlocked, sim.CanSendExpedition(others[0].X, others[0].Y));
    }

    [Fact]
    public void TheExpeditionComesBackWithSomething()
    {
        var (sim, poi) = WorldWithAnomaly();
        sim.DebugFillStorages();
        Assert.Equal(PlacementResult.Ok, sim.TrySendExpedition(poi.X, poi.Y));

        long ticks = sim.ExpeditionTicksLeft;
        for (long i = 0; i <= ticks; i++)
        {
            sim.Tick();
        }

        Assert.False(sim.ExpeditionRunning);
    }

    [Fact]
    public void TheRewardIsDecidedByThePlace_NotByWhenYouGo()
    {
        // Bez toho by šlo uložit hru před výpravou a losovat, dokud nepadne
        // relikvie.
        var content = PoiContent();
        var a = World(content, seed: 55);
        var b = World(content, seed: 55);

        var found = new List<PointOfInterest>();
        a.PointsOfInterest.InRange(-800, -800, 800, 800, found);
        Assert.NotEmpty(found);

        foreach (var poi in found)
        {
            Assert.Equal(a.PointsOfInterest.RewardIndexFor(poi), b.PointsOfInterest.RewardIndexFor(poi));
        }
    }

    [Fact]
    public void ARelicShowsUpInTheMultipliers()
    {
        var content = RelicContent();
        var sim = World(content, seed: 3);
        double before = sim.Bonuses.ProductionMult;

        var found = new List<PointOfInterest>();
        sim.PointsOfInterest.InRange(-2000, -2000, 2000, 2000, found);
        Assert.NotEmpty(found);

        sim.DebugFillStorages();
        Assert.Equal(PlacementResult.Ok, sim.TrySendExpedition(found[0].X, found[0].Y));
        long ticks = sim.ExpeditionTicksLeft;
        for (long i = 0; i <= ticks; i++)
        {
            sim.Tick();
        }

        Assert.Single(sim.Relics);
        Assert.True(sim.Bonuses.ProductionMult > before, "relikvie se nikam nepromítla");
    }

    [Fact]
    public void ARunningExpeditionSurvivesSaveAndLoad()
    {
        var content = PoiContent();
        var sim = World(content, seed: 21);
        sim.DebugFillStorages();

        var found = new List<PointOfInterest>();
        sim.PointsOfInterest.InRange(-2000, -2000, 2000, 2000, found);
        Assert.NotEmpty(found);
        Assert.Equal(PlacementResult.Ok, sim.TrySendExpedition(found[0].X, found[0].Y));
        sim.Tick();

        var serializer = new SaveGameSerializer();
        using var stream = new MemoryStream();
        serializer.Write(stream, sim, new SaveMetadata(21, "s", "test", DateTime.UtcNow));
        stream.Position = 0;
        var (loaded, _) = serializer.Read(stream, content);

        Assert.True(loaded.ExpeditionRunning);
        Assert.Equal(sim.ExpeditionTicksLeft, loaded.ExpeditionTicksLeft);
        Assert.Equal(sim.ExpeditionTarget, loaded.ExpeditionTarget);
        Assert.True(loaded.PointsOfInterest.IsClaimed(found[0].X, found[0].Y));
    }

    [Fact]
    public void AClaimedAnomalyIsNotOfferedAgain()
    {
        var (sim, poi) = WorldWithAnomaly();
        sim.PointsOfInterest.Claim(poi.X, poi.Y);

        var found = new List<PointOfInterest>();
        sim.PointsOfInterest.InRange(poi.X - 10, poi.Y - 10, poi.X + 10, poi.Y + 10, found);

        Assert.DoesNotContain(poi, found);
        Assert.Equal(PlacementResult.NotUnlocked, sim.CanSendExpedition(poi.X, poi.Y));
    }

    // ----- pomocné -----

    private static Simulation World(GameContent content, long seed) =>
        new(content, new UniformTerrain(1), seed);

    private static (Simulation Sim, PointOfInterest Poi) WorldWithAnomaly()
    {
        var sim = World(PoiContent(), seed: 12345);
        var found = new List<PointOfInterest>();
        sim.PointsOfInterest.InRange(-2000, -2000, 2000, 2000, found);
        Assert.NotEmpty(found);
        return (sim, found[0]);
    }

    private static GameContent PoiContent(int relicIndex = -1)
    {
        var biomes = new[] { TestContent.WaterBiome(), TestContent.LandBiome("grass") };
        var relics = new[] { new PoiRelicDef("test_relic", "production_mult", 0.5) };

        return TestContent.Build(
            biomes: biomes,
            pointsOfInterest: new PoiCatalog(
                new[]
                {
                    new PoiDef(
                        "ruin",
                        new[] { false, true },
                        MinDistanceFromStart: 40,
                        new[] { new ResourceAmount(0, 5) },
                        DurationTicks: 20,
                        new[] { new PoiRewardDef(1, new[] { new ResourceAmount(0, 100) }, relicIndex) }),
                },
                relics,
                RegionTiles: 64,
                ChancePercent: 50));
    }

    private static GameContent RelicContent() => PoiContent(relicIndex: 0);
}
