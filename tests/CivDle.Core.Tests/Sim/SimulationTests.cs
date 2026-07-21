using CivDle.Core.Content;
using CivDle.Core.Sim;
using CivDle.Core.Tests.Support;
using CivDle.Core.World;
using Xunit;

namespace CivDle.Core.Tests.Sim;

/// <summary>
/// Testy herní smyčky: stavění (validace umístění, cena), výroba (obsazenost,
/// stall na vstupech) a populace (jídlo jako soft pressure). Mapy se skládají
/// ručně, aby bylo jisté, jaký biom je kde.
/// </summary>
public class SimulationTests
{
    // ----- pomůcky -----

    /// <summary>Nekonečný terén s jediným biomem (velikost je jen historický parametr, ignoruje se).</summary>
    private static ITerrain UniformMap(int size, byte biomeIndex) => new UniformTerrain(biomeIndex);

    private static void RunTicks(Simulation sim, int ticks)
    {
        for (int i = 0; i < ticks; i++)
        {
            sim.Tick();
        }
    }

    /// <summary>Skutečný obsah + mapa samá louka (na tu jde stavět dům i farma).</summary>
    private static (GameContent Content, Simulation Sim) RealGrasslandWorld(int size = 16)
    {
        var content = TestData.LoadRealContent();
        var map = UniformMap(size, (byte)content.Biomes.IndexOf("grassland"));
        return (content, new Simulation(content, map));
    }

    // ----- výchozí stav -----

    [Fact]
    public void NewSimulation_StartsWithConfiguredState()
    {
        var (content, sim) = RealGrasslandWorld();

        for (int i = 0; i < content.Resources.Count; i++)
        {
            Assert.Equal(content.Resources[i].StartAmount, sim.GetResource(i));
        }

        Assert.Equal(content.Gameplay.StartingPopulation, sim.Population);
        Assert.Equal(content.Gameplay.BaseHousingCapacity, sim.HousingCapacity);
        Assert.Equal(0, sim.TotalWorkerSlots);
        Assert.Equal(0, sim.Buildings.Length);
    }

    // ----- stavění -----

    [Fact]
    public void PlaceBuilding_DeductsCostAndOccupiesTiles()
    {
        var (content, sim) = RealGrasslandWorld();
        int house = content.Buildings.IndexOf("house");
        int wood = content.Resources.IndexOf("wood");
        double woodBefore = sim.GetResource(wood);

        var result = sim.TryPlaceBuilding(house, 2, 2);

        Assert.Equal(PlacementResult.Ok, result);
        Assert.Equal(woodBefore - 5, sim.GetResource(wood));
        Assert.Equal(1, sim.Buildings.Length);
        Assert.Equal(content.Gameplay.BaseHousingCapacity + 4, sim.HousingCapacity);
        Assert.Equal(PlacementResult.Occupied, sim.CanPlace(house, 2, 2));
    }

    [Fact]
    public void PlaceBuilding_WrongBiome_IsRejected()
    {
        var content = TestData.LoadRealContent();
        var map = UniformMap(8, (byte)content.Biomes.IndexOf("ocean"));
        var sim = new Simulation(content, map);

        var result = sim.TryPlaceBuilding(content.Buildings.IndexOf("house"), 1, 1);

        Assert.Equal(PlacementResult.WrongBiome, result);
        Assert.Equal(0, sim.Buildings.Length);
    }

