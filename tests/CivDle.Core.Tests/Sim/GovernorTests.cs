using CivDle.Core.Content;
using CivDle.Core.Sim;
using CivDle.Core.Tests.Support;
using CivDle.Core.World;
using Xunit;

namespace CivDle.Core.Tests.Sim;

/// <summary>
/// Guvernér: co město postaví samo, když ho hráč nechá být.
///
/// <para>Každý test tady odpovídá chybě, kterou odhalilo měření na šesti seedech.
/// Nejsou to hypotetické případy — přesně tohle se dělo:</para>
/// <list type="bullet">
/// <item>město rostlo v lidech, ale nikdo nestavěl pole → hlad,</item>
/// <item>guvernér se zasekl na potřebě, kterou neuměl splnit, a přestal stavět úplně,</item>
/// <item>chtěl dům, neměl na prkna, a nikdy ho nenapadlo postavit pilu,</item>
/// <item>stavěl výrobny, které neměl kdo obsadit.</item>
/// </list>
/// </summary>
public class GovernorTests
{
    private const int Food = 0;
    private const int Wood = 1;
    private const int Planks = 2;

    private const int Farm = 0;
    private const int LumberCamp = 1;
    private const int Sawmill = 2;
    private const int House = 3;
    private const int Market = 4;

    /// <summary>
    /// Malý, ale úplný svět: pole (jídlo), tábor (dřevo), pila (dřevo→prkna),
    /// dům za prkna a trh se službou. Přesně ten řetěz, na kterém se guvernér lámal.
    /// </summary>
    private static GameContent Content(bool happinessOn = false)
    {
        var biomes = new[] { TestContent.WaterBiome(), TestContent.LandBiome("plain") };
        var resources = new[]
        {
            new Resource("food", new RgbColor(1, 1, 1), StartAmount: 0, BaseStorage: 100_000),
            new Resource("wood", new RgbColor(1, 1, 1), StartAmount: 0, BaseStorage: 100_000),
            new Resource("planks", new RgbColor(1, 1, 1), StartAmount: 0, BaseStorage: 100_000),
        };

        var all = new[] { true, true };

        var farm = new BuildingDef(
            "farm", "production", new RgbColor(1, 1, 1), 1, 1, WorkerSlots: 1, HousingCapacity: 0,
            BuildCost: Array.Empty<ResourceAmount>(),
            Recipe: new Recipe(Array.Empty<ResourceAmount>(), new[] { new ResourceAmount(Food, 5) }, 1),
            AllowedBiomes: all, StorageBonus: Array.Empty<ResourceAmount>(),
            AutoBuild: true, Buildable: true, UpgradesToIndex: -1,
            UpgradeCost: Array.Empty<ResourceAmount>(), PowerSupply: 0, PowerDemand: 0);

        var camp = farm with
        {
            Id = "lumber_camp",
            Recipe = new Recipe(Array.Empty<ResourceAmount>(), new[] { new ResourceAmount(Wood, 5) }, 1),
        };

        var sawmill = farm with
        {
            Id = "sawmill",
            Recipe = new Recipe(
                new[] { new ResourceAmount(Wood, 1) }, new[] { new ResourceAmount(Planks, 2) }, 1),
        };

        var house = new BuildingDef(
            "house", "housing", new RgbColor(1, 1, 1), 1, 1, WorkerSlots: 0, HousingCapacity: 4,
            BuildCost: new[] { new ResourceAmount(Planks, 4) },
            Recipe: null, AllowedBiomes: all, StorageBonus: Array.Empty<ResourceAmount>(),
            AutoBuild: true, Buildable: true, UpgradesToIndex: -1,
            UpgradeCost: Array.Empty<ResourceAmount>(), PowerSupply: 0, PowerDemand: 0);

        var market = house with { Id = "market", Category = "civic", HousingCapacity = 0, ServiceValue = 20 };

        var gameplay = TestContent.DefaultGameplay with
        {
            FoodResourceIndex = Food,
            FoodPerPersonPerSecond = 0.04,
            PopulationGrowthPerSecond = 0.5,
            AutoBuild = new AutoBuildConfig(IntervalTicks: 5, SearchRadius: 6, PopulationHeadroom: 2),
            HappinessOrNull = happinessOn
                ? new HappinessConfig(10, 0.55, 0.45, 0.25, PeoplePerServicePoint: 1, GrowthFloor: 0.15)
                : null,
        };

        return TestContent.Build(
            biomes, 1, resources, new[] { farm, camp, sawmill, house, market }, gameplay);
    }

