using CivDle.Core.Sim;
using CivDle.Core.Tests.Support;
using CivDle.Core.World;
using Xunit;

namespace CivDle.Core.Tests.Sim;

/// <summary>
/// Denní čas (fáze 5): čistě odvozený z tiků — deterministický, přežívá save/load
/// zadarmo (ukládá se jen TickCount). Skutečná data: den = 240 s = 2400 tiků,
/// start 0.32.
/// </summary>
public class DayNightTests
{
    private static Simulation NewSim(out CivDle.Core.Content.GameContent content)
    {
        content = TestData.LoadRealContent();
        var terrain = new UniformTerrain(content.Biomes.IndexOf("grassland"));
        return new Simulation(content, terrain);
    }

    [Fact]
    public void TimeOfDay_StartsAtConfiguredValue()
    {
        var sim = NewSim(out var content);

        Assert.Equal(content.Gameplay.DayNight.StartTimeOfDay, sim.TimeOfDay01, precision: 9);
        Assert.Equal(1, sim.DayNumber);
    }

    [Fact]
    public void TimeOfDay_AdvancesWithTicksAndWraps()
    {
        var sim = NewSim(out var content);
        double dayTicks = Simulation.TicksPerSecond * content.Gameplay.DayNight.DayLengthSeconds;

        // Za pomalým prvním dnem (úvod do hry) běží čas normálně: od jeho konce
        // se měří dál.
        var onboarding = content.Gameplay.Onboarding;
        long slowTicks = (long)(onboarding.FirstDaySeconds * Simulation.TicksPerSecond);
        for (long i = 0; i < slowTicks; i++)
        {
            sim.Tick();
        }

        double duskStart = onboarding.HasSlowFirstDay ? onboarding.FirstDayUntil : content.Gameplay.DayNight.StartTimeOfDay;
        Assert.Equal(duskStart, sim.TimeOfDay01, precision: 6);

        // Půl dne tiků → čas se posune o 0.5 a přeteče přes půlnoc do dalšího dne.
        for (int i = 0; i < (int)(dayTicks / 2); i++)
        {
            sim.Tick();
        }

        Assert.Equal(duskStart + 0.5 - 1.0, sim.TimeOfDay01, precision: 6);
        Assert.Equal(2, sim.DayNumber);

        // Druhá půlka → zase tentýž čas, pořád druhý den.
        for (int i = 0; i < (int)(dayTicks / 2); i++)
        {
            sim.Tick();
        }

        Assert.Equal(duskStart, sim.TimeOfDay01, precision: 6);
        Assert.Equal(2, sim.DayNumber);
    }
}
