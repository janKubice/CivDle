using CivDle.Core;
using CivDle.Core.Galaxy;
using CivDle.Core.Sim;
using CivDle.Input;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Myra.Graphics2D.UI;

namespace CivDle.Screens;

/// <summary>
/// Konec první kapitoly: brána je otevřená a hra to hráči <b>řekne</b>.
///
/// <para>Proč to ve hře je: dohrání bylo prázdno — všechno koupené, nic se
/// nestane. Sekvence z něj dělá zážitek (endgame.md, bod C2): časosběr od
/// první chalupy, kronika, statistiky a titulky se jmény sídel a osobností
/// z <b>téhle</b> hry. Potom hra běží dál — brána nic neresetuje.</para>
///
/// <para>Stránky jdou za sebou; časosběr a kronika jsou existující obrazovky,
/// které se jen vloží navrch a po zavření se pokračuje další stránkou.
/// Escape přeskočí na další — nikdo nesmí být v titulcích uvězněný.</para>
///
/// <para><b>Epilog galaxie</b> (konec druhé kapitoly, svety-design.md 2.7) je
/// tatáž sekvence nad celou galaxií: časosběr každého založeného světa,
/// kronika Domoviny, statistiky galaxie a titulky se sídly všech světů.
/// Nová hra+ se v něm nenabízí — galaxie běží dál.</para>
///
/// <para>Vrstva: UI. Ze simulace jen čte (<see cref="EndingSummary"/>,
/// <see cref="GalaxyEndingSummary"/>); sekvence se dá pustit znovu z menu
/// a nesmí na hru sáhnout.</para>
/// </summary>
public sealed class EndingScreen : IScreen
{
    private const int PanelWidth = 720;

    /// <summary>Rychlost titulků v pixelech za sekundu.</summary>
    private const float CreditsSpeed = 38f;

    private enum Page { Title, Timelapse, Chronicle, Stats, Credits, Final }

    private readonly ScreenManager _screens;
    private readonly Simulation _simulation;
    private readonly EndingSummary _summary;
    private readonly bool _replay;
    private readonly Action? _newGamePlus;

    /// <summary>Galaxie epilogu; null = konec první kapitoly (jen Domovina).</summary>
    private readonly GalaxySession? _galaxy;
    private readonly GalaxyEndingSummary? _galaxySummary;

    /// <summary>Kolikátý svět epilogu se přehrává v časosběru.</summary>
    private int _timelapseWorld;
    private readonly InputManager _input = new();
    private Desktop _desktop = null!;
    private Page _page = Page.Title;
    private bool _waitingForChild;
    private ScrollViewer? _credits;
    private float _creditsOffset;

    /// <param name="simulation">Hra, jejíž konec se ukazuje (jen se čte).</param>
    /// <param name="replay">Přehrává se z menu — na konci „zpět do menu", ne „pokračovat".</param>
    /// <param name="newGamePlus">Nová hra+ (jen při prvním dohrání); null = nenabízí se.</param>
    /// <param name="galaxy">
    /// Galaxie pro epilog druhé kapitoly; null = konec první kapitoly.
    /// <paramref name="simulation"/> je pak Domovina (kronika).
    /// </param>
    public EndingScreen(ScreenManager screens, Simulation simulation, bool replay, Action? newGamePlus,
        GalaxySession? galaxy = null)
    {
        _screens = screens;
        _simulation = simulation;
        _summary = EndingSummary.Of(simulation);
        _replay = replay;
        _galaxy = galaxy;
        _galaxySummary = galaxy is null ? null : GalaxyEndingSummary.Of(galaxy);
        _newGamePlus = galaxy is null ? newGamePlus : null;
        BuildUi();
        _screens.Loc.LanguageChanged += BuildUi;
    }

    public bool IsOverlay => false;

    public void OnActivated()
    {
        _input.Resync();

        // Vrátili jsme se z časosběru nebo kroniky — pokračuje se dál
        // (v epilogu galaxie nejdřív časosběrem dalšího světa).
        if (_waitingForChild)
        {
            _waitingForChild = false;
            if (_page == Page.Timelapse && PushNextWorldTimelapse())
            {
                return;
            }

            Next();
        }
    }

