using CivDle.Core.Content;
using CivDle.Core.Sim;
using CivDle.Core.Tests.Support;
using CivDle.Core.World;
using Xunit;

namespace CivDle.Core.Tests.Sim;

/// <summary>
/// Detekce osad (fáze 4): shluk budov dostane stabilní jméno, oddělené shluky
/// jsou oddělené osady. Syntetický obsah s levnou boudou — testuje se systém,
/// ne balanc cen; přepočet běží na intervalu (50 tiků z DefaultGameplay).
/// </summary>
public class SettlementTests
{
    private const int UpdateInterval = 50;

    /// <summary>Sim s levnou boudou (1 dřevo) na trávě — na shluky je surovin dost.</summary>
    private static (GameContent Content, Simulation Sim) HutWorld(long seed = 7)
    {
        var biomes = new[] { TestContent.WaterBiome(), TestContent.LandBiome("grass") };
        var resources = new[] { new Resource("wood", new RgbColor(140, 90, 40), StartAmount: 100, BaseStorage: 1000) };
        var content = TestContent.Build(biomes, 1, resources);
        return (content, new Simulation(content, new UniformTerrain(1), seed));
    }

    private static void RunTicks(Simulation sim, int ticks)
    {
        for (int i = 0; i < ticks; i++)
        {
            sim.Tick();
        }
    }

    /// <summary>Tři boudy blízko sebe — nejmenší platná osada (minBuildings 3).</summary>
    private static void PlaceTriple(Simulation sim, int x, int y)
    {
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuilding(0, x, y));
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuilding(0, x + 2, y));
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuilding(0, x, y + 2));
    }

    [Fact]
    public void Cluster_BecomesNamedSettlement()
    {
        var (content, sim) = HutWorld();
        PlaceTriple(sim, 5, 5);

        RunTicks(sim, UpdateInterval);

        var settlement = Assert.Single(sim.Settlements);
        Assert.Equal(3, settlement.BuildingCount);
        Assert.InRange(settlement.NameIndex, 0, content.SettlementNames.Count - 1);
        Assert.InRange(settlement.CenterX, 5f, 8f);
        Assert.InRange(settlement.CenterY, 5f, 8f);
    }

    [Fact]
    public void TooFewBuildings_NoSettlement()
    {
        var (_, sim) = HutWorld();
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuilding(0, 5, 5));
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuilding(0, 7, 5));

        RunTicks(sim, UpdateInterval);

        Assert.Empty(sim.Settlements);
    }

    [Fact]
    public void SeparatedClusters_AreSeparateSettlements()
    {
        var (_, sim) = HutWorld();
        PlaceTriple(sim, 3, 3);
        PlaceTriple(sim, 20, 20);

        RunTicks(sim, UpdateInterval);

        Assert.Equal(2, sim.Settlements.Count);
    }

    [Fact]
    public void Name_IsStableWhileSettlementGrows()
    {
        var (_, sim) = HutWorld();
        PlaceTriple(sim, 5, 5);
        RunTicks(sim, UpdateInterval);
        int originalName = sim.Settlements[0].NameIndex;

        // Dorůstání osady jméno nemění — určuje ho nejstarší budova.
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuilding(0, 7, 7));
        RunTicks(sim, UpdateInterval);

        var grown = Assert.Single(sim.Settlements);
        Assert.Equal(4, grown.BuildingCount);
        Assert.Equal(originalName, grown.NameIndex);
    }

    [Fact]
    public void SameSeed_SameName()
    {
        var (_, simA) = HutWorld(seed: 42);
        var (_, simB) = HutWorld(seed: 42);
        PlaceTriple(simA, 5, 5);
        PlaceTriple(simB, 5, 5);

        RunTicks(simA, UpdateInterval);
        RunTicks(simB, UpdateInterval);

        Assert.Equal(simA.Settlements[0].NameIndex, simB.Settlements[0].NameIndex);
    }
}
