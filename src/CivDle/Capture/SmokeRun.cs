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

        // Strom výzkumu: sto padesát uzlů a hledání nad nimi. Obrazovka sem
        // dřív vůbec nechodila, takže pád v ní by se projevil až u hráče.
        TechScreen? tech = null;
        // Stavební katalog má devadesát budov; hledání v něm musí projít
        // i tehdy, když dotaz nesedí na nic.
        Check("katalog: hledat", () =>
        {
            int hits = screen.SearchBuildMenuForSmoke("pil");
            int none = screen.SearchBuildMenuForSmoke("qwertzuiop");
            screen.SearchBuildMenuForSmoke(string.Empty);
            if (none != 0)
            {
                throw new InvalidOperationException($"nesmyslný dotaz vrátil {none} budov");
            }
        });
        Frames(screen, time);

        // Inspektor úzkých hrdel: projde všechny budovy a přebarví je, takže
        // pád v něm by přišel právě ve chvíli, kdy má hráč velké město.
        Check("inspektor: zapnout", screen.ShowBottlenecksForSmoke);
        Frames(screen, time);

        // Rozvod proudu se stejným zdůvodněním: legenda prochází všechny
        // budovy a obě legendy teď stojí v jednom stohu, takže se tu chytí
        // i rozbité rozvržení.
        Check("proud: zapnout", screen.ShowPowerForSmoke);
        Frames(screen, time);

        // Pauza: jediná obrazovka, přes kterou se ve hře ukládá — a smoke jí
        // dlouho vůbec neprocházel.
        Check("pauza: otevřít", () => screen.OpenPauseForSmoke());
        Frames(screen, time);
        Check("pauza: zavřít", () => screens.Pop());
        Frames(screen, time);

        // Výrobní řetězce: hledání přestavuje obrazovku po každém písmenu
        // a detail je druhá půlka, kterou smoke dosud neviděl vůbec.
        ChainsScreen? chains = null;
        Check("řetězce: obrazovka", () => chains = screen.OpenChainsForSmoke());
        Frames(screen, time);
        Check("řetězce: hledat", () =>
        {
            chains!.SearchForSmoke("prk");
            int none = chains.SearchForSmoke("qwertzuiop");
            chains.SearchForSmoke(string.Empty);
            if (none != 0)
            {
                throw new InvalidOperationException($"nesmyslný dotaz vrátil {none} surovin");
            }
        });
        Frames(screen, time);
        Check("řetězce: detail suroviny", () => chains!.ShowResourceForSmoke(0));
        Frames(screen, time);
        Check("řetězce: zavřít", () => screens.Pop());
        Frames(screen, time);

        Check("výzkum: obrazovka", () => tech = screen.OpenTechForSmoke());
        Frames(screen, time);
        Check("výzkum: hledat", () => tech!.SearchForSmoke("dre"));
        Frames(screen, time);
        Check("výzkum: hledat nesmysl", () => tech!.SearchForSmoke("qwertzuiop"));
        Frames(screen, time);
        Check("výzkum: zrušit hledání", () =>
        {
            tech!.SearchForSmoke(string.Empty);
            if (tech.SearchMatchCountForSmoke != screens.Content.Techs.Count)
            {
                throw new InvalidOperationException(
                    "po smazání dotazu se nevrátil celý strom "
                    + $"({tech.SearchMatchCountForSmoke} z {screens.Content.Techs.Count})");
            }
        });
        Check("výzkum: zavřít", () => screens.Pop());
        Frames(screen, time);

        Check("šablony: snímat", () => screen.ActivateToolForSmoke(SmokeTool.TemplateCapture));
        Frames(screen, time);
        Check("šablony: sejmout a položit", () => CaptureAndPlaceTemplate(screens, sim));

        // Podmoří: přístav otevře moře a na dně vyroste farma. Bez tohohle
        // kroku by se celá vrstva poprvé nakreslila až u hráče — a kreslí se
        // jinak než zástavba na souši (voda pod budovou, vlastní sprity).
        Check("podmoří: přístav a farma na dně", () => SubseaRound(screens, sim));
        Frames(screen, time);

        Check("orbita: vypustit a nakreslit", () => OrbitRound(screens, sim, time));
        Frames(screen, time);

        // Obrana: vlna útočníků na mapě. Kreslí se jinak než cokoli jiného
        // (agenti se zlomkovou polohou, proužky zdraví, šrafování na budovách)
        // a v běžné hře se ten kód nikdy nespustí.
        Check("obrana: vlna a věže", () => FrontierRound(screens, sim, screen, time));
        Frames(screen, time);

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

        // Úvod do hry: čerstvý svět bez jediné budovy. Nálet, táborák, šipka,
        // průvodcem vybraná budova a velké nápisy — všechno se kreslí jen
        // v prvních minutách nové hry, kam se smoke jinak nedostane.
        Check("úvod: nová hra, nálet a táborák", () => OnboardingRound(screens, time));

        Console.WriteLine($"smoke OK ({_passed.Count} kroků): {string.Join(", ", _passed)}");
    }

    private static void OnboardingRound(ScreenManager screens, GameTime time)
    {
        var content = screens.Content;
        long seed = content.Gameplay.Onboarding.QuickStartSeeds.Count > 0
            ? content.Gameplay.Onboarding.QuickStartSeeds[0]
            : 20260728;
        var preset = content.WorldGen.Presets[content.WorldGen.DefaultPresetIndex];
        var sim = new Simulation(content, new Core.World.ProceduralTerrain(content.Biomes, preset, seed), seed);
        var screen = new GameplayScreen(screens, sim, new WorldInfo(seed, "medium", preset.Id));
        try
        {
            screen.StartIntroForSmoke();
            Frames(screen, time);

            // Průvodce ukazuje na strom — sebrat z něj, jak by to udělal hráč.
            var guide = screen.GuideForSmoke;
            if (guide.Target.Kind != GuidePointer.Harvest)
            {
                throw new InvalidOperationException($"na startu průvodce neukazuje na strom, ale na {guide.Target.Kind}");
            }

            for (int i = 0; i < 20 && guide.Target.Kind == GuidePointer.Harvest; i++)
            {
                sim.TryHarvest(guide.Target.X, guide.Target.Y, out _, out _);
                for (int t = 0; t < 12; t++)
                {
                    sim.Tick(); // průvodce kontroluje krok jednou za sekundu
                }

                Frames(screen, time);
            }

            // Krok se stavbou: průvodce vybral budovu a vyznačil místo — postavit tam.
            if (guide.Target.Kind == GuidePointer.Build)
            {
                sim.TryPlaceBuilding(guide.Target.DefIndex, guide.Target.X, guide.Target.Y);
                Frames(screen, time);
            }

            foreach (var moment in Enum.GetValues<OnboardingMoment>())
            {
                screen.ShowMomentForSmoke(moment);
                Frames(screen, time);
            }
        }
        finally
        {
            screen.Dispose();
        }
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

    /// <summary>
    /// Uloží fotku v jiném rozlišení a bez proužku — cesta, kterou hráč jede na
    /// store snímky. Fotí se dvakrát: bez tilt-shiftu i s ním.
    ///
    /// <para>Ta druhá fotka tu je proto, že efekt jde přes vlastní render
    /// targety a přepínání cíle uprostřed kreslení. To je přesně ten druh věci,
    /// která projde překladačem a spadne až na cizí grafice — a jedině tady se
    /// to dá chytit dřív než u hráče. Obě fotky jdou do jiné složky: liší se
    /// jen jménem se sekundou a při dvou uloženích v téže sekundě by si
    /// přepsaly soubor.</para>
    /// </summary>
    private static void PhotoRound(ScreenManager screens, Simulation sim)
    {
        var camera = new Rendering.Camera2D();
        camera.SetViewport(1920, 1080);
        camera.CenterOn(
            new Vector2(sim.CityCenterX * Rendering.TerrainRenderer.TileSize,
                        sim.CityCenterY * Rendering.TerrainRenderer.TileSize), 2f);

        var card = new ShareCard(screens);

        card.Save(
            sim, camera, Path.Combine(Path.GetTempPath(), "civdle-smoke-photo"),
            ShareCardOptions.For(
                CivDle.Core.Config.CaptureResolution.Hd1080, withStrip: false, fullDetail: true));

        card.Save(
            sim, camera, Path.Combine(Path.GetTempPath(), "civdle-smoke-photo-tiltshift"),
            ShareCardOptions.For(
                CivDle.Core.Config.CaptureResolution.Hd1080, withStrip: true, fullDetail: true,
                tiltShift: true));
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

    /// <summary>
    /// Postaví u břehu přístav a hned za ním podmořskou farmu.
    ///
    /// <para>Když se u seedu smoke světa žádný břeh nenajde, krok se tiše
    /// přeskočí — smoke má hlídat pády, ne tvar generované mapy.</para>
    /// </summary>
    private static void SubseaRound(ScreenManager screens, Simulation sim)
    {
        var content = screens.Content;
        for (int i = 0; i < content.Techs.Count; i++)
        {
            sim.DebugGrantTech(i);
        }

        sim.DebugFillStorages();

        if (!CityFixture.TryFindShore(sim, out int shoreX, out int shoreY))
        {
            return;
        }

        int harbour = content.Buildings.IndexOf("harbor");
        int farm = content.Buildings.IndexOf("kelp_farm");

        // Přístav chce suchou dlaždici u vody, farma vodní v jeho dosahu —
        // obojí se hledá v okolí břehu, protože přesné souřadnice závisí na seedu.
        if (!TryPlaceNear(sim, harbour, shoreX, shoreY, wantWater: false))
        {
            Console.WriteLine($"podmoří: u břehu {shoreX},{shoreY} není místo pro přístav, přeskakuji");
            return;
        }

        int harbourX = sim.Buildings[^1].X, harbourY = sim.Buildings[^1].Y;

        // Farma se hledá od PŘÍSTAVU, ne od břehu: přístav mohl skončit dvacet
        // dlaždic vedle a dosah sítě se počítá od něj.
        if (!TryPlaceNear(sim, farm, harbourX, harbourY, wantWater: true))
        {
            Console.WriteLine($"podmoří: přístav na {harbourX},{harbourY}, ale farma se nikam nevešla");
            return;
        }

        Console.WriteLine(
            $"podmoří: přístav {harbourX},{harbourY}, síť pokrývá {sim.Subsea.CoveredTiles} dlaždic");

        // Fotka od přístavu, ne od centra města. Je to jediné místo, kde se
        // podmořské sprity a dosah sítě opravdu nakreslí — bez ní by se render
        // téhle vrstvy poprvé ukázal až u hráče.
        PhotoAt(screens, sim, harbourX, harbourY, "civdle-smoke-subsea");
    }

    /// <summary>
    /// Zapne obranu, postaví věnec věží a nechá přijít vlnu.
    ///
    /// <para>Režim se tu zapíná ladicí cestou schválně: ve hře se volí při
    /// zakládání světa a smoke běh staví nad hotovým městem.</para>
    /// </summary>
    private static void FrontierRound(
        ScreenManager screens, Simulation sim, GameplayScreen screen, GameTime time)
    {
        var content = screens.Content;
        if (!content.Frontier.IsAvailable)
        {
            return;
        }

        sim.EnableFrontierDefense();

        int tower = content.Buildings.IndexOf("watchtower");
        for (int i = 0; i < 16; i++)
        {
            double angle = Math.Tau * i / 16;
            // Na okraji města, ne uvnitř: uprostřed je všechno zastavěné
            // a věž by se nikam nevešla. Okruh hledání je malý schválně —
            // šestnáct prohledávání celé krajiny by se sečetlo do minut.
            TryPlaceNear(
                sim, tower,
                sim.CityCenterX + (int)Math.Round(Math.Cos(angle) * 30),
                sim.CityCenterY + (int)Math.Round(Math.Sin(angle) * 30),
                wantWater: false,
                radius: 10);
        }

        // Vlnu pošleme hned. Dotikat k ní poctivě je až dva a půl tisíce tiků
        // nad pětisetbudovým městem — minuty čekání na něco, co se stejně má
        // jen nakreslit.
        sim.Frontier.DebugForceWave(sim);

        // A pak dost dlouho, aby vlna došla k věžím: útočník ujde dvacetinu
        // dlaždice za tik, takže pár set tiků je pořád „na obzoru".
        for (int i = 0; i < 900; i++)
        {
            sim.Tick();
        }

        Console.WriteLine(
            $"obrana: na mapě {sim.Frontier.Count}, sestřeleno {sim.Frontier.Killed}, "
            + $"prošlo {sim.Frontier.ReachedCity}");

        // Kamera na město, ať útočníci opravdu projdou kreslením.
        screen.FocusForCapture(
            new Vector2(
                sim.CityCenterX * Rendering.TerrainRenderer.TileSize,
                sim.CityCenterY * Rendering.TerrainRenderer.TileSize),
            zoom: 2f);
        Frames(screen, time);
    }

    /// <summary>
    /// Vypustí družice a nechá orbitální obrazovku pár snímků žít — i s tím,
    /// co je na dráze, ne jen s prázdným kotoučem.
    ///
    /// <para>Obrazovka kreslí planetu a družice mimo mapu, takže se na ni
    /// žádný jiný krok nedostane.</para>
    /// </summary>
    private static void OrbitRound(ScreenManager screens, Simulation sim, GameTime time)
    {
        var content = screens.Content;
        if (!content.Orbit.IsEnabled)
        {
            return;
        }

        CityFixture.FillTheOrbit(sim, content);

        var orbitScreen = new OrbitScreen(screens, sim);
        screens.Push(orbitScreen);
        Frames(orbitScreen, time);
        Console.WriteLine($"orbita: kosmodrom = {sim.HasLaunchSite}, na dráze {sim.Orbit.TotalLaunched}");
        screens.Pop();
    }

    /// <summary>Uloží fotku vycentrovanou na konkrétní dlaždici.</summary>
    private static void PhotoAt(ScreenManager screens, Simulation sim, int tileX, int tileY, string folder)
    {
        var camera = new Rendering.Camera2D();
        camera.SetViewport(1920, 1080);
        camera.CenterOn(
            new Vector2(tileX * Rendering.TerrainRenderer.TileSize, tileY * Rendering.TerrainRenderer.TileSize),
            3f);

        new ShareCard(screens).Save(
            sim, camera, Path.Combine(Path.GetTempPath(), folder),
            ShareCardOptions.For(
                CivDle.Core.Config.CaptureResolution.Hd1080, withStrip: false, fullDetail: true));
    }

    /// <summary>
    /// Zkusí položit budovu na nejbližší vhodnou dlaždici v okolí bodu.
    ///
    /// <para>Okolí je široké schválně: <c>TryFindShore</c> hledá břeh, kde je
    /// zároveň zástavba (aby na snímku bylo vidět město u vody), takže těsně
    /// kolem něj je všechno zabrané. Nejbližší volné místo bylo v testu skoro
    /// dvacet dlaždic daleko — s užším okruhem se krok tiše přeskakoval.</para>
    /// </summary>
    private static bool TryPlaceNear(
        Simulation sim, int defIndex, int centerX, int centerY, bool wantWater, int radius = 60)
    {
        int Radius = radius;

        int bestDistance = int.MaxValue;
        int bestX = 0, bestY = 0;

        for (int dy = -Radius; dy <= Radius; dy++)
        {
            for (int dx = -Radius; dx <= Radius; dx++)
            {
                int x = centerX + dx, y = centerY + dy;
                if (sim.IsWaterAt(x, y) != wantWater || sim.CanPlace(defIndex, x, y) != PlacementResult.Ok)
                {
                    continue;
                }

                int distance = Math.Max(Math.Abs(dx), Math.Abs(dy));
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestX = x;
                    bestY = y;
                }
            }
        }

        return bestDistance != int.MaxValue
            && sim.TryPlaceBuildingFree(defIndex, bestX, bestY) == PlacementResult.Ok;
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
