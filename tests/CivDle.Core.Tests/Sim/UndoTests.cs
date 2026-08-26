using CivDle.Core.Content;
using CivDle.Core.Sim;
using CivDle.Core.Tests.Support;
using CivDle.Core.World;
using Xunit;

namespace CivDle.Core.Tests.Sim;

/// <summary>
/// Vrácení poslední hráčovy akce.
///
/// <para>Nejdůležitější test je ten, kde se svět pod akcí změnil: guvernér
/// staví dál, zatímco hráč přemýšlí, a „vrátit" budovu, která už není ta, co
/// postavil, by znamenalo zbourat cizí. Vrácení se tam musí <b>odmítnout</b>,
/// ne uhodnout.</para>
///
/// <para>Druhý v pořadí je ten o surovinách: vrácení bourání musí vzít
/// zpátky přesně tolik, kolik bourání vrátilo — jinak by z undo byl mlýnek
/// na suroviny.</para>
/// </summary>
public class UndoTests
{
    [Fact]
    public void NothingToUndoInAFreshWorld()
    {
        var (sim, _) = World();

        Assert.False(sim.Undo.CanUndo);
        Assert.Null(sim.UndoPreview);
        Assert.Equal(UndoResult.Empty, sim.TryUndo());
    }

    [Fact]
    public void UndoingABuildTakesItDownAndGivesTheCostBack()
    {
        var (sim, _) = World();
        double before = sim.GetResource(0);

        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuilding(0, 4, 4));
        Assert.True(sim.GetResource(0) < before);
        Assert.Equal(1, sim.Buildings.Length);

        Assert.Equal(UndoResult.Ok, sim.TryUndo());

        Assert.Equal(0, sim.Buildings.Length);
        Assert.False(sim.Undo.CanUndo);
    }

    [Fact]
    public void UndoingADemolishBringsTheBuildingBackAndTakesTheRefund()
    {
        var (sim, _) = World();
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuilding(0, 4, 4));
        double afterBuilding = sim.GetResource(0);

        Assert.Equal(PlacementResult.Ok, sim.TryDemolish(0));
        Assert.True(sim.GetResource(0) > afterBuilding, "bourání nic nevrátilo");

        Assert.Equal(UndoResult.Ok, sim.TryUndo());

        Assert.Equal(1, sim.Buildings.Length);
        Assert.Equal(afterBuilding, sim.GetResource(0), 6); // vrácená půlka šla zpátky
    }

    [Fact]
    public void UndoIsRefusedWhenSomethingElseStandsThereNow()
    {
        // Přesně ta situace, kvůli které se undo v simulaci nedělá naslepo.
        var (sim, _) = World();
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuilding(0, 4, 4));
        Assert.Equal(PlacementResult.Ok, sim.TryDemolish(0));

        // Zásobník teď navrchu nese bourání; na místo mezitím postavil někdo jiný.
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuildingFree(0, 4, 4));

        Assert.Equal(UndoResult.WorldChanged, sim.TryUndo());
    }

    [Fact]
    public void ARefusedUndoStaysOnTheStack()
    {
        // Odmítnutí není konec: až hráč uklidí, co překáží, musí to jít zkusit
        // znovu. Kdyby akce ze zásobníku zmizela, přišel by o ni.
        var (sim, _) = World();
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuilding(0, 4, 4));
        Assert.Equal(PlacementResult.Ok, sim.TryDemolish(0));
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuildingFree(0, 4, 4));

        Assert.Equal(UndoResult.WorldChanged, sim.TryUndo());
        Assert.True(sim.Undo.CanUndo);

        // Překážka pryč → vrácení projde.
        Assert.Equal(PlacementResult.Ok, sim.TryDemolish(0));
        Assert.Equal(UndoResult.Ok, sim.TryUndo()); // vrátí to poslední bourání
    }

    [Fact]
    public void RoadsGoBothWays()
    {
        var (sim, _) = World();
        Assert.Equal(PlacementResult.Ok, sim.TryBuildRoad(3, 3));
        Assert.True(sim.IsRoad(3, 3));

        Assert.Equal(UndoResult.Ok, sim.TryUndo());
        Assert.False(sim.IsRoad(3, 3));

        Assert.Equal(PlacementResult.Ok, sim.TryBuildRoad(3, 3));
        Assert.True(sim.TryRemoveRoad(3, 3));
        Assert.Equal(UndoResult.Ok, sim.TryUndo());
        Assert.True(sim.IsRoad(3, 3));
    }

    [Fact]
    public void UndoingDoesNotItselfBecomeSomethingToUndo()
    {
        // Kdyby se vrácení zapsalo do zásobníku, další „zpět" by ho zopakovalo
        // dokola a hráč by z toho nevyšel.
        var (sim, _) = World();
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuilding(0, 4, 4));
        Assert.Equal(1, sim.Undo.Count);

        Assert.Equal(UndoResult.Ok, sim.TryUndo());

        Assert.Equal(0, sim.Undo.Count);
    }

    [Fact]
    public void TheGovernorsBuildingsAreNotUndoable()
    {
        // Guvernér staví sám; vzít mu stavbu pod rukama by bylo horší než undo
        // nemít vůbec.
        var (sim, _) = World();
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuildingFree(0, 8, 8));

        Assert.False(sim.Undo.CanUndo);
    }

    [Fact]
    public void OnlyTheLastTwentyActionsAreRemembered()
    {
        var (sim, _) = World();
        for (int i = 0; i < UndoStack.Capacity + 10; i++)
        {
            Assert.Equal(PlacementResult.Ok, sim.TryBuildRoad(i, 0));
        }

        Assert.Equal(UndoStack.Capacity, sim.Undo.Count);

        // Navrchu je ta úplně poslední.
        Assert.Equal(UndoStack.Capacity + 9, sim.UndoPreview!.Value.X);
    }

    [Fact]
    public void AscendingForgetsEverything()
    {
        var content = TestContent.Build(
            biomes: new[] { TestContent.WaterBiome(), TestContent.LandBiome("grass") },
            prestige: new PrestigeConfig(
                new GoalCondition(MetricKind.TotalBuildings, -1, 1), MetricKind.Population, -1, 1));

        var sim = new Simulation(content, new UniformTerrain(1), seed: 1);
        sim.DebugFillStorages();
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuilding(0, 4, 4));
        Assert.True(sim.Undo.CanUndo);

        Assert.Equal(PlacementResult.Ok, sim.TryAscend());

        Assert.False(sim.Undo.CanUndo);
    }

    private static (Simulation Sim, GameContent Content) World()
    {
        var biomes = new[] { TestContent.WaterBiome(), TestContent.LandBiome("grass") };

        // Cena deset, ne jedna: bourání vrací půlku a z jedničky by po
        // zaokrouhlení dolů nezbylo nic, takže by test o vracení surovin
        // neměřil vůbec nic.
        var hut = TestContent.SimpleBuilding("hut", biomes.Length) with
        {
            BuildCost = new[] { new ResourceAmount(0, 10) },
        };

        var content = TestContent.Build(biomes: biomes, buildings: new[] { hut });
        var sim = new Simulation(content, new UniformTerrain(1), seed: 1);
        sim.DebugFillStorages();
        return (sim, content);
    }
}
