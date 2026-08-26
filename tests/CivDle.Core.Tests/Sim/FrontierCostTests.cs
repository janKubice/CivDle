using System.Diagnostics;
using CivDle.Core.Content;
using CivDle.Core.Sim;
using CivDle.Core.Tests.Support;
using CivDle.Core.World;
using Xunit;
using Xunit.Abstractions;

namespace CivDle.Core.Tests.Sim;

/// <summary>
/// Kolik stojí zapnutý režim obrany, když se zrovna nebojuje.
///
/// <para>Podmínka z plánu zněla „vypnutý režim = nulový dopad na délku tiku".
/// Tenhle test měří i to druhé: zapnutý režim v klidu mezi vlnami nesmí být
/// řádově dražší, jinak by se hra při obraně zadrhla ještě před prvním
/// útočníkem.</para>
/// </summary>
public class FrontierCostTests
{
    private readonly ITestOutputHelper _out;

    public FrontierCostTests(ITestOutputHelper output) => _out = output;

    [Fact]
    public void DefenceModeDoesNotMakeQuietTicksExpensive()
    {
        var plain = Measure(defended: false);
        var defended = Measure(defended: true);

        _out.WriteLine($"vypnuto {plain} ms, zapnuto {defended} ms");

        Assert.True(
            defended < Math.Max(200, plain * 4),
            $"zapnutá obrana zdražila klidný tik z {plain} ms na {defended} ms");
    }

    private long Measure(bool defended)
    {
        var content = TestData.LoadRealContent();
        var sim = new Simulation(content, new UniformTerrain(content.Biomes.IndexOf("grassland")));
        sim.SkipTutorial();
        for (int i = 0; i < content.Techs.Count; i++)
        {
            sim.DebugGrantTech(i);
        }

        sim.DebugFillStorages();
        sim.SetAutoUpgradeLevel(0);
        sim.SetAutoMerge(false);

        if (defended)
        {
            sim.EnableFrontierDefense();
        }

        int house = content.Buildings.IndexOf("house");
        for (int y = 0; y < 22; y++)
        {
            for (int x = 0; x < 22; x++)
            {
                sim.TryPlaceBuildingFree(house, x, y);
            }
        }

        if (defended)
        {
            int tower = content.Buildings.IndexOf("watchtower");
            for (int i = 0; i < 16; i++)
            {
                sim.TryPlaceBuildingFree(tower, 30 + i * 2, 30);
            }
        }

        // Zahřátí, ať se neměří JIT.
        for (int i = 0; i < 50; i++)
        {
            sim.Tick();
        }

        var watch = Stopwatch.StartNew();
        for (int i = 0; i < 1000; i++)
        {
            sim.Tick();
        }

        return watch.ElapsedMilliseconds;
    }
}
