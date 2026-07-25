using CivDle.Core.Content;
using CivDle.Core.Sim;
using CivDle.Core.Tests.Support;
using CivDle.Core.World;
using Xunit;

namespace CivDle.Core.Tests.Sim;

/// <summary>
/// Měřítko (progression-prestige.md §3): každý Vzestup zvedne strop populace
/// o řád a odemkne obsah (megastruktury). Strop je MĚKKÝ — růst se u něj zastaví,
/// nic se neboří; je to pobídka k Vzestupu, ne trest.
/// </summary>
public class AscensionTierTests
{
    private static ITerrain Grass() => new UniformTerrain(1);

    /// <summary>Obsah: „hut" (bydlení 100, volně stavitelná) + „spire" (megastruktura odemčená stupněm 1).</summary>
    private static GameContent TierContent(double tier0Cap = 10, double tier1Cap = 1000)
    {
        var biomes = new[] { TestContent.WaterBiome(), TestContent.LandBiome("grass") };
        var resources = new[] { new Resource("food", new RgbColor(200, 180, 60), StartAmount: 10000, BaseStorage: 100000) };
        var mask = new[] { false, true };
        var hut = new BuildingDef(
            "hut", "housing", new RgbColor(200, 100, 50), 1, 1,
            WorkerSlots: 0, HousingCapacity: 100,
            BuildCost: new[] { new ResourceAmount(0, 1) },
            Recipe: null, AllowedBiomes: mask, StorageBonus: System.Array.Empty<ResourceAmount>(),
            AutoBuild: false, Buildable: true, UpgradesToIndex: -1,
            UpgradeCost: System.Array.Empty<ResourceAmount>(), PowerSupply: 0, PowerDemand: 0);
        var spire = new BuildingDef(
            "spire", "megastructure", new RgbColor(150, 100, 220), 1, 1,
            WorkerSlots: 0, HousingCapacity: 5000,
            BuildCost: new[] { new ResourceAmount(0, 1) },
            Recipe: null, AllowedBiomes: mask, StorageBonus: System.Array.Empty<ResourceAmount>(),
            AutoBuild: false, Buildable: true, UpgradesToIndex: -1,
            UpgradeCost: System.Array.Empty<ResourceAmount>(), PowerSupply: 0, PowerDemand: 0);

        var tiers = new[]
        {
            new AscensionTierDef("small", 0, tier0Cap, System.Array.Empty<int>()),
            new AscensionTierDef("big", 1, tier1Cap, new[] { 1 }), // odemyká spire
        };

        // Vzestup hned dostupný (populace ≥ 1), ať jde tier posunout v testu.
        var prestige = new PrestigeConfig(
            new GoalCondition(MetricKind.Population, -1, 1), MetricKind.Population, -1, 1);
        var gameplay = TestContent.DefaultGameplay with { FoodPerPersonPerSecond = 0, StartingPopulation = 5 };
        return TestContent.Build(
            biomes, 1, resources, new[] { hut, spire }, gameplay,
            prestige: prestige, ascensionTiers: tiers);
    }

    private static void RunTicks(Simulation sim, int ticks)
    {
        for (int i = 0; i < ticks; i++)
        {
            sim.Tick();
        }
    }

    [Fact]
    public void StartsAtFirstTier_WithItsCap()
    {
        var content = TierContent();
        var sim = new Simulation(content, Grass());

        Assert.Equal("small", content.AscensionTiers[sim.CurrentTierIndex].Id);
        Assert.Equal(10, sim.PopulationCap);
    }

    [Fact]
    public void PopulationStopsAtScaleCap_EvenWithHousingSpare()
    {
        var sim = new Simulation(TierContent(tier0Cap: 10), Grass());
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuilding(0, 2, 2)); // bydlení pro 100

        RunTicks(sim, 600);

        // Bydlení by uneslo 100+, ale měřítko stupně 0 stropuje na 10.
        Assert.True(sim.HousingCapacity > 10);
        Assert.Equal(10, sim.Population, precision: 6);
    }

    [Fact]
    public void Ascending_RaisesTierAndCap()
    {
        var content = TierContent(tier0Cap: 10, tier1Cap: 1000);
        var sim = new Simulation(content, Grass());
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuilding(0, 2, 2));
        RunTicks(sim, 600);
        Assert.Equal(10, sim.Population, precision: 6); // u stropu

        Assert.Equal(PlacementResult.Ok, sim.TryAscend());

        Assert.Equal("big", content.AscensionTiers[sim.CurrentTierIndex].Id);
        Assert.Equal(1000, sim.PopulationCap);

        // Po Vzestupu roste dál na větším plátně (mapa se resetovala → postav znovu).
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuilding(0, 2, 2));
        RunTicks(sim, 600);
        Assert.True(sim.Population > 10, "vyšší stupeň měřítka má pustit populaci výš");
    }

    [Fact]
    public void Megastructure_LockedUntilItsTierIsReached()
    {
        var sim = new Simulation(TierContent(), Grass());

        Assert.False(sim.IsBuildingBuildable(1)); // spire patří stupni 1
        Assert.Equal(PlacementResult.NotUnlocked, sim.CanPlace(1, 2, 2));

        Assert.Equal(PlacementResult.Ok, sim.TryAscend());

        Assert.True(sim.IsBuildingBuildable(1));
        Assert.Equal(PlacementResult.Ok, sim.CanPlace(1, 2, 2));
    }

    [Fact]
    public void RealContent_MegastructuresAreTierGated()
    {
        // Megastruktury nesmí být dostupné hned v prvním běhu — jsou odměna za měřítko.
        var content = TestData.LoadRealContent();
        var sim = new Simulation(content, Grass());

        foreach (var id in new[] { "megacity_spire", "grand_exchange", "orbital_ring", "world_forge" })
        {
            Assert.True(content.Buildings.TryIndexOf(id, out int index), $"chybí megastruktura '{id}'");
            Assert.False(sim.IsBuildingBuildable(index), $"megastruktura '{id}' nemá být dostupná na startu");
        }
    }
}
