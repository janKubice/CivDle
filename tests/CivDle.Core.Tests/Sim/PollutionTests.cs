using CivDle.Core.Content;
using CivDle.Core.Sim;
using CivDle.Core.Tests.Support;
using CivDle.Core.World;
using Xunit;

namespace CivDle.Core.Tests.Sim;

/// <summary>
/// Znečištění: průmysl za sebou nechá stopu, kterou je vidět, a čističky ji umí
/// vzít zpátky.
///
/// <para>Testuje se to, co dělá z mechaniky rozhodnutí, a ne jen otravu:
/// bronzová doba zůstává čistá, huť za kopcem netrápí město, čistička to
/// opravdu spraví — a otrávená půda pod polem výnos sníží, ne zastaví.</para>
/// </summary>
public class PollutionTests
{
    private const int Food = 0;
    private const int Parts = 1;

    /// <summary>Interval systému v ticích — testy podle něj počítají, jak dlouho tikat.</summary>
    private const int Interval = 10;

    // Indexy budov v testovacím obsahu (pořadí předané do TestContent.Build).
    private const int Workshop = 0;
    private const int Smelter = 1;
    private const int Farm = 2;
    private const int Scrubber = 3;

    private static PollutionConfig DefaultConfig => new(
        IntervalTicks: Interval,
        SpreadRate: 0.08,
        DecayRate: 0.02,
        FullEffectAt: 60,
        HappinessPenalty: 0.25,
        ProductionPenalty: 0.35);

    /// <summary>Vzestup hned od začátku — pro test, že nový svět začíná čistý.</summary>
    private static PrestigeConfig EarlyAscension =>
        new(new GoalCondition(MetricKind.Population, -1, 1), MetricKind.Population, -1, 5);

    /// <summary>
    /// Obsah se čtyřmi budovami: čistá dílna, dýmající huť, farma (citlivá na
    /// půdu) a čistička vzduchu s volitelnou údržbou.
    /// </summary>
    private static GameContent Content(
        PollutionConfig? pollution = null, int cleanerUpkeep = 0, bool happiness = false)
    {
        var biomes = new[] { TestContent.WaterBiome(), TestContent.LandBiome("plain") };
        var resources = new[]
        {
            new Resource("food", new RgbColor(1, 1, 1), StartAmount: 500, BaseStorage: 1_000_000),
            new Resource("parts", new RgbColor(1, 1, 1), StartAmount: 500, BaseStorage: 1_000_000),
        };

        var workshop = new BuildingDef(
            "workshop", "production", new RgbColor(100, 100, 100), 1, 1,
            WorkerSlots: 1, HousingCapacity: 0,
            BuildCost: Array.Empty<ResourceAmount>(),
            Recipe: new Recipe(
                Array.Empty<ResourceAmount>(), new[] { new ResourceAmount(Parts, 1) }, TimeTicks: 1),
            AllowedBiomes: new[] { false, true },
            StorageBonus: Array.Empty<ResourceAmount>(),
            AutoBuild: false, Buildable: true, UpgradesToIndex: -1,
            UpgradeCost: Array.Empty<ResourceAmount>(), PowerSupply: 0, PowerDemand: 0);

        var smelter = workshop with
        {
            Id = "smelter",
            PollutionOrNull = new PollutionOutput(Air: 2.0, Water: 0, Soil: 2.0),
        };

        // Farma vyrábí jídlo → je citlivá na otrávenou půdu pod sebou.
        var farm = workshop with
        {
            Id = "farm",
            Recipe = new Recipe(
                Array.Empty<ResourceAmount>(), new[] { new ResourceAmount(Food, 10) }, TimeTicks: 1),
        };

        var scrubber = new BuildingDef(
            "scrubber", "civic", new RgbColor(140, 200, 190), 1, 1,
            WorkerSlots: 0, HousingCapacity: 0,
            BuildCost: Array.Empty<ResourceAmount>(),
            Recipe: null,
            AllowedBiomes: new[] { false, true },
            StorageBonus: Array.Empty<ResourceAmount>(),
            AutoBuild: false, Buildable: true, UpgradesToIndex: -1,
            UpgradeCost: Array.Empty<ResourceAmount>(), PowerSupply: 0, PowerDemand: 0,
            UpkeepOrNull: cleanerUpkeep > 0 ? new[] { new ResourceAmount(Parts, cleanerUpkeep) } : null,
            PollutionOrNull: new PollutionOutput(Air: -6.0, Water: 0, Soil: -6.0));

        var gameplay = TestContent.DefaultGameplay with
        {
            FoodResourceIndex = Food,
            FoodPerPersonPerSecond = 0,
            PopulationGrowthPerSecond = 0,
            PollutionOrNull = pollution ?? DefaultConfig,
            HappinessOrNull = happiness
                ? new HappinessConfig(
                    IntervalTicks: Interval, BaseHappiness: 0.55, ServiceWeight: 0.45,
                    OvercrowdingPenalty: 0.25, PeoplePerServicePoint: 12, GrowthFloor: 0.15)
                : null,
        };

        return TestContent.Build(
            biomes, 1, resources, new[] { workshop, smelter, farm, scrubber }, gameplay,
            prestige: EarlyAscension);
    }

