using CivDle.Core.Content;
using CivDle.Core.Sim;
using CivDle.Core.Tests.Support;
using CivDle.Core.World;
using Xunit;

namespace CivDle.Core.Tests.Sim;

/// <summary>
/// Guvernér na světech galaxie: co ho na Duně zaseklo a nesmí se vrátit.
///
/// <list type="bullet">
/// <item>Budova s tvrdým prahem sítě (háj bez vody neurodí) patří jen tam,
/// kam síť teče — jinak stavěl háj za hájem do suché pouště.</item>
/// <item>Výzkum nečeká na technologii, na kterou se nedá našetřit (sklo před
/// sklárnou) — přeskočí ji.</item>
/// <item>Surovinu, kterou chce další výzkum a nikdo ji nevyrábí, zajistí
/// (postaví výrobnu).</item>
/// </list>
/// </summary>
public class GovernorWorldTests
{
    private const int Water = 1;
    private const int Food = 0;
    private const int Glass = 1;

    private const int House = 0;
    private const int Grove = 1;
    private const int Well = 2;
    private const int Glassworks = 3;

    [Fact]
    public void AGroveOnlyGoesWhereWaterFlows()
    {
        // Hlad: guvernér chce háj. Voda teče jen u studny daleko od domu —
        // háj tedy patří tam, ne k domu do suché pouště.
        var (sim, _) = World(hungry: true);
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuildingFree(Well, 30, 30));
        sim.DebugCompleteConstruction();

        Run(sim, 300);

        var groves = sim.Buildings.ToArray().Where(b => b.DefIndex == Grove).ToList();
        Assert.NotEmpty(groves);
        Assert.All(groves, grove => Assert.True(
            sim.NetworkSteadyCoverageAt(Water, grove.X, grove.Y) >= 0.5,
            $"háj na {grove.X},{grove.Y} stojí tam, kam voda neteče (nebo nestačí)"));

