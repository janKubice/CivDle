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
/// Tech tree jako GRAF („síť"): uzly rozmístěné po sloupcích podle hloubky
/// závislostí, propojené čarami prerekvizit — vyzkoumáš jeden a otevřou se
/// navazující. Kreslí se přímo SpriteBatchem (Myra neumí čáry mezi widgety),
/// posouvá se tažením nebo klávesami. Simulace mezitím stojí.
///
/// Barva uzlu nese stav: hotovo (zelená), lze vyzkoumat (zlatá), chybí suroviny
/// (tlumená zlatá), zamčeno prerekvizitou (šedá). Kliknutí na dostupný uzel
/// spustí výzkum.
/// </summary>
public sealed class TechScreen : IScreen
{
    private static readonly Color ResearchedColor = new(60, 130, 80);
    private static readonly Color AvailableColor = new(150, 120, 45);
    private static readonly Color UnaffordableColor = new(88, 76, 44);
    private static readonly Color LockedColor = new(46, 52, 64);
    private static readonly Color EdgeDoneColor = new(110, 200, 130, 200);
    private static readonly Color EdgeColor = new(90, 100, 120, 160);

    private readonly ScreenManager _screens;
    private readonly Simulation _simulation;
    private readonly InputManager _input = new();
    private readonly TechGraphLayout _layout;
    private readonly SpriteFontBase _font;

    private Desktop _desktop = null!;
    private Vector2 _pan;
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
        spriteBatch.Draw(pixel, new Rectangle(0, 0, viewport.Width, viewport.Height), Color.Black * 0.78f);

        // Nejdřív hrany (pod uzly), ať síť čte jako pozadí vazeb.
        for (int i = 0; i < techs.Count; i++)
        {
            var target = Shift(_layout.Bounds(i));
            foreach (int prereq in techs[i].PrerequisiteIndices)
            {
                var source = Shift(_layout.Bounds(prereq));
                var from = new Vector2(source.Right, source.Center.Y);
                var to = new Vector2(target.Left, target.Center.Y);
                DrawLine(spriteBatch, pixel, from, to,
                    _simulation.IsTechResearched(prereq) ? EdgeDoneColor : EdgeColor,
                    _simulation.IsTechResearched(prereq) ? 3f : 2f);
            }
        }

        for (int i = 0; i < techs.Count; i++)
        {
            var box = Shift(_layout.Bounds(i));
            if (box.Right < 0 || box.Left > viewport.Width || box.Bottom < 0 || box.Top > viewport.Height)
            {
                continue; // culling — u velkého stromu se vyplatí
            }

            bool researched = _simulation.IsTechResearched(i);
            var status = _simulation.CanResearch(i);
            var fill = researched ? ResearchedColor
                : status == PlacementResult.Ok ? AvailableColor
                : status == PlacementResult.NotEnoughResources ? UnaffordableColor
                : LockedColor;

            spriteBatch.Draw(pixel, box, fill);

            // Zvýraznění pod kurzorem + rámeček.
            var border = i == _hovered ? Color.White : new Color(20, 24, 32);
            DrawBorder(spriteBatch, pixel, box, border, i == _hovered ? 2 : 1);

            var tech = techs[i];
            string name = loc[tech.NameKey];
            spriteBatch.DrawString(_font, name, new Vector2(box.X + 8, box.Y + 6), Color.White);

            // Druhý řádek: pasivní bonus, nebo cena — to hráče zajímá při rozhodování.
            string second = researched
                ? loc["tech.researched"]
                : CostFormat.Line(_screens.Content, loc, tech.Cost);
            spriteBatch.DrawString(_font, second, new Vector2(box.X + 8, box.Y + 30),
                researched ? new Color(190, 240, 200) : new Color(225, 225, 235));
        }

        spriteBatch.End();

        _desktop.Render();

        // Detail zvoleného uzlu až nad UI, ať nezmizí pod panelem.
        if (_hovered >= 0)
        {
            DrawTooltip(spriteBatch, pixel, techs[_hovered].DescriptionKey);
        }
    }

    public void Dispose() => _screens.Loc.LanguageChanged -= BuildUi;

    private Rectangle Shift(Rectangle bounds) =>
        new(bounds.X + (int)_pan.X, bounds.Y + (int)_pan.Y, bounds.Width, bounds.Height);

    private int NodeAt(Point screen)
    {
        var techs = _screens.Content.Techs;
        for (int i = 0; i < techs.Count; i++)
        {
            if (Shift(_layout.Bounds(i)).Contains(screen))
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>Posun drží graf v dohledu — nesmí se „utéct" mimo obrazovku.</summary>
    private void ClampPan(Viewport viewport)
    {
        float minX = Math.Min(0, viewport.Width - _layout.Width - 40);
        float minY = Math.Min(0, viewport.Height - _layout.Height - 120);
        _pan.X = Math.Clamp(_pan.X, minX, 40);
        _pan.Y = Math.Clamp(_pan.Y, minY, 80);
    }

    /// <summary>Nascrolluje na první nevyzkoumanou dostupnou technologii (kam pokračovat).</summary>
    private void CenterOnNextTech()
    {
        var techs = _screens.Content.Techs;
        var viewport = _screens.GraphicsDevice.Viewport;
        for (int i = 0; i < techs.Count; i++)
        {
            if (!_simulation.IsTechResearched(i) && _simulation.CanResearch(i) != PlacementResult.NotUnlocked)
            {
                var box = _layout.Bounds(i);
                _pan = new Vector2(viewport.Width / 2f - box.Center.X, viewport.Height / 2f - box.Center.Y);
                ClampPan(viewport);
                return;
            }
        }
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

    private static void DrawBorder(SpriteBatch spriteBatch, Texture2D pixel, Rectangle box, Color color, int thickness)
    {
        spriteBatch.Draw(pixel, new Rectangle(box.X, box.Y, box.Width, thickness), color);
        spriteBatch.Draw(pixel, new Rectangle(box.X, box.Bottom - thickness, box.Width, thickness), color);
        spriteBatch.Draw(pixel, new Rectangle(box.X, box.Y, thickness, box.Height), color);
        spriteBatch.Draw(pixel, new Rectangle(box.Right - thickness, box.Y, thickness, box.Height), color);
    }

    private void DrawTooltip(SpriteBatch spriteBatch, Texture2D pixel, string descriptionKey)
    {
        var viewport = _screens.GraphicsDevice.Viewport;
        string text = _screens.Loc[descriptionKey];
        var size = _font.MeasureString(text);
        var box = new Rectangle(
            (int)(viewport.Width / 2f - size.X / 2f) - 12,
            viewport.Height - 96,
            (int)size.X + 24,
            (int)size.Y + 16);

        spriteBatch.Begin();
        spriteBatch.Draw(pixel, box, new Color(18, 22, 30, 240));
        DrawBorder(spriteBatch, pixel, box, new Color(90, 100, 120), 1);
        spriteBatch.DrawString(_font, text, new Vector2(box.X + 12, box.Y + 8), Color.White);
        spriteBatch.End();
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
        _desktop = new Desktop { Root = root };
    }
}
