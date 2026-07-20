using CivDle.Core.Content;
using CivDle.Core.Sim;
using CivDle.Core.Tests.Support;
using CivDle.Core.World;
using Xunit;

namespace CivDle.Core.Tests.Sim;

/// <summary>
/// Sklady (fáze 3): suroviny mají kapacitu, výroba se u plného skladu zastropuje
/// (přebytek propadá, nic se neničí) a skladové budovy kapacitu zvedají.
/// </summary>
public class StorageTests
{
    private static ITerrain GrassMap(int size = 6) => new UniformTerrain(1);

    private static void RunTicks(Simulation sim, int ticks)
    {
        for (int i = 0; i < ticks; i++)
        {
            sim.Tick();
        }
    }

    /// <summary>Obsah: dřevo s malým skladem, „mince" na ceny, rychlý producent dřeva a sklad +10.</summary>
    private static GameContent StorageContent()
    {
        var biomes = new[] { TestContent.WaterBiome(), TestContent.LandBiome("grass") };
        var resources = new[]
        {
            new Resource("wood", new RgbColor(140, 90, 40), StartAmount: 0, BaseStorage: 5),
            new Resource("coin", new RgbColor(240, 200, 60), StartAmount: 10, BaseStorage: 1000),
        };
        var producer = new BuildingDef(
            "producer", "test", new RgbColor(100, 100, 100), 1, 1,
            WorkerSlots: 1, HousingCapacity: 0,
            BuildCost: new[] { new ResourceAmount(1, 1) },
            Recipe: new Recipe(
                Inputs: Array.Empty<ResourceAmount>(),
                Outputs: new[] { new ResourceAmount(0, 3) },
                TimeTicks: 1),
            AllowedBiomes: new[] { false, true },
            StorageBonus: Array.Empty<ResourceAmount>(),
            AutoBuild: false,
            Buildable: true, UpgradesToIndex: -1, UpgradeCost: Array.Empty<ResourceAmount>());
        var warehouse = new BuildingDef(
            "warehouse", "test", new RgbColor(120, 90, 60), 1, 1,
            WorkerSlots: 0, HousingCapacity: 0,
            BuildCost: new[] { new ResourceAmount(1, 1) },
            Recipe: null,
            AllowedBiomes: new[] { false, true },
            StorageBonus: new[] { new ResourceAmount(0, 10) },
            AutoBuild: false,
            Buildable: true, UpgradesToIndex: -1, UpgradeCost: Array.Empty<ResourceAmount>());

        var gameplay = TestContent.DefaultGameplay with { FoodPerPersonPerSecond = 0, FoodResourceIndex = 1 };
        return TestContent.Build(biomes, 1, resources, new[] { producer, warehouse }, gameplay);
    }

    [Fact]
    public void Production_IsCappedByStorage()
    {
        var content = StorageContent();
        var sim = new Simulation(content, GrassMap());
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuilding(0, 1, 1));

        // Producent sype 3 dřeva každý tik, ale sklad má jen 5 — přebytek propadá.
        RunTicks(sim, 20);

        Assert.Equal(5, sim.GetResource(0));
        Assert.Equal(5, sim.GetStorageCap(0));
    }

    [Fact]
    public void Warehouse_RaisesStorageCap()
    {
        var content = StorageContent();
        var sim = new Simulation(content, GrassMap());
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuilding(0, 1, 1));
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuilding(1, 3, 3));

        Assert.Equal(15, sim.GetStorageCap(0));

        RunTicks(sim, 20);

        Assert.Equal(15, sim.GetResource(0));
    }

    [Fact]
    public void Harvest_FullStorage_GivesNothing()
    {
        // Biom s klikacím výnosem +2 dřeva, sklad 3 a zásoba 2 → klik se nevejde.
        var biomes = new[]
        {
            TestContent.WaterBiome(),
            TestContent.LandBiome("grass") with { ClickYield = new ClickYield(0, 2) },
        };
        var resources = new[] { new Resource("wood", new RgbColor(1, 1, 1), StartAmount: 2, BaseStorage: 3) };
        var content = TestContent.Build(biomes, 1, resources);
        var sim = new Simulation(content, GrassMap());

        Assert.False(sim.TryHarvest(2, 2, out _, out _));
        Assert.Equal(2, sim.GetResource(0));
    }

    [Fact]
    public void RealContent_WarehouseDefinesStorageForAllResources()
    {
        var content = TestData.LoadRealContent();
        var warehouse = content.Buildings[content.Buildings.IndexOf("warehouse")];

        // Sklad má zvedat kapacitu každé suroviny — nová surovina bez skladu = chyba dat.
        Assert.Equal(content.Resources.Count, warehouse.StorageBonus.Count);
    }
}
