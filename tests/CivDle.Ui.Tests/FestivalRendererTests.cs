using CivDle.Core.Content;
using CivDle.Core.Sim;
using CivDle.Core.World;
using CivDle.Rendering;
using Microsoft.Xna.Framework;
using Xunit;

namespace CivDle.Ui.Tests;

/// <summary>
/// Výzdoba slavnosti: girlandy a stoupající lampiony.
///
/// <para>Hlídá se to, co u kulisy s časem opravdu selhává — <b>pool</b>.
/// Lampiony se pouštějí třikrát za sekundu a slavnost běží minuty; kdyby se
/// zapomněly vracet, rostl by jejich počet do nekonečna a s ním i snímky.
/// Druhá věc je, že mimo slavnost se nemá kreslit vůbec nic.</para>
/// </summary>
public class FestivalRendererTests
{
    [Fact]
    public void WithoutAFestivalNothingIsDrawn()
    {
        var (festival, sim, camera) = Scene();

        Run(festival, sim, camera, seconds: 5);

        Assert.False(festival.IsVisible);
        Assert.Equal(0, festival.ActiveLanterns);
    }

    [Fact]
    public void DuringALongFestivalTheLanternCountStaysBounded()
    {
        var (festival, sim, camera) = Scene();
        sim.GrantGoldenFestival();

        Run(festival, sim, camera, seconds: 60);

        Assert.True(festival.IsVisible);
        Assert.InRange(festival.ActiveLanterns, 1, FestivalRenderer.Capacity);
    }

    [Fact]
    public void AfterTheFestivalTheSkyEmptiesAgain()
    {
        var (festival, sim, camera) = Scene();
        sim.GrantGoldenFestival();
        Run(festival, sim, camera, seconds: 10);
        Assert.True(festival.ActiveLanterns > 0);

        // Doběh: slavnost skončí a lampiony ve vzduchu mají doletět, ne zmizet.
        while (sim.IsBoostActive)
        {
            sim.Tick();
        }

        Run(festival, sim, camera, seconds: 15);

        Assert.False(festival.IsVisible);
        Assert.Equal(0, festival.ActiveLanterns);
    }

    private static void Run(FestivalRenderer festival, Simulation sim, Camera2D camera, double seconds)
    {
        const float step = 1f / 30f;
        for (int i = 0; i < seconds / step; i++)
        {
            festival.Update(step, camera, sim);
        }
    }

    private static (FestivalRenderer Festival, Simulation Sim, Camera2D Camera) Scene()
    {
        var content = new ContentLoader().LoadFrom(Path.Combine(AppContext.BaseDirectory, "data"));
        var sim = new Simulation(content, new UniformTerrain(content.Biomes.IndexOf("grassland")));

        var camera = new Camera2D();
        camera.SetViewport(1280, 720);
        camera.CenterOn(Vector2.Zero, 2f);

        return (new FestivalRenderer(whitePixel: null!), sim, camera);
    }
}
