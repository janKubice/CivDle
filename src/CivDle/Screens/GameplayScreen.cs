using CivDle.Audio;
using CivDle.Core.Content;
using CivDle.Core.Sim;
using CivDle.Input;
using CivDle.Rendering;
using CivDle.Rendering.Effects;
using FontStashSharp;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Myra.Graphics2D;
using Myra.Graphics2D.Brushes;
using Myra.Graphics2D.UI;
using Myra.Graphics2D.UI.Styles;

namespace CivDle.Screens;

/// <summary>
/// Herní obrazovka: mapa s kamerou (pan WASD/šipky/pravé či prostřední tlačítko,
/// zoom kolečkem ke kurzoru), HUD se surovinami a populací, stavební menu
/// s ghost náhledem (levé tlačítko staví, pravý klik / Esc ruší výběr).
/// Simulace tiká pevným krokem přes <see cref="FixedStepLoop"/>; překrytí pauzou
/// Update zastaví, a tím stojí i simulace.
/// </summary>
public sealed class GameplayScreen : IScreen
{
    /// <summary>Rychlost posunu klávesami v pixelech obrazovky za sekundu.</summary>
    private const float PanSpeed = 700f;

    /// <summary>Násobek zoomu na jedno cvaknutí kolečka.</summary>
    private const float ZoomStep = 1.15f;

    /// <summary>Do kolika pixelů pohybu se puštění pravého tlačítka bere jako klik (zrušení stavby), ne pan.</summary>
    private const float RightClickDragTolerance = 6f;

    private readonly ScreenManager _screens;
    private readonly Simulation _simulation;
    private readonly WorldInfo _info;
    private readonly Camera2D _camera = new();
    private readonly MapRenderer _mapRenderer;
    private readonly BuildingRenderer _buildingRenderer;
    private readonly RoadRenderer _roadRenderer;
    private readonly DecorationRenderer _decorationRenderer;
    private readonly LightsRenderer _lightsRenderer;
    private readonly FaunaSystem _fauna;
    private readonly InputManager _input = new();
    private readonly FixedStepLoop _simLoop = new(Simulation.TicksPerSecond);
    private readonly ParticleSystem _particles = new();
    private readonly FloatingTextRenderer _floatingText = new();
    private readonly GameSounds _sounds = new();
    private readonly SpriteFontBase _popupFont;
    private readonly Dictionary<int, string> _popupTextCache = new();

    private Desktop _desktop = null!;
    private Label[] _resourceLabels = Array.Empty<Label>();
    private Label _populationLabel = null!;
    private Label _seedLabel = null!;
    private Label _worldLabel = null!;
    private Label _dayLabel = null!;
    private Label _cursorLabel = null!;
    private Label _statusLabel = null!;

    private int _selectedBuilding = -1;
    private int _ghostX;
    private int _ghostY;
    private PlacementResult _ghostResult;
    private bool _ghostVisible;
    private float _rightDragDistance;
    private int _knownBuildingCount;

    public GameplayScreen(ScreenManager screens, Simulation simulation, WorldInfo info)
    {
        _screens = screens;
        _simulation = simulation;
        _info = info;

        _mapRenderer = new MapRenderer(screens.GraphicsDevice, simulation.Map, screens.Content.Biomes, info.Seed);
        _buildingRenderer = new BuildingRenderer(screens.WhitePixel, screens.Content);
        _roadRenderer = new RoadRenderer(screens.WhitePixel, screens.Content);
        _decorationRenderer = new DecorationRenderer(screens.WhitePixel, screens.Content, info.Seed);
        _lightsRenderer = new LightsRenderer(screens.WhitePixel, screens.Content);
        _fauna = new FaunaSystem(screens.Content);
        _popupFont = Stylesheet.Current.LabelStyle.Font;
        _camera.SetWorldBounds(_mapRenderer.WorldPixelWidth, _mapRenderer.WorldPixelHeight);
        var viewport = screens.GraphicsDevice.Viewport;
        _camera.SetViewport(viewport.Width, viewport.Height);
        _camera.CenterOnWorld();
        _knownBuildingCount = simulation.Buildings.Length; // načtená hra: bez juice za staré budovy

        BuildUi();
        _screens.Loc.LanguageChanged += BuildUi;
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
            if (_selectedBuilding >= 0)
            {
                _selectedBuilding = -1;
            }
            else
            {
                _screens.Push(new PauseScreen(_screens, _simulation, _info));
                return;
            }
        }

