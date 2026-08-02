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

    private const int Gap = 22;

    /// <summary>Vnitřní okraje karty: nahoře místo na titulek, dole vzduch.</summary>
    private const int CardPadX = 14;
    private const int CardPadTop = 34;
    private const int CardPadBottom = 14;

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

        // Velikost karet podle obrazovky: na velkém monitoru se grafy roztáhnou,
        // na Steam Decku (800 px) se všech šest pořád vejde bez scrollování.
        int rows = (_series.Count + Columns - 1) / Columns;
        int cardWidth = Math.Clamp((viewport.Width - (Columns + 1) * Gap) / Columns, 340, 620);
        int cardHeight = Math.Clamp((viewport.Height - 170 - (rows + 1) * Gap) / Math.Max(1, rows), 120, 240);

        int totalWidth = Columns * cardWidth + (Columns - 1) * Gap;
        int totalHeight = rows * cardHeight + (rows - 1) * Gap;
        int left = (viewport.Width - totalWidth) / 2;
        int top = (viewport.Height - totalHeight) / 2 + 14;

        for (int i = 0; i < _series.Count; i++)
        {
            var (titleKey, color, values) = _series[i];
            var card = new Rectangle(
                left + i % Columns * (cardWidth + Gap),
                top + i / Columns * (cardHeight + Gap),
                cardWidth,
                cardHeight);

            // Karta s rámečkem — grafy nalepené přímo na ztmavené hře vypadaly
            // jako ladicí výpis, ne jako obrazovka pro hráče.
            spriteBatch.Draw(_screens.WhitePixel, card, new Color(24, 29, 38, 245));
            DrawBorder(spriteBatch, card, new Color(62, 72, 88));

            // Titulek vlevo, poslední hodnota vpravo a bíle — to je číslo, které
            // hráč hledá nejdřív. Legenda stranou by nutila přiřazovat barvy.
            spriteBatch.DrawString(
                _font, _screens.Loc[titleKey], new Vector2(card.Left + CardPadX, card.Top + 9), color);
            if (values.Count > 0)
            {
                string value = Format(titleKey, values[^1]);
                float valueWidth = _font.MeasureString(value).X;
                spriteBatch.DrawString(
                    _font, value, new Vector2(card.Right - CardPadX - valueWidth, card.Top + 9), Color.White);
            }

            var bounds = new Rectangle(
                card.Left + CardPadX,
                card.Top + CardPadTop,
                cardWidth - 2 * CardPadX,
                cardHeight - CardPadTop - CardPadBottom);
            _chart.Draw(spriteBatch, bounds, values, color);

            // Maximum osy u horního okraje — bez měřítka je křivka jen ozdoba.
            var (_, max) = LineChart.RangeOf(values);
            spriteBatch.DrawString(
                _font, Format(titleKey, max), new Vector2(bounds.Left + 4, bounds.Top + 2), new Color(118, 128, 144));
        }

        spriteBatch.End();
        _screens.RenderDesktop(this, _desktop);
    }

    /// <summary>Jednopixelový rámeček — Myra tu není, karty se kreslí přímo.</summary>
    private void DrawBorder(SpriteBatch spriteBatch, Rectangle r, Color color)
    {
        spriteBatch.Draw(_screens.WhitePixel, new Rectangle(r.Left, r.Top, r.Width, 1), color);
        spriteBatch.Draw(_screens.WhitePixel, new Rectangle(r.Left, r.Bottom - 1, r.Width, 1), color);
        spriteBatch.Draw(_screens.WhitePixel, new Rectangle(r.Left, r.Top, 1, r.Height), color);
        spriteBatch.Draw(_screens.WhitePixel, new Rectangle(r.Right - 1, r.Top, 1, r.Height), color);
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
