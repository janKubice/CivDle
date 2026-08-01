using CivDle.Core.Sim;
using CivDle.Core.World;
using CivDle.Input;
using CivDle.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Myra.Graphics2D;
using Myra.Graphics2D.UI;

namespace CivDle.Screens;

/// <summary>
/// Časosběr: přehrávka celého růstu města od první chalupy po metropoli —
/// nad skutečným terénem, v barvách skutečné zástavby, s kamerou jako ve hře.
///
/// <para>Proč to ve hře je: hráč staví hodiny a pak Vzestupem všechno smaže.
/// Zůstane mu číslo v bilanci — ale ne <b>obraz</b> toho, co postavil.</para>
///
/// <para>Terén se nekreslí ze snímků — rekonstruuje se ze seedu, stejně jako
/// při načtení savu. Kronika si tak nese jen barevné buňky zástavby (pár kB
/// na snímek) a pozadí je přesto bit za bitem to, na kterém hráč hrál.</para>
///
/// <para>Vrstva: čte hotové snímky a nic nepočítá ani nemění. Kamera je vlastní
/// instance <see cref="Camera2D"/> — stejné ovládání jako ve hře (tažení pravým
/// či prostředním tlačítkem, kolečko, WASD), ale nezávislá na té herní, takže
/// prohlížení časosběru nehne s pohledem rozehrané hry.</para>
/// </summary>
public sealed class TimelapseScreen : IScreen
{
    /// <summary>Kolik snímků za sekundu se přehrává.</summary>
    private const float FramesPerSecond = 12f;

    /// <summary>Jak dlouho se drží poslední snímek, než se přehrávka zopakuje.</summary>
    private const float HoldSeconds = 1.8f;

    /// <summary>Rychlost posunu kamery klávesnicí (pixely obrazovky za sekundu).</summary>
    private const float PanSpeed = 900f;

    private readonly ScreenManager _screens;
    private readonly CityHistory _history;
    private readonly ITerrain _terrain;
    private readonly InputManager _input = new();
    private readonly Camera2D _camera = new();
    private readonly TerrainRenderer _terrainRenderer;

    /// <summary>Uložení do sbírky; null = otevřeno už uložené (není co ukládat).</summary>
    private readonly Action? _saveToCollection;

    private Desktop _desktop = null!;
    private Label _caption = null!;
    private float _position;
    private float _holdTimer;
    private bool _playing = true;
    private bool _saved;

    /// <param name="terrain">Terén světa, nad kterým kronika vznikla (ze seedu).</param>
    /// <param name="seed">Seed světa — barevné odstíny terénu se z něj odvozují.</param>
    /// <param name="saveToCollection">
    /// Uloží časosběr do sbírky v menu; null, když je otevřený už uložený soubor.
    /// </param>
    public TimelapseScreen(
        ScreenManager screens, CityHistory history, ITerrain terrain, long seed, Action? saveToCollection = null)
    {
        _screens = screens;
        _history = history;
        _terrain = terrain;
        _saveToCollection = saveToCollection;
        _terrainRenderer = new TerrainRenderer(screens.GraphicsDevice, screens.Content.Biomes, seed);

        FrameCity();
        BuildUi();
        _screens.Loc.LanguageChanged += BuildUi;
        _screens.UiSettingsChanged += BuildUi;
    }

    public bool IsOverlay => true;

    public void OnActivated() => _input.Resync();

    public void Update(GameTime gameTime)
    {
        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
        _input.Update(dt);
        if (_input.WasPressed(Keys.Escape))
        {
            _screens.Pop();
            return;
        }

        if (_input.WasPressed(Keys.Space))
        {
            _playing = !_playing;
        }

        UpdateCamera(dt);
        Advance(dt);
    }

    /// <summary>Stejné ovládání kamery jako ve hře — hráč se nemusí nic nového učit.</summary>
    private void UpdateCamera(float dt)
    {
        var viewport = _screens.GraphicsDevice.Viewport;
        _camera.SetViewport(viewport.Width, viewport.Height);

        var move = Vector2.Zero;
        if (_input.IsDown(Keys.W) || _input.IsDown(Keys.Up)) move.Y -= 1f;
        if (_input.IsDown(Keys.S) || _input.IsDown(Keys.Down)) move.Y += 1f;
        if (_input.IsDown(Keys.A) || _input.IsDown(Keys.Left)) move.X -= 1f;
        if (_input.IsDown(Keys.D) || _input.IsDown(Keys.Right)) move.X += 1f;
        if (move != Vector2.Zero)
        {
            move.Normalize();
        }

        move += _input.PadCameraMove;
        if (move != Vector2.Zero)
        {
            _camera.PanWorld(move * (PanSpeed * dt / _camera.Zoom));
        }

        // Tažení mapy: pravé i prostřední tlačítko, jako ve hře. Levé zůstává
        // volné pro tlačítka přehrávače.
        if (_input.IsRightDown || _input.IsMiddleDown)
        {
            _camera.Pan(_input.MouseDelta);
        }

        if (_input.ScrollDelta != 0)
        {
            _camera.ZoomAt(_input.MousePosition.ToVector2(), _input.ScrollDelta > 0 ? 1.15f : 1f / 1.15f);
        }

        float padZoom = _input.PadZoomFactor(dt);
        if (padZoom != 1f)
        {
            _camera.ZoomAt(new Vector2(viewport.Width * 0.5f, viewport.Height * 0.5f), padZoom);
        }
    }

