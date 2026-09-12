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

    [Fact]
    public void ACrowdGathersWhereSomethingIsHappening()
    {
        // Bez sbíhajícího se davu je z události jen obrázek: stánky by stály na
        // návsi a lidi by chodili dál po svém, jako by se nic nedělo.
        //
        // Měří se až po zahřátí a proti vlastnímu klidovému stavu. Bez toho by
        // se do výsledku počítalo i to, že se pool teprve plní — čísla by
        // rostla sama od sebe a test by prošel, i kdyby událost nedělala nic.
        var (agents, sim, camera) = Scene(size: 20);
        var square = new Vector2(10 * TerrainRenderer.TileSize, 10 * TerrainRenderer.TileSize);

        Run(agents, sim, camera, seconds: WarmUpSeconds);
        double quiet = AverageNear(agents, sim, camera, square, MeasureSeconds);

        agents.Attraction = square;
        double during = AverageNear(agents, sim, camera, square, MeasureSeconds);

        _out.WriteLine($"klid {quiet:0.0}, během události {during:0.0}");
        Assert.True(during > quiet, $"na událost se nikdo nesešel ({quiet:0.0} → {during:0.0})");
    }

    [Fact]
    public void WhenTheEventEndsThePeopleGoBackToTheirOwnBusiness()
    {
        // Druhá půlka oblouku: trh skončí a dav se rozejde. Bez toho by se
        // u návsi jednou nashromáždili lidi a zůstali tam navždycky.
        var (agents, sim, camera) = Scene(size: 20);
        var square = new Vector2(10 * TerrainRenderer.TileSize, 10 * TerrainRenderer.TileSize);

        Run(agents, sim, camera, seconds: WarmUpSeconds);

        agents.Attraction = square;
        double during = AverageNear(agents, sim, camera, square, MeasureSeconds);

        agents.Attraction = null;
        // Kdo k trhu došel, ještě chvíli postojí — rozchod nezačne tím okamžikem,
        // kdy se stánky složí.
        Run(agents, sim, camera, seconds: 20);
        double after = AverageNear(agents, sim, camera, square, MeasureSeconds);

        _out.WriteLine($"během události {during:0.0}, po ní {after:0.0}");
        Assert.True(during > 1, "na trh se nikdo nesešel, takže se ani nemá kdo rozejít");
        Assert.True(after < during, $"po konci trhu se dav nerozešel ({during:0.0} → {after:0.0})");
    }

    /// <summary>Než se začne měřit: pool se musí naplnit, jinak se měří jeho růst.</summary>
    private const double WarmUpSeconds = 60;

    /// <summary>Jak dlouhý úsek se průměruje.</summary>
    private const double MeasureSeconds = 40;

    /// <summary>
    /// Průměrný počet lidí u místa za daný úsek. Jeden snímek by byl los —
    /// kdo kde zrovna stojí, se mezi snímky mění.
    /// </summary>
    private static double AverageNear(
        AgentSystem agents, Simulation sim, Camera2D camera, Vector2 place, double seconds)
    {
        const float step = 1f / 30f;
        long total = 0;
        int samples = 0;

        for (int i = 0; i < seconds / step; i++)
        {
            agents.Update(step, camera, sim);
            if (i % 15 == 0)
            {
                total += CountNear(agents, place);
                samples++;
            }
        }

        return samples == 0 ? 0 : total / (double)samples;
    }

    /// <summary>Kolik agentů stojí do tří dlaždic od místa.</summary>
    private static int CountNear(AgentSystem agents, Vector2 place)
    {
        float radius = 3f * TerrainRenderer.TileSize;
        return agents.PositionsForTests.Count(p => Vector2.Distance(p, place) < radius);
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

    private static (AgentSystem Agents, Simulation Sim, Camera2D Camera) Scene(int size = 10)
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
        // Uprostřed zůstane náves. Trh nevyroste na střeše domu, takže by test
        // na zastavěném čtverci měřil jen to, že se tam nedá dojít.
        int squareX = size / 2;
        int squareY = size / 2;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                if (Math.Abs(x - squareX) <= 2 && Math.Abs(y - squareY) <= 2)
                {
                    continue;
                }

                sim.TryPlaceBuildingFree((x + y) % 3 == 0 ? workshop : house, x, y);
            }
        }

        var camera = new Camera2D();
        camera.SetViewport(1280, 720);
        camera.CenterOn(
            new Vector2(size * 0.5f * TerrainRenderer.TileSize, size * 0.5f * TerrainRenderer.TileSize),
            2f);

        return (new AgentSystem(content), sim, camera);
    }
}
