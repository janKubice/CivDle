using CivDle.Core.Content;
using CivDle.Core.Sim;
using CivDle.Core.Tests.Support;
using CivDle.Core.World;
using Xunit;

namespace CivDle.Core.Tests.Sim;

/// <summary>
/// Guvernér, který se nezasekne: shání suroviny přes celý řetěz, staví těžbu
/// tam, kde je co těžit, a když neví jak dál, řekne to.
///
/// <para>Každý test odpovídá zámku, který se změřil na skutečných seedech:</para>
/// <list type="bullet">
/// <item>město na louce nemělo kam postavit dřevorubce (smí jen do lesa),</item>
/// <item>pila brala tři dřeva hned, jak přišla, a dům za pět se nepostavil nikdy,</item>
/// <item>k hladovým pilám přibývaly další pily místo dřevorubců,</item>
/// <item>vykácený dřevorubec stál navždy, i když o kus dál rostl les,</item>
/// <item>skála blíž k městu vyčerpala hledání dřív, než došlo na les,</item>
/// <item>lidé bez práce dostávali další hladovou pilu místo dřevorubce,</item>
/// <item>pekárna a mlýn vyrostly, i když obilné pole bylo za výzkumem.</item>
/// </list>
/// </summary>
public class GovernorSupplyTests
{
    private const byte Grass = 1;
    private const byte Forest = 2;
    private const byte Rock = 3;

    private const int Food = 0;
    private const int Wood = 1;
    private const int Planks = 2;
    private const int Ore = 3;
    private const int Metal = 4;
    private const int Bread = 5;
    private const int Flour = 6;
    private const int Grain = 7;

    private const int LumberCamp = 0;
    private const int Sawmill = 1;
    private const int House = 2;
    private const int SlowCamp = 3;
    private const int Mine = 4;
    private const int Smelter = 5;
    private const int Kiln = 6;
    private const int Mill = 7;
    private const int Bakery = 8;

    /// <summary>Kde roste velký les (daleko od města).</summary>
    private static readonly (int MinX, int MinY, int MaxX, int MaxY) FarForest = (30, 30, 38, 38);

    /// <summary>Skalnatý pás mezi městem a lesem (uzly rudy, dřevorubec tam nesmí).</summary>
    private static readonly (int MinX, int MinY, int MaxX, int MaxY) Rocks = (10, 10, 27, 27);

