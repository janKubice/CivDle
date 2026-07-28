using CivDle.Core.Content;
using CivDle.Core.Sim;
using CivDle.Core.Tests.Support;
using CivDle.Core.World;
using Xunit;

namespace CivDle.Core.Tests.Sim;

/// <summary>
/// Bonus za okolí: pila u lesa vyrábí víc než pila uprostřed stepi. Bez téhle
/// vrstvy je umístění budovy jen otázka volného místa — s ní je to rozhodnutí.
///
/// <para>Násobič se cachuje při položení, takže testy hlídají i to, že se přepočítá
/// při přesunu; jinak by si budova nesla bonus z původního místa navždy.</para>
/// </summary>
public class AdjacencyTests
{
    private const int Water = 0;
    private const int Plain = 1;
    private const int Forest = 2;

    /// <summary>Les je pruh x &lt; 10; zbytek mapy je holá pláň.</summary>
    private sealed class ForestStripTerrain : ITerrain
    {
        public byte BiomeAt(int x, int y) => x < 10 ? (byte)Forest : (byte)Plain;
    }

    private static GameContent Content(double perTile = 0.05, double max = 0.4, int radius = 1)
    {
        var biomes = new[]
        {
            TestContent.WaterBiome(),
            TestContent.LandBiome("plain"),
            TestContent.LandBiome("forest"),
        };
        var resources = new[] { new Resource("wood", new RgbColor(1, 1, 1), StartAmount: 100, BaseStorage: 1_000_000) };

        var rule = new AdjacencyRule(
            Biomes: new[] { false, false, true },
            Radius: radius,
            BonusPerTile: perTile,
            MaxBonus: max);

        var sawmill = new BuildingDef(
            "sawmill", "production", new RgbColor(100, 100, 100), 1, 1,
            WorkerSlots: 1, HousingCapacity: 0,
            BuildCost: new[] { new ResourceAmount(0, 1) },
            Recipe: new Recipe(
                Inputs: Array.Empty<ResourceAmount>(),
                Outputs: new[] { new ResourceAmount(0, 10) },
                TimeTicks: 1),
            AllowedBiomes: new[] { false, true, true },
            StorageBonus: Array.Empty<ResourceAmount>(),
            AutoBuild: false, Buildable: true, UpgradesToIndex: -1,
            UpgradeCost: Array.Empty<ResourceAmount>(), PowerSupply: 0, PowerDemand: 0,
            AdjacencyOrNull: rule);

        // Stejná budova bez pravidla — kontrolní vzorek.
        var shack = sawmill with { Id = "shack", AdjacencyOrNull = null };

        var gameplay = TestContent.DefaultGameplay with
        {
            FoodPerPersonPerSecond = 0,
            PopulationGrowthPerSecond = 0,
        };
        return TestContent.Build(biomes, Plain, resources, new[] { sawmill, shack }, gameplay);
    }

    private static double ProducedOver(Simulation sim, int ticks)
    {
        double before = sim.GetResource(0);
        for (int i = 0; i < ticks; i++)
        {
            sim.Tick();
        }

        return sim.GetResource(0) - before;
    }

    [Fact]
    public void BuildingNextToMatchingBiome_ProducesMore()
    {
        // Na x=10 sousedí s lesním pruhem (x=9), na x=20 je sama uprostřed pláně.
        var nearForest = new Simulation(Content(), new ForestStripTerrain());
        Assert.Equal(PlacementResult.Ok, nearForest.TryPlaceBuilding(0, 10, 5));

        var farAway = new Simulation(Content(), new ForestStripTerrain());
        Assert.Equal(PlacementResult.Ok, farAway.TryPlaceBuilding(0, 20, 5));

        double near = ProducedOver(nearForest, 20);
        double far = ProducedOver(farAway, 20);

        Assert.True(near > far, $"pila u lesa má vyrábět víc ({near} vs {far})");
    }

