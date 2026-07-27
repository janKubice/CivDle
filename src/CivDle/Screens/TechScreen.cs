using CivDle.Core.Sim;
using CivDle.Input;
using CivDle.Rendering;
using FontStashSharp;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Myra.Graphics2D.UI;
using Myra.Graphics2D.UI.Styles;

namespace CivDle.Screens;

/// <summary>
/// Tech tree jako SOUHVĚZDÍ: hvězdy v prstencích kolem středu (kořeny uprostřed,
/// pokročilé technologie na okraji), spojené čarami prerekvizit — vyzkoumáš jednu
/// a rozsvítí se navazující. Kreslí se přímo SpriteBatchem (Myra neumí čáry mezi
/// widgety), posouvá se tažením nebo klávesami. Simulace mezitím stojí.
///
/// Barva hvězdy nese stav: hotovo (zelená), lze vyzkoumat (zlatá, jemně pulzuje),
/// chybí suroviny (tlumená zlatá), zamčeno prerekvizitou (šedý bod). Kliknutí na
/// dostupnou hvězdu spustí výzkum, najetí ukáže bublinu s popisem <b>u kurzoru</b>.
/// U hvězdy je jen jméno — podrobnosti nese bublina, ať souhvězdí zůstane čitelné.
/// </summary>
public sealed class TechScreen : IScreen
{
    private static readonly Color ResearchedColor = new(120, 235, 165);
    private static readonly Color AvailableColor = new(255, 215, 120);
    private static readonly Color UnaffordableColor = new(160, 130, 80);
    private static readonly Color LockedColor = new(78, 88, 108);
    private static readonly Color EdgeDoneColor = new(110, 210, 150, 190);
    private static readonly Color EdgeColor = new(70, 82, 105, 130);

    private readonly ScreenManager _screens;
    private readonly Simulation _simulation;
    private readonly InputManager _input = new();
    private readonly TechGraphLayout _layout;
    private readonly SpriteFontBase _font;

    /// <summary>Meze přiblížení: oddálené souhvězdí ukáže celý strom, přiblížené popisky.</summary>
    private const float MinZoom = 0.28f;
    private const float MaxZoom = 1.4f;

    /// <summary>Pod tímhle přiblížením se jména vynechají — slila by se do šmouhy.</summary>
    private const float LabelZoom = 0.5f;

    private Desktop _desktop = null!;
    private Vector2 _pan;
    private float _zoom = 1f;
    private bool _dragging;
    private Point _dragOrigin;
    private Vector2 _panOrigin;
    private int _hovered = -1;

    public TechScreen(ScreenManager screens, Simulation simulation)
    {
        _screens = screens;
        _simulation = simulation;
        _layout = new TechGraphLayout(screens.Content.Techs);
        _font = Stylesheet.Current.LabelStyle.Font;
        BuildUi();
        _screens.Loc.LanguageChanged += BuildUi;
        _screens.UiSettingsChanged += BuildUi;

        // Start u prvního nevyzkoumaného uzlu, ať hráč hned vidí, kam pokračovat.
        CenterOnNextTech();
    }

    public bool IsOverlay => true;

    public void OnActivated() => _input.Resync();

    public void Update(GameTime gameTime)
    {
        _input.Update();
        if (_input.WasPressed(Keys.Escape))
        {
            _screens.Pop();
            return;
        }

        var viewport = _screens.GraphicsDevice.Viewport;
        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
        const float keyboardPanSpeed = 600f;
        if (_input.IsDown(Keys.A) || _input.IsDown(Keys.Left)) _pan.X += keyboardPanSpeed * dt;
        if (_input.IsDown(Keys.D) || _input.IsDown(Keys.Right)) _pan.X -= keyboardPanSpeed * dt;
        if (_input.IsDown(Keys.W) || _input.IsDown(Keys.Up)) _pan.Y += keyboardPanSpeed * dt;
        if (_input.IsDown(Keys.S) || _input.IsDown(Keys.Down)) _pan.Y -= keyboardPanSpeed * dt;

        bool overUi = _desktop.IsMouseOverGUI;
        var mouse = _input.MousePosition;

        // Kolečko přibližuje KE KURZORU — hráč si oddálí celé souhvězdí, najde větev
        // a přiblíží se zpět, aniž by ztratil místo, kam koukal.
        if (_input.ScrollDelta != 0 && !overUi)
        {
            var canvasUnderCursor = (mouse.ToVector2() - _pan) / _zoom;
            _zoom = Math.Clamp(_zoom * MathF.Pow(1.0015f, _input.ScrollDelta), MinZoom, MaxZoom);
            _pan = mouse.ToVector2() - canvasUnderCursor * _zoom;
        }

        // Tažení levým tlačítkem posouvá plátno; krátký klik (bez tažení) je výzkum.
        if (_input.WasLeftPressed && !overUi)
        {
            _dragging = true;
            _dragOrigin = mouse;
            _panOrigin = _pan;
        }

        if (_dragging && _input.IsLeftDown)
        {
            _pan = _panOrigin + new Vector2(mouse.X - _dragOrigin.X, mouse.Y - _dragOrigin.Y);
        }

        if (_input.WasLeftReleased)
        {
            bool wasClick = _dragging
                && Math.Abs(mouse.X - _dragOrigin.X) < 5
                && Math.Abs(mouse.Y - _dragOrigin.Y) < 5;
            _dragging = false;
            if (wasClick && !overUi)
            {
                int hit = NodeAt(mouse);
                if (hit >= 0)
                {
                    _simulation.TryResearch(hit); // neúspěch (drahé/zamčené) se jen neprojeví
                }
            }
        }

        ClampPan(viewport);
        _hovered = overUi ? -1 : NodeAt(mouse);
    }

