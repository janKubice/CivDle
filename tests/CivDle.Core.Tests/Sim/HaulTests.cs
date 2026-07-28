using CivDle.Core.Content;
using CivDle.Core.Sim;
using CivDle.Core.Tests.Support;
using CivDle.Core.World;
using Xunit;

namespace CivDle.Core.Tests.Sim;

/// <summary>
/// Svoz zboží: důl dvě stě dlaždic od města vyrábí míň, dokud k němu hráč
/// nepostaví sklad. Bez téhle vrstvy se zboží teleportuje a sklad je jen
/// „větší číslo kapacity".
///
/// <para>Přepočet je rozložený do ticků, takže testy po postavení skladu chvíli
/// tikají — to je záměr systému, ne slabina testu.</para>
/// </summary>
public class HaulTests
{
    private const int Wood = 0;

    private static GameContent Content(HaulConfig? haul = null)
    {
        var biomes = new[] { TestContent.WaterBiome(), TestContent.LandBiome("plain") };
        var resources = new[] { new Resource("wood", new RgbColor(1, 1, 1), StartAmount: 100, BaseStorage: 1_000_000) };

        var camp = new BuildingDef(
            "camp", "production", new RgbColor(100, 100, 100), 1, 1,
            WorkerSlots: 1, HousingCapacity: 0,
            BuildCost: Array.Empty<ResourceAmount>(),
            Recipe: new Recipe(
                Inputs: Array.Empty<ResourceAmount>(),
                Outputs: new[] { new ResourceAmount(Wood, 10) },
                TimeTicks: 1),
            AllowedBiomes: new[] { false, true },
            StorageBonus: Array.Empty<ResourceAmount>(),
            AutoBuild: false, Buildable: true, UpgradesToIndex: -1,
            UpgradeCost: Array.Empty<ResourceAmount>(), PowerSupply: 0, PowerDemand: 0);

        var depot = new BuildingDef(
            "depot", "storage", new RgbColor(120, 120, 120), 1, 1,
            WorkerSlots: 0, HousingCapacity: 0,
            BuildCost: Array.Empty<ResourceAmount>(),
            Recipe: null,
            AllowedBiomes: new[] { false, true },
            StorageBonus: new[] { new ResourceAmount(Wood, 100) },
            AutoBuild: false, Buildable: true, UpgradesToIndex: -1,
            UpgradeCost: Array.Empty<ResourceAmount>(), PowerSupply: 0, PowerDemand: 0);

        var gameplay = TestContent.DefaultGameplay with
        {
            FoodPerPersonPerSecond = 0,
            PopulationGrowthPerSecond = 0,
            HaulOrNull = haul ?? new HaulConfig(FreeDistance: 5, Range: 10, MinMultiplier: 0.3),
        };
        return TestContent.Build(biomes, 1, resources, new[] { camp, depot }, gameplay);
    }

    private static Simulation NewSim(HaulConfig? haul = null) =>
        new(Content(haul), new UniformTerrain(1));

    private static double ProducedOver(Simulation sim, int ticks)
    {
        double before = sim.GetResource(Wood);
        for (int i = 0; i < ticks; i++)
        {
            sim.Tick();
        }

        return sim.GetResource(Wood) - before;
    }

    /// <summary>Odtiká, dokud se rozložený přepočet svozu nepropíše do všech budov.</summary>
    private static void SettleHaul(Simulation sim)
    {
        for (int i = 0; i < 5; i++)
        {
            sim.Tick();
        }
    }

