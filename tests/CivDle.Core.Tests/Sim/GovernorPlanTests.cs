using CivDle.Core.Content;
using CivDle.Core.Sim;
using CivDle.Core.Tests.Support;
using CivDle.Core.World;
using Xunit;

namespace CivDle.Core.Tests.Sim;

/// <summary>
/// Plán guvernéra: kam tlačit a co smí stavět.
///
/// <para>Guvernér uměl jen „co smí vylepšovat" a jinak si dělal, co chtěl.
/// Testy hlídají dvě věci, na kterých ta kontrola stojí: že si hráč může
/// vybrat stranu (velikost vs. kvalita) <b>bez ztráty tempa</b>, a že zákaz
/// kategorie guvernér opravdu respektuje — i když si o ni město říká.</para>
/// </summary>
public class GovernorPlanTests
{
    private static readonly Resource[] Resources =
    {
        new("wood", new RgbColor(120, 90, 60), StartAmount: 100_000, BaseStorage: 1_000_000),
    };

    private static BuildingDef Def(string id, string category, bool autoBuild = true) => new(
        id, category, new RgbColor(180, 100, 60), 1, 1,
        WorkerSlots: 0, HousingCapacity: 10,
        BuildCost: new[] { new ResourceAmount(0, 1) },
        Recipe: null,
        AllowedBiomes: new[] { false, true },
        StorageBonus: Array.Empty<ResourceAmount>(),
        AutoBuild: autoBuild, Buildable: true,
        UpgradesToIndex: -1, UpgradeCost: Array.Empty<ResourceAmount>(),
        PowerSupply: 0, PowerDemand: 0);

    private static GameContent Content() => TestContent.Build(
        biomes: new[] { TestContent.WaterBiome(), TestContent.LandBiome("grass") },
        resources: Resources,
        buildings: new[] { Def("house", "housing"), Def("shrine", "monument") });

    private static Simulation World() => new(Content(), new UniformTerrain((byte)1));

    // ----- zaměření -----

    [Fact]
    public void TheDefaultPlanIsTheOldBehaviour()
    {
        // Kdo si nic nenastaví, nesmí poznat rozdíl. Proto má vyvážený plán
        // váhu přesně 1 na obou stranách.
        var plan = new GovernorPlan();

        Assert.Equal(GovernorFocus.Balanced, plan.Focus);
        Assert.Equal(1.0, plan.GrowthWeight);
        Assert.Equal(1.0, plan.QualityWeight);
    }

    [Fact]
    public void PickingASideAddsToItInsteadOfTakingFromTheOther()
    {
        // Kdyby volba jen dělila dosavadní rozpočet, byla by každá strana
        // zhoršením proti dnešku a nikdo by si ji nevybral.
        var plan = new GovernorPlan();

        plan.SetFocus(GovernorFocus.Growth);
        Assert.True(plan.GrowthWeight > 1.0);

        plan.SetFocus(GovernorFocus.Quality);
        Assert.True(plan.QualityWeight > 1.0);
    }

    [Fact]
    public void TheExtremesReallyStopTheOtherHalf()
    {
        var plan = new GovernorPlan();

        plan.SetFocus(GovernorFocus.Growth);
        Assert.False(plan.UpgradesAtAll);
        Assert.True(plan.BuildsAtAll);

        plan.SetFocus(GovernorFocus.Quality);
        Assert.False(plan.BuildsAtAll);
        Assert.True(plan.UpgradesAtAll);
    }

    [Fact]
    public void QualityOnlyReallyStopsBuilding()
    {
        var (sim, _) = RealWorld();

        sim.Plan.SetFocus(GovernorFocus.Quality);

        Assert.Equal(0, sim.AutoBuildBudget);
    }

    [Fact]
    public void UpgradingIsNotDerivedFromTheWeightedBuildBudget()
    {
        // Nejzrádnější případ celého plánu: kdyby se rozpočet na vylepšování
        // odvozoval od VÁŽENÉHO tempa stavby, „jen kvalita" by ho vynásobila
        // nulou a guvernér by nedělal vůbec nic — tedy přesně opak toho, co si
        // hráč vybral.
        //
        // Měří se to na vahách, ne na simulaci: rozpočet na vylepšování je bez
        // odemčeného guvernéra vždycky nula, takže by simulační test tuhle
        // záměnu nechytil (nula by vyšla tak jako tak).
        var plan = new GovernorPlan();
        plan.SetFocus(GovernorFocus.Quality);

        Assert.Equal(0.0, plan.GrowthWeight);
        Assert.True(plan.QualityWeight > 0, "na „jen kvalitě\" musí vylepšování zůstat naživu");
    }

    [Fact]
    public void GrowthFocusBuildsFasterThanBalanced()
    {
        var sim = World();

        sim.Plan.SetFocus(GovernorFocus.Balanced);
        int balanced = sim.AutoBuildBudget;

        sim.Plan.SetFocus(GovernorFocus.Growth);

        Assert.True(sim.AutoBuildBudget > balanced);
    }