    public void Update(GameTime gameTime)
    {
        _input.Update();
        if (_input.WasPressed(Keys.Escape) && _page != Page.Final)
        {
            Next();
            return;
        }

        if (_page == Page.Credits && _credits is not null)
        {
            _creditsOffset += (float)gameTime.ElapsedGameTime.TotalSeconds * CreditsSpeed;
            _credits.ScrollPosition = new Point(0, (int)_creditsOffset);
        }
    }

    public void Draw(GameTime gameTime) => _desktop.Render();

    /// <summary>Je sekvence na poslední stránce (smoke test)?</summary>
    public bool IsOnFinalPageForSmoke => _page == Page.Final;

    /// <summary>Další stránka bez klávesnice (smoke test prochází celou sekvenci).</summary>
    public void NextForSmoke() => Next();

    public void Dispose() => _screens.Loc.LanguageChanged -= BuildUi;

    /// <summary>Další stránka; časosběr a kronika se otevřou jako vlastní obrazovky.</summary>
    private void Next()
    {
        _page++;
        switch (_page)
        {
            case Page.Timelapse when _galaxySummary is not null:
                if (!PushNextWorldTimelapse())
                {
                    Next();
                }

                return;
            case Page.Timelapse when _simulation.HistoryEnabled && _simulation.History.Count > 1:
                _waitingForChild = true;
                _screens.Push(new TimelapseScreen(
                    _screens, _simulation.History, _simulation.Terrain, _simulation.Seed));
                return;
            case Page.Timelapse:
                Next(); // bez kroniky snímků není co přehrát
                return;
            case Page.Chronicle:
                _waitingForChild = true;
                _screens.Push(new ChroniclePageScreen(_screens, _simulation));
                return;
            case Page.Final when _galaxySummary is not null:
                if (!_screens.Profile.GalaxyEndingSeen)
                {
                    _screens.Profile.GalaxyEndingSeen = true;
                    _screens.SaveProfile();
                }

                break;
            case Page.Final:
                // Hráč sekvenci viděl celou — z menu ji teď jde pustit znovu.
                if (!_screens.Profile.ChapterEndingSeen)
                {
                    _screens.Profile.ChapterEndingSeen = true;
                    _screens.SaveProfile();
                }

                break;
        }

        _creditsOffset = 0;
        BuildUi();
    }

    /// <summary>
    /// Epilog galaxie: časosběr dalšího světa, který kroniku má. Svět se
    /// načte ze snímku jen ke čtení a po zavření časosběru se zahodí — v paměti
    /// nejsou všechny světy naráz.
    /// </summary>
    /// <returns>false = žádný další svět k přehrání.</returns>
    private bool PushNextWorldTimelapse()
    {
        if (_galaxySummary is null || _galaxy is null)
        {
            return false;
        }

        while (_timelapseWorld < _galaxySummary.Worlds.Count)
        {
            var world = _galaxy.PeekWorld(_galaxySummary.Worlds[_timelapseWorld++].WorldId);
            if (world.HistoryEnabled && world.History.Count > 1)
            {
                _waitingForChild = true;
                _screens.Push(new TimelapseScreen(
                    _screens, world.History, world.Terrain, world.Seed, content: world.Content));
                return true;
            }
        }

        return false;
    }

    private void BuildUi()
    {
        var layout = new VerticalStackPanel { Spacing = 12, Width = PanelWidth };
        switch (_page)
        {
            case Page.Title:
                TitlePage(layout);
                break;
            case Page.Stats when _galaxySummary is not null:
                GalaxyStatsPage(layout, _galaxySummary);
                break;
            case Page.Stats:
                StatsPage(layout);
                break;
            case Page.Credits when _galaxySummary is not null:
                GalaxyCreditsPage(layout, _galaxySummary);
                break;
            case Page.Credits:
                CreditsPage(layout);
                break;
            case Page.Final:
                FinalPage(layout);
                break;
            default:
                // Časosběr a kronika mají vlastní obrazovku; pod nimi stačí titul.
                TitlePage(layout);
                break;
        }

        _desktop = _screens.NewDesktop(UiFactory.MenuBackdrop(layout));
    }

