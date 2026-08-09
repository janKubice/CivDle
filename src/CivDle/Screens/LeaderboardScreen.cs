using CivDle.Core;
using CivDle.Core.Platform;
using CivDle.Core.Sim;
using CivDle.Input;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Myra.Graphics2D;
using Myra.Graphics2D.Brushes;
using Myra.Graphics2D.UI;

namespace CivDle.Screens;

/// <summary>
/// Žebříčky — „jak si stojím" napříč kategoriemi.
///
/// <para>Ukazuje u každé kategorie tři věci vedle sebe: <b>kolik má hráč teď</b>,
/// <b>jeho rekord</b> a <b>špičku</b>. Bez prostředního sloupce by běžící partie
/// vypadala jako jediná, která kdy byla; bez prvního by nebylo vidět, jak daleko
/// je do rekordu.</para>
///
/// <para>Bez Steamu je „špička" hráčův vlastní rekord — a je to tak napsané.
/// Prázdná tabulka s hláškou „nepřipojeno" je horší než poctivá osobní.</para>
///
/// <para>Vrstva: čte ze simulace a z platformy, nezapisuje do simulace.</para>
/// </summary>
public sealed class LeaderboardScreen : IScreen
{
    private const int PanelWidth = 720;
    private const int RowHeight = 30;

    private readonly ScreenManager _screens;
    private readonly Simulation _simulation;
    private readonly InputManager _input = new();
    private Desktop _desktop = null!;

    public LeaderboardScreen(ScreenManager screens, Simulation simulation)
    {
        _screens = screens;
        _simulation = simulation;

        // Otevření je nejlepší chvíle, kdy poslat aktuální stav: hráč se právě
        // ptá „jak si stojím", takže čísla musí být čerstvá.
        PlatformCatalog.PushStats(_screens.Platform, _simulation);
        PlatformCatalog.PushScores(_screens.Platform, _simulation);
        _screens.Platform.Flush();

        BuildUi();
        _screens.Loc.LanguageChanged += BuildUi;
    }

    public bool IsOverlay => true;

    public void OnActivated() => _input.Resync();

    public void Update(GameTime gameTime)
    {
        _input.Update();
        if (_input.WasPressed(Keys.Escape))
        {
            _screens.Pop();
        }
    }

    public void Draw(GameTime gameTime)
    {
        var viewport = _screens.GraphicsDevice.Viewport;
        var spriteBatch = _screens.SpriteBatch;
        spriteBatch.Begin();
        spriteBatch.Draw(_screens.WhitePixel, new Rectangle(0, 0, viewport.Width, viewport.Height), Color.Black * 0.66f);
        spriteBatch.End();

        _screens.RenderDesktop(this, _desktop);
    }

    public void Dispose() => _screens.Loc.LanguageChanged -= BuildUi;

    private void BuildUi()
    {
        var loc = _screens.Loc;
        var layout = new VerticalStackPanel
        {
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };

        layout.Widgets.Add(new Label
        {
            Text = loc["board.title"],
            TextColor = UiPalette.TextBright,
            HorizontalAlignment = HorizontalAlignment.Center,
        });

        // Řekni rovnou, odkud čísla jsou. Hráč jinak neví, jestli se poměřuje
        // se světem, nebo sám se sebou.
        layout.Widgets.Add(new Label
        {
            Text = loc[_screens.Platform.HasOnlineLeaderboards && _screens.Platform.LeaderboardsAllowed
                ? "board.sourceOnline"
                : "board.sourceLocal"],
            TextColor = UiPalette.Text,
            Wrap = true,
            Width = PanelWidth,
            HorizontalAlignment = HorizontalAlignment.Center,
        });

        layout.Widgets.Add(HeaderRow());

        var list = new VerticalStackPanel { Spacing = 4 };
        foreach (var board in PlatformCatalog.Leaderboards)
        {
            list.Widgets.Add(BoardRow(board));
        }

        layout.Widgets.Add(new ScrollViewer { Content = list, Height = 360, Width = PanelWidth });
        layout.Widgets.Add(UiFactory.MenuButton(loc["panel.close"], _screens.Pop));

        var panel = UiFactory.DarkPanel(layout);
        panel.HorizontalAlignment = HorizontalAlignment.Center;
        panel.VerticalAlignment = VerticalAlignment.Center;

        var root = new Panel();
        root.Widgets.Add(panel);
        _desktop = _screens.NewDesktop(root);
    }

