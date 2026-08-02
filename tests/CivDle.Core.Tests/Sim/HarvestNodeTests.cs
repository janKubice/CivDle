using CivDle.Core.Content;
using CivDle.Core.Sim;
using CivDle.Core.Tests.Support;
using CivDle.Core.World;
using Xunit;

namespace CivDle.Core.Tests.Sim;

/// <summary>
/// Krajina není nekonečná: uzel se vytěží a časem doroste.
///
/// <para>Do téhle chvíle byl každý strom bezedný, takže hráč neměl důvod se hnout
/// z místa. Se spotřebou má expanze smysl — a s dorůstáním má smysl i počkat
/// nebo zasadit háj.</para>
/// </summary>
public class HarvestNodeTests
{
    private const int Wood = 0;

    private static GameContent Content(int charges = 3, double regrowSeconds = 5)
    {
        var biomes = new[]
        {
            TestContent.WaterBiome(),
            TestContent.LandBiome("forest") with
            {
                ClickYield = new ClickYield(Wood, 2, charges, regrowSeconds),
            },
        };
        var resources = new[] { new Resource("wood", new RgbColor(1, 1, 1), StartAmount: 0, BaseStorage: 1_000_000) };
        var gameplay = TestContent.DefaultGameplay with
        {
            FoodPerPersonPerSecond = 0,
            PopulationGrowthPerSecond = 0,
        };
        return TestContent.Build(biomes, 1, resources, gameplay: gameplay);
    }

    private static Simulation NewSim(int charges = 3, double regrowSeconds = 5) =>
        new(Content(charges, regrowSeconds), new UniformTerrain(1));

    private static void Wait(Simulation sim, double seconds)
    {
        for (int i = 0; i < (int)(seconds * Simulation.TicksPerSecond) + 1; i++)
        {
            sim.Tick();
        }
    }

    [Fact]
    public void NodeRunsOutAfterItsCharges()
    {
        var sim = NewSim(charges: 3);

        Assert.True(sim.TryHarvest(5, 5, out _, out _));
        Assert.True(sim.TryHarvest(5, 5, out _, out _));
        Assert.True(sim.TryHarvest(5, 5, out _, out _));

        Assert.False(sim.TryHarvest(5, 5, out _, out _));
        Assert.Equal(0, sim.NodeChargesLeft(5, 5));
    }

    [Fact]
    public void EmptyNodeGivesNothing()
    {
        var sim = NewSim(charges: 1);
        Assert.True(sim.TryHarvest(5, 5, out _, out _));
        double after = sim.GetResource(Wood);

        Assert.False(sim.TryHarvest(5, 5, out _, out _));

        Assert.Equal(after, sim.GetResource(Wood), 6);
    }

    [Fact]
    public void NodeGrowsBackAfterItsTime()
    {
        var sim = NewSim(charges: 1, regrowSeconds: 5);
        Assert.True(sim.TryHarvest(5, 5, out _, out _));
        Assert.Equal(0, sim.NodeChargesLeft(5, 5));

        Wait(sim, 5);

        Assert.Equal(1, sim.NodeChargesLeft(5, 5));
        Assert.True(sim.TryHarvest(5, 5, out _, out _));
    }

    [Fact]
    public void NodeThatNeverRegrowsStaysEmpty()
    {
        // Ložiska rud se nevrací — proto se za nimi musí jít dál.
        var sim = NewSim(charges: 1, regrowSeconds: 0);
        Assert.True(sim.TryHarvest(5, 5, out _, out _));

        Wait(sim, 600);

        Assert.Equal(0, sim.NodeChargesLeft(5, 5));
        Assert.False(sim.TryHarvest(5, 5, out _, out _));
    }

    [Fact]
    public void HarvestingOneTileLeavesTheNeighboursAlone()
    {
        var sim = NewSim(charges: 1);
        Assert.True(sim.TryHarvest(5, 5, out _, out _));

        Assert.Equal(0, sim.NodeChargesLeft(5, 5));
        Assert.Equal(1, sim.NodeChargesLeft(6, 5));
        Assert.True(sim.TryHarvest(6, 5, out _, out _));
    }

    [Fact]
    public void UntouchedWorldCostsNoMemory()
    {
        // Na nekonečné mapě se nesmí pamatovat každý strom — jen ty načaté.
        var sim = NewSim(charges: 2);
        Assert.Equal(0, sim.Nodes.TouchedCount);

        sim.TryHarvest(5, 5, out _, out _);

        Assert.Equal(1, sim.Nodes.TouchedCount);
    }

