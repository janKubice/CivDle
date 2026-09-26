using CivDle.Core;
using CivDle.Core.Sim;
using CivDle.Input;
using CivDle.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Myra.Graphics2D.UI;

namespace CivDle.Screens;

/// <summary>
/// Skryté ladicí menu (Ctrl+Shift+D).
///
/// <para>Je to <b>nástroj pro vývoj, testování a natáčení</b>, ne herní obsah:
/// dostat se k pozdní fázi hry poctivě trvá hodiny, takže bez tohohle se nedá
/// vyzkoušet nic, co se děje po pár Vzestupech, ani připravit scéna na video.
/// Proto je schované za klávesovou zkratkou a v žádné nabídce na něj nevede
/// tlačítko — hráč, který ho nehledá, na něj nenarazí a nezkazí si tím vlastní
/// postup.</para>
///
/// <para>Páky jsou po sekcích v posuvném seznamu: s třiceti tlačítky by jeden
/// sloupec přerostl obrazovku. Nic se po kliknutí nepřestavuje (mění se jen
/// popisek přepínače a řádek s výsledkem), takže seznam zůstává, kde byl.</para>
///
/// <para>Sahá jen na veřejné příkazy simulace a na její <c>Debug*</c> páky —
/// tytéž, které testuje <c>DebugToolsTests</c>.</para>
///
/// <para>Texty nejsou lokalizované schválně: je to vývojářský nástroj a každý
/// klíč navíc by byl práce pro překladatele na věci, kterou hráč nevidí.</para>
/// </summary>
public sealed class DebugScreen : IScreen
{
    private const int PanelWidth = 660;

    /// <summary>Kolik tlačítek se vejde vedle sebe.</summary>
    private const int ButtonsPerRow = 3;

    /// <summary>Jak velký kus mapy odhalí „okolí“ (v dlaždicích).</summary>
    private const int RevealRadius = 220;

    /// <summary>
    /// Jak velký kus odhalí „velký kus světa“. Mlha se ukládá po čtvercích 8×8,
    /// takže i tisíc dlaždic je jen pár desítek tisíc záznamů.
    /// </summary>
    private const int WideRevealRadius = 1000;

    // Denní doby pro natáčení — sedí na přechody v DayNightCycle (svítání
    // končí v 0,32, soumrak začíná v 0,72).
    private const double Dawn = 0.26;
    private const double Noon = 0.50;
    private const double Dusk = 0.79;
    private const double Night = 0.95;

    private readonly ScreenManager _screens;
    private readonly Simulation _simulation;
    private readonly Camera2D _camera;
    private readonly Capture.CheatMode? _cheats;
    private readonly Action? _spawnGolden;
    private readonly InputManager _input = new();
    private Desktop? _desktop;
    private Label? _status;

    /// <param name="screens">Správce obrazovek.</param>
    /// <param name="simulation">Rozehraná simulace.</param>
    /// <param name="camera">Kamera — odhalení mapy jde kolem místa, kam se hráč dívá.</param>
    /// <param name="cheats">
    /// Přepínače na natáčení. <c>null</c> tam, kde se ladicí menu otevírá mimo
    /// hru — cheaty patří k rozehrané relaci, ne k menu.
    /// </param>
    /// <param name="spawnGolden">
    /// Pošle zlatý úlovek hned. Úlovky žijí v renderu (nejsou to simulace),
    /// takže je menu samo nevidí — dostane k nim jen tuhle páku.
    /// </param>
    public DebugScreen(
        ScreenManager screens,
        Simulation simulation,
        Camera2D camera,
        Capture.CheatMode? cheats = null,
        Action? spawnGolden = null)
    {
        _screens = screens;
        _simulation = simulation;
        _camera = camera;
        _cheats = cheats;
        _spawnGolden = spawnGolden;
        BuildUi();
    }

    public bool IsOverlay => true;

    public void OnActivated() => _input.Resync();

    /// <summary>Jedna páka: popisek, co udělá, a jestli ji smí zmáčknout smoke test.</summary>
    /// <param name="InSmoke">
    /// False u pák, které otevírají další okno nebo trvají dlouho (přetočení
    /// času o hodiny by smoke běh natáhlo o minuty).
    /// </param>
    private readonly record struct Lever(string Label, Action Run, bool InSmoke = true);

