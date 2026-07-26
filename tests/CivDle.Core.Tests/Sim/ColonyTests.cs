using CivDle.Core.Content;
using CivDle.Core.Sim;
using CivDle.Core.Tests.Support;
using CivDle.Core.World;
using Xunit;

namespace CivDle.Core.Tests.Sim;

/// <summary>
/// Guvernér (automatizace, stupeň 5): s politikou „auto_expand" sám založí
/// samostatnou kolonii daleko od stávající zástavby, jakmile je doma plno.
/// Bez politiky se nic takového neděje (automatizace se odemyká, není výchozí).
/// </summary>
public class ColonyTests
{
    private static ITerrain Grass() => new UniformTerrain(1);

    private const int Distance = 20;

    /// <summary>Obsah: auto-stavitelné bydlení „hut" (kapacita 1) a politika expanze.</summary>
    private static GameContent ColonyContent()
    {
        var biomes = new[] { TestContent.WaterBiome(), TestContent.LandBiome("grass") };
        var resources = new[] { new Resource("wood", new RgbColor(140, 90, 40), StartAmount: 1000, BaseStorage: 5000) };
        var hut = new BuildingDef(
            "hut", "housing", new RgbColor(200, 100, 50), 1, 1,
            WorkerSlots: 0, HousingCapacity: 1,
            BuildCost: new[] { new ResourceAmount(0, 1) },
            Recipe: null, AllowedBiomes: new[] { false, true },
            StorageBonus: System.Array.Empty<ResourceAmount>(),
            AutoBuild: true, Buildable: true, UpgradesToIndex: -1,
            UpgradeCost: System.Array.Empty<ResourceAmount>(),
            PowerSupply: 0, PowerDemand: 0);
        var policies = new[] { new GrowthPolicyDef("expand", "auto_expand", Distance) };

        // Populace zmražená nad kapacitou → trvalý tlak na bydlení (spouštěč expanze).
        var gameplay = TestContent.DefaultGameplay with
        {
            AutoBuild = new AutoBuildConfig(IntervalTicks: 1, SearchRadius: 2, PopulationHeadroom: 0),
            PopulationGrowthPerSecond = 0,
            FoodPerPersonPerSecond = 0,
            StartingPopulation = 50,
            BaseHousingCapacity = 1,
        };
        return TestContent.Build(biomes, 1, resources, new[] { hut }, gameplay, policies: policies);
    }

    private static void RunTicks(Simulation sim, int ticks)
    {
        for (int i = 0; i < ticks; i++)
        {
            sim.Tick();
        }
    }

    /// <summary>Nejdelší vzdálenost budovy od výchozí domoviny.</summary>
    private static double FarthestFromHome(Simulation sim, int homeX, int homeY)
    {
        double farthest = 0;
        foreach (var b in sim.Buildings)
        {
            double dx = b.X - homeX, dy = b.Y - homeY;
            farthest = Math.Max(farthest, Math.Sqrt(dx * dx + dy * dy));
        }

        return farthest;
    }

    [Fact]
    public void Governor_FoundsDistantColony()
    {
        var sim = new Simulation(ColonyContent(), Grass(), seed: 7);
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuilding(0, 0, 0)); // domovina
        Assert.True(sim.TogglePolicy(0)); // guvernér zapnutý

        RunTicks(sim, 200);

        // Auto-stavba plní okolí (radius 2); kolonie musí vzniknout výrazně dál.
        Assert.True(FarthestFromHome(sim, 0, 0) > Distance / 2.0,
            "guvernér má založit samostatnou kolonii daleko od domoviny");
    }

    [Fact]
    public void WithoutPolicy_NoDistantColony()
    {
        var sim = new Simulation(ColonyContent(), Grass(), seed: 7);
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuilding(0, 0, 0));
        // politika zůstává vypnutá

        RunTicks(sim, 200);

        // Bez guvernéra roste jen okolí domoviny (search radius 2 → nic daleko).
        Assert.False(sim.AutoExpandColonies);
        Assert.True(FarthestFromHome(sim, 0, 0) <= Distance / 2.0,
            "bez politiky se nemá zakládat vzdálená kolonie");
    }

    [Fact]
    public void ExpandPolicy_SetsColonyDistanceFromMagnitude()
    {
        var sim = new Simulation(ColonyContent(), Grass());
        Assert.True(sim.TogglePolicy(0));

        Assert.True(sim.AutoExpandColonies);
        Assert.Equal(Distance, sim.ColonyDistance);
    }
}
