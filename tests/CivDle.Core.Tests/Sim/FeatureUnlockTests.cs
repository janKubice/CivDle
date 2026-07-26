using CivDle.Core.Content;
using CivDle.Core.Sim;
using CivDle.Core.Tests.Support;
using CivDle.Core.World;
using Xunit;

namespace CivDle.Core.Tests.Sim;

/// <summary>
/// Odemykatelné funkce: UI má odhalovat schopnosti postupně, ne vysypat na hráče
/// všechno naráz. Podmínka je sdílená GoalCondition (stejná jako u úkolů a Vzestupu).
/// </summary>
public class FeatureUnlockTests
{
    private static ITerrain Grass() => new UniformTerrain(1);

    private static GameContent FeatureContent() => TestContent.Build(
        features: new[]
        {
            new FeatureDef("plant", new GoalCondition(MetricKind.Population, -1, 8)),
            new FeatureDef("demolish", new GoalCondition(MetricKind.TotalBuildings, -1, 2)),
        });

    [Fact]
    public void Feature_IsLockedUntilConditionIsMet()
    {
        var sim = new Simulation(FeatureContent(), Grass()); // populace 5 na startu

        Assert.False(sim.IsFeatureUnlocked("plant"));
    }

    [Fact]
    public void Feature_UnlocksWhenConditionIsMet()
    {
        var sim = new Simulation(FeatureContent(), Grass());
        Assert.False(sim.IsFeatureUnlocked("demolish"));

        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuilding(0, 2, 2));
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuilding(0, 4, 4));

        Assert.True(sim.IsFeatureUnlocked("demolish"));
    }

    [Fact]
    public void UnknownFeature_IsTreatedAsUnlocked()
    {
        // Chybějící definice nesmí funkci „zamknout" navždy — jinak by chyba v datech
        // udělala hru nehratelnou.
        var sim = new Simulation(FeatureContent(), Grass());

        Assert.True(sim.IsFeatureUnlocked("nejaka_budouci_funkce"));
    }

    [Fact]
    public void UnlockedCount_TracksProgress()
    {
        var sim = new Simulation(FeatureContent(), Grass());
        int before = sim.UnlockedFeatureCount;

        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuilding(0, 2, 2));
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuilding(0, 4, 4));

        Assert.True(sim.UnlockedFeatureCount > before, "odemčení se má projevit v počtu");
    }

    [Fact]
    public void RealContent_HidesAdvancedFeaturesAtStart()
    {
        // Na startu nemá hráč vidět zóny, guvernéra ani Vzestup.
        var content = TestData.LoadRealContent();
        var sim = new Simulation(content, Grass());

        foreach (var id in new[] { "zones", "governor", "ascend", "demolish" })
        {
            Assert.False(sim.IsFeatureUnlocked(id), $"funkce '{id}' nemá být na startu dostupná");
        }
    }
}
