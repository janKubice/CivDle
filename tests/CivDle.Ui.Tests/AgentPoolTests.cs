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
/// Chodci a postávající obyvatelé — hlídá se pool, ne vzhled.
///
/// <para>Chyba, které se u téhle vrstvy dělá, je vždycky tatáž: agent se
/// z nějaké větve nevrátí do poolu, počet se vyšplhá na strop a od té chvíle
/// se nikdo nový neobjeví. Město pak po deseti minutách hraní ztuhne — a nikdo
/// to nespojí s tím, že přibyla nová postavička.</para>
///
/// <para>Testuje se bez grafiky: chování agentů s kreslením nesouvisí a přes
/// grafickou kartu by se netestovalo vůbec.</para>
/// </summary>
public class AgentPoolTests
{
    private readonly ITestOutputHelper _out;

    public AgentPoolTests(ITestOutputHelper output) => _out = output;

    [Fact]
    public void AgentsAppearInALivedInCity()
    {
        var (agents, sim, camera) = Scene();

        Run(agents, sim, camera, seconds: 20);

        Assert.True(agents.CountForTests > 0, "ve městě o stovce budov se za dvacet vteřin neobjevil nikdo");
    }

    [Fact]
    public void ThePoolDoesNotFillUpAndStayFull()
    {
        // Šedesát vteřin s pohybující se kamerou — přesně ten scénář, ve kterém
        // se zapomenuté vracení do poolu projeví.
        var (agents, sim, camera) = Scene();

        Run(agents, sim, camera, seconds: 20);
        int early = agents.CountForTests;

        // Kamera krouží NAD městem, ne pryč od něj: kdyby odjela, agenti by
        // zmizeli sami a test by neověřil nic. Takhle se pořád objevují noví
        // a staří musí odcházet — přesně ta situace, ve které se zapomenuté
        // vracení do poolu projeví.
        var center = camera.Position;
        for (int step = 0; step < 40; step++)
        {
            double angle = Math.Tau * step / 8;
            camera.Position = center + new Vector2(
                (float)Math.Cos(angle) * TerrainRenderer.TileSize * 4,
                (float)Math.Sin(angle) * TerrainRenderer.TileSize * 4);
            Run(agents, sim, camera, seconds: 1);
        }

        int late = agents.CountForTests;
        _out.WriteLine($"po 20 s: {early}, po popojíždění kamerou: {late}");

        Assert.True(late <= AgentSystemLimits.MaxAgents, $"pool přetekl na {late}");
        Assert.True(
            late < AgentSystemLimits.MaxAgents,
            "pool je plný na doraz — vypadá to, že se agenti nevracejí");
        Assert.True(late > 0, "po krouživém pohybu nad městem by tam někdo být měl");
    }

    [Fact]
    public void ZoomingOutClearsThemOut()
    {
        // Při oddálení se chodci nekreslí, takže je nemá smysl ani držet.
        var (agents, sim, camera) = Scene();
        Run(agents, sim, camera, seconds: 10);
        Assert.True(agents.CountForTests > 0);

        camera.SetCaptureZoom(0.1f);
        agents.Update(0.1f, camera, sim);

        Assert.Equal(0, agents.CountForTests);
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

        int house = content.Buildings.IndexOf("house");
        for (int y = 0; y < 10; y++)
        {
            for (int x = 0; x < 10; x++)
            {
                sim.TryPlaceBuildingFree(house, x, y);
            }
        }

        var camera = new Camera2D();
        camera.SetViewport(1280, 720);
        camera.CenterOn(new Vector2(5 * TerrainRenderer.TileSize, 5 * TerrainRenderer.TileSize), 2f);

        return (new AgentSystem(content), sim, camera);
    }
}