    private void TitlePage(VerticalStackPanel layout)
    {
        var loc = _screens.Loc;
        bool galaxy = _galaxySummary is not null;
        layout.Widgets.Add(Centered(loc[galaxy ? "ending.galaxy.title" : "ending.title"], UiPalette.TextBright));
        layout.Widgets.Add(Wrapped(loc[galaxy ? "ending.galaxy.subtitle" : "ending.subtitle"], UiPalette.Text));
        layout.Widgets.Add(UiFactory.MenuButton(loc["ending.next"], Next));
    }

    /// <summary>Galaxie v číslech: součty přes všechny světy a řádek za každý svět.</summary>
    private void GalaxyStatsPage(VerticalStackPanel layout, GalaxyEndingSummary summary)
    {
        var loc = _screens.Loc;
        layout.Widgets.Add(Centered(loc["ending.galaxy.stats"], UiPalette.TextBright));

        void Line(string key, string value) =>
            layout.Widgets.Add(Centered(loc.Format(key, value), UiPalette.Text));

        Line("ending.stat.time", DurationFormat.Human(summary.GalacticSeconds));
        Line("ending.galaxy.stat.worlds", Numbers.Format(summary.Worlds.Count));
        Line("ending.stat.peak", Numbers.Format(summary.PeakPopulation));
        Line("ending.stat.buildings", Numbers.Format(summary.Buildings));
        Line("ending.galaxy.stat.stars", Numbers.Format(summary.Stars));
        Line("ending.stat.wonders", Numbers.Format(summary.WondersCompleted));
        Line("ending.stat.ascensions", Numbers.Format(summary.Ascensions));
        Line("ending.galaxy.stat.trade", Numbers.Format(summary.TradeShipped));

        layout.Widgets.Add(new Panel { Height = 8 });
        foreach (var world in summary.Worlds)
        {
            layout.Widgets.Add(Centered(loc.Format("ending.galaxy.stat.world",
                loc[$"world.{world.WorldId}"], Numbers.Format(world.Summary.PeakPopulation), Numbers.Format(world.Stars)),
                UiPalette.TextDim));
        }

        layout.Widgets.Add(UiFactory.MenuButton(loc["ending.next"], Next));
    }

    /// <summary>
    /// Titulky galaxie: za každý svět jeho sídla (jména jsou z obsahu toho
    /// světa), nakonec osobnosti Domoviny.
    /// </summary>
    private void GalaxyCreditsPage(VerticalStackPanel layout, GalaxyEndingSummary summary)
    {
        var loc = _screens.Loc;
        var list = new VerticalStackPanel { Spacing = 6, Width = PanelWidth - 40 };
        list.Widgets.Add(new Panel { Height = 360 });
        foreach (var world in summary.Worlds)
        {
            var names = _screens.Galaxy.For(world.WorldId).SettlementNames;
            list.Widgets.Add(Centered(loc[$"world.{world.WorldId}"], UiPalette.Accent));
            foreach (int name in world.Summary.SettlementNameIndices)
            {
                if (names.Count > 0)
                {
                    list.Widgets.Add(Centered(names[name % names.Count], UiPalette.Text));
                }
            }

            list.Widgets.Add(new Panel { Height = 24 });
        }

        var content = _screens.HomeContent;
        if (_summary.FigureIndices.Count > 0)
        {
            list.Widgets.Add(Centered(loc["ending.credits.figures"], UiPalette.Accent));
            foreach (int figure in _summary.FigureIndices)
            {
                if (figure >= 0 && figure < content.Figures.Count)
                {
                    list.Widgets.Add(Centered(loc[$"figure.{content.Figures[figure].Id}"], UiPalette.Text));
                }
            }

            list.Widgets.Add(new Panel { Height = 30 });
        }

        list.Widgets.Add(Centered(loc["ending.credits.thanks"], UiPalette.TextBright));
        list.Widgets.Add(new Panel { Height = 360 });

        _credits = new ScrollViewer { Content = list, Width = PanelWidth, Height = 420 };
        layout.Widgets.Add(_credits);
        layout.Widgets.Add(UiFactory.MenuButton(loc["ending.next"], Next));
    }