    private static Simulation NewSim(
        PollutionConfig? pollution = null, int cleanerUpkeep = 0, bool happiness = false) =>
        new(Content(pollution, cleanerUpkeep, happiness), new UniformTerrain(1));

    private static void Tick(Simulation sim, int ticks)
    {
        for (int i = 0; i < ticks; i++)
        {
            sim.Tick();
        }
    }

    [Fact]
    public void CleanIndustryLeavesNoTrace()
    {
        // Bronzová doba: budovy bez „pollution" v datech nezamoří nic, ať jich
        // hráč postaví kolik chce. Éra se nehlídá v kódu, hlídá se v obsahu.
        var sim = NewSim();
        for (int i = 0; i < 6; i++)
        {
            sim.TryPlaceBuilding(Workshop, 10 + i, 10);
        }

        Tick(sim, Interval * 20);

        Assert.Equal(0, sim.PollutionMap.DirtyCellCount);
        Assert.Equal(0, sim.AirPollutionSeverity);
    }

    [Fact]
    public void SmelterFoulsItsSurroundings()
    {
        var sim = NewSim();
        sim.TryPlaceBuilding(Smelter, 10, 10);

        Tick(sim, Interval * 20);

        Assert.True(sim.PollutionMap.At(10, 10, PollutionKind.Air) > 0);
        Assert.True(sim.PollutionMap.At(10, 10, PollutionKind.Soil) > 0);
        Assert.Equal(0, sim.PollutionMap.At(10, 10, PollutionKind.Water));
    }

    [Fact]
    public void PollutionSpreadsToNeighboursButThinsOut()
    {
        var sim = NewSim();
        sim.TryPlaceBuilding(Smelter, 10, 10);

        Tick(sim, Interval * 40);

        // O buňku vedle už něco je, ale míň než v ohnisku — bez rozlivu by šlo
        // znečištění obejít posunem o dlaždici, při rovnoměrném rozlivu by zase
        // nemělo smysl stavět dál od města.
        double here = sim.PollutionMap.At(10, 10, PollutionKind.Air);
        double next = sim.PollutionMap.At(10 + PollutionGrid.CellTiles, 10, PollutionKind.Air);

        Assert.True(next > 0);
        Assert.True(next < here);
    }

    [Fact]
    public void SmokeDoesNotReachAcrossTheMap()
    {
        // Tohle je celé rozhodnutí za mechanikou: postav hutě stranou a lidé
        // v centru se toho nenadýchají. Bez tohohle by byla poloha jedno.
        var sim = NewSim();
        sim.TryPlaceBuilding(Smelter, 400, 400);

        Tick(sim, Interval * 60);

        Assert.True(sim.PollutionMap.At(400, 400, PollutionKind.Air) > 0);
        Assert.Equal(0, sim.PollutionMap.At(0, 0, PollutionKind.Air));
    }

    [Fact]
    public void SmogTakesTheEdgeOffHappiness()
    {
        var sim = NewSim(happiness: true);
        sim.TryPlaceBuilding(Smelter, 10, 10); // jediná budova = i těžiště města

        double before = sim.HappinessParts.Total;
        Tick(sim, Interval * 60);

        var parts = sim.HappinessParts;
        Assert.True(parts.Pollution < 0);
        Assert.True(parts.Total < before);
        Assert.True(sim.AirPollutionSeverity > 0);
    }

