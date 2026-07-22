using CivDle.Core.Content;
using CivDle.Core.Sim;
using CivDle.Core.Tests.Support;
using CivDle.Core.World;
using Xunit;

namespace CivDle.Core.Tests.Sim;

/// <summary>
/// Rozvodná síť (éra Průmysl): budovy se spotřebou proudu jedou naplno jen když
/// dodávka pokryje poptávku; při podpětí se výrobní postup poměrně zpomalí —
/// žádný tvrdý trest, jen míň výkonu. Dodávka i poptávka je agregát z budov.
/// </summary>
public class PowerTests
{
    private static ITerrain Grass() => new UniformTerrain(1);

    /// <summary>Obsah: „coal" vstup, „widget" výstup; elektrárna (+10 proudu) a
    /// továrna (−20 proudu, coal→widget) s dělníky kvůli obsazenosti.</summary>
    private static GameContent PowerContent()
    {
        var biomes = new[] { TestContent.WaterBiome(), TestContent.LandBiome("grass") };
        var resources = new[]
        {
            new Resource("coal", new RgbColor(40, 40, 40), StartAmount: 100, BaseStorage: 1000),
            new Resource("widget", new RgbColor(80, 110, 130), StartAmount: 0, BaseStorage: 1000),
        };
        var plant = new BuildingDef(
            "plant", "power", new RgbColor(74, 74, 74), 1, 1,
            WorkerSlots: 0, HousingCapacity: 0,
            BuildCost: new[] { new ResourceAmount(0, 1) },
            Recipe: null,
            AllowedBiomes: new[] { false, true },
            StorageBonus: Array.Empty<ResourceAmount>(),
            AutoBuild: false, Buildable: true, UpgradesToIndex: -1, UpgradeCost: Array.Empty<ResourceAmount>(),
            PowerSupply: 10, PowerDemand: 0);
        var factory = new BuildingDef(
            "factory", "production", new RgbColor(84, 110, 122), 1, 1,
            WorkerSlots: 5, HousingCapacity: 0,
            BuildCost: new[] { new ResourceAmount(0, 1) },
            Recipe: new Recipe(
                Inputs: new[] { new ResourceAmount(0, 1) },
                Outputs: new[] { new ResourceAmount(1, 1) },
                TimeTicks: 4),
            AllowedBiomes: new[] { false, true },
            StorageBonus: Array.Empty<ResourceAmount>(),
            AutoBuild: false, Buildable: true, UpgradesToIndex: -1, UpgradeCost: Array.Empty<ResourceAmount>(),
            PowerSupply: 0, PowerDemand: 20);

        // Populace stabilní (bez růstu i spotřeby jídla) → obsazenost je konstantní 1.
        var gameplay = TestContent.DefaultGameplay with
        {
            PopulationGrowthPerSecond = 0,
            FoodPerPersonPerSecond = 0,
            StartingPopulation = 5,
        };
        return TestContent.Build(biomes, 1, resources, new[] { plant, factory }, gameplay);
    }

    private static void RunTicks(Simulation sim, int ticks)
    {
        for (int i = 0; i < ticks; i++)
        {
            sim.Tick();
        }
    }

    [Fact]
    public void PoweredBuilding_WithoutSupply_DoesNotProduce()
    {
        var sim = new Simulation(PowerContent(), Grass());
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuilding(1, 2, 2)); // jen továrna, žádná elektrárna

        Assert.Equal(20, sim.TotalPowerDemand);
        Assert.Equal(0, sim.TotalPowerSupply);
        Assert.Equal(0.0, sim.PowerFactor);

        RunTicks(sim, 40);

        // Mrtvá síť → postup neroste → žádné widgety.
        Assert.Equal(0, sim.GetResource(1));
    }

    [Fact]
    public void PoweredBuilding_FullSupply_Produces()
    {
        var sim = new Simulation(PowerContent(), Grass());
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuilding(1, 2, 2)); // továrna −20
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuilding(0, 4, 4)); // elektrárna +10
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuilding(0, 6, 6)); // elektrárna +10 → 20 ≥ 20

        Assert.Equal(1.0, sim.PowerFactor);

        RunTicks(sim, 40);

        Assert.True(sim.GetResource(1) > 0, "továrna s plným proudem má vyrábět");
    }

    [Fact]
    public void PartialSupply_ProducesSlowerThanFull()
    {
        // Půl proudu = půlka rychlosti postupu → za stejný čas míň widgetů.
        var half = new Simulation(PowerContent(), Grass());
        Assert.Equal(PlacementResult.Ok, half.TryPlaceBuilding(1, 2, 2));
        Assert.Equal(PlacementResult.Ok, half.TryPlaceBuilding(0, 4, 4)); // jen +10 z 20
        Assert.Equal(0.5, half.PowerFactor);

        var full = new Simulation(PowerContent(), Grass());
        Assert.Equal(PlacementResult.Ok, full.TryPlaceBuilding(1, 2, 2));
        Assert.Equal(PlacementResult.Ok, full.TryPlaceBuilding(0, 4, 4));
        Assert.Equal(PlacementResult.Ok, full.TryPlaceBuilding(0, 6, 6));

        RunTicks(half, 40);
        RunTicks(full, 40);

        Assert.True(half.GetResource(1) < full.GetResource(1), "podpětí musí zpomalit výrobu");
        Assert.True(half.GetResource(1) > 0, "poloviční proud pořád něco vyrobí");
    }
}