    public void Draw(GameTime gameTime)
    {
        var viewport = _screens.GraphicsDevice.Viewport;
        var spriteBatch = _screens.SpriteBatch;
        var pixel = _screens.WhitePixel;
        var techs = _screens.Content.Techs;
        var loc = _screens.Loc;

        spriteBatch.Begin();
        spriteBatch.Draw(pixel, new Rectangle(0, 0, viewport.Width, viewport.Height), new Color(6, 8, 16) * 0.92f);
        DrawStarfield(spriteBatch, pixel, viewport);

        // Nejdřív spojnice souhvězdí (pod hvězdami) — vazby čtou jako pozadí.
        for (int i = 0; i < techs.Count; i++)
        {
            var to = Shift(_layout.Center(i));
            foreach (int prereq in techs[i].PrerequisiteIndices)
            {
                bool done = _simulation.IsTechResearched(prereq);
                DrawLine(spriteBatch, pixel, Shift(_layout.Center(prereq)), to,
                    done ? EdgeDoneColor : EdgeColor, (done ? 2f : 1f) * Math.Max(_zoom, 0.6f));
            }
        }

        // Pulz zvýraznění dostupných hvězd — oko samo najde, kam pokračovat.
        float pulse = 0.75f + 0.25f * MathF.Sin((float)gameTime.TotalGameTime.TotalSeconds * 2.4f);
        float star = TechGraphLayout.StarSize * _zoom;

        for (int i = 0; i < techs.Count; i++)
        {
            var center = Shift(_layout.Center(i));
            float margin = TechGraphLayout.HitSize * _zoom;
            if (center.X < -margin || center.X > viewport.Width + margin
                || center.Y < -margin || center.Y > viewport.Height + margin)
            {
                continue; // culling — u velkého souhvězdí se vyplatí
            }

            bool researched = _simulation.IsTechResearched(i);
            var status = _simulation.CanResearch(i);
            var color = researched ? ResearchedColor
                : status == PlacementResult.Ok ? AvailableColor
                : status == PlacementResult.NotEnoughResources ? UnaffordableColor
                : LockedColor;

            bool hovered = i == _hovered;
            bool beckons = !researched && status == PlacementResult.Ok;

            // Na co mám, to PULZUJE a má prstenec. Na co nemám suroviny, jen tiše
            // svítí. Zamčené je matný bod. Rozdíl musí být vidět bez čtení textu.
            bool affordableSoon = !researched && status == PlacementResult.NotEnoughResources;
            float halo = hovered ? 1.4f : beckons ? pulse : affordableSoon ? 0.9f : 0.7f;

            // Hvězda = zář + kosočtvercové jádro (pixel otočený o 45°); zamčené uzly
            // jsou jen matné body, aby souhvězdí vedlo oko k tomu, co jde vyzkoumat.
            DrawDiamond(spriteBatch, pixel, center, star * 2.4f * halo, color * 0.22f);
            DrawDiamond(spriteBatch, pixel, center, star * 1.5f * halo, color * 0.4f);
            DrawDiamond(spriteBatch, pixel, center, star * halo, color);
            // Prstenec kolem dostupné hvězdy — nejsilnější signál „tohle si můžeš vzít".
            if (beckons)
            {
                DrawRing(spriteBatch, pixel, center, star * 1.9f * pulse, AvailableColor * 0.9f);
            }

            if (hovered)
            {
                DrawDiamond(spriteBatch, pixel, center, star * 0.45f, Color.White);
            }

            // Jméno pod hvězdou; zbytek (popis, cena, chybějící prerekvizity) nese
            // bublina u kurzoru — tabulka u každé hvězdy by souhvězdí zaplevelila.
            // Při silném oddálení se jména vypustí, aby zbyl čitelný obrazec hvězd.
            if (_zoom < LabelZoom && !hovered)
            {
                continue;
            }

            string name = loc[techs[i].NameKey];
            var size = _font.MeasureString(name) * _zoom;
            var labelColor = researched ? new Color(170, 225, 190)
                : status == PlacementResult.NotUnlocked ? new Color(120, 128, 145)
                : Color.White;
            spriteBatch.DrawString(_font, name,
                new Vector2(center.X - size.X / 2f, center.Y + star * 1.1f),
                hovered ? Color.White : labelColor,
                0f, Vector2.Zero, new Vector2(_zoom, _zoom));
        }

        spriteBatch.End();

        _desktop.Render();

        // Detail uzlu až nad UI (ať nezmizí pod panelem) a U KURZORU — hráč nemá
        // očima skákat na spodek obrazovky, aby zjistil, na co kouká.
        if (_hovered >= 0)
        {
            var tech = techs[_hovered];
            bool researched = _simulation.IsTechResearched(_hovered);
            var status = _simulation.CanResearch(_hovered);
            // Nejdřív VERDIKT („můžeš / nemáš na to / zamčené"), teprve pak popis.
            // Dřív musel hráč cenu porovnávat sám a nevěděl, na čem je.
            string verdict = researched
                ? loc["tech.researched"]
                : status switch
                {
                    PlacementResult.Ok => loc["tech.canResearch"],
                    PlacementResult.NotUnlocked => loc.Format("tech.needs", PrerequisiteNames(_hovered)),
                    _ => loc["tech.tooExpensive"],
                };

            string body = verdict + '\n' + loc[tech.DescriptionKey];
            if (!researched)
            {
                body += '\n' + loc.Format("panel.cost", CostFormat.Line(_screens.Content, loc, tech.Cost));
            }

            HoverTooltip.Draw(spriteBatch, pixel, _font, viewport, _input.MousePosition,
                loc[tech.NameKey], body,
                researched ? new Color(150, 235, 175)
                    : status == PlacementResult.Ok ? new Color(255, 215, 120)
                    : status == PlacementResult.NotEnoughResources ? new Color(235, 170, 110)
                    : new Color(160, 168, 184));
        }
    }

