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

        // Půl dne tiků → čas se posune o 0.5.
        for (int i = 0; i < (int)(dayTicks / 2); i++)
        {
            sim.Tick();
        }

        Assert.Equal(0.82, sim.TimeOfDay01, precision: 6);
        Assert.Equal(1, sim.DayNumber);

        // Druhá půlka dne → přetečení přes půlnoc a nový den.
        for (int i = 0; i < (int)(dayTicks / 2); i++)
        {
            sim.Tick();
        }

        Assert.Equal(0.32, sim.TimeOfDay01, precision: 6);
        Assert.Equal(2, sim.DayNumber);
    }
}
