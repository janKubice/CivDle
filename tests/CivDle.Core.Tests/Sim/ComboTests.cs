using CivDle.Core.Content;
using CivDle.Core.Sim;
using CivDle.Core.Tests.Support;
using CivDle.Core.World;
using Xunit;

namespace CivDle.Core.Tests.Sim;

/// <summary>
/// Klikací kombo: série rychlých sběrů zvedá výnos.
///
/// <para>Krit je náhoda, na kterou se čeká; kombo je něco, za co může hráč sám —
/// a to je v idle hře jediný důvod klikat dobrovolně. Série se počítá z tiků,
/// takže je deterministická a dá se otestovat bez měření reálného času.</para>
/// </summary>
public class ComboTests
{
    private const int Wood = 0;

    private static GameContent Content(ComboConfig? combo = null)
    {
        var biomes = new[]
        {
            TestContent.WaterBiome(),
            TestContent.LandBiome("grass") with { ClickYield = new ClickYield(Wood, 100) },
        };
        var resources = new[] { new Resource("wood", new RgbColor(1, 1, 1), StartAmount: 0, BaseStorage: 10_000_000) };
        var gameplay = TestContent.DefaultGameplay with
        {
            FoodPerPersonPerSecond = 0,
            PopulationGrowthPerSecond = 0,
            ComboOrNull = combo ?? new ComboConfig(WindowSeconds: 1.0, BonusPerStep: 0.1, MaxSteps: 5),
        };
        return TestContent.Build(biomes, 1, resources, gameplay: gameplay);
    }

    private static Simulation NewSim(ComboConfig? combo = null) =>
        new(Content(combo), new UniformTerrain(1));

    /// <summary>Sebere na dané dlaždici a vrátí výnos.</summary>
    private static int Harvest(Simulation sim, int x, int y)
    {
        Assert.True(sim.TryHarvest(x, y, out _, out int amount));
        return amount;
    }

    [Fact]
    public void FastClicksInARow_YieldMore()
    {
        var sim = NewSim();
        int first = Harvest(sim, 1, 1);
        int second = Harvest(sim, 2, 1);
        int third = Harvest(sim, 3, 1);

        Assert.True(second > first, $"druhý klik v sérii má nést víc ({second} vs {first})");
        Assert.True(third > second, $"třetí ještě víc ({third} vs {second})");
    }

    [Fact]
    public void SlowClicks_StartOver()
    {
        var sim = NewSim(new ComboConfig(WindowSeconds: 1.0, BonusPerStep: 0.1, MaxSteps: 5));
        int first = Harvest(sim, 1, 1);
        Harvest(sim, 2, 1);
        Harvest(sim, 3, 1);
        Assert.True(sim.ComboStreak > 1);

        // Pauza delší než okno série — kombo zhasne.
        for (int i = 0; i < (int)(Simulation.TicksPerSecond * 2); i++)
        {
            sim.Tick();
        }

        Assert.Equal(0, sim.ComboStreak);
        Assert.Equal(first, Harvest(sim, 4, 1));
    }

    [Fact]
    public void ComboStopsAtItsCap()
    {
        // Bez stropu by šlo klikáním donekonečna škálovat cokoli.
        var sim = NewSim(new ComboConfig(WindowSeconds: 5.0, BonusPerStep: 0.1, MaxSteps: 3));
        for (int i = 0; i < 20; i++)
        {
            Harvest(sim, i, 1);
        }

        Assert.Equal(1.3, sim.ComboMultiplier, 6);
    }

    [Fact]
    public void ComboSurvivesAShortPauseWithinTheWindow()
    {
        var sim = NewSim(new ComboConfig(WindowSeconds: 2.0, BonusPerStep: 0.1, MaxSteps: 5));
        Harvest(sim, 1, 1);
        for (int i = 0; i < (int)(Simulation.TicksPerSecond * 1.0); i++)
        {
            sim.Tick();
        }

        Harvest(sim, 2, 1);

        Assert.Equal(2, sim.ComboStreak);
        Assert.True(sim.ComboSecondsLeft > 0);
    }

    [Fact]
    public void WithoutCombo_EveryClickIsTheSame()
    {
        // Starší data blok neuvádějí — klikání se pak chová přesně jako dřív.
        var sim = NewSim(ComboConfig.Disabled);
        int first = Harvest(sim, 1, 1);
        int second = Harvest(sim, 2, 1);

        Assert.Equal(first, second);
        Assert.Equal(0, sim.ComboStreak);
        Assert.Equal(1.0, sim.ComboMultiplier, 6);
    }

    [Fact]
    public void RealContent_RewardsClickingButNotAbsurdly()
    {
        var combo = TestData.LoadRealContent().Gameplay.Combo;

        Assert.True(combo.IsEnabled, "kombo má být ve skutečných datech zapnuté");

        // Strop drží kombo v roli koření, ne hlavní ekonomiky.
        Assert.InRange(combo.Multiplier(1000), 1.1, 3.0);
    }
}