    [Fact]
    public void FullyRegrownTileIsForgottenAgain()
    {
        // Jinak by evidence rostla i tam, kde se les dávno vrátil.
        var sim = NewSim(charges: 2, regrowSeconds: 3);
        sim.TryHarvest(5, 5, out _, out _);
        sim.TryHarvest(5, 5, out _, out _);
        Assert.Equal(1, sim.Nodes.TouchedCount);

        Wait(sim, 3);
        sim.TryHarvest(5, 5, out _, out _); // první sběr po dorostení

        Assert.Equal(1, sim.Nodes.TouchedCount); // zase načatá, ale jen jedna
        Assert.Equal(1, sim.NodeChargesLeft(5, 5));
    }

    [Fact]
    public void PlantingRefillsASpentTile()
    {
        // Zasadit háj na vytěžené místo musí mít smysl — jinak by sázení
        // fungovalo jen na panenské krajině, kde ho nikdo nepotřebuje.
        var sim = NewSim(charges: 1, regrowSeconds: 10_000);
        Assert.True(sim.TryHarvest(5, 5, out _, out _));
        Assert.Equal(0, sim.NodeChargesLeft(5, 5));

        sim.AddResource(Wood, 1000);
        Assert.Equal(PlacementResult.Ok, sim.TryPlant(5, 5));

        Assert.True(sim.NodeChargesLeft(5, 5) > 0);
        Assert.True(sim.TryHarvest(5, 5, out _, out _));
    }

    [Fact]
    public void UnlimitedNodes_BehaveLikeBefore()
    {
        // Starší data nemají 'charges' — krajina pak zůstává nevyčerpatelná.
        var sim = NewSim(charges: 0, regrowSeconds: 0);
        for (int i = 0; i < 50; i++)
        {
            Assert.True(sim.TryHarvest(5, 5, out _, out _));
        }

        Assert.Equal(0, sim.Nodes.TouchedCount);
    }

    [Fact]
    public void ProductionBuilding_EatsTheForestAroundIt()
    {
        // Jádro celé mechaniky: les neubývá klikáním, ale prací. Pila kácí proto,
        // že v ní pracují lidé — a čím větší město, tím rychleji.
        var sim = new Simulation(ContentWithSawmill(charges: 2, radius: 1), new UniformTerrain(1));
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuildingFree(0, 5, 5));

        int before = TotalChargesAround(sim, 5, 5, 1);
        for (int i = 0; i < 60; i++)
        {
            sim.Tick();
        }

