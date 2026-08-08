using CivDle.Core.Content;
using CivDle.Core.Sim;
using CivDle.Core.Tests.Support;
using CivDle.Core.World;
using Xunit;

namespace CivDle.Core.Tests.Sim;

/// <summary>
/// Hromadné stavění: jedno gesto místo dvaceti kliků.
///
/// <para>Testy hlídají hlavně to, co odlišuje použitelnou funkci od nebezpečné:
/// plán nikdy neslíbí víc, než na co hráč má; nepovedená dlaždice hromadnou
/// stavbu nezastaví; a stavět se smí jen to, co plán označil jako proveditelné —
/// obejít <see cref="Simulation.TryPlaceBuilding"/> nesmí ani hromadná cesta.</para>
/// </summary>
public class BulkBuilderTests
{
    private const int Wood = 0;
    private const int Hut = 0;
    private const int Tower = 1;

    /// <summary>Cena jedné chalupy — z ní se počítá, kolik jich hráč utáhne.</summary>
    private const int HutCost = 10;

    private static GameContent Content(double startWood = 1000)
    {
        var biomes = new[] { TestContent.WaterBiome(), TestContent.LandBiome("plain") };
        var resources = new[]
        {
            new Resource("wood", new RgbColor(1, 1, 1), StartAmount: startWood, BaseStorage: 1_000_000),
        };

        var hut = new BuildingDef(
            "hut", "housing", new RgbColor(180, 140, 90), 1, 1,
            WorkerSlots: 0, HousingCapacity: 5,
            BuildCost: new[] { new ResourceAmount(Wood, HutCost) },
            Recipe: null,
            AllowedBiomes: new[] { false, true },
            StorageBonus: Array.Empty<ResourceAmount>(),
            AutoBuild: false, Buildable: true, UpgradesToIndex: -1,
            UpgradeCost: Array.Empty<ResourceAmount>(), PowerSupply: 0, PowerDemand: 0);

        // Dvoudlaždicová budova: mřížka hromadné stavby musí respektovat půdorys,
        // jinak by se kusy překrývaly a půlka plánu by spadla na „obsazeno".
        var tower = hut with { Id = "tower", FootprintWidth = 2, FootprintHeight = 2 };

        var gameplay = TestContent.DefaultGameplay with
        {
            FoodPerPersonPerSecond = 0,
            PopulationGrowthPerSecond = 0,
        };

        return TestContent.Build(biomes, 1, resources, new[] { hut, tower }, gameplay);
    }

    private static (Simulation Sim, BulkBuilder Bulk) NewGame(double startWood = 1000, ITerrain? terrain = null)
    {
        var content = Content(startWood);
        var sim = new Simulation(content, terrain ?? new UniformTerrain(1));
        return (sim, new BulkBuilder(sim, content));
    }

    // ----- kolik si můžu dovolit -----

    [Fact]
    public void Affordable_CountsWhatTheStockpileCovers()
    {
        var (_, bulk) = NewGame(startWood: 95);

        Assert.Equal(9, bulk.Affordable(Hut));
    }

    [Fact]
    public void Affordable_WithoutResources_IsZero()
    {
        var (_, bulk) = NewGame(startWood: 5);

        Assert.Equal(0, bulk.Affordable(Hut));
    }

    // ----- tažení -----

    [Fact]
    public void DraggingARow_PlansOnePerTile()
    {
        var (_, bulk) = NewGame();
        var plan = new List<BulkSlot>();

        int buildable = bulk.PlanArea(Hut, 10, 10, 14, 10, plan);

        Assert.Equal(5, plan.Count);
        Assert.Equal(5, buildable);
        Assert.All(plan, slot => Assert.Equal(10, slot.Y));
    }

    [Fact]
    public void DraggingABlock_FillsTheWholeRectangle()
    {
        var (_, bulk) = NewGame();
        var plan = new List<BulkSlot>();

        int buildable = bulk.PlanArea(Hut, 0, 0, 2, 3, plan);

        Assert.Equal(12, plan.Count); // 3 sloupce × 4 řádky
        Assert.Equal(12, buildable);
    }