    /// <summary>Sekce menu: nadpis a páky pod ním.</summary>
    private readonly record struct Section(string Title, Lever[] Levers);

    private void BuildUi()
    {
        var layout = new VerticalStackPanel { Spacing = 10, Width = PanelWidth };

        layout.Widgets.Add(new Label
        {
            Text = "DEBUG",
            TextColor = UiPalette.Bad,
            HorizontalAlignment = HorizontalAlignment.Center,
        });

        layout.Widgets.Add(new Label
        {
            Text = "Ladicí nástroje pro testování a natáčení. Nepatří k hraní.",
            TextColor = Color.Gray,
            Wrap = true,
            Width = PanelWidth - 20,
            HorizontalAlignment = HorizontalAlignment.Center,
        });

        var list = new VerticalStackPanel { Spacing = 6 };

        // Trvalé přepínače nahoře, jednorázové páky pod nimi. Na natáčení je
        // rozdíl podstatný: jednorázová páka uprostřed záběru nepomůže.
        if (_cheats is not null)
        {
            var cheats = _cheats;
            list.Widgets.Add(SectionTitle("Natáčení"));
            list.Widgets.Add(ToggleRow("debug.cheatResources", () => cheats.Resources, cheats.ToggleResources));
            list.Widgets.Add(ToggleRow("debug.cheatGovernor", () => cheats.Governor, cheats.ToggleGovernor));
        }

        foreach (var section in Sections())
        {
            list.Widgets.Add(SectionTitle(section.Title));
            AddLevers(list, section.Levers);
        }

        int height = Math.Clamp(_screens.GraphicsDevice.Viewport.Height - 280, 280, 640);
        layout.Widgets.Add(new ScrollViewer { Content = list, Height = height, Width = PanelWidth });

        _status = new Label
        {
            Text = string.Empty,
            TextColor = UiPalette.Good,
            Wrap = true,
            Width = PanelWidth - 20,
        };
        layout.Widgets.Add(_status);

        layout.Widgets.Add(UiFactory.SmallButton(_screens.Loc["panel.close"], _screens.Pop));

        var panel = UiFactory.DarkPanel(layout);
        panel.HorizontalAlignment = HorizontalAlignment.Center;
        panel.VerticalAlignment = VerticalAlignment.Center;

        var root = new Panel();
        root.Widgets.Add(panel);
        _desktop = _screens.NewDesktop(root);
    }

    /// <summary>
    /// Všechny páky po sekcích. Jedno místo, odkud je čte UI i smoke test —
    /// páka, která by v menu byla a ve smoke ne, by mohla spadnout nepozorovaně.
    /// </summary>
    private Section[] Sections() => new[]
    {
        new Section("Prestiž", new Lever[]
        {
            new("+1 000 bodů Vzestupu", () => GrantAscensionPoints(1_000)),
            new("+1 mil. bodů Vzestupu", () => GrantAscensionPoints(1_000_000)),
            new("+1 mld. bodů Vzestupu", () => GrantAscensionPoints(1_000_000_000)),
            new("+1 000 bodů Odkazu", () => GrantLegacy(1_000)),
            new("+1 mil. bodů Odkazu", () => GrantLegacy(1_000_000)),
            new("+1 mld. bodů Odkazu", () => GrantLegacy(1_000_000_000)),
            new("+1 úroveň Vzestupu", () => GrantAscensionLevels(1)),
            new("+5 úrovní Vzestupu", () => GrantAscensionLevels(5)),
            new("+25 úrovní Vzestupu", () => GrantAscensionLevels(25)),
            new("+1 hloubka Odkazu", DeepenLegacy),
            new("Vzestup: vše na max", MaxPrestigeUpgrades),
            new("Odkaz: vše na max", MaxLegacyUpgrades),
        }),
        new Section("Suroviny", new Lever[]
        {
            new("+10 tis. od všeho", () => GrantResources(10_000)),
            new("+1 mil. od všeho", () => GrantResources(1_000_000)),
            new("Naplnit sklady", FillStorages),
            new("Vyprázdnit sklady", EmptyStorages),
        }),
        new Section("Výzkum", new Lever[]
        {
            new("Vyzkoumat, na co mám", ResearchReachable),
            new("Vyzkoumat vše zdarma", ResearchEverything),
        }),
        new Section("Město", new Lever[]
        {
            new("+10 tis. obyvatel", () => AddPopulation(10_000)),
            new("+1 mil. obyvatel", () => AddPopulation(1_000_000)),
            new("Dostavět rozestavěné", CompleteConstruction),
            new("Auto-stavba ×20 / 1 min", () => BoostBuilding(20, 60)),
            new("Auto-stavba ×100 / 10 s", () => BoostBuilding(100, 10)),
        }),
        new Section("Čas", new Lever[]
        {
            new("Přetočit o hodinu", () => SkipTime(3600)),
            new("Přetočit o 8 hodin", () => SkipTime(8 * 3600)),
            new("Přetočit o 12 hodin", () => SkipTime(12 * 3600)),
            new("Další roční období", NextSeason),
            new("Svítání", () => SetTimeOfDay(Dawn, "svítání")),
            new("Poledne", () => SetTimeOfDay(Noon, "poledne")),
            new("Soumrak", () => SetTimeOfDay(Dusk, "soumrak")),
            new("Noc", () => SetTimeOfDay(Night, "noc")),
        }),
        new Section("Svět", new Lever[]
        {
            new("Odhalit okolí", () => RevealMap(RevealRadius)),
            new("Odhalit velký kus světa", () => RevealMap(WideRevealRadius)),
            new("Spustit náhodnou událost", TriggerEvent, InSmoke: false),
            new("Zlatý úlovek hned", SpawnGolden),
        }),
    };