    private Widget HeaderRow()
    {
        var loc = _screens.Loc;
        var row = new HorizontalStackPanel { Spacing = 0, Width = PanelWidth };
        row.Widgets.Add(Cell(loc["board.category"], 300, UiPalette.TextDim));
        row.Widgets.Add(Cell(loc["board.now"], 140, UiPalette.TextDim));
        row.Widgets.Add(Cell(loc["board.best"], 140, UiPalette.TextDim));
        row.Widgets.Add(Cell(loc["board.top"], 140, UiPalette.TextDim));
        return row;
    }

    private Widget BoardRow(LeaderboardDef board)
    {
        var row = new HorizontalStackPanel
        {
            Spacing = 0,
            Width = PanelWidth - 24,
            Height = RowHeight,
            Padding = new Thickness(8, 4),
            Background = new SolidBrush(UiPalette.Panel),
        };

        long current = CurrentValue(board.Id);
        long? best = _screens.Platform.PersonalBest(board.Id);
        var top = _screens.Platform.TopScores(board.Id, 1);

        // Zelená, když se hráč právě dotahuje na svůj rekord — u vzestupných
        // (rychlostních) žebříčků je „lepší" naopak nižší.
        bool atBest = best.HasValue && (board.Ascending ? current <= best.Value : current >= best.Value);

        row.Widgets.Add(Cell(_screens.Loc[board.LabelKey], 300, Color.White));
        row.Widgets.Add(Cell(Format(current, board), 140, atBest ? UiPalette.Good : Color.LightGray));
        row.Widgets.Add(Cell(best.HasValue ? Format(best.Value, board) : "—", 140, UiPalette.TextBright));
        row.Widgets.Add(Cell(top.Count > 0 ? Format(top[0].Score, board) : "—", 140, UiPalette.Accent));
        return row;
    }

    private static Label Cell(string text, int width, Color color) => new()
    {
        Text = text,
        TextColor = color,
        Width = width,
        VerticalAlignment = VerticalAlignment.Center,
    };

    /// <summary>Čas se ukazuje jako čas, počty jako zkrácená velká čísla.</summary>
    private static string Format(long value, LeaderboardDef board) =>
        board.IsTime
            ? DurationFormat.Human(value / 1000.0)
            : Numbers.Format(value);

    /// <summary>
    /// Co má hráč právě teď. Musí sedět s tím, co posílá
    /// <see cref="PlatformCatalog.PushScores"/> — jinak by sloupec „teď"
    /// ukazoval něco jiného, než co se odešle.
    /// </summary>
    private long CurrentValue(string boardId) => boardId switch
    {
        "LB_TOTAL_POWER" => (long)Math.Min(_simulation.TotalPower(), long.MaxValue / 2),
        "LB_PEAK_POPULATION" => Math.Max(_simulation.PeakPopulation, (long)_simulation.Population),
        "LB_ASCENSIONS" => _simulation.AscensionLevel,
        "LB_LEGACIES" => _simulation.LegacyDepth,
        "LB_GRAND_WORK" => _simulation.GrandWorkStage,
        "LB_BUILDINGS" => _simulation.Buildings.Length,
        "LB_CITIES_ABSORBED" => _simulation.CitiesJoined,
        "LB_TILES_EXPLORED" => _simulation.Fog.ExploredChunks,
        _ => 0,
    };
}