    [Fact]
    public void DraggingBackwards_WorksToo()
    {
        // Hráč táhne stejně často doleva nahoru jako doprava dolů.
        var (_, bulk) = NewGame();
        var plan = new List<BulkSlot>();

        bulk.PlanArea(Hut, 10, 10, 7, 10, plan);

        Assert.Equal(4, plan.Count);
        Assert.Contains(plan, slot => slot.X == 7);
        Assert.Contains(plan, slot => slot.X == 10);
    }

    [Fact]
    public void TheGridStepsByTheFootprint()
    {
        // U dvoudlaždicové budovy se musí kroky posouvat po dvou, jinak by se
        // sousední kusy překrývaly a plán by byl z poloviny nepostavitelný.
        var (_, bulk) = NewGame();
        var plan = new List<BulkSlot>();

        int buildable = bulk.PlanArea(Tower, 0, 0, 5, 0, plan);

        Assert.Equal(3, plan.Count); // x = 0, 2, 4
        Assert.Equal(3, buildable);
        Assert.Equal(new[] { 0, 2, 4 }, plan.Select(s => s.X).ToArray());
    }

    [Fact]
    public void ThePlanNeverPromisesMoreThanThePlayerCanPay()
    {
        // Tohle je jádro: bez průběžné útraty by plán slíbil deset chalup
        // za peníze na tři a hráč by po puštění tlačítka koukal na sedm děr.
        var (_, bulk) = NewGame(startWood: 3 * HutCost);
        var plan = new List<BulkSlot>();

        int buildable = bulk.PlanArea(Hut, 0, 0, 9, 0, plan);

        Assert.Equal(10, plan.Count);
        Assert.Equal(3, buildable);
        Assert.Equal(3, plan.Count(s => s.WillBuild));
        Assert.All(
            plan.Where(s => !s.WillBuild),
            s => Assert.Equal(PlacementResult.NotEnoughResources, s.Result));
    }

    [Fact]
    public void OccupiedTilesAreMarkedButDoNotStopThePlan()
    {
        var (sim, bulk) = NewGame();
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuilding(Hut, 2, 0));
        var plan = new List<BulkSlot>();

        int buildable = bulk.PlanArea(Hut, 0, 0, 4, 0, plan);