    private void Advance(float dt)
    {
        if (_history.Count == 0 || !_playing)
        {
            return;
        }

        // Na konci se chvíli počká a pak se jede znovu — hotové město si zaslouží,
        // aby na něj šlo koukat, a smyčka ušetří tlačítko „přehrát znovu".
        if (_position >= _history.Count - 1)
        {
            _holdTimer += dt;
            if (_holdTimer >= HoldSeconds)
            {
                _holdTimer = 0f;
                _position = 0f;
            }
        }
        else
        {
            _position = Math.Min(_history.Count - 1, _position + dt * FramesPerSecond);
        }

        UpdateCaption();
    }

    public void Draw(GameTime gameTime)
    {
        var viewport = _screens.GraphicsDevice.Viewport;
        var spriteBatch = _screens.SpriteBatch;
        var pixel = _screens.WhitePixel;

        // Skutečný terén pod přehrávkou — to samé místo, na kterém hráč hrál.
        _terrainRenderer.Draw(spriteBatch, _camera, _terrain);
        DrawCells(spriteBatch, pixel);

        // Lehké ztmavení pruhů nahoře a dole, ať popisky přehrávače neplavou
        // v pixelech živé mapy.
        spriteBatch.Begin();
        spriteBatch.Draw(pixel, new Rectangle(0, 0, viewport.Width, 88), Color.Black * 0.45f);
        spriteBatch.Draw(pixel, new Rectangle(0, viewport.Height - 96, viewport.Width, 96), Color.Black * 0.45f);
        DrawProgressBar(spriteBatch, pixel, viewport);
        spriteBatch.End();

        _screens.RenderDesktop(this, _desktop);
    }

    /// <summary>
    /// Nakreslí zástavbu aktuálního snímku ve world-space: buňka = čtverec
    /// 8×8 dlaždic v barvě skutečné budovy. Buňky, které oproti minulému snímku
    /// přibyly, se rozsvítí — je vidět, co zrovna vyrostlo.
    /// </summary>
    private void DrawCells(SpriteBatch spriteBatch, Texture2D pixel)
    {
        if (_history.Count == 0)
        {
            return;
        }

        int index = Math.Clamp((int)_position, 0, _history.Count - 1);
        int previous = Math.Max(0, index - 1);
        const int cellWorld = CityHistory.TilesPerCell * TerrainRenderer.TileSize;

        spriteBatch.Begin(samplerState: SamplerState.PointClamp, transformMatrix: _camera.Transform);
        for (int cy = 0; cy < CityHistory.GridSize; cy++)
        {
            for (int cx = 0; cx < CityHistory.GridSize; cx++)
            {
                if (_history.ColorAt(index, cx, cy) is not { } color)
                {
                    continue;
                }

                int worldX = (cx - CityHistory.GridSize / 2) * cellWorld;
                int worldY = (cy - CityHistory.GridSize / 2) * cellWorld;
                var tint = color.ToXna();

                // Tmavý podklad pod barvou: buňka se odlepí od terénu i tam,
                // kde má budova podobnou barvu jako biom pod ní.
                spriteBatch.Draw(pixel, new Rectangle(worldX, worldY, cellWorld, cellWorld), Color.Black * 0.35f);
                spriteBatch.Draw(
                    pixel,
                    new Rectangle(worldX + 2, worldY + 2, cellWorld - 4, cellWorld - 4),
                    tint * 0.9f);

                if (!_history.IsOccupied(previous, cx, cy))
                {
                    spriteBatch.Draw(
                        pixel, new Rectangle(worldX + 2, worldY + 2, cellWorld - 4, cellWorld - 4),
                        Color.White * 0.35f); // novostavba zazáří
                }
            }
        }

        spriteBatch.End();
    }

    /// <summary>Pruh postupu dole — hráč vidí, kde v příběhu je.</summary>
    private void DrawProgressBar(SpriteBatch spriteBatch, Texture2D pixel, Viewport viewport)
    {
        if (_history.Count <= 1)
        {
            return;
        }

        int width = viewport.Width - 240;
        int left = (viewport.Width - width) / 2;
        int y = viewport.Height - 88;
        float progress = _position / (_history.Count - 1);

        spriteBatch.Draw(pixel, new Rectangle(left, y, width, 4), new Color(60, 66, 74));
        spriteBatch.Draw(pixel, new Rectangle(left, y, (int)(width * progress), 4), new Color(255, 226, 150));
    }

