using CivDle.Core.Sim;
using CivDle.Input;
using CivDle.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Myra.Graphics2D.UI;

namespace CivDle.Screens;

/// <summary>
/// Herní obrazovka: mapa s kamerou (pan WASD/šipky/tažení, zoom kolečkem ke kurzoru),
/// HUD s informacemi o světě a dlaždicí pod kurzorem, Esc otevírá pauzu.
/// Simulace tiká pevným krokem přes <see cref="FixedStepLoop"/>; když obrazovku
/// překryje pauza, Update se nevolá a simulace tím pádem stojí.
/// </summary>
public sealed class GameplayScreen : IScreen
{
    /// <summary>Rychlost posunu klávesami v pixelech obrazovky za sekundu.</summary>
    private const float PanSpeed = 700f;

    /// <summary>Násobek zoomu na jedno cvaknutí kolečka.</summary>
    private const float ZoomStep = 1.15f;

    private readonly ScreenManager _screens;
    private readonly Simulation _simulation;
    private readonly Camera2D _camera = new();
    private readonly MapRenderer _mapRenderer;
    private readonly InputManager _input = new();
    private readonly FixedStepLoop _simLoop = new(Simulation.TicksPerSecond);
    private readonly Desktop _desktop;
    private readonly Label _cursorLabel;

    public GameplayScreen(ScreenManager screens, Simulation simulation, WorldInfo info)
    {
        _screens = screens;
        _simulation = simulation;

        _mapRenderer = new MapRenderer(screens.GraphicsDevice, simulation.Map, screens.Content.Biomes, info.Seed);
        _camera.SetWorldBounds(_mapRenderer.WorldPixelWidth, _mapRenderer.WorldPixelHeight);
        var viewport = screens.GraphicsDevice.Viewport;
        _camera.SetViewport(viewport.Width, viewport.Height);
        _camera.CenterOnWorld();

        _cursorLabel = new Label();
        _desktop = new Desktop { Root = BuildHud(info) };
    }

    public bool IsOverlay => false;

    public void OnActivated() => _input.Resync();

    public void Update(GameTime gameTime)
    {
        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
        _input.Update();

        var viewport = _screens.GraphicsDevice.Viewport;
        _camera.SetViewport(viewport.Width, viewport.Height);

        if (_input.WasPressed(Keys.Escape))
        {
            _screens.Push(new PauseScreen(_screens));
            return;
        }

        UpdateCamera(dt);

        int ticks = _simLoop.Advance(gameTime.ElapsedGameTime.TotalSeconds);
        for (int i = 0; i < ticks; i++)
        {
            _simulation.Tick();
        }

        UpdateCursorLabel();
    }

    public void Draw(GameTime gameTime)
    {
        _mapRenderer.Draw(_screens.SpriteBatch, _camera);
        _desktop.Render();
    }

    public void Dispose() => _mapRenderer.Dispose();

    private void UpdateCamera(float dt)
    {
        var move = Vector2.Zero;
        if (_input.IsDown(Keys.W) || _input.IsDown(Keys.Up)) move.Y -= 1f;
        if (_input.IsDown(Keys.S) || _input.IsDown(Keys.Down)) move.Y += 1f;
        if (_input.IsDown(Keys.A) || _input.IsDown(Keys.Left)) move.X -= 1f;
        if (_input.IsDown(Keys.D) || _input.IsDown(Keys.Right)) move.X += 1f;
        if (move != Vector2.Zero)
        {
            move.Normalize();
            // Dělení zoomem: posun je konstantní v pixelech obrazovky, ne světa.
            _camera.PanWorld(move * (PanSpeed * dt / _camera.Zoom));
        }

        bool mouseOverUi = _desktop.IsMouseOverGUI;
        if ((_input.IsLeftDown || _input.IsMiddleDown) && !mouseOverUi)
        {
            _camera.Pan(_input.MouseDelta);
        }

        if (_input.ScrollDelta != 0 && !mouseOverUi)
        {
            float factor = MathF.Pow(ZoomStep, _input.ScrollDelta / 120f);
            _camera.ZoomAt(_input.MousePosition.ToVector2(), factor);
        }
    }

    private void UpdateCursorLabel()
    {
        var world = _camera.ScreenToWorld(_input.MousePosition.ToVector2());
        int tileX = (int)MathF.Floor(world.X / MapRenderer.TileSize);
        int tileY = (int)MathF.Floor(world.Y / MapRenderer.TileSize);

        var map = _simulation.Map;
        if (map.InBounds(tileX, tileY))
        {
            var biome = _screens.Content.Biomes[map.BiomeAt(tileX, tileY)];
            _cursorLabel.Text = $"Dlaždice {tileX}, {tileY} · {biome.Name}";
        }
        else
        {
            _cursorLabel.Text = string.Empty;
        }
    }

    private Widget BuildHud(WorldInfo info)
    {
        var worldInfoStack = new VerticalStackPanel { Spacing = 2 };
        worldInfoStack.Widgets.Add(new Label { Text = $"Seed: {info.Seed}" });
        worldInfoStack.Widgets.Add(new Label { Text = $"Svět: {info.PresetName} · {info.SizeName}" });
        worldInfoStack.Widgets.Add(_cursorLabel);

        var topLeft = UiFactory.DarkPanel(worldInfoStack);
        topLeft.HorizontalAlignment = HorizontalAlignment.Left;
        topLeft.VerticalAlignment = VerticalAlignment.Top;

        var help = UiFactory.DarkPanel(new Label
        {
            Text = "Posun: WASD / šipky / tažení myší · Zoom: kolečko · Esc: pauza",
        });
        help.HorizontalAlignment = HorizontalAlignment.Center;
        help.VerticalAlignment = VerticalAlignment.Bottom;

        var root = new Panel();
        root.Widgets.Add(topLeft);
        root.Widgets.Add(help);
        return root;
    }
}
