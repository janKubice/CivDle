using CivDle.Core.Content;
using CivDle.Core.Sim;
using CivDle.Core.Tests.Support;
using CivDle.Core.World;
using Xunit;

namespace CivDle.Core.Tests.Onboarding;

/// <summary>
/// První den trvá déle. Dřív se stmívalo v 1:36 a ve 2:10 byla noc — přesně
/// když lidé z dema odcházeli. Den se musí prodloužit plynule (bez skoku času)
/// a jen z tiků, ať načtená hra má tentýž čas.
/// </summary>
public class FirstDayTests
{
    private static Simulation Build(double firstDaySeconds)
    {
        var onboarding = OnboardingConfig.Disabled with { FirstDaySeconds = firstDaySeconds, FirstDayUntil = 0.72 };
        var gameplay = TestContent.DefaultGameplay with { OnboardingOrNull = onboarding };
        return new Simulation(TestContent.Build(gameplay: gameplay), new UniformTerrain(1));
    }

    private static void RunSeconds(Simulation sim, double seconds)
    {
        long ticks = (long)Math.Round(seconds * Simulation.TicksPerSecond);
        for (long i = 0; i < ticks; i++)
        {
            sim.Tick();
        }
    }

    [Fact]
    public void DuskComesAfterTheConfiguredFirstDay()
    {
        var sim = Build(firstDaySeconds: 270);
        Assert.Equal(0.32, sim.TimeOfDay01, 6); // ráno jako dřív

        RunSeconds(sim, 135);
        Assert.Equal(0.52, sim.TimeOfDay01, 6); // v půlce pomalého úseku

        RunSeconds(sim, 135);
        Assert.Equal(0.72, sim.TimeOfDay01, 6); // soumrak začíná až tady (4:30)
    }

    [Fact]
    public void AfterDuskTimeRunsAtNormalSpeed()
    {
        var sim = Build(firstDaySeconds: 270);
        RunSeconds(sim, 270);

        RunSeconds(sim, 24); // desetina dne o délce 240 s

        Assert.Equal(0.82, sim.TimeOfDay01, 6);
    }

    [Fact]
    public void TimeNeverJumps()
    {
        // Skok by na obrazovce vypadal jako bliknutí světla — a u čísla dne
        // by přeskočil volby i období.
        var sim = Build(firstDaySeconds: 270);
        double previous = sim.TimeOfDay01;
        long previousDay = sim.DayNumber;
        for (int i = 0; i < 6000; i++)
        {
            sim.Tick();
            double now = sim.TimeOfDay01;
            double step = now - previous;
            if (step < 0)
            {
                step += 1; // půlnoc
            }

            Assert.InRange(step, 0, 0.01);
            Assert.InRange(sim.DayNumber - previousDay, 0, 1);
            previous = now;
            previousDay = sim.DayNumber;
        }
    }

    [Fact]
    public void WithoutASlowFirstDayNothingChanges()
    {
        var sim = Build(firstDaySeconds: 0);

        RunSeconds(sim, 96); // 0,4 dne

        Assert.Equal(0.72, sim.TimeOfDay01, 6);
    }
}
