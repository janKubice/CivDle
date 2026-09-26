using CivDle.Core.Content;
using CivDle.Core.Sim;
using CivDle.Core.Tests.Support;
using CivDle.Core.World;
using Xunit;

namespace CivDle.Core.Tests.Sim;

/// <summary>
/// Guvernér jako plánovač cílů: sklady, věda, víra, obnova krajiny a proud.
///
/// <para>Každý test odpovídá tomu, co guvernér předtím nedělal vůbec, nebo
/// tomu, co první verze plánovače přehnala (změřeno na 90 minutách bez hráče):
/// pět set skladů u města na stropu měřítka a tři sta lesních školek
/// rozesetých po městě, protože se k lesu nevešly.</para>
/// </summary>
public class GovernorGoalsTests
{
    private const byte Grass = 1;
    private const byte Forest = 2;

    private const int Food = 0;
    private const int Wood = 1;
    private const int Planks = 2;
    private const int Science = 3;
    private const int Faith = 4;
    private const int Goods = 5;

    private const int House = 0;
    private const int Warehouse = 1;
    private const int Stall = 2;
    private const int Library = 3;
    private const int Shrine = 4;
    private const int Nursery = 5;
    private const int LumberCamp = 6;
    private const int Factory = 7;
    private const int Generator = 8;
    private const int Statue = 9;
    private const int Woodshed = 10;

    /// <summary>Lesík: pár stromů po jednom sběru, bez dorůstání — dá se vytěžit v testu.</summary>
    private static readonly (int MinX, int MinY, int MaxX, int MaxY) Grove = (20, 20, 22, 22);

    /// <summary>Hluboký les: od středu je louka dál, než guvernér hledá místo kolem budovy.</summary>
    private static readonly (int MinX, int MinY, int MaxX, int MaxY) DeepForest = (12, 12, 34, 34);

    private static GovernorConfig ByRole(double minCoverage = 0.9) => new(
        BuildsByRole: true,
        new StorageGoalConfig(0.95),
        new SupplyGoalConfig(Science, MinPopulation: 0, TargetSeconds: 60),
        new SupplyGoalConfig(Faith, MinPopulation: 0, TargetSeconds: 60),
        new LandscapeGoalConfig(4),
        new PowerGoalConfig(minCoverage));