    private static Simulation NewSim(bool happinessOn = false)
    {
        var sim = new Simulation(Content(happinessOn), new UniformTerrain(1));
        // Tábor je zadarmo — dům stojí prkna, kterých je na startu nula, takže
        // by se nepostavil a guvernér by neměl kde růst.
        sim.TryPlaceBuilding(LumberCamp, 0, 0);
        return sim;
    }

    private static void Tick(Simulation sim, int ticks)
    {
        for (int i = 0; i < ticks; i++)
        {
            sim.Tick();
        }
    }

    private static int CountOf(Simulation sim, int defIndex)
    {
        int count = 0;
        foreach (var building in sim.Buildings)
        {
            if (building.DefIndex == defIndex)
            {
                count++;
            }
        }

        return count;
    }

    [Fact]
    public void AHungryCityBuildsFarms()
    {
        // Dřív uměl guvernér postavit jen dům, takže město rostlo do hladu
        // a pak stálo. Nakrmit se musí umět samo.
        var sim = NewSim();

        Tick(sim, 300);

        Assert.True(CountOf(sim, Farm) > 0, "Hladové město si mělo postavit pole.");
    }

    [Fact]
    public void ItBuildsTheSupplyChainForWhatItWants()
    {
        // Tohle je ta chyba, kvůli které město stálo na stropu bydlení se 170
        // dřeva ve skladu: dům stojí prkna, prkna nebyla, a protože je žádný
        // recept nespotřebovává, guvernéra nikdy nenapadlo postavit pilu.
        var sim = NewSim();

        Tick(sim, 600);

        Assert.True(CountOf(sim, Sawmill) > 0, "Na dům jsou potřeba prkna — pila musela vzniknout.");
        Assert.True(CountOf(sim, House) > 0, "S prkny už měl přibýt dům.");
    }

    [Fact]
    public void ItKeepsGrowingPastTheFirstHousingCap()
    {
        var sim = NewSim();
        double startCapacity = sim.HousingCapacity;

        Tick(sim, 900);

        Assert.True(sim.HousingCapacity > startCapacity, "Kapacita bydlení měla vyrůst.");
        Assert.True(sim.Population > 10, $"Populace uvázla na {sim.Population:0.0}.");
    }

    [Fact]
    public void ItDoesNotStallOnANeedItCannotMeet()
    {
        // Guvernér se zasekával na první potřebě: když na ni neuměl nic postavit,
        // vrátil se z cyklu a k bydlení se nikdy nedostal.
        var sim = NewSim();
        sim.AddResource(Food, 100_000); // hlad je vyřešený napořád

        Tick(sim, 600);

        Assert.True(CountOf(sim, House) > 0, "Syté město musí pokračovat k dalším potřebám.");
    }

    [Fact]
    public void ItBuildsServicesWhenPeopleAreUnhappy()
    {
        // Spokojenost padala na 0.31, protože guvernér neuměl postavit trh.
        var sim = NewSim(happinessOn: true);
        sim.AddResource(Food, 100_000);
        sim.AddResource(Planks, 100_000);

        Tick(sim, 900);

        Assert.True(CountOf(sim, Market) > 0, "Neobsloužené město si mělo postavit trh.");
    }

    [Fact]
    public void ItDoesNotFillTheMapWithBuildingsNobodyStaffs()
    {
        // 158 prázdných budov a 444 pracovních míst na 126 obyvatel — guvernér
        // stavěl výrobny, které neměl kdo obsadit, a spotřeboval materiál
        // potřebný na domy a služby.
        var sim = NewSim();
        sim.AddResource(Food, 100_000);
        sim.AddResource(Planks, 100_000);

        Tick(sim, 1200);

        Assert.True(
            sim.TotalWorkerSlots <= Math.Max(4, sim.Population * 3),
            $"Pracovních míst {sim.TotalWorkerSlots} na {sim.Population:0.0} obyvatel — město staví do prázdna.");
    }

    [Fact]
    public void ACityThatLacksNothingReportsNoNeed()
    {
        // Guvernér není stavitel pro stavění. Rostoucí město chce pořád bydlení,
        // takže se to nedá ověřit tikáním — tohle je přímý dotaz na posouzení.
        var content = Content();
        var sim = new Simulation(content, new UniformTerrain(1));
        sim.TryPlaceBuilding(LumberCamp, 0, 0);
        sim.AddResource(Food, 100_000);
        sim.AddResource(Wood, 100_000);
        sim.AddResource(Planks, 100_000);

        // Bydlení s rezervou: populace ať má kam růst.
        for (int i = 0; i < 6; i++)
        {
            sim.TryPlaceBuilding(House, 2 + i, 2);
        }

        var needs = new GovernorNeeds(content);

        Assert.False(needs.IsHungry(sim));
        Assert.False(needs.NeedsHousing(sim));
        Assert.Equal(CityNeed.None, needs.Assess(sim));
    }
}