    /// <summary>Jména chybějících prerekvizit — u zamčeného uzlu je to jediná užitečná informace.</summary>
    private string PrerequisiteNames(int techIndex)
    {
        var loc = _screens.Loc;
        var techs = _screens.Content.Techs;
        var missing = techs[techIndex].PrerequisiteIndices
            .Where(p => !_simulation.IsTechResearched(p))
            .Select(p => loc[techs[p].NameKey]);
        return string.Join(", ", missing);
    }

    public void Dispose()
    {
        _screens.Loc.LanguageChanged -= BuildUi;
        _screens.UiSettingsChanged -= BuildUi;
    }

    /// <summary>Plátno → obrazovka (přiblížení a posun).</summary>
    private Vector2 Shift(Vector2 point) => point * _zoom + _pan;

    /// <summary>
    /// Kosočtverec = pixel otočený o 45° kolem svého středu. Levná „hvězda" bez
    /// vlastní textury — vrstvením průhledných kosočtverců vznikne zář.
    /// </summary>
    private static void DrawDiamond(SpriteBatch spriteBatch, Texture2D pixel, Vector2 center, float size, Color color)
    {
        spriteBatch.Draw(pixel, center, null, color, MathHelper.PiOver4,
            new Vector2(0.5f, 0.5f), new Vector2(size, size), SpriteEffects.None, 0f);
    }

    /// <summary>Tenký prstenec kolem hvězdy — vyznačí, co jde vyzkoumat hned teď.</summary>
    private static void DrawRing(SpriteBatch spriteBatch, Texture2D pixel, Vector2 center, float radius, Color color)
    {
        const int Segments = 12;
        for (int i = 0; i < Segments; i++)
        {
            float angle = i * MathHelper.TwoPi / Segments;
            var point = center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius;
            DrawDiamond(spriteBatch, pixel, point, radius * 0.22f, color);
        }
    }