    [Fact]
    public void RemoteBuilding_ProducesLessThanOneNextToTheCity()
    {
        // Obě města mají jednu jedinou budovu, takže těžiště sedí na ní —
        // rozdíl proto udělá až druhá budova daleko od té první.
        var sim = NewSim();
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuildingFree(0, 0, 0));
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuildingFree(0, 60, 0));
        SettleHaul(sim);

        Assert.True(sim.Buildings[0].HaulMult > sim.Buildings[1].HaulMult,
            $"vzdálená budova má mít horší svoz ({sim.Buildings[0].HaulMult} vs {sim.Buildings[1].HaulMult})");
        Assert.True(sim.Buildings[1].HaulMult < 1f);
    }

    [Fact]
    public void BuildingADepotNearby_RestoresTheProduction()
    {
        var sim = NewSim();
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuildingFree(0, 0, 0));
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuildingFree(0, 60, 0));
        SettleHaul(sim);
        double before = ProducedOver(sim, 20);

        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuildingFree(1, 61, 0)); // sklad k dolu
        SettleHaul(sim);
        double after = ProducedOver(sim, 20);

        Assert.True(after > before, $"sklad u vzdáleného dolu má výrobu zvednout ({after} vs {before})");
    }

    [Fact]
    public void DemolishingTheDepot_TakesTheBonusAway()
    {
        var sim = NewSim();
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuildingFree(0, 0, 0));
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuildingFree(0, 60, 0));
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuildingFree(1, 61, 0));
        SettleHaul(sim);
        float withDepot = sim.Buildings[1].HaulMult;

        Assert.Equal(PlacementResult.Ok, sim.TryDemolish(2)); // sklad pryč
        SettleHaul(sim);

        Assert.True(sim.Buildings[1].HaulMult < withDepot,
            $"bez skladu má svoz zase klesnout ({sim.Buildings[1].HaulMult} vs {withDepot})");
    }

    [Fact]
    public void PenaltyNeverGoesBelowTheFloor()
    {
        // Kolonie na druhém konci mapy zpomalí, ale nesmí umřít — hra netrestá
        // do nuly, jen tlačí měkce.
        var haul = new HaulConfig(FreeDistance: 5, Range: 10, MinMultiplier: 0.3);
        var sim = NewSim(haul);
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuildingFree(0, 0, 0));
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuildingFree(0, 100_000, 0));
        SettleHaul(sim);

        Assert.Equal(0.3f, sim.Buildings[1].HaulMult, 3);
    }

    [Fact]
    public void NearbyBuildings_PayNothing()
    {
        var sim = NewSim();
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuildingFree(0, 0, 0));
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuildingFree(0, 2, 1));
        SettleHaul(sim);

        Assert.Equal(1f, sim.Buildings[0].HaulMult, 3);
        Assert.Equal(1f, sim.Buildings[1].HaulMult, 3);
    }

    [Fact]
    public void MovedBuilding_GetsTheHaulOfItsNewSpot()
    {
        var sim = NewSim();
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuildingFree(0, 0, 0));
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuildingFree(0, 1, 0));
        SettleHaul(sim);
        float atHome = sim.Buildings[1].HaulMult;

        Assert.Equal(PlacementResult.Ok, sim.TryMoveBuilding(1, 80, 0));

        Assert.True(sim.Buildings[1].HaulMult < atHome,
            $"po přesunu do dálky má svoz klesnout hned ({sim.Buildings[1].HaulMult} vs {atHome})");
    }

    [Fact]
    public void DisabledHaul_LeavesEveryoneAtFullSpeed()
    {
        // Starší data blok neuvádějí — hra se pak musí chovat přesně jako dřív.
        var sim = new Simulation(Content(HaulConfig.Disabled), new UniformTerrain(1));
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuildingFree(0, 0, 0));
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuildingFree(0, 100_000, 0));
        SettleHaul(sim);

        Assert.Equal(1f, sim.Buildings[1].HaulMult, 3);
    }

    [Theory]
    [InlineData(0, 1.0)]
    [InlineData(5, 1.0)]
    [InlineData(15, 0.5)]
    [InlineData(1000, 0.3)]
    public void Formula_FallsOffSmoothlyAndThenFlattens(int distance, double expected)
    {
        var haul = new HaulConfig(FreeDistance: 5, Range: 10, MinMultiplier: 0.3);

        Assert.Equal(expected, haul.Multiplier(distance), 3);
    }

    [Fact]
    public void RealContent_TurnsHaulOn()
    {
        var haul = TestData.LoadRealContent().Gameplay.Haul;

        Assert.True(haul.IsEnabled, "svoz má být ve skutečných datech zapnutý");
        Assert.True(haul.MinMultiplier > 0, "vzdálená kolonie má zpomalit, ne umřít");
    }
}