        bool mouseOverUi = _desktop.IsMouseOverGUI;
        UpdateCamera(dt, mouseOverUi);
        UpdatePlacement(mouseOverUi);
        UpdateHarvest(mouseOverUi);

        int ticks = _simLoop.Advance(gameTime.ElapsedGameTime.TotalSeconds);
        for (int i = 0; i < ticks; i++)
        {
            _simulation.Tick();
        }

        EmitNewBuildingJuice();
        _particles.Update(dt);
        _floatingText.Update(dt);
        _fauna.Update(dt, _camera, _simulation);
        RefreshHudTexts();
    }

    public void Draw(GameTime gameTime)
    {
        var spriteBatch = _screens.SpriteBatch;
        _mapRenderer.Draw(spriteBatch, _camera);
        _decorationRenderer.Draw(spriteBatch, _camera, _simulation.Map);
        _roadRenderer.Draw(spriteBatch, _camera, _simulation);
        _buildingRenderer.Draw(spriteBatch, _camera, _simulation);
        _fauna.Draw(spriteBatch, _screens.WhitePixel, _camera);
        _particles.Draw(spriteBatch, _screens.WhitePixel, _camera);

        // Den/noc: ztmavení scény a pak aditivní světla, ať září skrz tmu.
        double timeOfDay = _simulation.TimeOfDay01;
        DayNightCycle.DrawOverlay(
            spriteBatch, _screens.WhitePixel, _screens.GraphicsDevice.Viewport,
            _screens.Content.Gameplay.DayNight, timeOfDay);
        _lightsRenderer.Draw(spriteBatch, _camera, _simulation, DayNightCycle.NightFactor(timeOfDay));

        if (_ghostVisible && _selectedBuilding >= 0)
        {
            var def = _screens.Content.Buildings[_selectedBuilding];
            _buildingRenderer.DrawGhost(
                spriteBatch, _camera, def, _ghostX, _ghostY, _ghostResult == PlacementResult.Ok);
        }

        _desktop.Render();

        // Popupy a jmenovky osad až nad UI — hráč je nesmí přehlédnout.
        _floatingText.Draw(spriteBatch, _camera, _popupFont);
        DrawSettlementLabels(spriteBatch);
    }

    /// <summary>Jmenovky osad ve screen-space nad těžištěm shluku (orientace na mapě, fáze 4).</summary>
    private void DrawSettlementLabels(Microsoft.Xna.Framework.Graphics.SpriteBatch spriteBatch)
    {
        var settlements = _simulation.Settlements;
        if (settlements.Count == 0)
        {
            return;
        }

        var names = _screens.Content.SettlementNames;
        spriteBatch.Begin();
        for (int i = 0; i < settlements.Count; i++)
        {
            var settlement = settlements[i];
            string name = names[settlement.NameIndex];
            var world = new Vector2(settlement.CenterX * MapRenderer.TileSize, settlement.CenterY * MapRenderer.TileSize);
            var screen = _camera.WorldToScreen(world);
            var size = _popupFont.MeasureString(name);
            var position = new Vector2(screen.X - size.X * 0.5f, screen.Y - size.Y * 0.5f);

            spriteBatch.DrawString(_popupFont, name, position + new Vector2(1f, 1f), Color.Black * 0.75f);
            spriteBatch.DrawString(_popupFont, name, position, Color.White * 0.92f);
        }

        spriteBatch.End();
    }

    public void Dispose()
    {
        _screens.Loc.LanguageChanged -= BuildUi;
        _mapRenderer.Dispose();
        _sounds.Dispose();
    }

    // ----- vstup -----

    private void UpdateCamera(float dt, bool mouseOverUi)
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

        // Levé tlačítko je pro stavění; mapou se táhne pravým nebo prostředním.
        if ((_input.IsRightDown || _input.IsMiddleDown) && !mouseOverUi)
        {
            _camera.Pan(_input.MouseDelta);
        }

        if (_input.ScrollDelta != 0 && !mouseOverUi)
        {
            float factor = MathF.Pow(ZoomStep, _input.ScrollDelta / 120f);
            _camera.ZoomAt(_input.MousePosition.ToVector2(), factor);
        }
    }

    private void UpdatePlacement(bool mouseOverUi)
    {
        // Pravé tlačítko: krátký klik ruší výběr budovy, tažení je pan kamery.
        if (_input.WasRightPressed)
        {
            _rightDragDistance = 0f;
        }

        if (_input.IsRightDown)
        {
            _rightDragDistance += _input.MouseDelta.Length();
        }

        if (_input.WasRightReleased && _rightDragDistance < RightClickDragTolerance && _selectedBuilding >= 0)
        {
            _selectedBuilding = -1;
        }

        _ghostVisible = false;
        if (_selectedBuilding < 0 || mouseOverUi)
        {
            return;
        }

        var def = _screens.Content.Buildings[_selectedBuilding];
        var world = _camera.ScreenToWorld(_input.MousePosition.ToVector2());
        int tileX = (int)MathF.Floor(world.X / MapRenderer.TileSize);
        int tileY = (int)MathF.Floor(world.Y / MapRenderer.TileSize);

        // Kurzor míří na střed půdorysu, ať se velké budovy pokládají přirozeně.
        _ghostX = tileX - (def.FootprintWidth - 1) / 2;
        _ghostY = tileY - (def.FootprintHeight - 1) / 2;
        _ghostResult = _simulation.CanPlace(_selectedBuilding, _ghostX, _ghostY);
        _ghostVisible = true;

        if (_input.WasLeftPressed && _ghostResult == PlacementResult.Ok)
        {
            // Výběr zůstává — idle hráč typicky staví víc budov za sebou.
            // Juice řeší společně EmitNewBuildingJuice (pokryje i auto-stavbu).
            _simulation.TryPlaceBuilding(_selectedBuilding, _ghostX, _ghostY);
        }
    }

    /// <summary>
    /// Prach + žuchnutí pro každou nově vzniklou budovu — společné pro ruční stavbu
    /// i auto-stavbu (nová budova se pozná růstem počtu v simulaci).
    /// </summary>
    private void EmitNewBuildingJuice()
    {
        var buildings = _simulation.Buildings;
        if (buildings.Length <= _knownBuildingCount)
        {
            return;
        }

        for (int i = _knownBuildingCount; i < buildings.Length; i++)
        {
            ref readonly var building = ref buildings[i];
            var def = _screens.Content.Buildings[building.DefIndex];
            var center = new Vector2(
                (building.X + def.FootprintWidth * 0.5f) * MapRenderer.TileSize,
                (building.Y + def.FootprintHeight * 0.5f) * MapRenderer.TileSize);
            _particles.SpawnBurst(center, new Color(205, 195, 175), 14, 50f, 170f); // prach dopadu
            _particles.SpawnBurst(center, def.MapColor.ToXna(), 6, 40f, 120f);
        }

        _sounds.PlayPlace();
        _knownBuildingCount = buildings.Length;
    }

    /// <summary>Ruční těžba: klik na dlaždici s výnosem (les, hory) — s popupem, třískami a zvukem.</summary>
    private void UpdateHarvest(bool mouseOverUi)
    {
        if (_selectedBuilding >= 0 || mouseOverUi || !_input.WasLeftPressed)
        {
            return;
        }

        var world = _camera.ScreenToWorld(_input.MousePosition.ToVector2());
        int tileX = (int)MathF.Floor(world.X / MapRenderer.TileSize);
        int tileY = (int)MathF.Floor(world.Y / MapRenderer.TileSize);

        if (!_simulation.TryHarvest(tileX, tileY, out int resourceIndex, out int amount))
        {
            return;
        }

        var content = _screens.Content;
        var tileCenter = new Vector2((tileX + 0.5f) * MapRenderer.TileSize, (tileY + 0.5f) * MapRenderer.TileSize);
        var resourceColor = content.Resources[resourceIndex].MapColor.ToXna();
        var biomeColor = content.Biomes[_simulation.Map.BiomeAt(tileX, tileY)].MapColor.ToXna();

        _floatingText.Add(tileCenter, PopupText(resourceIndex, amount), resourceColor);
        _particles.SpawnBurst(tileCenter, biomeColor, 10, 45f, 150f); // „třísky" v barvě biomu
        _particles.SpawnBurst(tileCenter, resourceColor, 5, 35f, 110f);
        _sounds.PlayChop();
    }

    /// <summary>Texty popupů se cachují — žádné skládání stringů při každém kliku.</summary>
    private string PopupText(int resourceIndex, int amount)
    {
        int key = resourceIndex * 100_000 + amount;
        if (!_popupTextCache.TryGetValue(key, out var text))
        {
            text = $"+{amount}";
            _popupTextCache[key] = text;
        }

        return text;
    }

    // ----- HUD -----

    private void BuildUi()
    {
        var loc = _screens.Loc;
        var content = _screens.Content;

        // Levý horní roh: suroviny + populace.
        var resourceBar = new HorizontalStackPanel { Spacing = 14 };
        _resourceLabels = new Label[content.Resources.Count];
        for (int i = 0; i < content.Resources.Count; i++)
        {
            var item = new HorizontalStackPanel { Spacing = 5 };
            item.Widgets.Add(new Panel
            {
                Width = 12,
                Height = 12,
                VerticalAlignment = VerticalAlignment.Center,
                Background = new SolidBrush(content.Resources[i].MapColor.ToXna()),
            });
            _resourceLabels[i] = new Label { VerticalAlignment = VerticalAlignment.Center };
            item.Widgets.Add(_resourceLabels[i]);
            resourceBar.Widgets.Add(item);
        }

        _populationLabel = new Label { VerticalAlignment = VerticalAlignment.Center };
        resourceBar.Widgets.Add(_populationLabel);

        var topLeft = UiFactory.DarkPanel(resourceBar);
        topLeft.HorizontalAlignment = HorizontalAlignment.Left;
        topLeft.VerticalAlignment = VerticalAlignment.Top;

        // Pravý horní roh: informace o světě a dlaždici pod kurzorem.
        _seedLabel = new Label();
        _worldLabel = new Label();
        _dayLabel = new Label();
        _cursorLabel = new Label();
        var worldInfoStack = new VerticalStackPanel { Spacing = 2 };
        worldInfoStack.Widgets.Add(_seedLabel);
        worldInfoStack.Widgets.Add(_worldLabel);
        worldInfoStack.Widgets.Add(_dayLabel);
        worldInfoStack.Widgets.Add(_cursorLabel);
        var topRight = UiFactory.DarkPanel(worldInfoStack);
        topRight.HorizontalAlignment = HorizontalAlignment.Right;
        topRight.VerticalAlignment = VerticalAlignment.Top;

        // Spodek: stavební menu se stavovou hláškou.
        _statusLabel = new Label { HorizontalAlignment = HorizontalAlignment.Center };
        var buildButtons = new HorizontalStackPanel
        {
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        for (int i = 0; i < content.Buildings.Count; i++)
        {
            buildButtons.Widgets.Add(BuildingButton(i));
        }

        var buildStack = new VerticalStackPanel { Spacing = 6 };
        buildStack.Widgets.Add(_statusLabel);
        buildStack.Widgets.Add(buildButtons);
        var bottomCenter = UiFactory.DarkPanel(buildStack);
        bottomCenter.HorizontalAlignment = HorizontalAlignment.Center;
        bottomCenter.VerticalAlignment = VerticalAlignment.Bottom;

        // Levý dolní roh: nápověda ovládání.
        var help = UiFactory.DarkPanel(new Label { Text = loc["hud.help"], TextColor = Color.Gray });
        help.HorizontalAlignment = HorizontalAlignment.Left;
        help.VerticalAlignment = VerticalAlignment.Bottom;

        var root = new Panel();
        root.Widgets.Add(topLeft);
        root.Widgets.Add(topRight);
        root.Widgets.Add(bottomCenter);
        root.Widgets.Add(help);

        _desktop = new Desktop { Root = root };
        RefreshHudTexts();
    }

    /// <summary>Tlačítko budovy: jméno + cena z definice (žádné texty natvrdo).</summary>
    private Button BuildingButton(int defIndex)
    {
        var loc = _screens.Loc;
        var content = _screens.Content;
        var def = content.Buildings[defIndex];

        var caption = new VerticalStackPanel { Spacing = 2 };
        caption.Widgets.Add(new Label
        {
            Text = loc[def.NameKey],
            HorizontalAlignment = HorizontalAlignment.Center,
        });
        caption.Widgets.Add(new Label
        {
            Text = string.Join("  ", def.BuildCost.Select(c => $"{c.Amount} {loc[content.Resources[c.ResourceIndex].NameKey]}")),
            TextColor = Color.Gray,
            HorizontalAlignment = HorizontalAlignment.Center,
        });

        var button = new Button
        {
            Content = caption,
            Padding = new Thickness(10, 6),
        };
        button.Click += (_, _) => _selectedBuilding = _selectedBuilding == defIndex ? -1 : defIndex;
        return button;
    }

    private void RefreshHudTexts()
    {
        var loc = _screens.Loc;
        var content = _screens.Content;

        for (int i = 0; i < _resourceLabels.Length; i++)
        {
            _resourceLabels[i].Text = $"{(long)_simulation.GetResource(i)}/{(long)_simulation.GetStorageCap(i)}";
        }

        _populationLabel.Text = loc.Format("hud.population", (long)_simulation.Population, _simulation.HousingCapacity);
        _seedLabel.Text = loc.Format("hud.seed", _info.Seed);
        _worldLabel.Text = loc.Format(
            "hud.world", loc[$"preset.{_info.PresetId}"], loc[$"worldsize.{_info.SizeId}"]);

        double hours = _simulation.TimeOfDay01 * 24.0;
        _dayLabel.Text = loc.Format("hud.day", _simulation.DayNumber, (int)hours, (int)((hours - (int)hours) * 60));

        UpdateCursorLabel();
        UpdateStatusLabel();
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
            _cursorLabel.Text = _screens.Loc.Format("hud.cursor", tileX, tileY, _screens.Loc[biome.NameKey]);
        }
        else
        {
            _cursorLabel.Text = string.Empty;
        }
    }

    private void UpdateStatusLabel()
    {
        var loc = _screens.Loc;
        if (_selectedBuilding < 0)
        {
            _statusLabel.Text = loc["build.title"];
            _statusLabel.TextColor = Color.LightGray;
            return;
        }

        var def = _screens.Content.Buildings[_selectedBuilding];
        if (_ghostVisible && _ghostResult != PlacementResult.Ok)
        {
            _statusLabel.Text = loc[ErrorKey(_ghostResult)];
            _statusLabel.TextColor = new Color(235, 120, 110);
        }
        else
        {
            _statusLabel.Text = loc.Format("build.placing", loc[def.NameKey]);
            _statusLabel.TextColor = Color.White;
        }
    }

    private static string ErrorKey(PlacementResult result) => result switch
    {
        PlacementResult.OutOfBounds => "build.error.outOfBounds",
        PlacementResult.Occupied => "build.error.occupied",
        PlacementResult.WrongBiome => "build.error.wrongBiome",
        PlacementResult.NotEnoughResources => "build.error.resources",
        _ => "build.title",
    };
}
