using CivDle.Core.Save;
using CivDle.Core.Sim;
using CivDle.Screens;
using Microsoft.Xna.Framework;

namespace CivDle.Capture;

/// <summary>
/// Projede herní obrazovku všemi nástroji a zkusí je použít — bez okna a bez
/// člověka u myši.
///
/// <para>Existuje kvůli konkrétní bolesti: pády, které se objeví až po kliknutí
/// na nástroj, testy simulace nechytí (jsou v UI vrstvě) a ruční hraní je
/// nespolehlivé. Tohle projde všechny režimy za pár sekund a spadne stejně
/// hlasitě jako hra, takže je hned vidět, kde.</para>
///
/// <para>Spouští se přes <c>--smoke</c>. Vypíše, co prošlo; první výjimka
/// probublá ven a skončí v <c>crash.log</c> se stackem.</para>
/// </summary>
public sealed class SmokeRun
{
    private readonly List<string> _passed = new();

    /// <summary>Vytvoří scénu, na které má smysl nástroje zkoušet.</summary>
    public static Simulation BuildScene(ScreenManager screens) =>
        CityFixture.Grow(screens.Content, seed: 20260728, minutes: 6);

    /// <summary>
    /// Zapne postupně každý nástroj, nechá obrazovku pár snímků žít a zkusí
    /// příkazy, které nástroj provádí. Volá se z herní smyčky, aby měla Myra
    /// i grafika normální podmínky.
    /// </summary>
    public void Run(ScreenManager screens, GameplayScreen screen, Simulation sim, GameTime time)
    {
        Check("silnice: zapnout", () => screen.ActivateToolForSmoke(SmokeTool.Road));
        Frames(screen, time);
        Check("silnice: postavit", () => BuildRoadsAround(sim));

        Check("silnice: bourat", () => screen.ActivateToolForSmoke(SmokeTool.RoadErase));
        Frames(screen, time);
        Check("silnice: zbourat", () => RemoveRoadsAround(sim));

        Check("sloučení: zapnout", () => screen.ActivateToolForSmoke(SmokeTool.Merge));
        Frames(screen, time);
        Check("sloučení: sloučit", () => MergeAround(sim));

        Check("sázení: zapnout", () => screen.ActivateToolForSmoke(SmokeTool.Plant));
        Frames(screen, time);

        // Šablony: obrazovka i snímání. Pád tady hráč nahlásil hned po vydání
        // a smoke ho nechytil, protože tenhle nástroj vůbec neprocházel.
        Check("šablony: obrazovka", () => screen.OpenTemplatesForSmoke());
        Frames(screen, time);
        Check("šablony: zavřít", () => screens.Pop());
        Frames(screen, time);

        Check("šablony: snímat", () => screen.ActivateToolForSmoke(SmokeTool.TemplateCapture));
        Frames(screen, time);
        Check("šablony: sejmout a položit", () => CaptureAndPlaceTemplate(screens, sim));

        Check("nástroje: vypnout", () => screen.ActivateToolForSmoke(SmokeTool.None));
        Frames(screen, time);

        // Fotka i video: obojí kreslí do render targetu mimo obrazovku a obojí
        // umí spadnout způsobem, který se na obrazovce nikdy neprojeví.
        Check("fotka: uložit bez proužku ve vysokém rozlišení", () => PhotoRound(screens, sim));
        Check("video: vyrenderovat pár snímků", () => VideoRound(screens, sim));

        // Vzestup: nákup po dávkách staví obrazovku znovu po každé koupi.
        Check("vzestup: nákup po dávkách", () => AscensionRound(screens, sim, time));

        // Continue: ulož → načti → postav obrazovku nad načtenou simulací.
        // Přesně tahle cesta hráči spadla, a testy simulace ji nechytí — kříží
        // save vrstvu s UI vrstvou.
        Simulation? loaded = null;
        Check("save: uložit + načíst", () =>
        {
            var serializer = new SaveGameSerializer();
            using var stream = new MemoryStream();
            serializer.Write(stream, sim, new SaveMetadata(sim.Seed, "medium", "continents", DateTime.UtcNow));
            stream.Position = 0;
            (loaded, _) = serializer.Read(stream, screens.Content);
        });

        Check("save: obrazovka po Continue", () =>
        {
            // Dohon offline času se do smoke vejde celý — je to pár minut.
            // Hra ho pouští po dávkách přes načítací obrazovku; tady jde o to,
            // že obrazovka nad DOHNANOU simulací nespadne.
            var offline = OfflineProgress.Apply(loaded!, DateTime.UtcNow.AddMinutes(-3), DateTime.UtcNow);
            var continued = new GameplayScreen(
                screens, loaded!, new WorldInfo(sim.Seed, "medium", "continents"), offline);
            Frames(continued, time);
            continued.Dispose();
        });

        Console.WriteLine($"smoke OK ({_passed.Count} kroků): {string.Join(", ", _passed)}");
    }

