using CivDle.Core.Content;
using CivDle.Core.Sim;
using CivDle.Core.World;
using CivDle.Rendering;
using CivDle.Rendering.Effects;
using Microsoft.Xna.Framework;
using Xunit;
using Xunit.Abstractions;

namespace CivDle.Ui.Tests;

/// <summary>
/// Chodci, kteří někam jdou.
///
/// <para>Dřív se cíl losoval jako bod do deseti dlaždic kolem. Pořád se něco
/// hýbalo, takže to na první pohled vypadalo živě — jenže <b>nikdo nikdy nikam
/// nedošel</b> a město se ve tři ráno nelišilo od poledne. Testuje se proto to,
/// co se tím opravilo: že se dochází a že na denní době záleží.</para>
/// </summary>
public class AgentErrandTests
{
    private readonly ITestOutputHelper _out;

    public AgentErrandTests(ITestOutputHelper output) => _out = output;

    [Fact]
    public void PeopleActuallyGetWhereTheyAreGoing()
    {
        // Tohle je ta oprava v jednom tvrzení.
        var (agents, sim, camera) = Scene();

        Run(agents, sim, camera, seconds: 60);

        _out.WriteLine($"příchodů: {agents.ArrivalsForTests}");
        Assert.True(agents.ArrivalsForTests > 0, "za minutu nikdo nikam nedošel");
    }

    [Fact]
    public void TheStreetsAreQuieterAtNightThanAtNoon()
    {
        var noon = Busyness(0.5);
        var night = Busyness(0.02);

        _out.WriteLine($"poledne {noon}, noc {night}");
        Assert.True(night < noon, $"v noci je stejně rušno jako v poledne ({night} vs {noon})");
    }

    [Fact]
    public void TheCityIsNeverCompletelyDeserted()
    {
        // Vylidněná ulice vypadá jako vypnutá hra, ne jako spící město.
        Assert.True(Busyness(0.02) > 0, "v noci nezůstal venku vůbec nikdo");
    }

    [Fact]
    public void WalkingWearsTheGroundIntoPaths()
    {
        // Stezka je jediná stopa, kterou po sobě chodci nechají — bez ní vypadá
        // prázdné náměstí stejně jako to, kudy celý den chodí lidi.
        var (agents, sim, camera) = Scene();

        Run(agents, sim, camera, seconds: 30);

        Assert.True(agents.Footfall.Count > 0, "za půl minuty chození se neošlapala jediná dlaždice");
    }

    [Fact]
    public void PathsNeverFormOnTopOfRoads()
    {
        // Stezka vzniká tam, kudy se chodí NAVZDORY tomu, že tudy cesta nevede.
        // Ošlapávat dlažbu by znamenalo kreslit hlínu přes silnici.
        var (agents, sim, camera) = Scene();
        for (int x = 0; x < 10; x++)
        {
            sim.AddRoadTileForTest(x, 11);
        }

        Run(agents, sim, camera, seconds: 30);

        for (int x = 0; x < 10; x++)
        {
            Assert.Equal(0f, agents.Footfall.WearAt(x, 11));
        }
    }

    /// <summary>Kolik lidí je venku v danou denní dobu.</summary>
    private static int Busyness(double timeOfDay)
    {
        var (agents, sim, camera) = Scene();
        TickUntil(sim, timeOfDay);
        Run(agents, sim, camera, seconds: 30);
        return agents.CountForTests;
    }

    /// <summary>
    /// Odtiká simulaci na zadanou denní dobu. Čas je čistá funkce počtu tiků,
    /// takže se nedá nastavit — musí se k němu dojít.
    /// </summary>
    private static void TickUntil(Simulation sim, double timeOfDay)
    {
        for (int i = 0; i < 400_000; i++)
        {
            if (Math.Abs(sim.TimeOfDay01 - timeOfDay) < 0.02)
            {
                return;
            }

            sim.Tick();
        }

        Assert.Fail($"nepodařilo se dotikat na denní dobu {timeOfDay}");
    }

    private static void Run(AgentSystem agents, Simulation sim, Camera2D camera, double seconds)
    {
        const float step = 1f / 30f;
        for (int i = 0; i < seconds / step; i++)
        {
            agents.Update(step, camera, sim);
        }
    }

    private static (AgentSystem Agents, Simulation Sim, Camera2D Camera) Scene()
    {
        var content = new ContentLoader().LoadFrom(Path.Combine(AppContext.BaseDirectory, "data"));
        var sim = new Simulation(content, new UniformTerrain(content.Biomes.IndexOf("grassland")));
        sim.SkipTutorial();
        for (int i = 0; i < content.Techs.Count; i++)
        {
            sim.DebugGrantTech(i);
        }

        sim.DebugFillStorages();
        sim.SetAutoUpgradeLevel(0);
        sim.SetAutoMerge(false);

        // Domy i dílny: bez obojího by pochůzka neměla kam mířit a test by
        // ověřoval jen to, že se něco hýbe.
        int house = content.Buildings.IndexOf("house");
        int workshop = content.Buildings.IndexOf("lumber_camp");
        for (int y = 0; y < 10; y++)
        {
            for (int x = 0; x < 10; x++)
            {
                sim.TryPlaceBuildingFree((x + y) % 3 == 0 ? workshop : house, x, y);
            }
        }

        var camera = new Camera2D();
        camera.SetViewport(1280, 720);
        camera.CenterOn(new Vector2(5 * TerrainRenderer.TileSize, 5 * TerrainRenderer.TileSize), 2f);

        return (new AgentSystem(content), sim, camera);
    }
}