    private Label SectionTitle(string text) => new()
    {
        Text = text,
        TextColor = UiPalette.Accent,
        Margin = new Myra.Graphics2D.Thickness(0, 6, 0, 0),
    };

    private static void AddLevers(VerticalStackPanel list, Lever[] levers)
    {
        HorizontalStackPanel? row = null;
        for (int i = 0; i < levers.Length; i++)
        {
            if (i % ButtonsPerRow == 0)
            {
                row = new HorizontalStackPanel { Spacing = 6 };
                list.Widgets.Add(row);
            }

            var button = UiFactory.SmallButton(levers[i].Label, levers[i].Run);
            button.Width = (PanelWidth - 40) / ButtonsPerRow;
            row!.Widgets.Add(button);
        }
    }

    /// <summary>
    /// Řádek s přepínačem: popisek nese stav, aby bylo z jednoho pohledu vidět,
    /// co je zapnuté. Bez toho by autor hádal, proč se sklady samy plní.
    /// </summary>
    private Widget ToggleRow(string labelKey, Func<bool> isOn, Action toggle)
    {
        string Caption() => $"{_screens.Loc[labelKey]} — {_screens.Loc[isOn() ? "common.on" : "common.off"]}";

        var button = UiFactory.SmallButton(Caption(), () => { });
        button.Click += (_, _) =>
        {
            toggle();
            if (button.Content is Label label)
            {
                label.Text = Caption();
            }
        };

        return button;
    }

    // ----- prestiž -----

    private void GrantAscensionPoints(long amount)
    {
        _simulation.DebugGrantPrestigePoints(amount);
        Report($"+{Numbers.Format(amount)} bodů Vzestupu (celkem {Numbers.Format(_simulation.PrestigePoints)})");
    }

    private void GrantLegacy(long amount)
    {
        _simulation.DebugGrantLegacyPoints(amount);
        Report($"+{Numbers.Format(amount)} bodů Odkazu (celkem {Numbers.Format(_simulation.LegacyPoints)})");
    }

    private void GrantAscensionLevels(int levels)
    {
        _simulation.DebugGrantAscensionLevels(levels);
        Report($"úroveň Vzestupu je {_simulation.AscensionLevel}, strop měřítka "
            + $"{Numbers.Format(_simulation.PopulationCap)}");
    }

    private void DeepenLegacy()
    {
        _simulation.DebugDeepenLegacy(1);
        Report($"hloubka Odkazu {_simulation.LegacyDepth}, další Odkaz od {_simulation.LegacyRequirement()}");
    }