    private void StatsPage(VerticalStackPanel layout)
    {
        var loc = _screens.Loc;
        layout.Widgets.Add(Centered(loc["ending.stats"], UiPalette.TextBright));

        void Line(string key, string value) =>
            layout.Widgets.Add(Centered(loc.Format(key, value), UiPalette.Text));

        Line("ending.stat.time", DurationFormat.Human(_summary.GameSeconds));
        Line("ending.stat.peak", Numbers.Format(_summary.PeakPopulation));
        Line("ending.stat.buildings", Numbers.Format(_summary.Buildings));
        Line("ending.stat.ascensions", Numbers.Format(_summary.Ascensions));
        Line("ending.stat.wonders", Numbers.Format(_summary.WondersCompleted));
        Line("ending.stat.contracts", Numbers.Format(_summary.ContractsCompleted));
        if (_summary.WavesRepelled > 0)
        {
            Line("ending.stat.waves", Numbers.Format(_summary.WavesRepelled));
        }

        Line("ending.stat.challenges", Numbers.Format(_screens.Profile.WonChallenges.Count));
        layout.Widgets.Add(UiFactory.MenuButton(loc["ending.next"], Next));
    }

    /// <summary>
    /// Titulky: jména sídel a osobností z téhle hry. Hráč je vymyslet nemusel,
    /// ale žil s nimi hodiny — proto patří do titulků víc než jména vývojářů.
    /// </summary>
    private void CreditsPage(VerticalStackPanel layout)
    {
        var loc = _screens.Loc;
        var content = _screens.Content;
        var list = new VerticalStackPanel { Spacing = 6, Width = PanelWidth - 40 };

        // Prázdné místo nahoře: titulky přijedou zespodu, ne hned celé.
        list.Widgets.Add(new Panel { Height = 360 });
        list.Widgets.Add(Centered(loc["ending.credits.settlements"], UiPalette.Accent));
        foreach (int name in _summary.SettlementNameIndices)
        {
            if (content.SettlementNames.Count > 0)
            {
                list.Widgets.Add(Centered(content.SettlementNames[name % content.SettlementNames.Count], UiPalette.Text));
            }
        }

        if (_summary.FigureIndices.Count > 0)
        {
            list.Widgets.Add(new Panel { Height = 30 });
            list.Widgets.Add(Centered(loc["ending.credits.figures"], UiPalette.Accent));
            foreach (int figure in _summary.FigureIndices)
            {
                if (figure >= 0 && figure < content.Figures.Count)
                {
                    list.Widgets.Add(Centered(loc[$"figure.{content.Figures[figure].Id}"], UiPalette.Text));
                }
            }
        }

        list.Widgets.Add(new Panel { Height = 30 });
        list.Widgets.Add(Centered(loc["ending.credits.thanks"], UiPalette.TextBright));
        list.Widgets.Add(new Panel { Height = 360 });

        _credits = new ScrollViewer { Content = list, Width = PanelWidth, Height = 420 };
        layout.Widgets.Add(_credits);
        layout.Widgets.Add(UiFactory.MenuButton(loc["ending.next"], Next));
    }

    private void FinalPage(VerticalStackPanel layout)
    {
        var loc = _screens.Loc;
        string prefix = _galaxySummary is null ? "ending.final" : "ending.galaxy.final";
        layout.Widgets.Add(Centered(loc[prefix], UiPalette.TextBright));
        layout.Widgets.Add(Wrapped(loc[_replay ? prefix + ".replay" : prefix + ".desc"], UiPalette.Text));

        if (_replay)
        {
            layout.Widgets.Add(UiFactory.MenuButton(loc["ending.backToMenu"], () => _screens.ReplaceAll(new MainMenuScreen(_screens))));
            return;
        }

        layout.Widgets.Add(UiFactory.MenuButton(loc["ending.continue"], _screens.Pop));
        if (_newGamePlus is { } newGamePlus)
        {
            layout.Widgets.Add(UiFactory.MenuButton(loc["ending.newGamePlus"], newGamePlus));
        }
    }

    private static Label Centered(string text, Color color) => new()
    {
        Text = text,
        TextColor = color,
        HorizontalAlignment = HorizontalAlignment.Center,
    };

    private static Label Wrapped(string text, Color color) => new()
    {
        Text = text,
        TextColor = color,
        Wrap = true,
        Width = PanelWidth - 20,
    };
}
