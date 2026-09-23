using CivDle.Core.Content;
using CivDle.Core.Save;
using CivDle.Core.Sim;
using CivDle.Core.Tests.Support;
using CivDle.Core.World;
using Xunit;

namespace CivDle.Core.Tests.Sim;

/// <summary>
/// Rezerva guvernéra: materiál odložený na stavbu, na kterou se šetří.
///
/// <para>Vznikla kvůli zámku, který trval sto minut: pila si brala tři dřeva,
/// jakmile přišla, a dům za pět dřev se tak nikdy nepostavil. Testy hlídají, že
/// rezervu respektuje všechno, co může počkat — a nic z toho, co počkat nesmí.</para>
/// </summary>
public class ConstructionClaimTests
{
    private const int Food = 0;
    private const int Wood = 1;
    private const int Planks = 2;

    private const int Sawmill = 0;
    private const int House = 1;
    private const int Market = 2;

    private static GameContent Content(bool seasons = false, bool happiness = false)
    {
        var resources = new[]
        {
            new Resource("food", new RgbColor(1, 1, 1), StartAmount: 0, BaseStorage: 1000),
            new Resource("wood", new RgbColor(1, 1, 1), StartAmount: 0, BaseStorage: 1000),
            new Resource("planks", new RgbColor(1, 1, 1), StartAmount: 0, BaseStorage: 1000),
        };
        var sawmill = TestContent.Converter("sawmill", Wood, 3, Planks, 1, timeTicks: 5, workerSlots: 1);
        var house = TestContent.SimpleBuilding("house", 2, housing: 4) with
        {
            BuildCost = new[] { new ResourceAmount(Wood, 5), new ResourceAmount(Planks, 4) },
        };
        var market = TestContent.Service("market", serviceValue: 50, upkeepResource: Wood, upkeepAmount: 1);

        var gameplay = TestContent.DefaultGameplay with
        {
            PopulationGrowthPerSecond = 0.0,
            FoodPerPersonPerSecond = 0.0,
            HappinessOrNull = happiness
                ? new HappinessConfig(10, 0.55, 0.45, 0.25, PeoplePerServicePoint: 1, GrowthFloor: 0.15)
                : null,
        };

        return TestContent.Build(
            resources: resources,
            buildings: new[] { sawmill, house, market },
            gameplay: gameplay,
            seasons: seasons ? WinterOnly(Wood) : null);
    }

    /// <summary>Kalendář, ve kterém je pořád zima a topí se dřevem.</summary>
    private static SeasonCalendar WinterOnly(int fuel) => new(
        Seasons: new[]
        {
            new SeasonDef("winter", new RgbColor(200, 220, 240), TintAlpha: 0,
                FoodProductionMult: 1, HarvestMult: 1, GrowthMult: 1,
                FuelPerPersonPerSecond: 1.0, ColdGrowthMult: 0.25),
        },
        DaysPerSeason: 1000,
        FuelResourceIndex: fuel);

    private static void Run(Simulation sim, int ticks)
    {
        for (int i = 0; i < ticks; i++)
        {
            sim.Tick();
        }
    }

    [Fact]
    public void AConverterOnlyUsesWhatIsAboveTheClaim()
    {
        var sim = new Simulation(Content(), new UniformTerrain(1));
        sim.TryPlaceBuildingFree(Sawmill, 0, 0);
        sim.DebugSetResource(Wood, 7);
        sim.SetClaimForTest(House); // pět dřev je odložených na dům

        Run(sim, 100);

        // 7 − 5 = 2 < 3: pila nesmí začít, jinak by sáhla na dřevo pro dům.
        Assert.Equal(7, sim.GetResource(Wood), 6);
        Assert.Equal(BuildingStall.MissingInput, sim.Buildings[0].Stall);
    }

    [Fact]
    public void TheClaimIsAFloorNotABlock()
    {
        // Rezerva nesmí zablokovat řetěz, který ji má naplnit: dům chce dřevo
        // i prkna a prkna jsou ze dřeva. Pila smí řezat přebytek nad rezervou.
        var sim = new Simulation(Content(), new UniformTerrain(1));
        sim.TryPlaceBuildingFree(Sawmill, 0, 0);
        sim.DebugSetResource(Wood, 20);
        sim.SetClaimForTest(House);

        Run(sim, 200);

        Assert.True(sim.GetResource(Planks) > 0, "pila přestala řezat i přebytek nad rezervou");
        Assert.True(sim.GetResource(Wood) >= 5 - 1e-9, "pila sáhla do rezervy");
    }

    [Fact]
    public void HeatingDoesNotBurnTheClaim()
    {
        // V zimě dřív topení spálilo každé dřevo a guvernér nenašetřil ani na
        // dřevorubce. Lidé radši chvíli mrznou.
        var sim = new Simulation(Content(seasons: true), new UniformTerrain(1));
        sim.TryPlaceBuildingFree(House, 0, 0);
        sim.DebugSetResource(Wood, 5);
        sim.SetClaimForTest(House);

        Run(sim, 50);

        Assert.Equal(5, sim.GetResource(Wood), 6);
        Assert.False(sim.HasFuelForHeating);
    }

    [Fact]
    public void UpkeepWaitsWhileTheGovernorSaves()
    {
        // Údržba sýpek snědla celý přítok dřeva (0,74 z 0,77/s) a farma se
        // šetřila půl hodiny. Služba chvíli nejede — to je menší zlo.
        var sim = new Simulation(Content(happiness: true), new UniformTerrain(1));
        sim.TryPlaceBuildingFree(Market, 0, 0);
        sim.DebugSetResource(Wood, 5);
        sim.SetClaimForTest(House);

        Run(sim, 100);

        Assert.Equal(5, sim.GetResource(Wood), 6);
    }

    [Fact]
    public void ThePlayerIsNotBoundByTheClaim()
    {
        // Rezerva je guvernérova fronta, ne daň z hráčových rozhodnutí.
        var sim = new Simulation(Content(), new UniformTerrain(1));
        sim.DebugSetResource(Wood, 5);
        sim.DebugSetResource(Planks, 4);
        sim.SetClaimForTest(Market);

        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuilding(House, 0, 0));
    }

    [Fact]
    public void TheClaimSurvivesSaveAndLoad()
    {
        // Výroba se podle rezervy řídí každý tik — načtená hra bez ní by pár
        // sekund řezala dřevo, které původní hra držela na dům, a rozešla by se.
        var content = Content();
        var terrain = new ProceduralTerrain(content.Biomes, content.WorldGen.Presets[0], 1);
        var saved = new Simulation(content, terrain, 1);
        saved.SetClaimForTest(House);
        var stream = new MemoryStream();
        new SaveGameSerializer().Write(stream, saved, new SaveMetadata(1, "s", content.WorldGen.Presets[0].Id, DateTime.UtcNow));
        stream.Position = 0;
        var (loaded, _) = new SaveGameSerializer().Read(stream, content);

        Assert.Equal(House, loaded.Claim.DefIndex);
        Assert.Equal(5, loaded.Claim.AmountOf(Wood), 6);
        Assert.Equal(4, loaded.Claim.AmountOf(Planks), 6);
    }
}