    // ----- kategorie -----

    [Fact]
    public void EverythingIsAllowedByDefault()
    {
        // Zakázané, ne povolené: seznam povolených by tiše zablokoval každou
        // budovu, která přibude v aktualizaci nebo v modu.
        var plan = new GovernorPlan();

        Assert.True(plan.AllowsCategory("housing"));
        Assert.True(plan.AllowsCategory("cokoli_z_modu"));
    }

    [Fact]
    public void ABlockedCategoryStaysBlocked()
    {
        var plan = new GovernorPlan();

        plan.SetCategoryAllowed("monument", false);

        Assert.False(plan.AllowsCategory("monument"));
        Assert.True(plan.AllowsCategory("housing"));
    }

    [Fact]
    public void BlockingIsReversible()
    {
        var plan = new GovernorPlan();

        plan.SetCategoryAllowed("monument", false);
        plan.SetCategoryAllowed("monument", true);

        Assert.True(plan.AllowsCategory("monument"));
        Assert.Empty(plan.BlockedCategories);
    }

    [Fact]
    public void BlockingEverythingStopsTheGovernorDead()
    {
        // Tohle je celý smysl přepínače: i když má město plné sklady a potřebu,
        // zakázanou kategorii nepostaví. Jede přes skutečný obsah — na dvou
        // vymyšlených budovách by se to dalo splnit i omylem.
        var (sim, content) = RealWorld();
        foreach (string category in Categories(content))
        {
            sim.Plan.SetCategoryAllowed(category, false);
        }

        int before = sim.Buildings.Length;
        Grow(sim);

        Assert.Equal(before, sim.Buildings.Length);
    }

    [Fact]
    public void UnblockingLetsItBuildAgain()
    {
        // Druhá půlka téhož: bez tohohle by test výše prošel i pro guvernéra,
        // který nestaví vůbec nic, tedy i kdyby byl přepínač úplně mimo.
        var (sim, _) = RealWorld();

        int before = sim.Buildings.Length;
        Grow(sim);

        Assert.True(sim.Buildings.Length > before, "auto-stavba nepostavila vůbec nic");
    }

    [Fact]
    public void BlockingOneCategoryStopsExactlyThatOne()
    {
        // Zakáže se kategorie, o které z testu výše víme, že se doopravdy
        // staví — jinak by test prošel i pro přepínač, který nedělá nic.
        var (sim, content) = RealWorld();
        int before = CountOfCategory(sim, content, "housing");

        sim.Plan.SetCategoryAllowed("housing", false);
        Grow(sim);

        Assert.Equal(before, CountOfCategory(sim, content, "housing"));
    }

    // ----- pomůcky -----

    /// <summary>Svět nad skutečným obsahem s domkem uprostřed a plnými sklady.</summary>
    private static (Simulation Sim, GameContent Content) RealWorld()
    {
        var content = TestData.LoadRealContent();
        var sim = new Simulation(content, new UniformTerrain(GrassBiome(content)));
        sim.DebugFillStorages();
        sim.TryPlaceBuilding(FirstAutoBuildable(content), sim.CityCenterX, sim.CityCenterY);

        // Bez tlaku nemá guvernér co řešit a správně nestaví nic. Lidi navíc
        // jsou tedy součást zadání testu, ne berlička: teprve pak je vidět,
        // jestli plán jeho volbu ovlivňuje.
        sim.DebugAddPopulation(500);
        return (sim, content);
    }

    /// <summary>Nechá město chvíli růst s plnými sklady, ať na suroviny nenarazí.</summary>
    private static void Grow(Simulation sim)
    {
        for (int i = 0; i < 1200; i++)
        {
            sim.DebugFillStorages();
            sim.Tick();
        }
    }

    private static byte GrassBiome(GameContent content)
    {
        for (int i = 0; i < content.Biomes.Count; i++)
        {
            if (content.Biomes[i].Id == "grassland")
            {
                return (byte)i;
            }
        }

        return 1;
    }

    private static int FirstAutoBuildable(GameContent content)
    {
        for (int i = 0; i < content.Buildings.Count; i++)
        {
            if (content.Buildings[i].AutoBuild && content.Buildings[i].Category == "housing")
            {
                return i;
            }
        }

        return 0;
    }

    private static IEnumerable<string> Categories(GameContent content)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < content.Buildings.Count; i++)
        {
            seen.Add(content.Buildings[i].Category);
        }

        return seen;
    }

    private static int CountOfCategory(Simulation sim, GameContent content, string category)
    {
        int count = 0;
        var buildings = sim.Buildings;
        for (int i = 0; i < buildings.Length; i++)
        {
            if (content.Buildings[buildings[i].DefIndex].Category == category)
            {
                count++;
            }
        }

        return count;
    }
}
