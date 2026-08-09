using CivDle.Core.Content;
using CivDle.Core.Sim;
using CivDle.Core.Tests.Support;
using CivDle.Core.World;
using Xunit;

namespace CivDle.Core.Tests.Sim;

/// <summary>
/// Co všechno guvernér staví.
///
/// <para>Hráč hlásil, že „guvernér vůbec nestaví doly, kácení stromů ani lesní
/// školky — jen domy a pole". Příčina byla jedna podmínka: jakmile počet
/// pracovních míst přerostl populaci × 2,5, přestal stavět <b>cokoli
/// s dělníky</b>, a to natrvalo. Zbyly mu domy (nula míst) a pole (hlad tuhle
/// kontrolu obchází).</para>
/// </summary>
public class GovernorVarietyTests
{
    private const int Wood = 0;
    private const int Food = 1;

    private static readonly Resource[] Resources =
    {
        new("wood", new RgbColor(120, 90, 60), StartAmount: 100, BaseStorage: 1000),
        new("food", new RgbColor(200, 160, 80), StartAmount: 1000, BaseStorage: 1000),
    };

    private static BuildingDef House() => new(
        "house", "housing", new RgbColor(180, 100, 60), 1, 1,
        WorkerSlots: 0, HousingCapacity: 6,
        BuildCost: new[] { new ResourceAmount(Wood, 5) },
        Recipe: null,
        AllowedBiomes: new[] { false, true },
        StorageBonus: Array.Empty<ResourceAmount>(),
        AutoBuild: true, Buildable: true,
        UpgradesToIndex: -1, UpgradeCost: Array.Empty<ResourceAmount>(),
        PowerSupply: 0, PowerDemand: 0);

    /// <summary>
    /// Dřevorubci: mají dělníky a vyrábějí dřevo z ničeho (jako těžba v datech).
    /// Platí se jídlem, ne dřevem — výrobna, která stojí to, co teprve vyrobí,
    /// by šla postavit jen dokud je čeho dost, a v nouzi právě ne.
    /// </summary>
    private static BuildingDef LumberCamp() => new(
        "lumber_camp", "production", new RgbColor(90, 120, 70), 1, 1,
        WorkerSlots: 4, HousingCapacity: 0,
        BuildCost: new[] { new ResourceAmount(Food, 20) },
        Recipe: new Recipe(Array.Empty<ResourceAmount>(), new[] { new ResourceAmount(Wood, 2) }, 20),
        AllowedBiomes: new[] { false, true },
        StorageBonus: Array.Empty<ResourceAmount>(),
        AutoBuild: true, Buildable: true,
        UpgradesToIndex: -1, UpgradeCost: Array.Empty<ResourceAmount>(),
        PowerSupply: 0, PowerDemand: 0);

    /// <summary>Výchozí nastavení testů auto-stavbu vypíná; tady ji potřebujeme zapnutou.</summary>
    private static GameplayConfig Gameplay() => TestContent.DefaultGameplay with
    {
        FoodResourceIndex = Food,
        FoodPerPersonPerSecond = 0.04,
        PopulationGrowthPerSecond = 0.5,
        AutoBuild = new AutoBuildConfig(IntervalTicks: 5, SearchRadius: 6, PopulationHeadroom: 2),
    };

    private static GameContent Content() => TestContent.Build(
        biomes: new[] { TestContent.WaterBiome(), TestContent.LandBiome("grass") },
        resources: Resources,
        buildings: new[] { House(), LumberCamp() },
        gameplay: Gameplay());

    private static Simulation World(GameContent content)
    {
        var sim = new Simulation(content, new UniformTerrain((byte)1));
        sim.TryPlaceBuilding(0, 0, 0); // první budovu musí položit hráč
        return sim;
    }

    /// <summary>
    /// Utratí surovinu stavěním domů na volné dlaždice, dokud neklesne pod mez.
    /// Každý dům jinam a s pojistkou proti nekonečné smyčce: obsazené místo by
    /// jinak zásobu nesnížilo a test by se zacyklil.
    /// </summary>
    private static void Drain(Simulation sim, int resource, double target)
    {
        for (int i = 0; i < 4000 && sim.GetResource(resource) > target; i++)
        {
            sim.TryPlaceBuilding(0, 40 + i % 60, 40 + i / 60);
        }

        Assert.True(sim.GetResource(resource) <= target,
            $"nepodařilo se zásobu srazit pod {target}, zbylo {sim.GetResource(resource)}");
    }

    private static int CountOf(Simulation sim, int defIndex)
    {
        int count = 0;
        foreach (var building in sim.Buildings.ToArray())
        {
            if (building.DefIndex == defIndex)
            {
                count++;
            }
        }

        return count;
    }

    [Fact]
    public void TheGovernorBuildsProductionAndNotOnlyHouses()
    {
        var content = Content();
        var sim = World(content);

        for (int i = 0; i < 3000; i++)
        {
            sim.Tick();
        }

        Assert.True(CountOf(sim, 1) > 0,
            $"guvernér postavil {CountOf(sim, 0)} domů a ani jednu výrobnu");
    }

    [Fact]
    public void FullEmploymentIsNotAReasonToStopBuilding()
    {
        // Jádro chyby: strop pracovních míst byl tvrdý zákaz. Když nikdo
        // nezahálí, je další výrobna přesně to, co město potřebuje.
        var content = Content();
        var sim = World(content);
        for (int i = 0; i < 3000; i++)
        {
            sim.Tick();
        }

        int before = CountOf(sim, 1);
        Assert.Equal(0, sim.IdleBuildings);

        for (int i = 0; i < 3000; i++)
        {
            sim.Tick();
        }

        Assert.True(CountOf(sim, 1) >= before, "počet výroben nesmí zamrznout");
    }

    [Fact]
    public void ProductionKeepsUpWithHousing()
    {
        // Ne „jen domy": po delším běhu má stát aspoň jedna výrobna na každých
        // pár domů, jinak město roste v lidech a nikdo pro ně nic nedělá.
        var content = Content();
        var sim = World(content);

        for (int i = 0; i < 6000; i++)
        {
            sim.Tick();
        }

        int houses = CountOf(sim, 0);
        int works = CountOf(sim, 1);
        Assert.True(works * 10 >= houses, $"{houses} domů proti {works} výrobnám");
    }
}
