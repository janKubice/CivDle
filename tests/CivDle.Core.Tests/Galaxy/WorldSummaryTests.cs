using CivDle.Core.Content;
using CivDle.Core.Galaxy;
using CivDle.Core.Sim;
using CivDle.Core.Tests.Support;
using CivDle.Core.World;
using Xunit;

namespace CivDle.Core.Tests.Galaxy;

/// <summary>
/// Souhrnná simulace (svety-design.md 2.4, 7.6): svět, na který se hráč
/// nedívá, se jen počítá. Musí se trefit do toho, co by svět opravdu vyrobil,
/// a nesmí dělat nic, co by živý svět neudělal (přetéct sklad, zabít lidi).
/// </summary>
public class WorldSummaryTests
{
    [Fact]
    public void StocksGrowByFlowTimesTimeUpToTheStorage()
    {
        var summary = Summary(stock: 100, flow: 2, cap: 500);

        Assert.Equal(300, summary.Advance(100).StockOf("glass"), 6);
        Assert.Equal(500, summary.Advance(1_000).StockOf("glass"), 6); // co se nevejde, propadne
    }

    [Fact]
    public void AShrinkingStockStopsAtZero()
    {
        var summary = Summary(stock: 100, flow: -3, cap: 500);

        Assert.Equal(40, summary.Advance(20).StockOf("glass"), 6);
        Assert.Equal(0, summary.Advance(1_000).StockOf("glass"), 6);
    }

    [Fact]
    public void AnOverfullStoreIsNotCutDown()
    {
        // Sklad zbouraný po naplnění: živý svět zásobu neubere, jen nepřidá.
        var summary = Summary(stock: 800, flow: 1, cap: 500);

        Assert.Equal(800, summary.Advance(100).StockOf("glass"), 6);
    }

    [Fact]
    public void PeopleGrowTowardHousingAndNeverDie()
    {
        var growing = new WorldSummary(new[] { "glass" }, new[] { 0.0 }, new[] { 0.0 }, new[] { 1.0 }, 100, 150, 0.5);
        var overfull = new WorldSummary(new[] { "glass" }, new[] { 0.0 }, new[] { 0.0 }, new[] { 1.0 }, 200, 150, 0.5);

        Assert.Equal(120, growing.Advance(40).Population, 6);
        Assert.Equal(150, growing.Advance(4_000).Population, 6);
        Assert.Equal(200, overfull.Advance(4_000).Population, 6); // hladový svět neroste, ale ani neumírá
    }

    [Fact]
    public void AdvanceIsPure()
    {
        var summary = Summary(stock: 100, flow: 2, cap: 500);

        summary.Advance(50);

        Assert.Equal(100, summary.StockOf("glass"), 6);
        Assert.Equal(summary.Advance(50).StockOf("glass"), summary.Advance(50).StockOf("glass"));
    }

    [Fact]
    public void ADeltaMovesOnlyWhatFits()
    {
        var summary = Summary(stock: 100, flow: 0, cap: 150);

        var taken = summary.WithDelta("glass", -300, out double outgoing);
        var given = summary.WithDelta("glass", 300, out double incoming);

        Assert.Equal(-100, outgoing, 6);
        Assert.Equal(0, taken.StockOf("glass"), 6);
        Assert.Equal(50, incoming, 6);
        Assert.Equal(150, given.StockOf("glass"), 6);
        Assert.Equal(0, summary.WithDelta("spice", 10, out double unknown).StockOf("spice"));
        Assert.Equal(0, unknown);
    }

    [Fact]
    public void TheEstimateMatchesWhatTheWorldReallyProduces()
    {
        // Svět s dílnou, která vyrábí pořád stejně: odhad na deset minut dopředu
        // se musí trefit do toho, co svět za deset minut opravdu vyrobí (do 5 %).
        var sim = Workshop();
        var meter = new FlowMeter();
        for (int i = 0; i < 700; i++)
        {
            sim.Tick();
            meter.Sample(sim);
        }

        var summary = WorldSummary.Measure(sim, meter);
        double predicted = summary.Advance(600).StockOf("glass");
        for (int i = 0; i < 6_000; i++)
        {
            sim.Tick();
        }

        double actual = sim.GetResource(0);
        Assert.True(actual > summary.StockOf("glass") + 100, "dílna má za deset minut něco vyrobit");
        Assert.InRange(predicted, actual * 0.95, actual * 1.05);
    }

    [Fact]
    public void AFreshWorldFallsBackToTheSmoothedLedger()
    {
        var sim = Workshop();
        var meter = new FlowMeter();
        for (int i = 0; i < 20; i++)
        {
            sim.Tick();
        }

        var flows = new double[sim.Content.Resources.Count];
        meter.Measure(sim, flows);

        Assert.Equal(sim.Ledger.NetPerSecond(0), flows[0], 9);
    }

    private static WorldSummary Summary(double stock, double flow, double cap) =>
        new(new[] { "glass" }, new[] { stock }, new[] { flow }, new[] { cap }, 10, 10, 0);

    private static Simulation Workshop()
    {
        var biomes = new[] { TestContent.WaterBiome(), TestContent.LandBiome("sand") };
        var resources = new[] { new Resource("glass", new RgbColor(180, 220, 230), 0, BaseStorage: 1_000_000) };
        var kiln = TestContent.SimpleBuilding("kiln", biomes.Length) with
        {
            BuildCost = Array.Empty<ResourceAmount>(),
            Recipe = new Recipe(Array.Empty<ResourceAmount>(), new[] { new ResourceAmount(0, 3) }, 25),
        };
        var gameplay = TestContent.DefaultGameplay with { FoodPerPersonPerSecond = 0, PopulationGrowthPerSecond = 0 };
        var content = TestContent.Build(biomes, 1, resources, new[] { kiln }, gameplay);
        var sim = new Simulation(content, new UniformTerrain(1));
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuildingFree(0, 0, 0));
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuildingFree(0, 3, 0));
        sim.DebugCompleteConstruction();
        return sim;
    }
}
