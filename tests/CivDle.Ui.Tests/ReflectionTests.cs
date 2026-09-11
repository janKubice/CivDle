using CivDle.Core.Content;
using CivDle.Core.Sim;
using CivDle.Core.World;
using CivDle.Rendering;
using Xunit;

namespace CivDle.Ui.Tests;

/// <summary>
/// Odrazy budov na hladině.
///
/// <para>Břeh byl dosud čára, na které město končilo. Voda vedle přístavu
/// nevěděla, že tam přístav je — vypadala stejně jako voda uprostřed oceánu.
/// Odraz je to jediné, co hladinu spojí s tím, co nad ní stojí, a zároveň
/// nejlevnější způsob, jak z modré plochy udělat vodu.</para>
///
/// <para>Testuje se rozhodnutí „odráží se to?", protože je to jediná netriviální
/// část efektu a jde ověřit bez grafické karty. Kdyby vyšlo příliš štědře,
/// visely by odrazy pod domy uprostřed pevniny.</para>
/// </summary>
public class ReflectionTests
{
    [Fact]
    public void ABuildingOnTheShoreReflects()
    {
        var sim = NewWorld();
        var (x, y) = FindShore(sim);

        Assert.True(BuildingRenderer.ReflectsOnWater(sim, x, y, width: 1, height: 1));
    }

    [Fact]
    public void ABuildingInlandDoesNot()
    {
        // Odraz pod domem uprostřed pevniny je ta nejnápadnější možná chyba.
        var sim = NewWorld();
        var (x, y) = FindInland(sim);

        Assert.False(BuildingRenderer.ReflectsOnWater(sim, x, y, width: 1, height: 1));
    }

    [Fact]
    public void OnlyTheBottomEdgeCounts()
    {
        // Hladina se odráží směrem k divákovi, tedy dolů po obrazovce. Voda
        // nad budovou nebo vedle ní odraz nedělá — a hlavně by to stálo čtyři
        // dotazy na dlaždici u každé budovy ve výřezu.
        var sim = NewWorld();
        var (x, y) = FindNorthShore(sim);

        Assert.False(BuildingRenderer.ReflectsOnWater(sim, x, y, width: 1, height: 1));
    }

    [Fact]
    public void AWideBuildingReflectsWhenAnyOfItsEdgeTouchesWater()
    {
        // Přístav je široký a vody se dotýká jen rohem. Kdyby se zkoumal jen
        // střed, největší stavby u vody by odraz neměly.
        var sim = NewWorld();
        var (x, y) = FindShore(sim);

        Assert.True(BuildingRenderer.ReflectsOnWater(sim, x - 2, y, width: 3, height: 1));
    }

    /// <summary>Dlaždice souše, pod kterou je voda.</summary>
    private static (int X, int Y) FindShore(Simulation sim)
    {
        for (int y = -120; y < 120; y++)
        {
            for (int x = -120; x < 120; x++)
            {
                if (!sim.IsWaterAt(x, y) && sim.IsWaterAt(x, y + 1) && !sim.IsWaterAt(x - 2, y))
                {
                    return (x, y);
                }
            }
        }

        throw new Xunit.Sdk.XunitException("v testovacím světě se nenašel břeh");
    }

    /// <summary>Dlaždice souše, nad kterou je voda a pod kterou souš.</summary>
    private static (int X, int Y) FindNorthShore(Simulation sim)
    {
        for (int y = -120; y < 120; y++)
        {
            for (int x = -120; x < 120; x++)
            {
                if (!sim.IsWaterAt(x, y) && sim.IsWaterAt(x, y - 1) && !sim.IsWaterAt(x, y + 1))
                {
                    return (x, y);
                }
            }
        }

        throw new Xunit.Sdk.XunitException("v testovacím světě se nenašel severní břeh");
    }

    /// <summary>Dlaždice souše, kolem které voda není.</summary>
    private static (int X, int Y) FindInland(Simulation sim)
    {
        for (int y = -120; y < 120; y++)
        {
            for (int x = -120; x < 120; x++)
            {
                if (!sim.IsWaterAt(x, y) && !sim.IsWaterAt(x, y + 1) && !sim.IsWaterAt(x, y + 2))
                {
                    return (x, y);
                }
            }
        }

        throw new Xunit.Sdk.XunitException("v testovacím světě se nenašla souš");
    }

    private static Simulation NewWorld()
    {
        var content = new ContentLoader().LoadFrom(Path.Combine(AppContext.BaseDirectory, "data"));
        var preset = content.WorldGen.Presets[content.WorldGen.DefaultPresetIndex];
        var sim = new Simulation(content, new ProceduralTerrain(content.Biomes, preset, 20260728), 20260728);
        sim.SkipTutorial();
        return sim;
    }
}
