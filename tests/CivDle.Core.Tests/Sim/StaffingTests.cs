using CivDle.Core.Content;
using CivDle.Core.Sim;
using CivDle.Core.Tests.Support;
using CivDle.Core.World;
using Xunit;

namespace CivDle.Core.Tests.Sim;

/// <summary>
/// Obsazenost budov dělníky. Balanční nástroj ukázal, že rovnoměrné dělení
/// populace přes všechna pracovní místa dělalo z přestavění trest: každá další
/// výrobna zpomalila i všechny předchozí. Tyhle testy hlídají, že se dělníci
/// rozdělují po budovách a že nová budova nanejvýš stojí prázdná.
/// </summary>
public class StaffingTests
{
    private static readonly Resource[] WoodOnly =
    {
        new("wood", new RgbColor(140, 90, 40), StartAmount: 0, BaseStorage: 10_000),
    };

    /// <summary>Simulace s N výrobnami (každá 5 míst) na jednolité louce.</summary>
    private static Simulation WithProducers(int count, out GameContent content)
    {
        // 5 pracovních míst na budovu při startovní populaci → první budova plná,
        // druhá už nemá koho zaměstnat. Růst populace je vypnutý, aby měření
        // nezáviselo na tom, kolik lidí mezitím přibude.
        var producer = TestContent.Producer("mill", outputResource: 0, amount: 10, timeTicks: 10, workerSlots: 5);
        var gameplay = TestContent.DefaultGameplay with { PopulationGrowthPerSecond = 0.0 };
        content = TestContent.Build(resources: WoodOnly, buildings: new[] { producer }, gameplay: gameplay);

        var sim = new Simulation(content, new UniformTerrain((byte)1));
        for (int i = 0; i < count; i++)
        {
            Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuilding(0, i * 2, 0));
        }

        return sim;
    }

    private static double RunAndMeasure(Simulation sim, int ticks)
    {
        for (int i = 0; i < ticks; i++)
        {
            sim.Tick();
        }

        return sim.GetResource(0);
    }

    [Fact]
    public void SecondBuilding_DoesNotSlowDownTheFirst()
    {
        var oneBuilding = WithProducers(1, out _);
        var twoBuildings = WithProducers(2, out _);

        double one = RunAndMeasure(oneBuilding, 100);
        double two = RunAndMeasure(twoBuildings, 100);

        // Přesně tohle byla past: dřív dvě budovy vyrobily dohromady tolik co jedna
        // (obě na půl plynu). Stavět víc nikdy nesmí ubrat.
        Assert.True(two >= one,
            $"Druhá budova ubrala výrobu: jedna dala {one}, dvě daly {two}.");
    }

    [Fact]
    public void WorkersGoToTheOlderBuilding_NewOneIdles()
    {
        var sim = WithProducers(2, out _);
        RunAndMeasure(sim, 100);

        // Pořadí je pořadí stavby: starší budova si dělníky drží, nová stojí.
        Assert.Equal(1, sim.IdleBuildings);
    }

    [Fact]
    public void EnoughPeople_StaffEverything()
    {
        var producer = TestContent.Producer("mill", outputResource: 0, amount: 10, timeTicks: 10, workerSlots: 1);
        var content = TestContent.Build(resources: WoodOnly, buildings: new[] { producer });
        var sim = new Simulation(content, new UniformTerrain((byte)1));

        // Startovní populace pokryje obě budovy po jednom dělníkovi.
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuilding(0, 0, 0));
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuilding(0, 2, 0));
        RunAndMeasure(sim, 30);

        Assert.Equal(0, sim.IdleBuildings);
    }

    [Fact]
    public void NoPeople_MeansNoProduction()
    {
        var producer = TestContent.Producer("mill", outputResource: 0, amount: 10, timeTicks: 10, workerSlots: 5);
        var gameplay = TestContent.DefaultGameplay with { StartingPopulation = 0 };
        var content = TestContent.Build(resources: WoodOnly, buildings: new[] { producer }, gameplay: gameplay);
        var sim = new Simulation(content, new UniformTerrain((byte)1));
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuilding(0, 0, 0));

        Assert.Equal(0, RunAndMeasure(sim, 60));
        Assert.Equal(1, sim.IdleBuildings);
    }

    [Fact]
    public void BuildingsWithoutWorkers_RunRegardless()
    {
        // Budova bez pracovních míst (0) nesmí čekat na dělníky ani je brát —
        // jinak by ji vyhladověly výrobny postavené dřív.
        var free = TestContent.Producer("spring", outputResource: 0, amount: 10, timeTicks: 10, workerSlots: 0);
        var content = TestContent.Build(resources: WoodOnly, buildings: new[] { free });
        var sim = new Simulation(content, new UniformTerrain((byte)1));
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuilding(0, 0, 0));

        Assert.True(RunAndMeasure(sim, 60) > 0);
        Assert.Equal(0, sim.IdleBuildings);
    }
}
