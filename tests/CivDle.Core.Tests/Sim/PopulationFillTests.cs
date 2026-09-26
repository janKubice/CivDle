using CivDle.Core.Content;
using CivDle.Core.Sim;
using CivDle.Core.Tests.Support;
using CivDle.Core.World;
using Xunit;

namespace CivDle.Core.Tests.Sim;

/// <summary>
/// Růst populace podle volného bydlení. Dřív byl přírůstek pevný — vesnice
/// i milionové město přibíraly stejně lidí za sekundu, panelák pro čtyřicet se
/// plnil minuty a mrakodrap hodiny.
/// </summary>
public class PopulationFillTests
{
    private static Simulation World(double fillRate, int housing)
    {
        var resources = new[] { new Resource("food", new RgbColor(1, 1, 1), StartAmount: 100_000, BaseStorage: 1_000_000) };
        var tower = TestContent.SimpleBuilding("tower", 2, housing: housing) with { BuildCost = Array.Empty<ResourceAmount>() };
        var gameplay = TestContent.DefaultGameplay with
        {
            PopulationGrowthPerSecond = 0.12,
            FoodPerPersonPerSecond = 0.0,
            PopulationFillRate = fillRate,
        };
        var content = TestContent.Build(resources: resources, buildings: new[] { tower }, gameplay: gameplay);
        var sim = new Simulation(content, new UniformTerrain(1));
        sim.TryPlaceBuildingFree(0, 0, 0);
        return sim;
    }

    private static void Run(Simulation sim, int seconds)
    {
        for (int i = 0; i < seconds * (int)Simulation.TicksPerSecond; i++)
        {
            sim.Tick();
        }
    }

    [Fact]
    public void WithoutAFillRate_GrowthIsTheOldFixedPace()
    {
        var sim = World(fillRate: 0.0, housing: 1000);
        double before = sim.Population;

        Run(sim, 10);

        Assert.Equal(before + 1.2, sim.Population, 3); // 0,12 lidí za sekundu × 10 s
    }

    [Fact]
    public void ABigEmptyBuildingFillsFarFaster()
    {
        var fixedPace = World(fillRate: 0.0, housing: 1000);
        var filling = World(fillRate: 0.01, housing: 1000);

        Run(fixedPace, 60);
        Run(filling, 60);

        Assert.True(filling.Population > fixedPace.Population * 5,
            $"prázdný dům pro tisíc lidí se plní pořád pomalu ({filling.Population:0} proti {fixedPace.Population:0})");
    }

    [Fact]
    public void NobodyMovesInAboveTheCeiling()
    {
        var sim = World(fillRate: 1.0, housing: 50);

        Run(sim, 120);

        Assert.True(sim.Population <= sim.HousingCapacity + 1e-9);
    }

    [Fact]
    public void GrowthSlowsAsTheHousingFillsUp()
    {
        // Podíl volných míst: čím méně jich zbývá, tím pomaleji se stěhuje.
        var config = TestContent.DefaultGameplay with { PopulationGrowthPerSecond = 0.1, PopulationFillRate = 0.01 };

        Assert.Equal(0.1 + 10.0, config.GrowthPerSecond(1000), 6);
        Assert.Equal(0.1 + 0.1, config.GrowthPerSecond(10), 6);
        Assert.Equal(0.1, config.GrowthPerSecond(-5), 6); // přeplněno: jen pevný základ, nikdy záporně
    }
}
