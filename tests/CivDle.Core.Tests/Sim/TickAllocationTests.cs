using CivDle.Core.Content;
using CivDle.Core.Sim;
using CivDle.Core.Tests.Support;
using CivDle.Core.World;
using Xunit;
using Xunit.Abstractions;

namespace CivDle.Core.Tests.Sim;

/// <summary>
/// Kolik tik alokuje.
///
/// <para>CLAUDE.md říká „žádné alokace v tikové smyčce nad hodně entitami".
/// Dokud se to neměří, je to zbožné přání: alokace se do tiku vloudí jedním
/// nevinným <c>new List&lt;&gt;()</c> a projeví se až jako záškub u hráče,
/// kterého to potká po hodině hraní.</para>
///
/// <para>Strop je schválně velkorysý — nejde o to zakázat každý bajt, ale
/// chytit řádový skok. Kdyby někdo přidal alokaci na budovu za tik, tenhle
/// test spadne dřív, než se to dostane do buildu.</para>
/// </summary>
public class TickAllocationTests
{
    /// <summary>Kolik bajtů na tik se ještě snese. Měřeno, ne odhadnuto.</summary>
    private const long BudgetPerTick = 2_048;

    private readonly ITestOutputHelper _out;

    public TickAllocationTests(ITestOutputHelper output) => _out = output;

    [Fact]
    public void TheTickLoopStaysWithinItsAllocationBudget()
    {
        var sim = GrownCity();

        // Zahřátí: první tiky roztahují pole a plní cache, to se neměří.
        for (int i = 0; i < 300; i++)
        {
            sim.Tick();
        }

        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1000; i++)
        {
            sim.Tick();
        }

        long perTick = (GC.GetAllocatedBytesForCurrentThread() - before) / 1000;
        _out.WriteLine($"{perTick} B/tik při {sim.Buildings.Length} budovách");

        Assert.True(
            perTick <= BudgetPerTick,
            $"tik alokuje {perTick} B, strop je {BudgetPerTick} B");
    }

    [Fact]
    public void AllocationDoesNotGrowWithTheCity()
    {
        // Tohle je ta skutečná past: alokace „na budovu za tik" vypadá při
        // deseti domech nevinně a při deseti tisících položí hru.
        long small = Measure(Grown(size: 10));
        long large = Measure(Grown(size: 30));

        _out.WriteLine($"malé město {small} B/tik, velké {large} B/tik");

        Assert.True(
            large <= Math.Max(BudgetPerTick, small * 3),
            $"devětkrát víc budov zvedlo alokaci z {small} B na {large} B — to roste s městem");
    }

    private static long Measure(Simulation sim)
    {
        for (int i = 0; i < 300; i++)
        {
            sim.Tick();
        }

        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 500; i++)
        {
            sim.Tick();
        }

        return (GC.GetAllocatedBytesForCurrentThread() - before) / 500;
    }

    private static Simulation GrownCity() => Grown(size: 30);

    private static Simulation Grown(int size)
    {
        var content = TestData.LoadRealContent();
        var sim = new Simulation(content, new UniformTerrain(content.Biomes.IndexOf("grassland")));
        sim.SkipTutorial();
        for (int i = 0; i < content.Techs.Count; i++)
        {
            sim.DebugGrantTech(i);
        }

        sim.DebugFillStorages();

        // Guvernér ať do toho nesahá: měří se tik, ne to, co zrovna postavil.
        sim.SetAutoUpgradeLevel(0);
        sim.SetAutoMerge(false);

        int house = content.Buildings.IndexOf("house");
        int farm = content.Buildings.IndexOf("farm");
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                sim.TryPlaceBuildingFree((x + y) % 4 == 0 ? farm : house, x, y);
            }
        }

        return sim;
    }
}