    private void MaxPrestigeUpgrades()
    {
        _simulation.DebugMaxPrestigeUpgrades();
        Report("všechny upgrady Vzestupu jsou na maximu");
    }

    private void MaxLegacyUpgrades()
    {
        _simulation.DebugMaxLegacyUpgrades();
        Report("všechny upgrady Odkazu jsou na maximu");
    }

    // ----- suroviny -----

    private void GrantResources(double amount)
    {
        _simulation.DebugGrantEveryResource(amount);
        Report($"+{Numbers.Format(amount)} od každé z {_simulation.ResourceCount} surovin "
            + "(sklady se zvětšily, aby se to vešlo)");
    }

    private void FillStorages()
    {
        _simulation.DebugFillStorages();
        Report("sklady naplněny na maximum");
    }

    private void EmptyStorages()
    {
        _simulation.DebugEmptyStorages();
        Report("sklady jsou prázdné");
    }

    // ----- výzkum -----

    /// <summary>
    /// Vyzkoumá všechno, co jde zaplatit — opakovaně, protože každý hotový uzel
    /// odemkne další.
    /// </summary>
    private void ResearchReachable()
    {
        int done = 0;
        bool progressed = true;
        while (progressed)
        {
            progressed = false;
            for (int i = 0; i < _screens.Content.Techs.Count; i++)
            {
                if (_simulation.TryResearch(i) == PlacementResult.Ok)
                {
                    done++;
                    progressed = true;
                }
            }
        }

        Report($"vyzkoumáno {done} technologií");
    }

    /// <summary>
    /// Vyzkoumá celý strom bez placení. Uzly mimo výřez dema zůstanou zamčené
    /// (viz <see cref="Simulation.DebugGrantTech"/>) — hranice dema nesmí jít
    /// obejít ladicím menu.
    /// </summary>
    private void ResearchEverything()
    {
        int done = 0;
        int locked = 0;
        for (int i = 0; i < _screens.Content.Techs.Count; i++)
        {
            if (_simulation.IsTechResearched(i))
            {
                continue;
            }

            if (_simulation.DebugGrantTech(i))
            {
                done++;
            }
            else
            {
                locked++;
            }
        }

        Report(locked > 0
            ? $"vyzkoumáno {done} technologií, {locked} je mimo demo"
            : $"vyzkoumáno {done} technologií");
    }

    // ----- město -----

    private void AddPopulation(double amount)
    {
        double before = _simulation.Population;
        _simulation.DebugAddPopulation(amount);
        Report($"populace {Numbers.Format(before)} → "
            + $"{Numbers.Format(_simulation.Population)} (strop drží měřítko i bydlení)");
    }

    private void CompleteConstruction()
    {
        _simulation.DebugCompleteConstruction();
        Report("všechno rozestavěné je dostavěné");
    }

    private void BoostBuilding(double multiplier, double seconds)
    {
        _simulation.DebugBoostAutoBuild(multiplier, seconds);
        Report($"auto-stavba jede ×{multiplier:0} po dobu {seconds:0} s");
    }

    // ----- čas -----

    /// <summary>
    /// Přetočí zadaný čas stejnou cestou jako dohánění offline času — po krocích
    /// v <see cref="Update"/>, s postupem v řádku výsledku. Dřív se odtikalo
    /// všechno naráz a u velkého města okno zamrzlo na desítky minut.
    /// </summary>
    private void SkipTime(double seconds)
    {
        if (_skip is not null)
        {
            return; // jedno přetáčení stačí
        }

        var now = DateTime.UtcNow;
        _skip = new OfflineCatchUp(_simulation, now.AddSeconds(-seconds), now);
        _skipSeconds = seconds;
    }

    /// <summary>Běžící přetáčení času (null = žádné).</summary>
    private OfflineCatchUp? _skip;
    private double _skipSeconds;
    private readonly System.Diagnostics.Stopwatch _skipClock = new();

    /// <summary>Kolik milisekund snímku smí přetáčení zabrat — zbytek patří oknu.</summary>
    private const double SkipMillisPerFrame = 12.0;

