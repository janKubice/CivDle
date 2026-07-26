using CivDle.Core.Content;
using CivDle.Core.Sim;
using CivDle.Core.Tests.Support;
using CivDle.Core.World;
using Xunit;

namespace CivDle.Core.Tests.Sim;

/// <summary>
/// Ekonomická identita biomů (living-map.md §5): biomy nejsou jen jiná grafika —
/// na úrodném terénu se vyrábí líp než v suché stepi. Násobič se cachuje u budovy
/// (hot path se nesmí vzorkovat terén každý tik) a musí jít s ní i při přesunu.
/// </summary>
public class BiomeEconomyTests
{
    /// <summary>Levá polovina mapy je „bohatý" biom (index 1), pravá „chudý" (index 2).</summary>
    private sealed class SplitTerrain : ITerrain
    {
        public byte BiomeAt(int x, int y) => x < 10 ? (byte)1 : (byte)2;
    }

    private static GameContent EconomyContent()
    {
        var biomes = new[]
        {
            TestContent.WaterBiome(),
            TestContent.LandBiome("rich") with { ProductionMult = 2.0 },
            TestContent.LandBiome("poor") with { ProductionMult = 0.5 },
        };
        var resources = new[] { new Resource("wood", new RgbColor(1, 1, 1), StartAmount: 100, BaseStorage: 100000) };
        var producer = new BuildingDef(
            "producer", "production", new RgbColor(100, 100, 100), 1, 1,
            WorkerSlots: 1, HousingCapacity: 0,
            BuildCost: new[] { new ResourceAmount(0, 1) },
            Recipe: new Recipe(
                Inputs: System.Array.Empty<ResourceAmount>(),
                Outputs: new[] { new ResourceAmount(0, 10) },
                TimeTicks: 1),
            AllowedBiomes: new[] { false, true, true },
            StorageBonus: System.Array.Empty<ResourceAmount>(),
            AutoBuild: false, Buildable: true, UpgradesToIndex: -1,
            UpgradeCost: System.Array.Empty<ResourceAmount>(), PowerSupply: 0, PowerDemand: 0);

        var gameplay = TestContent.DefaultGameplay with
        {
            FoodPerPersonPerSecond = 0,
            PopulationGrowthPerSecond = 0,
        };
        return TestContent.Build(biomes, 1, resources, new[] { producer }, gameplay);
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
    public void RichBiome_OutproducesPoorBiome()
    {
        var rich = new Simulation(EconomyContent(), new SplitTerrain());
        Assert.Equal(PlacementResult.Ok, rich.TryPlaceBuilding(0, 2, 2)); // bohatý biom

        var poor = new Simulation(EconomyContent(), new SplitTerrain());
        Assert.Equal(PlacementResult.Ok, poor.TryPlaceBuilding(0, 15, 2)); // chudý biom

        double richYield = ProducedOver(rich, 30);
        double poorYield = ProducedOver(poor, 30);

        Assert.True(richYield > poorYield, $"bohatý biom má vyrábět víc ({richYield} vs {poorYield})");
    }

    [Fact]
    public void MovingBuilding_UpdatesItsBiomeMultiplier()
    {
        // Cache se musí přesunout s budovou, jinak by si nesla starý biom navždy.
        var sim = new Simulation(EconomyContent(), new SplitTerrain());
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuilding(0, 2, 2)); // bohatý
        double richYield = ProducedOver(sim, 30);

        Assert.Equal(PlacementResult.Ok, sim.TryMoveBuilding(0, 15, 2)); // přesun do chudého
        double poorYield = ProducedOver(sim, 30);

        Assert.True(poorYield < richYield, $"po přesunu do chudého biomu má výroba klesnout ({poorYield} vs {richYield})");
    }

    [Fact]
    public void RealContent_GivesBiomesDistinctEconomies()
    {
        var content = TestData.LoadRealContent();
        double forest = content.Biomes[content.Biomes.IndexOf("forest")].Production;
        double steppe = content.Biomes[content.Biomes.IndexOf("steppe")].Production;
        double grass = content.Biomes[content.Biomes.IndexOf("grassland")].Production;

        Assert.True(forest > steppe, "biomy mají mít rozdílnou ekonomiku");
        Assert.Equal(1.0, grass); // louka zůstává neutrálním měřítkem
    }
}