    public void Dispose()
    {
        _screens.Loc.LanguageChanged -= BuildUi;
        _screens.UiSettingsChanged -= BuildUi;
        _terrainRenderer.Dispose();
    }

    /// <summary>
    /// Nastaví kameru tak, aby bylo celé město (poslední snímek) v záběru.
    /// Bez toho by přehrávka začínala pohledem někam do prázdna.
    /// </summary>
    private void FrameCity()
    {
        var viewport = _screens.GraphicsDevice.Viewport;
        _camera.SetViewport(viewport.Width, viewport.Height);

        int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
        int last = _history.Count - 1;
        for (int cy = 0; cy < CityHistory.GridSize; cy++)
        {
            for (int cx = 0; cx < CityHistory.GridSize; cx++)
            {
                if (!_history.IsOccupied(last, cx, cy))
                {
                    continue;
                }

                minX = Math.Min(minX, cx);
                minY = Math.Min(minY, cy);
                maxX = Math.Max(maxX, cx);
                maxY = Math.Max(maxY, cy);
            }
        }

        const int cellWorld = CityHistory.TilesPerCell * TerrainRenderer.TileSize;
        if (minX > maxX)
        {
            _camera.Position = Vector2.Zero; // prázdná kronika → střed světa
            _camera.SetZoom(0.6f);
            return;
        }

        var center = new Vector2(
            (minX + maxX + 1 - CityHistory.GridSize) * 0.5f * cellWorld,
            (minY + maxY + 1 - CityHistory.GridSize) * 0.5f * cellWorld);
        _camera.Position = center;

        // Zoom tak, aby se vešel celý obrys města s rezervou po stranách.
        float spanX = (maxX - minX + 3) * cellWorld;
        float spanY = (maxY - minY + 3) * cellWorld;
        float zoom = Math.Min(viewport.Width / spanX, (viewport.Height - 180) / spanY);
        _camera.SetZoom(Math.Clamp(zoom, 0.15f, 2.5f));
    }

    private void BuildUi()
    {
        var loc = _screens.Loc;

        var header = new VerticalStackPanel
        {
            Spacing = 4,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 16, 0, 0),
        };
        header.Widgets.Add(new Label
        {
            Text = loc["timelapse.title"],
            HorizontalAlignment = HorizontalAlignment.Center,
            TextColor = new Color(255, 226, 150),
        });

        _caption = new Label
        {
            Text = string.Empty,
            HorizontalAlignment = HorizontalAlignment.Center,
            TextColor = Color.LightGray,
        };
        header.Widgets.Add(_caption);
        header.Widgets.Add(new Label
        {
            Text = loc["timelapse.controls"],
            HorizontalAlignment = HorizontalAlignment.Center,
            TextColor = new Color(150, 160, 175),
        });

        var bottom = new VerticalStackPanel
        {
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(0, 0, 0, 20),
        };

        // Prázdná kronika není chyba — na začátku hry ještě není co přehrávat.
        if (_history.Count == 0)
        {
            bottom.Widgets.Add(new Label
            {
                Text = loc["timelapse.empty"],
                HorizontalAlignment = HorizontalAlignment.Center,
                TextColor = new Color(150, 160, 175),
            });
        }

        var buttons = new HorizontalStackPanel { Spacing = 8, HorizontalAlignment = HorizontalAlignment.Center };
        buttons.Widgets.Add(UiFactory.SmallButton(loc["timelapse.toggle"], () => _playing = !_playing));
        buttons.Widgets.Add(UiFactory.SmallButton(loc["timelapse.restart"], () =>
        {
            _position = 0f;
            _holdTimer = 0f;
            _playing = true;
        }));

        if (_saveToCollection is not null && _history.Count > 1)
        {
            buttons.Widgets.Add(UiFactory.SmallButton(
                _saved ? loc["timelapse.savedNow"] : loc["timelapse.save"],
                () =>
                {
                    if (_saved)
                    {
                        return; // dvojklik nemá vyrábět dva stejné soubory
                    }

                    _saveToCollection();
                    _saved = true;
                    BuildUi();
                }));
        }

        buttons.Widgets.Add(UiFactory.SmallButton(loc["panel.close"], _screens.Pop));
        bottom.Widgets.Add(buttons);

        var root = new Panel();
        root.Widgets.Add(header);
        root.Widgets.Add(bottom);
        _desktop = _screens.NewDesktop(root);
        UpdateCaption();
    }

    private void UpdateCaption()
    {
        if (_history.Count == 0)
        {
            _caption.Text = string.Empty;
            return;
        }

        var frame = _history.FrameAt(Math.Clamp((int)_position, 0, _history.Count - 1));
        _caption.Text = _screens.Loc.Format(
            "timelapse.caption",
            DurationFormat.Human(frame.Seconds),
            CivDle.Core.Numbers.Format(frame.Population),
            frame.Buildings);
    }
}