    /// <summary>
    /// Poslední pád přetáčení. Ve hře se jen vypíše a přetáčení skončí (menu
    /// nesmí shodit hru), ale smoke ho musí ohlásit jako chybu — jinak by
    /// prošel „OK" s pádem v logu, jak se to stalo s přeplněnou obranou.
    /// </summary>
    private Exception? _skipFailure;

    private void AdvanceSkip()
    {
        if (_skip is null)
        {
            return;
        }

        _skipClock.Restart();
        try
        {
            while (!_skip.IsDone && _skipClock.Elapsed.TotalMilliseconds < SkipMillisPerFrame)
            {
                _skip.Advance(1);
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Přetáčení času selhalo: {ex}");
            _skipFailure = ex;
            _skip.Skip();
        }

        if (!_skip.IsDone)
        {
            Report($"přetáčím o {_skipSeconds / 3600:0.#} h… {(int)Math.Round(_skip.Progress * 100)} %");
            return;
        }

        _skip.Finish();
        Report($"přetočeno o {_skipSeconds / 3600:0.#} h"
            + (_skip.IsEstimated ? " (část odhadem po úsecích)" : string.Empty));
        _skip = null;
    }

    private void SetTimeOfDay(double timeOfDay01, string name)
    {
        _simulation.DebugSetTimeOfDay(timeOfDay01);
        Report($"je {name}, den {_simulation.DayNumber}");
    }

    private void NextSeason()
    {
        int before = _simulation.CurrentSeasonIndex;
        _simulation.DebugAdvanceSeason();
        var season = _simulation.CurrentSeason;
        Report(season is null || before == _simulation.CurrentSeasonIndex
            ? "hra nemá roční období"
            : $"nové období: {_screens.Loc[season.NameKey]}, den {_simulation.DayNumber}");
    }

    // ----- svět -----

    private void RevealMap(int radius)
    {
        int tileX = (int)(_camera.Position.X / TerrainRenderer.TileSize);
        int tileY = (int)(_camera.Position.Y / TerrainRenderer.TileSize);
        _simulation.Fog.Reveal(tileX, tileY, radius);
        Report($"odhaleno {radius} dlaždic kolem {tileX},{tileY}");
    }

    /// <summary>
    /// Ukáže náhodnou událost hned. Jde mimo ředitele (ContentDirector) — ten
    /// hlídá rozestupy mezi událostmi, a právě ty se tu chtějí přeskočit.
    /// </summary>
    private void TriggerEvent()
    {
        int count = _screens.Content.Events.Count;
        if (count == 0)
        {
            Report("hra nemá žádné události");
            return;
        }

        int index = Random.Shared.Next(count);
        _screens.Push(new EventScreen(_screens, _simulation, index));
    }

    private void SpawnGolden()
    {
        if (_spawnGolden is null)
        {
            Report("zlatý úlovek jde poslat jen ze hry");
            return;
        }

        _spawnGolden();
        Report("zlatý úlovek je na cestě — zavři menu a hledej ho na obrazovce");
    }

    private void Report(string text)
    {
        if (_status is not null)
        {
            _status.Text = text;
        }
    }

    /// <summary>
    /// Smoke test: zmáčkne každou páku kromě těch, které otevírají další okno
    /// nebo trvají dlouho. Páka, která spadne, spadne tady — ne autorovi
    /// uprostřed natáčení.
    /// </summary>
    internal void PullEveryLeverForSmoke()
    {
        foreach (var section in Sections())
        {
            foreach (var lever in section.Levers)
            {
                if (lever.InSmoke)
                {
                    lever.Run();
                }
            }
        }

        // Přetáčení běží po krocích v Update; ve smoke se dotáhne do konce,
        // ať projde i odhadovaná část dohánění nad skutečným městem.
        while (_skip is not null)
        {
            AdvanceSkip();
        }

        if (_skipFailure is { } failure)
        {
            throw new InvalidOperationException("Přetáčení času v ladicím menu selhalo.", failure);
        }
    }

    public void Update(GameTime gameTime)
    {
        _input.Update();
        AdvanceSkip();
        if (_input.WasPressed(Keys.Escape) && _skip is null)
        {
            _screens.Pop();
        }
    }

    public void Draw(GameTime gameTime)
    {
        _desktop?.Render();
    }

    public void Dispose()
    {
    }
}