    private static GameContent Content(bool renewableForest = false, bool breadChain = false)
    {
        var yield = new ClickYield(Wood, 1, Charges: 1, RegrowSeconds: renewableForest ? 30 : 0);
        var biomes = new[]
        {
            TestContent.WaterBiome(),
            TestContent.LandBiome("grass"),
            TestContent.LandBiome("forest") with { ClickYield = yield },
            TestContent.LandBiome("rock") with { ClickYield = new ClickYield(Ore, 1, Charges: 1, RegrowSeconds: 0) },
        };

        var resources = new[]
        {
            new Resource("food", new RgbColor(1, 1, 1), StartAmount: 0, BaseStorage: 1000),
            new Resource("wood", new RgbColor(1, 1, 1), StartAmount: 0, BaseStorage: 1000),
            new Resource("planks", new RgbColor(1, 1, 1), StartAmount: 0, BaseStorage: 1000),
            new Resource("ore", new RgbColor(1, 1, 1), StartAmount: 0, BaseStorage: 1000),
            new Resource("metal", new RgbColor(1, 1, 1), StartAmount: 0, BaseStorage: 1000),
            new Resource("bread", new RgbColor(1, 1, 1), StartAmount: 0, BaseStorage: 1000),
            new Resource("flour", new RgbColor(1, 1, 1), StartAmount: 0, BaseStorage: 1000),
            new Resource("grain", new RgbColor(1, 1, 1), StartAmount: 0, BaseStorage: 1000),
        };

        bool[] grassOnly = { false, true, false, false };
        bool[] forestOnly = { false, false, true, false };

        var lumberCamp = TestContent.Producer("lumber_camp", Wood, 2, timeTicks: 5, biomeCount: 4) with
        {
            Category = "production",
            AllowedBiomes = forestOnly,
            AutoBuild = true,
            TerrainHarvestRadius = 2,
            BuildCost = new[] { new ResourceAmount(Wood, 5) },
        };
        var sawmill = TestContent.Converter("sawmill", Wood, 3, Planks, 1, timeTicks: 5, biomeCount: 4,
            buildCost: new[] { new ResourceAmount(Wood, 20) }, autoBuild: true) with
        {
            Category = "production",
            AllowedBiomes = grassOnly,
        };
        var house = TestContent.SimpleBuilding("house", 4, housing: 4) with
        {
            Category = "housing",
            AllowedBiomes = grassOnly,
            AutoBuild = true,
            BuildCost = new[] { new ResourceAmount(Wood, 5), new ResourceAmount(Planks, 4) },
        };

        // Pomalý zdroj dřeva, který guvernér sám nestaví — pevný přítok pro testy zámků.
        var slowCamp = TestContent.Producer("slow_camp", Wood, 1, timeTicks: 20, biomeCount: 4) with
        {
            AllowedBiomes = grassOnly,
        };
        var mine = TestContent.Producer("mine", Ore, 2, timeTicks: 5, biomeCount: 4) with
        {
            Category = "production",
            AllowedBiomes = grassOnly,
        };
        var smelter = TestContent.Converter("smelter", Ore, 2, Metal, 1, timeTicks: 5, biomeCount: 4) with
        {
            Category = "production",
            AllowedBiomes = grassOnly,
        };

        // Pomalý spotřebitel dřeva: jeden dřevorubec ho uživí, takže guvernér
        // nemá důvod stavět další — jen když dřevorubci dojde les.
        var kiln = TestContent.Converter("kiln", Wood, 1, Planks, 1, timeTicks: 20, biomeCount: 4) with
        {
            AllowedBiomes = grassOnly,
        };

        var gameplay = TestContent.DefaultGameplay with
        {
            FoodResourceIndex = Food,
            FoodPerPersonPerSecond = 0.0,
            PopulationGrowthPerSecond = 0.0,
            AutoBuild = new AutoBuildConfig(IntervalTicks: 5, SearchRadius: 6, PopulationHeadroom: 2),
        };

        var buildings = new List<BuildingDef> { lumberCamp, sawmill, house, slowCamp, mine, smelter, kiln };
        if (breadChain)
        {
            // Chléb ← mouka ← obilí; obilné pole je „za výzkumem" (nestavitelné).
            // Větší dům za chléb dělá z chleba stavební materiál — přesně to,
            // co guvernéra ve hře dovedlo k pekárně.
            buildings.Add(TestContent.Converter("mill", Grain, 3, Flour, 2, timeTicks: 5, biomeCount: 4,
                buildCost: new[] { new ResourceAmount(Wood, 5) }, autoBuild: true) with
            {
                Category = "production",
                AllowedBiomes = grassOnly,
            });
            buildings.Add(TestContent.Converter("bakery", Flour, 2, Bread, 3, timeTicks: 5, biomeCount: 4,
                buildCost: new[] { new ResourceAmount(Wood, 5) }, autoBuild: true) with
            {
                Category = "production",
                AllowedBiomes = grassOnly,
            });
            buildings.Add(TestContent.Producer("grain_field", Grain, 4, timeTicks: 5, biomeCount: 4) with
            {
                Category = "production",
                AllowedBiomes = grassOnly,
                Buildable = false,
            });
            buildings.Add(TestContent.SimpleBuilding("bread_house", 4, housing: 8) with
            {
                Category = "housing",
                AllowedBiomes = grassOnly,
                AutoBuild = true,
                BuildCost = new[] { new ResourceAmount(Bread, 10) },
            });
        }

        return TestContent.Build(biomes, fallbackBiomeIndex: 1, resources, buildings, gameplay);
    }

    /// <summary>Louka s lesem daleko od města; volitelně malý háj hned vedle a skála mezi.</summary>
    private static Simulation World(
        bool grove = false, bool renewableForest = false, bool rocks = false, bool breadChain = false)
    {
        var map = new WorldMap(48, 48);
        Array.Fill(map.BiomeIndices, Grass);
        if (rocks)
        {
            Paint(map, Rocks, Rock);
        }

        Paint(map, FarForest);
        if (grove)
        {
            Paint(map, (8, 8, 9, 9));
        }

        return new Simulation(Content(renewableForest, breadChain), new GridTerrain(map), seed: 11);
    }

    private static void Paint(WorldMap map, (int MinX, int MinY, int MaxX, int MaxY) area, byte biome = Forest)
    {
        for (int y = area.MinY; y <= area.MaxY; y++)
        {
            for (int x = area.MinX; x <= area.MaxX; x++)
            {
                map.BiomeIndices[map.Index(x, y)] = biome;
            }
        }
    }

    private static void Run(Simulation sim, int ticks)
    {
        for (int i = 0; i < ticks; i++)
        {
            sim.Tick();
        }
    }