        // Dřív háj vodu u nové studny „neviděl" (dodávka se píše jen tam, kde
        // ji někdo chce, a dvě studny u jednoho háje hlásily každá nulu volného
        // výkonu) a guvernér místo něj stavěl studnu za studnou.
        int wells = sim.Buildings.ToArray().Count(b => b.DefIndex == Well);
        Assert.True(wells <= 6, $"{wells} studen na pár hájů");
    }

    [Fact]
    public void ResearchSkipsATechItCanNeverAfford()
    {
        // „Sklářství levné" chce sklo, které nic nevyrábí; „Zemědělství" je
        // dražší, ale jídlo je. Dřív guvernér čekal na sklo navždy.
        var glassTech = new TechDef("glass_lore", new[] { new ResourceAmount(Glass, 5) }, Array.Empty<int>(), Array.Empty<int>());
        var farming = new TechDef("farming", new[] { new ResourceAmount(Food, 50) }, Array.Empty<int>(), Array.Empty<int>());
        var (sim, _) = World(techs: new[] { glassTech, farming }, glassworksUnlocked: false);
        sim.Plan.SetChoosesResearch(true);
        sim.DebugSetResource(Food, 500);

        Run(sim, 200);

        Assert.True(sim.IsTechResearched(1), "zemědělství se má vyzkoumat, i když je sklářství levnější");
        Assert.False(sim.IsTechResearched(0));
    }

    [Fact]
    public void ResearchMovesOnWhenAMaterialNeverFlows()
    {
        // Sklárna je odemčená, ale nemá kde stát (na Duně: solné pánve bez
        // solné pláně u města). Levné sklářství by čekalo navždy — výzkum
        // mezitím vezme to, na co město má.
        var glassTech = new TechDef("glass_lore", new[] { new ResourceAmount(Glass, 5) }, Array.Empty<int>(), Array.Empty<int>());
        var farming = new TechDef("farming", new[] { new ResourceAmount(Food, 50) }, Array.Empty<int>(), Array.Empty<int>());
        var (sim, _) = World(techs: new[] { glassTech, farming }, glassworksHasSite: false);
        sim.Plan.SetChoosesResearch(true);
        sim.DebugSetResource(Food, 500);

        Run(sim, 200);

        Assert.True(sim.IsTechResearched(1), "zemědělství se má vyzkoumat, i když sklářství čeká na sklo");
        Assert.False(sim.IsTechResearched(0));
    }

    [Fact]
    public void TheGovernorBuildsWhatTheNextResearchNeeds()
    {
        var glassTech = new TechDef("glass_lore", new[] { new ResourceAmount(Glass, 5) }, Array.Empty<int>(), Array.Empty<int>());
        var (sim, _) = World(techs: new[] { glassTech }, glassworksUnlocked: true);
        sim.Plan.SetChoosesResearch(true);

        Run(sim, 300);

        Assert.Contains(sim.Buildings.ToArray(), b => b.DefIndex == Glassworks);
    }

    [Fact]
    public void AWorkshopWaitsForPowerItCannotGet()
    {
        // Mráz na začátku: proud dává jen modul (4) a elektrárna čeká na
        // výzkum. Lidé bez práce „dostávali" dílnu za dílnou — sto
        // čtyřicet brusíren na proud pro dvě, každá jela na šedesátinu.
        var sim = PoweredWorld(plantUnlocked: false);

        Run(sim, 600);

        int workshops = sim.Buildings.ToArray().Count(b => b.DefIndex == PoweredWorkshop);
        Assert.InRange(workshops, 1, 2);
    }

    [Fact]
    public void AWorkshopMayGoFirstWhenAPlantCanFollow()
    {
        // Umí-li guvernér elektrárnu postavit, dílna na ni nečeká — cíl proudu
        // ji přistaví k dílně (tak se to na Domovině dělalo vždycky).
        var sim = PoweredWorld(plantUnlocked: true);

        Run(sim, 600);

        int workshops = sim.Buildings.ToArray().Count(b => b.DefIndex == PoweredWorkshop);
        Assert.True(workshops > 2, $"jen {workshops} dílen, i když elektrárna jde postavit");
    }

    private const int PoweredLander = 1;
    private const int PoweredWorkshop = 2;

    /// <summary>
    /// Město s lidmi bez práce: dům stojí sklo, sklo dělá dílna na proud,
    /// proud dává modul (4 = dvě dílny) a — když ji test odemkne — elektrárna.
    /// </summary>
    private static Simulation PoweredWorld(bool plantUnlocked)
    {
        var biomes = new[] { TestContent.WaterBiome(), TestContent.LandBiome("tundra") };
        var resources = new[]
        {
            new Resource("food", new RgbColor(200, 180, 60), 0, BaseStorage: 10_000),
            new Resource("glass", new RgbColor(150, 210, 220), 0, BaseStorage: 10_000),
        };
        var land = new[] { false, true };

        BuildingDef Def(string id, int workers = 0, int housing = 0, Recipe? recipe = null, bool autoBuild = false,
            int powerSupply = 0, int powerDemand = 0, ResourceAmount[]? cost = null, bool buildable = true) => new(
            id, "test", new RgbColor(1, 1, 1), 1, 1,
            WorkerSlots: workers, HousingCapacity: housing, BuildCost: cost ?? Array.Empty<ResourceAmount>(),
            Recipe: recipe, AllowedBiomes: land, StorageBonus: Array.Empty<ResourceAmount>(),
            AutoBuild: autoBuild, Buildable: buildable, UpgradesToIndex: -1,
            UpgradeCost: Array.Empty<ResourceAmount>(), PowerSupply: powerSupply, PowerDemand: powerDemand);

        var buildings = new[]
        {
            Def("house", housing: 5, autoBuild: true, cost: new[] { new ResourceAmount(Glass, 1) }),
            Def("lander", housing: 80, powerSupply: 4, buildable: false), // přistane, nestaví se
            Def("glassworks", workers: 3, autoBuild: true, powerDemand: 2,
                recipe: new Recipe(Array.Empty<ResourceAmount>(), new[] { new ResourceAmount(Glass, 1) }, 10)),
            Def("power_plant", autoBuild: true, powerSupply: 10),
        };

        // Zamčenou elektrárnu odemyká výzkum, na který město nemá.
        var techs = plantUnlocked
            ? Array.Empty<TechDef>()
            : new[] { new TechDef("steam", new[] { new ResourceAmount(Food, 1_000_000) }, Array.Empty<int>(), new[] { 3 }) };

        var gameplay = TestContent.DefaultGameplay with
        {
            FoodPerPersonPerSecond = 0,
            PopulationGrowthPerSecond = 0,
            StartingPopulation = 60,
            AutoBuild = new AutoBuildConfig(IntervalTicks: 5, SearchRadius: 6, PopulationHeadroom: 2),
            GovernorOrNull = new GovernorConfig(
                true, StorageGoalConfig.Off, SupplyGoalConfig.Off, SupplyGoalConfig.Off,
                LandscapeGoalConfig.Off, new PowerGoalConfig(0.9)),
        };

        var content = TestContent.Build(biomes, 1, resources, buildings, gameplay, techs: techs)
            .WithWorld(WorldProfile.Home with { Id = "frost", LandingModuleIndex = PoweredLander });
        var sim = new Simulation(content, new UniformTerrain(1), seed: 5);
        Assert.Equal(PlacementResult.Ok, sim.Land(0, 0));
        return sim;
    }

    private static (Simulation Sim, GameContent Content) World(
        IReadOnlyList<TechDef>? techs = null, bool glassworksUnlocked = true, bool hungry = false,
        bool glassworksHasSite = true)
    {
        var biomes = new[] { TestContent.WaterBiome(), TestContent.LandBiome("sand") };
        var resources = new[]
        {
            new Resource("food", new RgbColor(200, 180, 60), 0, BaseStorage: 10_000),
            new Resource("glass", new RgbColor(150, 210, 220), 0, BaseStorage: 10_000),
        };
        var land = new[] { false, true };

        BuildingDef Def(string id, Recipe? recipe, NetworkUse? use, int housing = 0, bool autoBuild = false, bool[]? biomes = null) => new(
            id, "test", new RgbColor(1, 1, 1), 1, 1,
            WorkerSlots: 0, HousingCapacity: housing, BuildCost: Array.Empty<ResourceAmount>(),
            Recipe: recipe, AllowedBiomes: biomes ?? land, StorageBonus: Array.Empty<ResourceAmount>(),
            AutoBuild: autoBuild, Buildable: true, UpgradesToIndex: -1,
            UpgradeCost: Array.Empty<ResourceAmount>(), PowerSupply: 0, PowerDemand: 0,
            NetworksOrNull: use is { } u ? new[] { u } : null);

        var buildings = new[]
        {
            Def("house", null, null, housing: 5),
            Def("grove", new Recipe(Array.Empty<ResourceAmount>(), new[] { new ResourceAmount(Food, 1) }, 10),
                new NetworkUse(Water, 0, 10, 0, CutoffBelow: 0.5), autoBuild: true),
            Def("well", null, new NetworkUse(Water, 10, 0, 0)),
            Def("glassworks", new Recipe(Array.Empty<ResourceAmount>(), new[] { new ResourceAmount(Glass, 1) }, 10), null,
                biomes: glassworksHasSite ? null : new[] { false, false }),
        };

        // Sklárnu odemyká technologie, kterou nikdo nezkoumá — dokud ji
        // test nechce odemčenou.
        var allTechs = new List<TechDef>(techs ?? Array.Empty<TechDef>());
        if (!glassworksUnlocked)
        {
            allTechs.Add(new TechDef("glassmaking", new[] { new ResourceAmount(Food, 1_000_000) },
                Array.Empty<int>(), new[] { Glassworks }));
        }

        var gameplay = TestContent.DefaultGameplay with
        {
            FoodPerPersonPerSecond = hungry ? 0.5 : 0,
            PopulationGrowthPerSecond = 0,
            AutoBuild = new AutoBuildConfig(IntervalTicks: 5, SearchRadius: 6, PopulationHeadroom: 2),
            GovernorOrNull = new GovernorConfig(
                true, StorageGoalConfig.Off, SupplyGoalConfig.Off, SupplyGoalConfig.Off,
                LandscapeGoalConfig.Off, new PowerGoalConfig(0.9)),
        };

        var content = TestContent.Build(biomes, 1, resources, buildings, gameplay, techs: allTechs)
            .WithNetworks(new[] { new NetworkTypeDef("water", 2, NetworkShortage.Slowdown, 0, new RgbColor(60, 160, 230)) });
        var sim = new Simulation(content, new UniformTerrain(1), seed: 5);

        // Guvernér roste od zástavby — první dům je vždycky hráčův.
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuildingFree(House, 0, 0));
        sim.DebugCompleteConstruction();
        return (sim, content);
    }

    private static void Run(Simulation sim, int ticks)
    {
        for (int i = 0; i < ticks; i++)
        {
            sim.Tick();
        }
    }
}