    private static GameContent Content(
        GovernorConfig governor, IReadOnlyList<TechDef>? techs = null, FaithCatalog? faith = null,
        bool nurseryNeedsGrass = false)
    {
        var biomes = new[]
        {
            TestContent.WaterBiome(),
            TestContent.LandBiome("grass"),
            TestContent.LandBiome("forest") with { ClickYield = new ClickYield(Wood, 1, Charges: 1, RegrowSeconds: 0) },
        };

        var resources = new[]
        {
            new Resource("food", new RgbColor(1, 1, 1), StartAmount: 500, BaseStorage: 1000),
            new Resource("wood", new RgbColor(1, 1, 1), StartAmount: 400, BaseStorage: 1000),
            new Resource("planks", new RgbColor(1, 1, 1), StartAmount: 400, BaseStorage: 1000),
            new Resource("science", new RgbColor(1, 1, 1), StartAmount: 0, BaseStorage: 100),
            new Resource("faith", new RgbColor(1, 1, 1), StartAmount: 0, BaseStorage: 100),
            new Resource("goods", new RgbColor(1, 1, 1), StartAmount: 0, BaseStorage: 1000),
        };

        bool[] grassOnly = { false, true, false };
        bool[] forestOnly = { false, false, true };
        var cheap = new[] { new ResourceAmount(Wood, 5) };

        var buildings = new[]
        {
            TestContent.SimpleBuilding("house", 3, housing: 4) with
            {
                Category = "housing", AutoBuild = true, AllowedBiomes = grassOnly, BuildCost = cheap,
            },
            TestContent.SimpleBuilding("warehouse", 3) with
            {
                Category = "storage", AllowedBiomes = grassOnly, BuildCost = cheap,
                StorageBonus = new[] { new ResourceAmount(Wood, 500), new ResourceAmount(Science, 500) },
            },
            // Pojme víc, ale chce lidi — sklad kvůli dřevu z něj nemá dělat přístavní čtvrť.
            TestContent.SimpleBuilding("stall", 3) with
            {
                Category = "civic", AllowedBiomes = grassOnly, BuildCost = cheap, WorkerSlots = 4,
                StorageBonus = new[] { new ResourceAmount(Wood, 900), new ResourceAmount(Science, 900) },
            },
            // Vstupem je dřevo, které umí vyrobit dřevník: guvernér nezačne
            // řetěz, jehož vstup nemá kdo dodat (prkna tu nikdo nedělá).
            TestContent.Converter("library", Wood, 1, Science, 5, timeTicks: 5, biomeCount: 3, buildCost: cheap) with
            {
                Category = "science", AllowedBiomes = grassOnly,
            },
            TestContent.Producer("shrine", Faith, 2, timeTicks: 5, biomeCount: 3) with
            {
                Category = "faith", AllowedBiomes = grassOnly, BuildCost = cheap,
            },
            TestContent.Producer("nursery", Wood, 1, timeTicks: 50, biomeCount: 3) with
            {
                Category = "production", BuildCost = cheap, ReforestRadius = 5,
                AllowedBiomes = nurseryNeedsGrass ? grassOnly : new[] { false, true, true },
            },
            TestContent.Producer("lumber_camp", Wood, 1, timeTicks: 2, biomeCount: 3) with
            {
                Category = "production", AllowedBiomes = forestOnly, TerrainHarvestRadius = 1,
            },
            TestContent.Producer("factory", Goods, 1, timeTicks: 5, biomeCount: 3) with
            {
                Category = "industry", AllowedBiomes = grassOnly, PowerDemand = 10,
            },
            TestContent.SimpleBuilding("generator", 3) with
            {
                Category = "power", AllowedBiomes = grassOnly, BuildCost = cheap, PowerSupply = 20,
            },
            // Monument se službou: guvernér ho nesmí postavit, ani kdyby služby chyběly.
            TestContent.SimpleBuilding("statue", 3) with
            {
                Category = "monument", AllowedBiomes = grassOnly, BuildCost = cheap, ServiceValue = 50,
            },
            TestContent.Producer("woodshed", Wood, 1, timeTicks: 2, biomeCount: 3) with
            {
                Category = "production", AllowedBiomes = grassOnly,
            },
        };

        var gameplay = TestContent.DefaultGameplay with
        {
            FoodResourceIndex = Food,
            FoodPerPersonPerSecond = 0.0,
            PopulationGrowthPerSecond = 0.0,
            AutoBuild = new AutoBuildConfig(IntervalTicks: 5, SearchRadius: 6, PopulationHeadroom: 2),
            GovernorOrNull = governor,
        };

        return TestContent.Build(
            biomes, fallbackBiomeIndex: Grass, resources, buildings, gameplay, techs: techs, faith: faith);
    }