    private void Check(string what, Action action)
    {
        action();
        _passed.Add(what);
    }

    private static void Frames(IScreen screen, GameTime time)
    {
        for (int i = 0; i < 3; i++)
        {
            screen.Update(time);
            screen.Draw(time);
        }
    }

    /// <summary>Otevře Vzestup s hromadou bodů a utratí je všemi násobiči.</summary>
    private static void AscensionRound(ScreenManager screens, Simulation sim, GameTime time)
    {
        sim.DebugGrantPrestigePoints(100_000);
        var ascension = new AscensionScreen(
            screens, sim, new WorldInfo(sim.Seed, "medium", "continents"));
        screens.Push(ascension);
        Frames(ascension, time);
        ascension.BuyEverythingForSmoke();
        Frames(ascension, time);
        screens.Pop();
    }

    /// <summary>Uloží fotku v jiném rozlišení a bez proužku — cesta, kterou hráč jede na store snímky.</summary>
    private static void PhotoRound(ScreenManager screens, Simulation sim)
    {
        var camera = new Rendering.Camera2D();
        camera.SetViewport(1920, 1080);
        camera.CenterOn(
            new Vector2(sim.CityCenterX * Rendering.TerrainRenderer.TileSize,
                        sim.CityCenterY * Rendering.TerrainRenderer.TileSize), 2f);

        string directory = Path.Combine(Path.GetTempPath(), "civdle-smoke-photo");
        var options = ShareCardOptions.For(
            CivDle.Core.Config.CaptureResolution.Hd1080, withStrip: false, fullDetail: true);

        new ShareCard(screens).Save(sim, camera, directory, options);
    }

    /// <summary>
    /// Vyrenderuje pár snímků videa. Schválně jen pár: jde o to, jestli projde
    /// render target, vzorkování jízdy a zápis PNG — ne o délku.
    /// </summary>
    private static void VideoRound(ScreenManager screens, Simulation sim)
    {
        var take = new CameraTake();
        var center = new Vector2(
            sim.CityCenterX * Rendering.TerrainRenderer.TileSize,
            sim.CityCenterY * Rendering.TerrainRenderer.TileSize);
        take.Record(0, center, 2f);
        take.Record(0.05, center + new Vector2(64, 32), 2.2f);

        string directory = Path.Combine(Path.GetTempPath(), "civdle-smoke-video");
        var options = ShareCardOptions.For(
            CivDle.Core.Config.CaptureResolution.Hd1080, withStrip: false, fullDetail: true);

        using var render = new VideoRender(screens, sim, take, options, directory);
        while (render.RenderNextFrame())
        {
        }
    }

    private static void BuildRoadsAround(Simulation sim)
    {
        for (int i = -20; i <= 20; i++)
        {
            sim.TryBuildRoad(sim.CityCenterX + i, sim.CityCenterY);
            sim.TryBuildRoad(sim.CityCenterX, sim.CityCenterY + i);
        }
    }

    private static void RemoveRoadsAround(Simulation sim)
    {
        for (int i = -20; i <= 20; i++)
        {
            sim.TryRemoveRoad(sim.CityCenterX + i, sim.CityCenterY);
            sim.TryRemoveRoad(sim.CityCenterX, sim.CityCenterY + i);
        }
    }

    /// <summary>Sejme kus města do šablony a hned ji zkusí položit jinam.</summary>
    private static void CaptureAndPlaceTemplate(ScreenManager screens, Simulation sim)
    {
        var template = TemplateTool.Capture(
            sim, screens.Content, "smoke",
            sim.CityCenterX - 4, sim.CityCenterY - 4, sim.CityCenterX + 4, sim.CityCenterY + 4);

        TemplateTool.CountPlaceable(sim, screens.Content, template, sim.CityCenterX + 40, sim.CityCenterY + 40);
        TemplateTool.Place(sim, screens.Content, template, sim.CityCenterX + 40, sim.CityCenterY + 40);
    }

    private static void MergeAround(Simulation sim)
    {
        for (int y = -14; y <= 14; y++)
        {
            for (int x = -14; x <= 14; x++)
            {
                sim.TryMerge(sim.CityCenterX + x, sim.CityCenterY + y);
            }
        }
    }
}

/// <summary>Nástroje, které smoke test prochází.</summary>
public enum SmokeTool
{
    None,
    Road,
    RoadErase,
    Merge,
    Plant,

    /// <summary>Snímání šablony zástavby (bod 44).</summary>
    TemplateCapture,
}