    [Fact]
    public void PlaceBuilding_WorksAnywhereOnInfiniteMap()
    {
        var (content, sim) = RealGrasslandWorld();
        int farm = content.Buildings.IndexOf("farm"); // 2×2

        // Nekonečná mapa — žádné „mimo mapu"; stavět jde i daleko a na záporných souřadnicích.
        Assert.Equal(PlacementResult.Ok, sim.CanPlace(farm, 9999, 9999));
        Assert.Equal(PlacementResult.Ok, sim.CanPlace(farm, -500, -500));
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuilding(farm, -500, -500));
    }

    [Fact]
    public void PlaceBuilding_FootprintCollision_IsRejected()
    {
        var (content, sim) = RealGrasslandWorld();
        int farm = content.Buildings.IndexOf("farm"); // 2×2
        int house = content.Buildings.IndexOf("house");

        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuilding(farm, 4, 4));
        // Dům na pravém dolním rohu farmy → koliduje.
        Assert.Equal(PlacementResult.Occupied, sim.CanPlace(house, 5, 5));
        Assert.Equal(PlacementResult.Ok, sim.CanPlace(house, 6, 6));
    }

    [Fact]
    public void PlaceBuilding_WithoutResources_IsRejected()
    {
        var (content, sim) = RealGrasslandWorld();
        int house = content.Buildings.IndexOf("house"); // 5 dřeva + 4 prkna; start 10 prken

        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuilding(house, 0, 0));
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuilding(house, 2, 0));
        // Na třetí dům už nezbývají prkna (10 − 4 − 4 = 2 < 4).
        Assert.Equal(PlacementResult.NotEnoughResources, sim.TryPlaceBuilding(house, 4, 0));
    }

    // ----- výroba -----

    [Fact]
    public void Production_LumberCampProducesWoodOverTime()
    {
        var content = TestData.LoadRealContent();
        var map = UniformMap(8, (byte)content.Biomes.IndexOf("forest"));
        var sim = new Simulation(content, map);
        int wood = content.Resources.IndexOf("wood");

        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuilding(content.Buildings.IndexOf("lumber_camp"), 1, 1));
        double woodAfterBuild = sim.GetResource(wood);

        // Populace 5 ≥ 2 sloty → plná obsazenost; recept: 2 dřeva / 40 tiků.
        RunTicks(sim, 39);
        Assert.Equal(woodAfterBuild, sim.GetResource(wood));

        RunTicks(sim, 1);
        Assert.Equal(woodAfterBuild + 2, sim.GetResource(wood));

        RunTicks(sim, 40);
        Assert.Equal(woodAfterBuild + 4, sim.GetResource(wood));
    }

    [Fact]
    public void Production_UnderstaffedBuildingsRunSlower()
    {
        var content = TestData.LoadRealContent();
        var map = UniformMap(8, (byte)content.Biomes.IndexOf("mountains"));
        var sim = new Simulation(content, map);
        int stone = content.Resources.IndexOf("stone");

        // Dva kamenolomy = 6 slotů, populace 5 → obsazenost 5/6; recept 50 tiků
        // se protáhne na ~60. (Populace na horách bez kapacity neroste.)
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuilding(content.Buildings.IndexOf("quarry"), 0, 0));
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuilding(content.Buildings.IndexOf("quarry"), 2, 0));
        double stoneAfterBuild = sim.GetResource(stone);

        RunTicks(sim, 55);
        Assert.Equal(stoneAfterBuild, sim.GetResource(stone));

        RunTicks(sim, 7);
        Assert.Equal(stoneAfterBuild + 4, sim.GetResource(stone));
    }

    [Fact]
    public void Production_MissingInput_StallsUntilSupplied()
    {
        // Syntetický řetězec: pila spotřebovává dřevo → prkna; dřevo dojde → stall.
        var biomes = new[] { TestContent.WaterBiome(), TestContent.LandBiome("grass") };
        var resources = new[]
        {
            new Resource("wood", new RgbColor(140, 90, 40), StartAmount: 7, BaseStorage: 1000),
            new Resource("planks", new RgbColor(200, 170, 110), StartAmount: 0, BaseStorage: 1000),
        };
        var sawmill = new BuildingDef(
            "sawmill", "test", new RgbColor(120, 90, 60), 1, 1,
            WorkerSlots: 1, HousingCapacity: 0,
            BuildCost: new[] { new ResourceAmount(0, 1) },
            Recipe: new Recipe(
                Inputs: new[] { new ResourceAmount(0, 3) },
                Outputs: new[] { new ResourceAmount(1, 1) },
                TimeTicks: 5),
            AllowedBiomes: new[] { false, true },
            StorageBonus: Array.Empty<ResourceAmount>(),
            AutoBuild: false,
            Buildable: true, UpgradesToIndex: -1, UpgradeCost: Array.Empty<ResourceAmount>());
        // Spotřeba jídla vypnutá — test měří jen výrobu, ne ujídání „dřeva jako jídla".
        var gameplay = TestContent.DefaultGameplay with { FoodPerPersonPerSecond = 0 };
        var content = TestContent.Build(biomes, 1, resources, new[] { sawmill }, gameplay);
        var sim = new Simulation(content, UniformMap(4, 1));

        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuilding(0, 0, 0)); // dřevo: 7 − 1 = 6

        // Dva cykly spotřebují 6 dřeva; třetí nemá vstupy a musí stát.
        RunTicks(sim, 40);

        Assert.Equal(0, sim.GetResource(0));
        Assert.Equal(2, sim.GetResource(1));
    }

    // ----- ruční těžba (klik) -----

    [Fact]
    public void Harvest_ForestTile_GivesWood()
    {
        var content = TestData.LoadRealContent();
        var map = UniformMap(8, (byte)content.Biomes.IndexOf("forest"));
        var sim = new Simulation(content, map);
        int wood = content.Resources.IndexOf("wood");
        double woodBefore = sim.GetResource(wood);

        bool harvested = sim.TryHarvest(3, 3, out int resourceIndex, out int amount);

        Assert.True(harvested);
        Assert.Equal(wood, resourceIndex);
        Assert.Equal(2, amount);
        Assert.Equal(woodBefore + 2, sim.GetResource(wood));
    }

    [Fact]
    public void Harvest_BiomeWithoutYield_GivesNothing()
    {
        var (content, sim) = RealGrasslandWorld();
        int wood = content.Resources.IndexOf("wood");
        double woodBefore = sim.GetResource(wood);

        Assert.False(sim.TryHarvest(3, 3, out _, out _));
        Assert.Equal(woodBefore, sim.GetResource(wood));
    }

    [Fact]
    public void Harvest_OccupiedTile_GivesNothing()
    {
        var content = TestData.LoadRealContent();
        var map = UniformMap(8, (byte)content.Biomes.IndexOf("forest"));
        var sim = new Simulation(content, map);
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuilding(content.Buildings.IndexOf("lumber_camp"), 2, 2));

        Assert.False(sim.TryHarvest(2, 2, out _, out _), "Zastavěná dlaždice nemá dávat suroviny.");
        // Volná lesní dlaždice (i záporná — mapa je nekonečná) sběr dá.
        Assert.True(sim.TryHarvest(-1, 0, out _, out _));
    }

    // ----- populace -----

    [Fact]
    public void Population_GrowsWhenFedAndHoused_AndEatsFood()
    {
        var (content, sim) = RealGrasslandWorld();
        int food = content.Resources.IndexOf("food");
        double foodBefore = sim.GetResource(food);

        RunTicks(sim, 50);

        Assert.True(sim.Population > content.Gameplay.StartingPopulation, "Populace má růst.");
        Assert.True(sim.GetResource(food) < foodBefore, "Populace má jíst.");
    }

    [Fact]
    public void Population_StopsAtHousingCapacity()
    {
        var (content, sim) = RealGrasslandWorld();

        RunTicks(sim, 300); // dost času na dorůst kapacity (6) i s rezervou

        Assert.Equal(content.Gameplay.BaseHousingCapacity, sim.Population, precision: 5);
    }

    [Fact]
    public void Population_StopsGrowingWithoutFood()
    {
        var (content, sim) = RealGrasslandWorld();
        int house = content.Buildings.IndexOf("house");
        int food = content.Resources.IndexOf("food");

        // Kapacita nahoru, ať růst zastaví jídlo, ne bydlení.
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuilding(house, 0, 0));
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuilding(house, 2, 0));

        RunTicks(sim, 3000);
        Assert.Equal(0, sim.GetResource(food));
        double populationWhenStarved = sim.Population;
        Assert.True(populationWhenStarved < sim.HousingCapacity, "Bez jídla se kapacita nedoplní.");

        RunTicks(sim, 500);
        Assert.Equal(populationWhenStarved, sim.Population);
    }

    [Fact]
    public void Tick_IsDeterministic()
    {
        var (content, simA) = RealGrasslandWorld();
        var simB = new Simulation(content, UniformMap(16, (byte)content.Biomes.IndexOf("grassland")));
        int house = content.Buildings.IndexOf("house");

        simA.TryPlaceBuilding(house, 1, 1);
        simB.TryPlaceBuilding(house, 1, 1);
        RunTicks(simA, 500);
        RunTicks(simB, 500);

        Assert.Equal(simA.Population, simB.Population);
        for (int i = 0; i < content.Resources.Count; i++)
        {
            Assert.Equal(simA.GetResource(i), simB.GetResource(i));
        }
    }

    // ----- dostupnost (HUD zvýraznění) -----

    [Fact]
    public void CanAfford_ReflectsResourcesRegardlessOfLocation()
    {
        var (content, sim) = RealGrasslandWorld();

        // Start: dřevo 30, prkna 10 → na dům (dřevo 5, prkna 4) mám, na dřevařský
        // dvůr (prkna 12) ne — nezávisle na místě/biomu.
        Assert.True(sim.CanAfford(content.Buildings.IndexOf("house")));
        Assert.False(sim.CanAfford(content.Buildings.IndexOf("lumberyard")));
    }
}