    private static Simulation World(GameContent content, bool deepForest = false)
    {
        var map = new WorldMap(48, 48);
        Array.Fill(map.BiomeIndices, Grass);
        var forest = deepForest ? DeepForest : Grove;
        for (int y = forest.MinY; y <= forest.MaxY; y++)
        {
            for (int x = forest.MinX; x <= forest.MaxX; x++)
            {
                map.BiomeIndices[map.Index(x, y)] = Forest;
            }
        }

        var sim = new Simulation(content, new GridTerrain(map), seed: 3);

        // Guvernér roste od zástavby — první dům je vždycky hráčův.
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuildingFree(House, 5, 5));
        return sim;
    }

    /// <summary>Technologie, která stojí vědu — ať má hráč na co šetřit.</summary>
    private static TechDef ScienceTech(int cost) =>
        new("lore", new[] { new ResourceAmount(Science, cost) }, Array.Empty<int>(), Array.Empty<int>());

    private static void Run(Simulation sim, int ticks)
    {
        for (int i = 0; i < ticks; i++)
        {
            sim.Tick();
        }
    }

    private static int CountOf(Simulation sim, int defIndex) =>
        sim.Buildings.ToArray().Count(b => b.DefIndex == defIndex);

    [Fact]
    public void AResearchThatDoesNotFitGetsAWarehouse()
    {
        // Dřív se cena přes sklad tiše přeskočila a město na ní viselo navždy.
        var sim = World(Content(ByRole(), techs: new[] { ScienceTech(300) }));

        Run(sim, 60);

        Assert.True(CountOf(sim, Warehouse) >= 1, "cena výzkumu se nevejde a sklad nepřibyl");
        Assert.Equal(0, CountOf(sim, Stall)); // čistý sklad má přednost před stánkem s lidmi
    }

    [Fact]
    public void TheClassicGovernorStillBuildsOnlyAutoBuild()
    {
        // Bez bloku governor v datech se nic nemění — mody a starší data.
        var sim = World(Content(GovernorConfig.Classic, techs: new[] { ScienceTech(300) }));

        Run(sim, 60);

        Assert.Equal(0, CountOf(sim, Warehouse));
        Assert.Equal(0, CountOf(sim, Library));
        Assert.Equal(0, CountOf(sim, Shrine));
    }

    [Fact]
    public void AFullStoreWithNothingToSpendOnGetsNoMoreStorage()
    {
        // Regrese: město na stropu měřítka vyrábí přebytky donekonečna a první
        // verze plánovače k nim stavěla sklad za skladem (pět set za hodinu).
        var sim = World(Content(ByRole()));
        for (int i = 0; i < 6; i++)
        {
            Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuildingFree(Woodshed, 10 + i, 12));
        }

        sim.DebugFillStorages();
        Run(sim, 300);

        Assert.Equal(0, CountOf(sim, Warehouse));
        Assert.Equal(0, CountOf(sim, Stall));
    }

    [Fact]
    public void ResearchWaitingForScienceGetsALibrary()
    {
        var sim = World(Content(ByRole(), techs: new[] { ScienceTech(50) }));

        Run(sim, 60);

        Assert.True(CountOf(sim, Library) >= 1, "na výzkum chybí věda a knihovna nepřibyla");
        Assert.Equal(0, CountOf(sim, Statue)); // monument je hráčovo rozhodnutí
    }

    [Fact]
    public void WithoutAnythingToResearchThereIsNoLibrary()
    {
        // Knihovna bez výzkumu jen sní prkna.
        var sim = World(Content(ByRole()));

        Run(sim, 120);

        Assert.Equal(0, CountOf(sim, Library));
    }

    [Fact]
    public void APrayerWaitingForFaithGetsAShrine()
    {
        var prayers = new DefRegistry<PrayerDef>(
            new[] { new PrayerDef("rain", "bless_rain", BaseCost: 20, BaseChance: 0.8, ChanceFalloff: 0.1, Magnitude: 1, RadiusTiles: 0) },
            p => p.Id, "modlitba");

        var sim = World(Content(ByRole(), faith: new FaithCatalog(Faith, prayers)));

        Run(sim, 60);

        Assert.True(CountOf(sim, Shrine) >= 1, "na modlitbu chybí víra a svatyně nepřibyla");
    }

    [Fact]
    public void AStrippedCampGetsOneNurseryBesideIt()
    {
        // Regrese: školka, která se nevešla k lesu, spadla kamkoli do města,
        // les neobnovila a guvernér příští kolo stavěl další (tři sta školek).
        var sim = World(Content(ByRole()));
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuildingFree(LumberCamp, 21, 21));

        Run(sim, 400);

        var nurseries = sim.Buildings.ToArray().Where(b => b.DefIndex == Nursery).ToList();
        var nursery = Assert.Single(nurseries);
        Assert.True(Math.Abs(nursery.X - 21) <= 5 && Math.Abs(nursery.Y - 21) <= 5,
            $"školka stojí na {nursery.X},{nursery.Y}, daleko od vytěženého lesa");
    }

    [Fact]
    public void ANurseryThatDoesNotFitByTheForestIsNotBuiltElsewhere()
    {
        // Školka jinde ve městě les neobnoví — a potřeba by trvala dál, takže
        // by guvernér stavěl další a další (změřeno: tři sta školek).
        var sim = World(Content(ByRole(), nurseryNeedsGrass: true), deepForest: true);
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuildingFree(LumberCamp, 23, 23));

        Run(sim, 400);

        Assert.Equal(0, CountOf(sim, Nursery));
    }

    [Fact]
    public void AFactoryInTheDarkGetsAGeneratorNextToIt()
    {
        var sim = World(Content(ByRole()));
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuildingFree(Factory, 12, 12));

        Run(sim, 60);

        var generator = Assert.Single(sim.Buildings.ToArray().Where(b => b.DefIndex == Generator));
        Assert.True(Math.Abs(generator.X - 12) <= 6 && Math.Abs(generator.Y - 12) <= 6);
        Assert.Equal(1.0, sim.PowerFactor, 6);
    }

    [Fact]
    public void ThePlanIsSortedByUrgency()
    {
        // UI ukazuje první tři body plánu; musí to být ty nejnaléhavější.
        var sim = World(Content(ByRole(), techs: new[] { ScienceTech(300) }));
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuildingFree(Factory, 12, 12));

        Run(sim, 5);

        var agenda = sim.GovernorAgenda;
        Assert.NotEmpty(agenda);
        for (int i = 1; i < agenda.Count; i++)
        {
            Assert.True(agenda[i - 1].Urgency >= agenda[i].Urgency);
        }

        Assert.Equal(CityNeed.Power, agenda[0].Need); // továrna potmě předběhne sklad
    }

    [Fact]
    public void TheGovernorResearchesOnlyWhenThePlayerLetsIt()
    {
        // Strom je v první hře hráčova volba — automatika ho bere jen na požádání.
        var tech = new TechDef("carpentry", new[] { new ResourceAmount(Wood, 10) }, Array.Empty<int>(), Array.Empty<int>());
        var sim = World(Content(ByRole(), techs: new[] { tech }));

        Run(sim, 30);
        Assert.False(sim.IsTechResearched(0));

        sim.Plan.SetChoosesResearch(true);
        Run(sim, 30);
        Assert.True(sim.IsTechResearched(0));
    }

    [Fact]
    public void TheGovernorResearchLeavesThePlayersReserveAlone()
    {
        // Rezerva je hráčova pojistka „tohle mi nesahej" — platí i pro výzkum.
        var free = Array.Empty<ResourceAmount>();
        var techs = new[]
        {
            new TechDef("carpentry", new[] { new ResourceAmount(Wood, 10) }, Array.Empty<int>(), Array.Empty<int>()),
            new TechDef(Simulation.GovernorTechId, free, Array.Empty<int>(), Array.Empty<int>()),
            new TechDef(Simulation.GovernorReserveTechId, free, Array.Empty<int>(), Array.Empty<int>()),
        };
        var sim = World(Content(ByRole(), techs: techs));
        Assert.Equal(PlacementResult.Ok, sim.TryResearch(1));
        Assert.Equal(PlacementResult.Ok, sim.TryResearch(2));
        sim.SetGovernorReserve(0.9);
        sim.Plan.SetChoosesResearch(true);

        Run(sim, 60);

        Assert.False(sim.IsTechResearched(0), "guvernér sáhl na rezervu kvůli výzkumu");
    }
    [Theory]
    [InlineData("warehouse", true)]
    [InlineData("tree_nursery", true)]
    [InlineData("fishery", true)]
    [InlineData("library", true)]
    [InlineData("shrine", true)]
    [InlineData("coal_power_plant", true)]
    [InlineData("copper_mine", true)]
    [InlineData("great_statue", false)]   // monument
    [InlineData("spaceport", false)]      // megastavba, staví se minuty
    [InlineData("sea_dome", false)]       // podmoří
    [InlineData("irrigation_works", false)] // terraformace mění mapu
    [InlineData("lumberyard", false)]     // vzniká jen vylepšením
    public void RealContent_TheGovernorBuildsByRoleButLeavesBigDecisionsToThePlayer(string id, bool considered)
    {
        // Skutečná data: tohle je to, co hráč uvidí. Dřív byl seznam jen
        // sedmnáct budov se značkou autoBuild — žádný sklad, školka ani elektrárna.
        var content = TestData.LoadRealContent();
        var sim = new Simulation(content, new UniformTerrain((byte)1));

        Assert.True(content.Gameplay.Governor.BuildsByRole);
        Assert.Equal(considered, sim.GovernorConsiders(content.Buildings.IndexOf(id)));
    }

    [Fact]
    public void RealContent_KnowsWhatScienceAndFaithAre()
    {
        var content = TestData.LoadRealContent();
        var governor = content.Gameplay.Governor;

        Assert.Equal("science", content.Resources[governor.Knowledge.ResourceIndex].Id);
        Assert.Equal("faith", content.Resources[governor.Faith.ResourceIndex].Id);
    }

    [Fact]
    public void RealContent_AQuarryMayStandOnThePlainsBesideTheRocks()
    {
        // Kámen u města na louce se dal brát jen ručně: lom smel jen na hory.
        var content = TestData.LoadRealContent();
        var quarry = content.Buildings[content.Buildings.IndexOf("quarry")];

        Assert.True(quarry.IsBiomeAllowed(content.Biomes.IndexOf("grassland")));
        Assert.False(quarry.IsBiomeAllowed(content.Biomes.IndexOf("forest"))); // lom mezi stromy by kácel les
    }
}
