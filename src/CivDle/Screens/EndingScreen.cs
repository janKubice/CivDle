using CivDle.Core;
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
/// <para>Vrstva: UI. Ze simulace jen čte (<see cref="EndingSummary"/>);
/// sekvence se dá pustit znovu z menu a nesmí na hru sáhnout.</para>
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
    private readonly InputManager _input = new();
    private Desktop _desktop = null!;
    private Page _page = Page.Title;
    private bool _waitingForChild;
    private ScrollViewer? _credits;
    private float _creditsOffset;

    /// <param name="simulation">Hra, jejíž konec se ukazuje (jen se čte).</param>
    /// <param name="replay">Přehrává se z menu — na konci „zpět do menu", ne „pokračovat".</param>
    /// <param name="newGamePlus">Nová hra+ (jen při prvním dohrání); null = nenabízí se.</param>
    public EndingScreen(ScreenManager screens, Simulation simulation, bool replay, Action? newGamePlus)
    {
        _screens = screens;
        _simulation = simulation;
        _summary = EndingSummary.Of(simulation);
        _replay = replay;
        _newGamePlus = newGamePlus;
        BuildUi();
        _screens.Loc.LanguageChanged += BuildUi;
    }

    public bool IsOverlay => false;

    public void OnActivated()
    {
        _input.Resync();

        // Vrátili jsme se z časosběru nebo kroniky — pokračuje se dál.
        if (_waitingForChild)
        {
            _waitingForChild = false;
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

    public void Dispose() => _screens.Loc.LanguageChanged -= BuildUi;

    /// <summary>Další stránka; časosběr a kronika se otevřou jako vlastní obrazovky.</summary>
    private void Next()
    {
        _page++;
        switch (_page)
        {
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

    private void BuildUi()
    {
        var layout = new VerticalStackPanel { Spacing = 12, Width = PanelWidth };
        switch (_page)
        {
            case Page.Title:
                TitlePage(layout);
                break;
            case Page.Stats:
                StatsPage(layout);
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
        layout.Widgets.Add(Centered(loc["ending.title"], UiPalette.TextBright));
        layout.Widgets.Add(Wrapped(loc["ending.subtitle"], UiPalette.Text));
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
        layout.Widgets.Add(Centered(loc["ending.final"], UiPalette.TextBright));
        layout.Widgets.Add(Wrapped(loc[_replay ? "ending.final.replay" : "ending.final.desc"], UiPalette.Text));

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