    [Fact]
    public void TilesUnderTheBuilding_DoNotCount()
    {
        // Budova stojící v lese má bonus jen za okolí, ne za dlaždici pod sebou —
        // tu už ocení BiomeMult a jinak by se stejná věc počítala dvakrát.
        var sim = new Simulation(Content(radius: 1), new ForestStripTerrain());
        int tiles = sim.CountAdjacencyTiles(0, 5, 5); // uvnitř lesa, radius 1

        Assert.Equal(8, tiles); // celý prstenec 3×3 bez středu
    }

    [Fact]
    public void Bonus_IsCapped()
    {
        // 8 lesních dlaždic × 20 % by bylo +160 %, strop je +40 %.
        var sim = new Simulation(Content(perTile: 0.2, max: 0.4), new ForestStripTerrain());
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuilding(0, 5, 5));

        Assert.Equal(1.4f, sim.Buildings[0].AdjacencyMult, 3);
    }

    [Fact]
    public void BuildingWithoutRule_HasNeutralMultiplier()
    {
        var sim = new Simulation(Content(), new ForestStripTerrain());
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuilding(1, 5, 5)); // shack uprostřed lesa

        Assert.Equal(1f, sim.Buildings[0].AdjacencyMult, 3);
    }

    [Fact]
    public void MovingBuilding_RecomputesTheBonus()
    {
        var sim = new Simulation(Content(), new ForestStripTerrain());
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuilding(0, 5, 5)); // v lese
        float inForest = sim.Buildings[0].AdjacencyMult;

        Assert.Equal(PlacementResult.Ok, sim.TryMoveBuilding(0, 20, 5)); // na pláň
        float onPlain = sim.Buildings[0].AdjacencyMult;

        Assert.True(inForest > onPlain, $"po přesunu z lesa má bonus zmizet ({inForest} → {onPlain})");
        Assert.Equal(1f, onPlain, 3);
    }

    [Fact]
    public void Preview_MatchesWhatTheBuildingGetsAfterPlacing()
    {
        // Náhled při stavbě musí souhlasit s realitou, jinak hráči lže.
        var sim = new Simulation(Content(), new ForestStripTerrain());
        double preview = sim.AdjacencyMultiplierAt(0, 9, 5);

        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuilding(0, 9, 5));

        Assert.Equal(preview, sim.Buildings[0].AdjacencyMult, 3);
    }

    [Fact]
    public void BuildingRestoredFromASave_GetsItsBonusBack()
    {
        // Sav nese jen typ a pozici; násobič se musí odvodit z terénu při obnově,
        // jinak by po restartu hry pila u lesa vyráběla jako na pláni. Obnova ze
        // savu jde stejnou cestou jako TryPlaceBuildingFree.
        var placed = new Simulation(Content(), new ForestStripTerrain());
        Assert.Equal(PlacementResult.Ok, placed.TryPlaceBuilding(0, 5, 5));

        var restored = new Simulation(Content(), new ForestStripTerrain());
        Assert.Equal(PlacementResult.Ok, restored.TryPlaceBuildingFree(0, 5, 5));

        Assert.Equal(placed.Buildings[0].AdjacencyMult, restored.Buildings[0].AdjacencyMult, 3);
        Assert.True(restored.Buildings[0].AdjacencyMult > 1f);
    }

    [Fact]
    public void RealContent_GivesSomeBuildingsAReasonToStandSomewhere()
    {
        var content = TestData.LoadRealContent();
        int withRule = 0;
        foreach (var def in content.Buildings.All)
        {
            if (def.Adjacency is not { } rule)
            {
                continue;
            }

            withRule++;
            Assert.NotNull(def.Recipe); // bonus bez výroby by se neprojevil
            Assert.True(rule.MaxBonus > 0);
            Assert.True(rule.TilesForFullBonus > 0);
        }

        Assert.True(withRule >= 6, $"na umístění má záležet u víc budov, pravidlo má jen {withRule}");
    }

    [Fact]
    public void UnusedField_KeepsWaterOutOfTheRule()
    {
        // Pojistka proti překlepu v testovacích datech: index 0 je voda a ta se
        // do lesního pravidla počítat nemá.
        var content = Content();
        var rule = content.Buildings[0].Adjacency!;

        Assert.False(rule.Counts(Water));
        Assert.False(rule.Counts(Plain));
        Assert.True(rule.Counts(Forest));
    }
}
