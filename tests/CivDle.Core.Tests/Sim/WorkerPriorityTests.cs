using CivDle.Core.Content;
using CivDle.Core.Sim;
using CivDle.Core.Tests.Support;
using CivDle.Core.World;
using Xunit;

namespace CivDle.Core.Tests.Sim;

/// <summary>
/// Dělníci jdou nejdřív tam, kde práce něco vyrobí. Dřív dostala pila bez dřeva
/// lidi přednostně (prkna byla „nedostatková") a dřevorubec, který by ji nakrmil,
/// stál prázdný — při málo lidech zámek navždy.
/// </summary>
public class WorkerPriorityTests
{
    private const int Wood = 0;
    private const int Planks = 1;

    private static readonly Resource[] Resources =
    {
        new("wood", new RgbColor(140, 90, 40), StartAmount: 0, BaseStorage: 1000),
        new("planks", new RgbColor(200, 160, 90), StartAmount: 0, BaseStorage: 1000),
    };

    /// <summary>Stálý počet lidí, nikdo nejí (jídlem je v testovacím obsahu surovina 0 = dřevo).</summary>
    private static readonly GameplayConfig Frozen = TestContent.DefaultGameplay with
    {
        PopulationGrowthPerSecond = 0.0,
        FoodPerPersonPerSecond = 0.0,
    };

    private static Simulation SawmillThenCamp(double population)
    {
        var sawmill = TestContent.Converter("sawmill", Wood, 3, Planks, 1, timeTicks: 10, workerSlots: 2);
        var camp = TestContent.Producer("camp", Wood, 2, timeTicks: 10, workerSlots: 2);
        var content = TestContent.Build(resources: Resources, buildings: new[] { sawmill, camp }, gameplay: Frozen);
        var sim = new Simulation(content, new UniformTerrain((byte)1));

        // Pila je starší — dřív tím pádem měla přednost.
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuilding(0, 0, 0));
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuilding(1, 2, 0));
        sim.SetPopulationForTest(population);
        return sim;
    }

    private static void Run(Simulation sim, int ticks)
    {
        for (int i = 0; i < ticks; i++)
        {
            sim.Tick();
        }
    }

    [Fact]
    public void HungryConverter_DoesNotStarveItsOwnSupplier()
    {
        var sim = SawmillThenCamp(population: 2);

        Run(sim, 300);

        Assert.True(sim.GetResource(Planks) > 0,
            "řetězec zamrzl: pila bez dřeva držela oba dělníky a dřevorubec stál");
    }

    [Fact]
    public void UnstaffedHungryConverter_ReportsMissingInput_NotMissingPeople()
    {
        var sim = SawmillThenCamp(population: 2);

        sim.Tick(); // dřevo je zatím na nule

        // „Chybí lidé" by hráče poslalo stavět domy. Skutečná příčina je dřevo.
        Assert.Equal(BuildingStall.MissingInput, sim.Buildings[0].Stall);
    }

    [Fact]
    public void BlockedBuildings_DoNotCountAsIdle()
    {
        var sim = SawmillThenCamp(population: 0);

        sim.Tick();

        // Dřevorubec by pracoval, kdyby měl lidi → prázdný. Pila by stála i tak → ne.
        Assert.Equal(1, sim.IdleBuildings);
    }

    [Fact]
    public void BlockedBuildings_StillGetLeftoverWorkers()
    {
        // Lidí je dost pro všechny: hladová pila dostane zbytek, ať se rozjede
        // hned, jak dřevo dorazí, a ne až po dalším přerozdělení.
        var sim = SawmillThenCamp(population: 10);

        sim.Tick();

        Assert.Equal(4, sim.EmployedWorkers);
    }
}