    private static int CountOf(Simulation sim, int defIndex) =>
        sim.Buildings.ToArray().Count(b => b.DefIndex == defIndex);

    private static bool Inside((int MinX, int MinY, int MaxX, int MaxY) area, BuildingInstance building) =>
        building.X >= area.MinX && building.X <= area.MaxX && building.Y >= area.MinY && building.Y <= area.MaxY;

    [Fact]
    public void ALumberCampIsBuiltInTheForest_NotNextToTown()
    {
        // Město stojí na louce, dřevorubec smí jen do lesa. Dřív hledal místo do
        // šesti dlaždic od domů — a nenašel nikdy.
        var sim = World();
        sim.TryPlaceBuildingFree(Sawmill, 5, 5); // pila bez dřeva = vyschlý vstup
        sim.DebugSetResource(Wood, 10);

        Run(sim, 20);

        var camp = sim.Buildings.ToArray().Single(b => b.DefIndex == LumberCamp);
        Assert.True(Inside(FarForest, camp), $"dřevorubec stojí mimo les ({camp.X},{camp.Y})");
    }

    [Fact]
    public void RocksCloserToTown_DoNotHideTheForest()
    {
        // Seed 777001: hory u města, les o patnáct dlaždic dál. Hledání bralo
        // první uzly jakéhokoli druhu, všechny pokusy padly na skálu (kam
        // dřevorubec nesmí) — a guvernér pak stavěl pilu za pilou.
        var sim = World(rocks: true);
        sim.TryPlaceBuildingFree(Sawmill, 5, 5);
        sim.DebugSetResource(Wood, 10);

        Run(sim, 20);

        var camp = sim.Buildings.ToArray().Single(b => b.DefIndex == LumberCamp);
        Assert.True(Inside(FarForest, camp), $"dřevorubec stojí mimo les ({camp.X},{camp.Y})");
    }

    [Fact]
    public void JoblessPeopleGetALumberCamp_NotAnotherHungrySawmill()
    {
        // Lidé bez práce → „postav výrobnu nejprázdnější suroviny" → prkna → pila.
        // Jenže stávající pila stojí bez dřeva a nová by stála vedle ní.
        // Změřeno: 47 pil a jeden dřevorubec.
        var sim = World(renewableForest: true);
        sim.TryPlaceBuildingFree(SlowCamp, 2, 2); // dřevo teče, jen pomalu
        sim.TryPlaceBuildingFree(Sawmill, 4, 2);
        for (int x = 0; x <= 18; x += 2)
        {
            sim.TryPlaceBuildingFree(House, x, 12); // bydlení dost, ať řeší jen práci
        }

        sim.SetPopulationForTest(40);
        sim.DebugSetResource(Wood, 60);

        // Pár kol guvernéra: dřevo zatím nemá přebytek, takže pila nesmí přibýt.
        // (Až ho dřevorubci vytvoří, další pila je v pořádku — to už není tenhle zámek.)
        Run(sim, 30);

        Assert.Equal(1, CountOf(sim, Sawmill));
        Assert.True(CountOf(sim, LumberCamp) > 0, "lidem bez práce nepřibyl dřevorubec");
    }

    [Fact]
    public void ItDoesNotStartAChainItCannotFinish()
    {
        // Město chce bydlení; nejlepší dům stojí chléb. Guvernér by sháněl
        // chléb → pekárna → mouka → mlýn → obilí, a obilné pole je za
        // výzkumem: pekárna i mlýn by stály od prvního dne.
        var sim = World(breadChain: true);
        sim.TryPlaceBuildingFree(House, 2, 2);
        sim.SetPopulationForTest(9); // strop 10 − rezerva 2 → chce další dům
        sim.DebugSetResource(Wood, 200); // na pekárnu i mlýn by bylo

        Run(sim, 200);

        Assert.Equal(0, CountOf(sim, Bakery));
        Assert.Equal(0, CountOf(sim, Mill));
    }

    [Fact]
    public void AStuckChainNamesItsRoot_NotTheMiddle()
    {
        // Hráč postavil pekárnu sám. Guvernér ji nenakrmí — ale musí říct
        // proč správně: chybí obilí (vyzkoumej pole), ne mouka (postav mlýn,
        // který by stál taky).
        var sim = World(breadChain: true);
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuildingFree(Bakery, 5, 5));
        sim.DebugSetResource(Wood, 200);

        Run(sim, 20);

