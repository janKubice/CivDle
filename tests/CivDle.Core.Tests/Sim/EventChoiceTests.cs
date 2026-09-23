using CivDle.Core.Content;
using CivDle.Core.Sim;
using CivDle.Core.Tests.Support;
using CivDle.Core.World;
using Xunit;

namespace CivDle.Core.Tests.Sim;

/// <summary>
/// Volba v události jde přes simulaci: zaplatí cenu, připíše zisk a spustí
/// dočasný dozvuk. Dozvuk je to, co z „nic nedělat" dělá rozhodnutí —
/// ignorovaná povodeň stojí úrodu, pozvaná karavana zrychlí růst.
/// </summary>
public class EventChoiceTests
{
    private const int Food = 0;
    private const int Stone = 1;

    private static Resource[] Resources() => new[]
    {
        new Resource("food", new RgbColor(200, 180, 60), StartAmount: 0, BaseStorage: 10_000),
        new Resource("stone", new RgbColor(128, 128, 128), StartAmount: 0, BaseStorage: 10_000),
    };

    /// <summary>Bez jídla i bez růstu — ať se měří jen výroba a volba.</summary>
    private static GameplayConfig Quiet => TestContent.DefaultGameplay with
    {
        PopulationGrowthPerSecond = 0.0,
        FoodPerPersonPerSecond = 0.0,
    };

    private static EventEffectDef Flood => new(EventEffectKind.Production, Food, 0.5, Seconds: 10);

    private static EventDef TestEvent => new("flood", new[]
    {
        new EventChoiceDef("event.flood.rebuild",
            Cost: new[] { new ResourceAmount(Stone, 20) },
            Gain: new[] { new ResourceAmount(Food, 30) }),
        new EventChoiceDef("event.flood.move_on",
            Cost: Array.Empty<ResourceAmount>(),
            Gain: Array.Empty<ResourceAmount>(),
            Effect: Flood),
        new EventChoiceDef("event.flood.celebrate",
            Cost: Array.Empty<ResourceAmount>(),
            Gain: Array.Empty<ResourceAmount>(),
            Effect: new EventEffectDef(EventEffectKind.Growth, -1, 1.5, Seconds: 10)),
    });

    private static Simulation Build(GameplayConfig? gameplay = null, params BuildingDef[] buildings)
    {
        var content = TestContent.Build(
            resources: Resources(), buildings: buildings.Length > 0 ? buildings : null,
            gameplay: gameplay ?? Quiet, events: new[] { TestEvent });
        var sim = new Simulation(content, new UniformTerrain((byte)1));
        for (int i = 0; i < buildings.Length; i++)
        {
            Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuildingFree(i, i * 2, 0));
        }

        return sim;
    }

    private static void Run(Simulation sim, int ticks)
    {
        for (int i = 0; i < ticks; i++)
        {
            sim.Tick();
        }
    }

    [Fact]
    public void Choosing_PaysTheCostAndCreditsTheGain()
    {
        var sim = Build();
        sim.DebugSetResource(Stone, 25);

        Assert.True(sim.TryChooseEventOption(0, 0));

        Assert.Equal(5, sim.GetResource(Stone), 6);
        Assert.Equal(30, sim.GetResource(Food), 6);
    }

    [Fact]
    public void AnUnaffordableChoice_DoesNothing()
    {
        var sim = Build();
        sim.DebugSetResource(Stone, 10);

        Assert.False(sim.CanChooseEventOption(0, 0));
        Assert.False(sim.TryChooseEventOption(0, 0));

        Assert.Equal(10, sim.GetResource(Stone), 6);
        Assert.Equal(0, sim.GetResource(Food), 6);
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(1, 0)]
    [InlineData(0, 3)]
    public void AChoiceThatDoesNotExist_IsRefused(int eventIndex, int choiceIndex)
    {
        // Obrazovka posílá indexy — rozbitý index nesmí shodit simulaci.
        var sim = Build();

        Assert.False(sim.TryChooseEventOption(eventIndex, choiceIndex));
    }

    [Fact]
    public void IgnoringTheFlood_SlowsFoodUntilTheEffectEnds()
    {
        var farm = TestContent.Producer("farm", Food, 1, timeTicks: 1);
        var sim = Build(null, farm);
        Run(sim, 10);
        double before = sim.GetResource(Food);
        Run(sim, 10);
        double normal = sim.GetResource(Food) - before;

        Assert.True(sim.TryChooseEventOption(0, 1));
        before = sim.GetResource(Food);
        Run(sim, 10);
        double flooded = sim.GetResource(Food) - before;

        Assert.True(normal > 0, "bez výroby test nic neměří");
        Assert.Equal(normal * 0.5, flooded, 6);
    }

    [Fact]
    public void TheEffectExpiresOnTime()
    {
        var sim = Build();
        Assert.True(sim.TryChooseEventOption(0, 1));
        long ticks = (long)(Flood.Seconds * Simulation.TicksPerSecond);

        Run(sim, (int)ticks - 1);
        Assert.Single(sim.EventEffects.Active);
        Assert.Equal(0.5, sim.EventEffects.ProductionMult(Food));

        Run(sim, 1);
        Assert.Empty(sim.EventEffects.Active);
        Assert.Equal(1.0, sim.EventEffects.ProductionMult(Food));
    }

    [Fact]
    public void AProductionEffect_TouchesOnlyItsResource()
    {
        var sim = Build();
        Assert.True(sim.TryChooseEventOption(0, 1));

        Assert.Equal(0.5, sim.EventEffects.ProductionMult(Food));
        Assert.Equal(1.0, sim.EventEffects.ProductionMult(Stone));
        Assert.Equal(1.0, sim.EventEffects.GrowthMult);
    }

    [Fact]
    public void TwoEffects_Stack()
    {
        var sim = Build();
        Assert.True(sim.TryChooseEventOption(0, 1));
        Assert.True(sim.TryChooseEventOption(0, 1));

        Assert.Equal(0.25, sim.EventEffects.ProductionMult(Food), 9);
    }

    [Fact]
    public void AGrowthEffect_SpeedsUpGrowth()
    {
        var growing = Quiet with { PopulationGrowthPerSecond = 0.5, BaseHousingCapacity = 1000 };
        var normal = Build(growing);
        var invited = Build(growing);
        Assert.True(invited.TryChooseEventOption(0, 2));

        Run(normal, 50);
        Run(invited, 50);

        double normalGrowth = normal.Population - growing.StartingPopulation;
        double invitedGrowth = invited.Population - growing.StartingPopulation;
        Assert.True(normalGrowth > 0, "bez růstu test nic neměří");
        Assert.Equal(normalGrowth * 1.5, invitedGrowth, 6);
    }

    [Fact]
    public void Ascension_ClearsRunningEffects()
    {
        // Povodeň patřila starému městu — nový běh s ní nezačíná.
        var sim = Build();
        Assert.True(sim.TryChooseEventOption(0, 1));

        sim.SetPopulationForTest(TestContent.DefaultPrestige.Requirement.Target);
        Assert.Equal(PlacementResult.Ok, sim.TryAscend());

        Assert.Empty(sim.EventEffects.Active);
        Assert.Equal(1.0, sim.EventEffects.ProductionMult(Food));
    }
}