        Assert.True(TotalChargesAround(sim, 5, 5, 1) < before, "výrobna má okolí ubírat sama od sebe");
        Assert.True(sim.Nodes.TouchedCount > 0);
    }

    [Fact]
    public void BuildingStopsWhenItRunsOutOfLand()
    {
        // Vytěžené okolí výrobnu zastaví — to je ten tlak, kvůli kterému má smysl
        // expandovat, sázet nebo ji přesunout. Nic se přitom neboří.
        var sim = new Simulation(ContentWithSawmill(charges: 1, radius: 1), new UniformTerrain(1));
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuildingFree(0, 5, 5));

        for (int i = 0; i < 400; i++)
        {
            sim.Tick();
        }

        double stalled = sim.GetResource(Wood);
        for (int i = 0; i < 200; i++)
        {
            sim.Tick();
        }

        Assert.Equal(stalled, sim.GetResource(Wood), 6);
        Assert.True(sim.Buildings[0].OutOfResources, "budova má hlásit, že v dosahu nic není");

        // A hlavně musí říct PROČ. Bez důvodu viděl hráč jen červený roh
        // a neměl jak zjistit, co má udělat.
        Assert.Equal(BuildingStall.NoTerrain, sim.Buildings[0].Stall);
    }

    [Fact]
    public void BiggerCityEatsTheLandFaster()
    {
        // „Víc lidí → větší spotřeba" musí platit i pro krajinu, ne jen pro sklad.
        int slow = ChargesUsedWithPopulation(population: 1);
        int fast = ChargesUsedWithPopulation(population: 40);

        Assert.True(fast > slow, $"větší město má ubírat krajinu rychleji ({fast} vs {slow})");
    }

    /// <summary>Kolik nábojů zmizí z okolí za daný čas při zadané populaci.</summary>
    private static int ChargesUsedWithPopulation(double population)
    {
        var content = ContentWithSawmill(charges: 40, radius: 3, workerSlots: 4);
        var gameplay = content.Gameplay with { StartingPopulation = population };
        var sim = new Simulation(content.WithGameplay(gameplay), new UniformTerrain(1));

        // Několik pil vedle sebe: s jedinou by rozdíl schoval strop jejích slotů.
        for (int i = 0; i < 6; i++)
        {
            sim.TryPlaceBuildingFree(0, 5 + i * 8, 5);
        }

        int before = 0;
        for (int i = 0; i < 6; i++)
        {
            before += TotalChargesAround(sim, 5 + i * 8, 5, 3);
        }

        for (int t = 0; t < 300; t++)
        {
            sim.Tick();
        }

        int after = 0;
        for (int i = 0; i < 6; i++)
        {
            after += TotalChargesAround(sim, 5 + i * 8, 5, 3);
        }

        return before - after;
    }

    private static int TotalChargesAround(Simulation sim, int cx, int cy, int radius)
    {
        int total = 0;
        for (int y = cy - radius; y <= cy + radius; y++)
        {
            for (int x = cx - radius; x <= cx + radius; x++)
            {
                total += sim.NodeChargesLeft(x, y);
            }
        }

        return total;
    }

    /// <summary>Obsah s pilou, která bere dřevo přímo z okolního lesa.</summary>
    private static GameContent ContentWithSawmill(int charges, int radius, int workerSlots = 1)
    {
        var biomes = new[]
        {
            TestContent.WaterBiome(),
            TestContent.LandBiome("forest") with { ClickYield = new ClickYield(Wood, 2, charges, 0) },
        };
        var resources = new[] { new Resource("wood", new RgbColor(1, 1, 1), StartAmount: 0, BaseStorage: 1_000_000) };

        var sawmill = new BuildingDef(
            "sawmill", "production", new RgbColor(100, 100, 100), 1, 1,
            WorkerSlots: workerSlots, HousingCapacity: 0,
            BuildCost: Array.Empty<ResourceAmount>(),
            Recipe: new Recipe(
                Inputs: Array.Empty<ResourceAmount>(),
                Outputs: new[] { new ResourceAmount(Wood, 1) },
                TimeTicks: 2),
            AllowedBiomes: new[] { false, true },
            StorageBonus: Array.Empty<ResourceAmount>(),
            AutoBuild: false, Buildable: true, UpgradesToIndex: -1,
            UpgradeCost: Array.Empty<ResourceAmount>(), PowerSupply: 0, PowerDemand: 0,
            TerrainHarvestRadius: radius);

        var gameplay = TestContent.DefaultGameplay with
        {
            FoodPerPersonPerSecond = 0,
            PopulationGrowthPerSecond = 0,
        };
        return TestContent.Build(biomes, 1, resources, new[] { sawmill }, gameplay);
    }

    [Fact]
    public void RealContent_MakesForestsRenewableAndOreFinite()
    {
        var content = TestData.LoadRealContent();
        var forest = content.Biomes[content.Biomes.IndexOf("forest")].ClickYield;
        var mountains = content.Biomes[content.Biomes.IndexOf("mountains")].ClickYield;

        Assert.NotNull(forest);
        Assert.True(forest!.IsExhaustible, "les má jít vytěžit");
        Assert.True(forest.IsRenewable, "les má dorůstat — jinak by hra po chvíli stála");

        Assert.NotNull(mountains);
        Assert.True(mountains!.IsExhaustible);
        Assert.False(mountains.IsRenewable, "kámen se nevrací, za tím se musí jít dál");

        // Dorůstání má být v minutách, ne v sekundách — les, který se vrátí,
        // než hráč dojde na druhý konec města, není žádné omezení.
        Assert.True(forest.RegrowSeconds >= 300, $"les dorůstá moc rychle ({forest.RegrowSeconds} s)");
    }

    [Fact]
    public void RealContent_HasBuildingsThatEatTheLand()
    {
        var content = TestData.LoadRealContent();
        int harvesters = content.Buildings.All.Count(b => b.HarvestsTerrain);

        Assert.True(harvesters >= 8, $"z krajiny má brát víc výroben, bere jen {harvesters}");
        foreach (var def in content.Buildings.All.Where(b => b.HarvestsTerrain))
        {
            Assert.NotNull(def.Recipe);
        }
    }
}