    [Fact]
    public void ScrubberCleansTheAirBackUp()
    {
        var sim = NewSim();
        sim.TryPlaceBuilding(Smelter, 10, 10);
        Tick(sim, Interval * 40);
        double dirty = sim.PollutionMap.At(10, 10, PollutionKind.Air);
        Assert.True(dirty > 0);

        sim.TryPlaceBuilding(Scrubber, 11, 10); // do stejné buňky
        Tick(sim, Interval * 40);

        Assert.True(sim.PollutionMap.At(10, 10, PollutionKind.Air) < dirty);
    }

    [Fact]
    public void CleanedWorldForgetsItsCells()
    {
        // Náprava musí jít domyslet do konce: vyčištěná mapa je zase čistá,
        // ne „skoro čistá navždy". Bez toho by mechanika byla jen trest.
        var sim = NewSim();
        sim.TryPlaceBuilding(Smelter, 10, 10);
        Tick(sim, Interval * 20);
        Assert.True(sim.PollutionMap.DirtyCellCount > 0);

        Assert.Equal(PlacementResult.Ok, sim.TryDemolish(0));
        sim.TryPlaceBuilding(Scrubber, 10, 10);
        Tick(sim, Interval * 1500);

        Assert.Equal(0, sim.PollutionMap.DirtyCellCount);
    }

    [Fact]
    public void UnpaidScrubberStopsCleaning()
    {
        // Čistička s prázdnou pokladnou nedělá nic — náprava je průběžný náklad,
        // ne jednorázový nákup.
        var sim = NewSim(cleanerUpkeep: 1_000_000);
        sim.TryPlaceBuilding(Smelter, 10, 10);
        sim.TryPlaceBuilding(Scrubber, 11, 10);

        Tick(sim, Interval * 20);

        Assert.True(sim.PollutionMap.At(10, 10, PollutionKind.Air) > 0);
    }

    [Fact]
    public void PoisonedSoilCutsHarvestButNeverStopsIt()
    {
        var sim = NewSim();
        sim.TryPlaceBuilding(Farm, 20, 20);
        Tick(sim, Interval * 5);
        double cleanRate = FoodPerInterval(sim);

        sim.TryPlaceBuilding(Smelter, 21, 20); // struska do stejné buňky
        Tick(sim, Interval * 80);
        double dirtyRate = FoodPerInterval(sim);

        Assert.True(dirtyRate < cleanRate);
        Assert.True(dirtyRate > 0); // brzda, ne stopka
    }

    [Fact]
    public void SmelterItselfDoesNotCareAboutItsOwnMess()
    {
        // Huť nepěstuje a nebere ze země — smog jí výrobu nesnižuje. Jinak by se
        // těžký průmysl sám udusil a mechanika by se zvrhla v „nestav továrny".
        var sim = NewSim();
        sim.TryPlaceBuilding(Smelter, 10, 10);
        Tick(sim, Interval * 80);

        Assert.Equal(1f, sim.Buildings[0].PollutionMult);
    }

    [Fact]
    public void DisabledConfigKeepsTheWorldClean()
    {
        // Starší data bez bloku „pollution" se musí načíst a hrát jako dřív.
        var sim = NewSim(PollutionConfig.Disabled);
        sim.TryPlaceBuilding(Smelter, 10, 10);

        Tick(sim, Interval * 40);

        Assert.False(sim.PollutionEnabled);
        Assert.Equal(0, sim.PollutionMap.DirtyCellCount);
    }

    [Fact]
    public void AscendingRebuildsAWorldWithoutSmog()
    {
        var sim = NewSim();
        sim.TryPlaceBuilding(Smelter, 10, 10);
        Tick(sim, Interval * 40);
        Assert.True(sim.PollutionMap.DirtyCellCount > 0);

        Assert.Equal(PlacementResult.Ok, sim.TryAscend());

        Assert.Equal(0, sim.PollutionMap.DirtyCellCount);
    }

    /// <summary>Kolik jídla přibude za jeden interval systému — měřítko výnosu farmy.</summary>
    private static double FoodPerInterval(Simulation sim)
    {
        double before = sim.GetResource(Food);
        Tick(sim, Interval);
        return sim.GetResource(Food) - before;
    }
}