    /// <summary>
    /// Statické hvězdné pozadí: deterministicky rozseté body v souřadnicích plátna,
    /// takže se s posunem hýbou spolu se souhvězdím. Nic se neukládá — pozice jsou
    /// funkce indexu (stejný trik jako u terénu).
    /// </summary>
    private void DrawStarfield(SpriteBatch spriteBatch, Texture2D pixel, Viewport viewport)
    {
        const int Count = 260;
        int span = Math.Max(_layout.Width, _layout.Height);
        for (int i = 0; i < Count; i++)
        {
            uint hash = (uint)HashCode.Combine(i, 0x5EED);
            float x = (hash % 65521u) / 65521f * span;
            float y = ((hash >> 11) % 65521u) / 65521f * span;
            var point = Shift(new Vector2(x, y));
            if (point.X < 0 || point.X > viewport.Width || point.Y < 0 || point.Y > viewport.Height)
            {
                continue;
            }

            float brightness = 0.12f + (hash >> 24) / 255f * 0.28f;
            float size = 1f + (hash >> 22 & 1);
            spriteBatch.Draw(pixel, new Rectangle((int)point.X, (int)point.Y, (int)size, (int)size),
                Color.White * brightness);
        }
    }

    private int NodeAt(Point screen)
    {
        // Hit test v souřadnicích plátna — jinak by se musel škálovat každý obdélník.
        var canvas = (screen.ToVector2() - _pan) / _zoom;
        var techs = _screens.Content.Techs;
        for (int i = 0; i < techs.Count; i++)
        {
            if (_layout.Bounds(i).Contains((int)canvas.X, (int)canvas.Y))
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>Posun drží souhvězdí v dohledu — nesmí se „utéct" mimo obrazovku.</summary>
    private void ClampPan(Viewport viewport)
    {
        _pan.X = ClampAxis(_pan.X, viewport.Width, (int)(_layout.Width * _zoom));
        _pan.Y = ClampAxis(_pan.Y, viewport.Height, (int)(_layout.Height * _zoom));
    }

    /// <summary>Plátno menší než okno se vycentruje; větší se posouvá jen v rámci svých okrajů.</summary>
    private static float ClampAxis(float pan, int viewportSize, int canvasSize) =>
        canvasSize <= viewportSize
            ? (viewportSize - canvasSize) / 2f
            : Math.Clamp(pan, viewportSize - canvasSize, 0f);

    /// <summary>Nascrolluje na první nevyzkoumanou dostupnou technologii (kam pokračovat).</summary>
    private void CenterOnNextTech()
    {
        var techs = _screens.Content.Techs;
        var viewport = _screens.GraphicsDevice.Viewport;
        for (int i = 0; i < techs.Count; i++)
        {
            if (!_simulation.IsTechResearched(i) && _simulation.CanResearch(i) != PlacementResult.NotUnlocked)
            {
                CenterOn(_layout.Center(i), viewport);
                return;
            }
        }

        // Vše vyzkoumáno (nebo prázdný strom) — ukaž souhvězdí jako celek.
        CenterOn(new Vector2(_layout.Width / 2f, _layout.Height / 2f), viewport);
    }

    private void CenterOn(Vector2 canvasPoint, Viewport viewport)
    {
        _pan = new Vector2(viewport.Width / 2f, viewport.Height / 2f) - canvasPoint * _zoom;
        ClampPan(viewport);
    }

    private static void DrawLine(SpriteBatch spriteBatch, Texture2D pixel, Vector2 from, Vector2 to, Color color, float thickness)
    {
        var delta = to - from;
        float length = delta.Length();
        if (length < 0.01f)
        {
            return;
        }

        float angle = MathF.Atan2(delta.Y, delta.X);
        spriteBatch.Draw(pixel, from, null, color, angle, Vector2.Zero, new Vector2(length, thickness), SpriteEffects.None, 0f);
    }

    private void BuildUi()
    {
        var loc = _screens.Loc;

        var header = new VerticalStackPanel
        {
            Spacing = 4,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Top,
        };
        header.Widgets.Add(new Label { Text = loc["hud.tech"], HorizontalAlignment = HorizontalAlignment.Center });
        header.Widgets.Add(new Label
        {
            Text = loc["tech.graphHelp"],
            HorizontalAlignment = HorizontalAlignment.Center,
            TextColor = Color.LightGray,
        });

        var headerPanel = UiFactory.DarkPanel(header);
        headerPanel.HorizontalAlignment = HorizontalAlignment.Center;
        headerPanel.VerticalAlignment = VerticalAlignment.Top;

        var close = UiFactory.MenuButton(loc["panel.close"], _screens.Pop);
        close.HorizontalAlignment = HorizontalAlignment.Center;
        close.VerticalAlignment = VerticalAlignment.Bottom;

        var root = new Panel();
        root.Widgets.Add(headerPanel);
        root.Widgets.Add(close);
        _desktop = _screens.NewDesktop(root);
    }
}