        Assert.Equal(GovernorActivity.Stuck, sim.GovernorStatus.Activity);
        Assert.Equal(GovernorBlocker.NoProducer, sim.GovernorStatus.Blocker);
        Assert.Equal(Grain, sim.GovernorStatus.ResourceIndex);
        Assert.Equal(0, CountOf(sim, Mill));
    }

    [Fact]
    public void AnExhaustedLumberCampMovesToFreshForest()
    {
        // Háj u města se vykácí a neobnoví. O kus dál roste les — přestěhovat
        // dřevorubce je zadarmo a nic dalšího se nestaví.
        var sim = World(grove: true);
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuildingFree(LumberCamp, 8, 8));
        sim.TryPlaceBuildingFree(Kiln, 3, 3);
        sim.TryPlaceBuildingFree(House, 3, 5); // dost bydlení, ať guvernér neřeší nic jiného
        sim.TryPlaceBuildingFree(House, 5, 5);

        Run(sim, 1500);

        Assert.Equal(1, CountOf(sim, LumberCamp));
        var camp = sim.Buildings.ToArray().Single(b => b.DefIndex == LumberCamp);
        Assert.True(Inside(FarForest, camp), $"vykácený dřevorubec zůstal stát na ({camp.X},{camp.Y})");
    }

    [Fact]
    public void AHouseGetsBuiltEvenWhenASawmillEatsEveryLog()
    {
        // Zámek na sto minut: pila bere tři dřeva, jakmile tam jsou, dům chce
        // pět. Bez rezervy se na pět nikdy nedostane.
        var sim = World();
        sim.TryPlaceBuildingFree(SlowCamp, 2, 2);
        sim.TryPlaceBuildingFree(Sawmill, 4, 2);
        sim.SetPopulationForTest(6); // strop bydlení 6 → chce dům

        Run(sim, 3000);

        Assert.True(CountOf(sim, House) > 0, "dům se nepostavil — pila spolykala každé dřevo");
    }

    [Fact]
    public void WhileSavingTheGovernorSaysWhatFor()
    {
        var sim = World();
        sim.TryPlaceBuildingFree(SlowCamp, 2, 2);
        sim.TryPlaceBuildingFree(Sawmill, 4, 2);
        sim.SetPopulationForTest(6);

        Run(sim, 20);

        Assert.Equal(GovernorActivity.Saving, sim.GovernorStatus.Activity);
        Assert.Equal(House, sim.GovernorStatus.DefIndex);
        Assert.Equal(House, sim.Claim.DefIndex);
    }

    [Fact]
    public void HungrySawmillsGetMoreWood_NotMoreSawmills()
    {
        // Dřív: pily nemají dřevo → „prkna nikdo nedělá" → další pila. Osm pil
        // na dva dřevorubce. Úzké hrdlo je dřevo.
        var sim = World(renewableForest: true);
        sim.TryPlaceBuildingFree(SlowCamp, 2, 2);
        sim.TryPlaceBuildingFree(Sawmill, 4, 2);
        sim.TryPlaceBuildingFree(Sawmill, 6, 2);
        sim.SetPopulationForTest(8);

        Run(sim, 3000);

        Assert.Equal(2, CountOf(sim, Sawmill));
        Assert.True(CountOf(sim, LumberCamp) > 0, "k hladovým pilám nepřibyl dřevorubec");
    }

    [Fact]
    public void WithNoWoodAtAllItAsksThePlayer()
    {
        // Na dřevorubce je potřeba dřevo, a to neteče. Z toho se automatika
        // sama nedostane — musí to říct, ne tiše čekat.
        var sim = World();
        sim.TryPlaceBuildingFree(House, 2, 2);
        sim.SetPopulationForTest(9); // strop 10 − rezerva 2 → chce další dům

        Run(sim, 20);

        Assert.Equal(GovernorBlocker.Bootstrap, sim.GovernorStatus.Blocker);
        Assert.Equal(LumberCamp, sim.Claim.DefIndex); // co hráč nasbírá, drží se na dřevorubce

        bool reported = false;
        while (sim.TryDequeueNotification(out var note))
        {
            reported |= note.Kind == NotificationKind.GovernorStuck && note.TitleKey == "toast.governor.bootstrap";
        }

        Assert.True(reported, "guvernér uvízl potichu");
    }

    [Fact]
    public void ItFeedsAChainThePlayerStarted_EvenWithABuildingItNeverBuildsAlone()
    {
        // Hráč postavil huť; ruda nikde. Důl nemá značku autoBuild — ale nechat
        // huť stát navždy by bylo horší než ho dostavět.
        var sim = World();
        sim.TryPlaceBuildingFree(Smelter, 5, 5);

        Run(sim, 20);

        Assert.Equal(1, CountOf(sim, Mine));
    }
}
