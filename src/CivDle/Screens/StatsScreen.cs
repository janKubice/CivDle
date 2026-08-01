using CivDle.Core.Sim;
using CivDle.Input;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using FontStashSharp;
using Myra.Graphics2D;
using Myra.Graphics2D.UI;
using Myra.Graphics2D.UI.Styles;

namespace CivDle.Screens;

/// <summary>
/// Moje čísla: grafy toho, jak město rostlo — obyvatelé, zástavba, bydlení,
/// spokojenost, zamoření, sídla.
///
/// <para>Proč to ve hře je: idle hra je z velké části o číslech, ale hráč z nich
/// dosud viděl jen ten <b>okamžik</b>. Křivka je něco jiného než číslo: ukáže,
/// kdy se růst zastavil, kdy spokojenost spadla a jestli to bylo tehdy, co
/// postavil hutě. Z HUD se tím stane zpětná vazba, ne jen ciferník.</para>
///
/// <para>Vrstva: čte hotové snímky z časosběru (stejný záznam, ze kterého se
/// přehrává růst) a nic nepočítá navíc — jedna cena, dvě funkce.</para>
/// </summary>
public sealed class StatsScreen : IScreen
{
    /// <summary>Kolik grafů je vedle sebe.</summary>
    private const int Columns = 2;

    private const int ChartWidth = 380;
    private const int ChartHeight = 120;
    private const int Gap = 24;

    private readonly ScreenManager _screens;
    private readonly CityHistory _history;
    private readonly InputManager _input = new();

    private readonly SpriteFontBase _font = Stylesheet.Current.LabelStyle.Font;

    private LineChart _chart = null!;
    private Desktop _desktop = null!;

    /// <summary>Řady, které se kreslí — jméno, barva a čísla z časosběru.</summary>
    private readonly List<(string TitleKey, Color Color, List<double> Values)> _series = new();

    public StatsScreen(ScreenManager screens, CityHistory history)
    {
        _screens = screens;
        _history = history;
        CollectSeries();
        BuildUi();
        _screens.Loc.LanguageChanged += BuildUi;
        _screens.UiSettingsChanged += BuildUi;
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
        _chart ??= new LineChart(_screens.WhitePixel);

        spriteBatch.Begin();
        spriteBatch.Draw(_screens.WhitePixel, new Rectangle(0, 0, viewport.Width, viewport.Height), Color.Black * 0.85f);

        int rows = (_series.Count + Columns - 1) / Columns;
        int totalWidth = Columns * ChartWidth + (Columns - 1) * Gap;
        int totalHeight = rows * (ChartHeight + Gap + 18);
        int left = (viewport.Width - totalWidth) / 2;
        int top = (viewport.Height - totalHeight) / 2 + 10;

        for (int i = 0; i < _series.Count; i++)
        {
            int column = i % Columns;
            int row = i / Columns;
            var bounds = new Rectangle(
                left + column * (ChartWidth + Gap),
                top + row * (ChartHeight + Gap + 18) + 18,
                ChartWidth,
                ChartHeight);

            // Jméno a poslední hodnota přímo nad grafem — legenda stranou by
            // nutila hráče přiřazovat barvy k názvům.
            var values = _series[i].Values;
            string label = _screens.Loc[_series[i].TitleKey];
            if (values.Count > 0)
            {
                label += "   " + Format(_series[i].TitleKey, values[^1]);
            }

            spriteBatch.DrawString(
                _font, label, new Vector2(bounds.Left, bounds.Top - 18), _series[i].Color);
            _chart.Draw(spriteBatch, bounds, values, _series[i].Color);
        }

        spriteBatch.End();
        _screens.RenderDesktop(this, _desktop);
    }

    public void Dispose()
    {
        _screens.Loc.LanguageChanged -= BuildUi;
        _screens.UiSettingsChanged -= BuildUi;
    }

    /// <summary>
    /// Vytáhne z časosběru řady k vykreslení. Dělá se to jednou při otevření —
    /// snímků jsou stovky a přepočítávat je každý snímek by bylo plýtvání.
    /// </summary>
    private void CollectSeries()
    {
        var population = new List<double>();
        var buildings = new List<double>();
        var housing = new List<double>();
        var happiness = new List<double>();
        var pollution = new List<double>();
        var settlements = new List<double>();

        for (int i = 0; i < _history.Count; i++)
        {
            var frame = _history.FrameAt(i);
            population.Add(frame.Population);
            buildings.Add(frame.Buildings);
            housing.Add(frame.HousingCapacity);
            happiness.Add(frame.Happiness);
            pollution.Add(frame.Pollution);
            settlements.Add(frame.Settlements);
        }

        _series.Add(("stats.population", new Color(120, 200, 255), population));
        _series.Add(("stats.buildings", new Color(255, 214, 120), buildings));
        _series.Add(("stats.housing", new Color(180, 160, 240), housing));
        _series.Add(("stats.happiness", new Color(140, 230, 160), happiness));
        _series.Add(("stats.pollution", new Color(230, 140, 110), pollution));
        _series.Add(("stats.settlements", new Color(150, 210, 210), settlements));
    }

    private void BuildUi()
    {
        var loc = _screens.Loc;

        var header = new VerticalStackPanel
        {
            Spacing = 4,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 20, 0, 0),
        };
        header.Widgets.Add(new Label
        {
            Text = loc["stats.title"],
            HorizontalAlignment = HorizontalAlignment.Center,
            TextColor = new Color(255, 226, 150),
        });

        // Popisek říká, co ta čísla vlastně jsou — bez něj by graf beze jména
        // vypadal jako ozdoba.
        header.Widgets.Add(new Label
        {
            Text = _history.Count == 0
                ? loc["stats.empty"]
                : loc.Format("stats.covers", DurationFormat.Human(_history.FrameAt(_history.Count - 1).Seconds)),
            HorizontalAlignment = HorizontalAlignment.Center,
            TextColor = Color.LightGray,
        });

        var bottom = new VerticalStackPanel
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(0, 0, 0, 20),
        };
        bottom.Widgets.Add(UiFactory.MenuButton(loc["panel.close"], _screens.Pop));

        var root = new Panel();
        root.Widgets.Add(header);
        root.Widgets.Add(bottom);
        _desktop = _screens.NewDesktop(root);
    }

    /// <summary>
    /// Poslední hodnota řady čitelně: podíly v procentech, počty velkými čísly.
    /// </summary>
    private static string Format(string titleKey, double value) => titleKey switch
    {
        "stats.happiness" => $"{value * 100:F0} %",
        _ => CivDle.Core.Numbers.Format(value),
    };
}