        Assert.Equal(5, plan.Count);
        Assert.Equal(4, buildable);
        Assert.Equal(PlacementResult.Occupied, plan.Single(s => s.X == 2).Result);
    }

    [Fact]
    public void OneGestureCannotExceedTheCap()
    {
        // Bez stropu by tažení přes půl mapy položilo tisíce budov a zamrzlo snímek.
        var (_, bulk) = NewGame(startWood: 1_000_000);
        var plan = new List<BulkSlot>();
        int cap = bulk.Config.MaxPerAction;

        bulk.PlanArea(Hut, 0, 0, cap * 2, 0, plan);

        Assert.Equal(cap, plan.Count);
    }

    // ----- násobiče ×N -----

    [Fact]
    public void BatchPlacesTheRequestedCountAroundTheAnchor()
    {
        var (_, bulk) = NewGame();
        var plan = new List<BulkSlot>();

        int planned = bulk.PlanNear(Hut, 50, 50, 5, plan);

        Assert.Equal(5, planned);
        Assert.All(plan, slot => Assert.True(slot.WillBuild));
        Assert.Contains(plan, slot => slot.X == 50 && slot.Y == 50); // kotva první
    }

    [Fact]
    public void BatchGrowsOutwardsFromTheAnchor()
    {
        // Skupina má vyrůst kompaktně kolem místa kliknutí, ne v pásu přes mapu.
        var (_, bulk) = NewGame();
        var plan = new List<BulkSlot>();

        bulk.PlanNear(Hut, 0, 0, 9, plan);

        Assert.Equal(9, plan.Count);
        Assert.All(plan, slot => Assert.True(Math.Max(Math.Abs(slot.X), Math.Abs(slot.Y)) <= 1));
    }

    [Fact]
    public void BatchSkipsPlacesWhereItCannotBuild()
    {
        var (sim, bulk) = NewGame();
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuilding(Hut, 0, 0));
        var plan = new List<BulkSlot>();

        int planned = bulk.PlanNear(Hut, 0, 0, 3, plan);

        Assert.Equal(3, planned);
        Assert.DoesNotContain(plan, slot => slot.X == 0 && slot.Y == 0);
    }

    [Fact]
    public void BatchStopsAtWhatThePlayerCanAfford()
    {
        var (_, bulk) = NewGame(startWood: 2 * HutCost);
        var plan = new List<BulkSlot>();

        int planned = bulk.PlanNear(Hut, 0, 0, 25, plan);

        Assert.Equal(2, planned);
    }

    [Fact]
    public void BatchOnUnbuildableGroundGivesUpInsteadOfSearchingForever()
    {
        // Samá voda: hledání musí skončit, ne zamrznout hru.
        var (_, bulk) = NewGame(terrain: new UniformTerrain(0));
        var plan = new List<BulkSlot>();

        int planned = bulk.PlanNear(Hut, 0, 0, 25, plan);

        Assert.Equal(0, planned);
        Assert.Empty(plan);
    }

    // ----- stavba -----

    [Fact]
    public void BuildPlacesExactlyThePlannedPieces()
    {
        var (sim, bulk) = NewGame(startWood: 3 * HutCost);
        var plan = new List<BulkSlot>();
        bulk.PlanArea(Hut, 0, 0, 9, 0, plan);

        int placed = bulk.Build(Hut, plan);

        Assert.Equal(3, placed);
        Assert.Equal(3, sim.Buildings.Length);
        Assert.Equal(0, sim.GetResource(Wood), 3);
    }

    [Fact]
    public void BuildPaysForEveryPiece()
    {
        var (sim, bulk) = NewGame(startWood: 1000);
        var plan = new List<BulkSlot>();
        bulk.PlanArea(Hut, 0, 0, 4, 0, plan);

        bulk.Build(Hut, plan);

        Assert.Equal(1000 - 5 * HutCost, sim.GetResource(Wood), 3);
    }

    [Fact]
    public void BuildIgnoresSlotsThePlanRejected()
    {
        var (sim, bulk) = NewGame();
        var plan = new List<BulkSlot>
        {
            new(0, 0, PlacementResult.Ok),
            new(1, 0, PlacementResult.Occupied),
            new(2, 0, PlacementResult.NotEnoughResources),
        };

        int placed = bulk.Build(Hut, plan);

        Assert.Equal(1, placed);
        Assert.Single(sim.Buildings.ToArray());
    }

    [Fact]
    public void BulkBuiltClusterGetsARoad()
    {
        // Hromadná stavba klade domy natěsno. Auto-silnice dřív u každého z nich
        // usoudila „dotýká se souseda, takže se k cestě dostane přes blok" — jenže
        // ten blok k žádné cestě nevedl a ×25 postavilo město bez jediné ulice.
        var (sim, bulk) = NewGame(startWood: 100_000);
        sim.AddRoadTileForTest(30, 40); // síť existuje, ale nedotýká se nové čtvrti

        var plan = new List<BulkSlot>();
        bulk.PlanNear(Hut, 40, 40, 25, plan);
        Assert.Equal(25, bulk.Build(Hut, plan));

        for (int i = 0; i < sim.Buildings.Length; i++)
        {
            Assert.True(sim.IsBuildingConnected(i), $"budova {i} zůstala bez napojení");
        }
    }

    [Fact]
    public void TouchingHousesDoNotGetPavedBetweenThemBeforeAnyRoadExists()
    {
        // Opačná past: na úplném začátku hry, kdy město ještě žádnou cestu nemá,
        // se mezi dva sousední domy dláždit nesmí — z toho vznikala šachovnice
        // a blok 2×2 nešlo vůbec postavit.
        var (sim, _) = NewGame();

        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuilding(Hut, 5, 5));
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuilding(Hut, 6, 5));

        Assert.Empty(sim.RoadTiles);
    }

    [Fact]
    public void BuildCannotSneakPastTheRules()
    {
        // Plán je jen návrh. Kdyby hromadná cesta obešla TryPlaceBuilding, dala
        // by se jí postavit budova do vody nebo zadarmo.
        var (sim, bulk) = NewGame(startWood: 0);
        var plan = new List<BulkSlot> { new(0, 0, PlacementResult.Ok) };

        int placed = bulk.Build(Hut, plan);

        Assert.Equal(0, placed);
        Assert.Empty(sim.Buildings.ToArray());
    }
}
