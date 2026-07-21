using CivDle.Audio;
using CivDle.Core.Sim;
using CivDle.Input;
using CivDle.Rendering;
using CivDle.Rendering.Effects;
using FontStashSharp;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Myra.Graphics2D;
using Myra.Graphics2D.Brushes;
using Myra.Graphics2D.UI;
using Myra.Graphics2D.UI.Styles;

namespace CivDle.Screens;

/// <summary>
/// Herní obrazovka nad NEKONEČNOU mapou: kamera (pan WASD/šipky/pravé či prostřední
/// tlačítko, zoom kolečkem ke kurzoru), moderní HUD se surovinami (ikony), stavební
/// menu s ghost náhledem, klikací těžba stromů/kamenů (anticipace + payoff), živý
/// svět (chodci, vozíky). Simulace tiká pevným krokem; překrytí pauzou Update
/// zastaví, a tím stojí i simulace.
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
    private readonly TerrainRenderer _terrainRenderer;
    private readonly DecorationRenderer _decorationRenderer;
    private readonly HarvestableRenderer _harvestables;
    private readonly RoadRenderer _roadRenderer;
    private readonly BuildingRenderer _buildingRenderer;
    private readonly LightsRenderer _lightsRenderer;
    private readonly FaunaSystem _fauna;
    private readonly AgentSystem _agents;
    private readonly InputManager _input = new();
    private readonly FixedStepLoop _simLoop = new(Simulation.TicksPerSecond);
    private readonly ParticleSystem _particles = new();
    private readonly FloatingTextRenderer _floatingText = new();
    private readonly GameSounds _sounds = new();
    private readonly MinimapRenderer _minimap;
    private readonly ToastRenderer _toasts;
    private readonly SpriteFontBase _popupFont;
    private readonly Dictionary<int, string> _popupTextCache = new();

    private Desktop _desktop = null!;
    private Label[] _resourceLabels = Array.Empty<Label>();
    private Label _populationLabel = null!;
    private Label _dayLabel = null!;
    private Label _cursorLabel = null!;
    private Label _statusLabel = null!;
    private HorizontalStackPanel _buildCategoryPanel = null!;
    private HorizontalStackPanel _buildItemsPanel = null!;
    private string _selectedCategory = string.Empty;
    private readonly List<(int DefIndex, Button Button, Label PriceLabel)> _buildButtons = new();
    private VerticalStackPanel _goalsPanel = null!;
    private readonly List<(CivDle.Core.Sim.GoalCondition Condition, Label Progress)> _goalSlots = new();
    private bool _goalsDirty = true;

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
        // Už odemčené achievementy z profilu, ať se v téhle hře nespouštějí znovu.
        _simulation.SeedUnlockedAchievements(screens.Profile.UnlockedAchievements);
        screens.DisposeMenuBackground(); // pod hrou už netiká ukázkové město z menu

        _terrainRenderer = new TerrainRenderer(screens.GraphicsDevice, screens.Content.Biomes, info.Seed);
        _decorationRenderer = new DecorationRenderer(screens.WhitePixel, screens.Content, info.Seed);
        _harvestables = new HarvestableRenderer(screens.Sprites, screens.Content);
        _roadRenderer = new RoadRenderer(screens.WhitePixel, screens.Content);
        _buildingRenderer = new BuildingRenderer(screens.WhitePixel, screens.Content, screens.Sprites);
        _lightsRenderer = new LightsRenderer(screens.WhitePixel, screens.Content);
        _fauna = new FaunaSystem(screens.Content);
        _agents = new AgentSystem(screens.Content, screens.Sprites);
        _minimap = new MinimapRenderer(screens.GraphicsDevice, screens.Content.Biomes, screens.WhitePixel);
        _popupFont = Stylesheet.Current.LabelStyle.Font;
        _toasts = new ToastRenderer(screens.WhitePixel, _popupFont);

        var viewport = screens.GraphicsDevice.Viewport;
        _camera.SetViewport(viewport.Width, viewport.Height);
        _camera.CenterOn(FindStartFocus(), zoom: 2.2f);
        _knownBuildingCount = simulation.Buildings.Length; // načtená hra: bez juice za staré budovy

        BuildUi();
        _screens.Loc.LanguageChanged += BuildUi;
    }

    public bool IsOverlay => false;

    public void OnActivated()
    {
        _input.Resync();
        // Návrat z overlaye (výzkum, detail budovy, Vzestup) mohl odemknout budovy
        // nebo zresetovat éru — stavební menu přebuduj a srovnej počítadlo budov,
        // ať po Vzestupu (reset na 0 budov) nevystřelí prach za „nové" budovy.
        RefreshBuildMenu();
        _goalsDirty = true;
        _knownBuildingCount = _simulation.Buildings.Length;
    }

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
        _harvestables.Update(dt);
        _particles.Update(dt);
        _floatingText.Update(dt);
        _fauna.Update(dt, _camera, _simulation);
        _agents.Update(dt, _camera, _simulation);
        _minimap.Update(dt, _camera, _simulation);
        DrainNotifications();
        _toasts.Update(dt);
        RefreshHudTexts();
    }

    public void Draw(GameTime gameTime)
    {
        var spriteBatch = _screens.SpriteBatch;
        _terrainRenderer.Draw(spriteBatch, _camera, _simulation.Terrain);
        _decorationRenderer.Draw(spriteBatch, _camera, _simulation.Terrain);
        _harvestables.Draw(spriteBatch, _camera, _simulation);
        _roadRenderer.Draw(spriteBatch, _camera, _simulation);
        _buildingRenderer.Draw(spriteBatch, _camera, _simulation);
        _agents.Draw(spriteBatch, _camera);
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

        // Minimapa, popupy a toasty až nad UI — hráč je nesmí přehlédnout.
        _minimap.Draw(spriteBatch, _screens.GraphicsDevice.Viewport, _camera, _simulation);
        _floatingText.Draw(spriteBatch, _camera, _popupFont);
        DrawSettlementLabels(spriteBatch);
        _toasts.Draw(spriteBatch, _screens.GraphicsDevice.Viewport);
    }

    /// <summary>Jmenovky osad ve screen-space nad těžištěm shluku (orientace na mapě, fáze 4).</summary>
    private void DrawSettlementLabels(SpriteBatch spriteBatch)
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
            var world = new Vector2(settlement.CenterX * TerrainRenderer.TileSize, settlement.CenterY * TerrainRenderer.TileSize);
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
        _terrainRenderer.Dispose();
        _minimap.Dispose();
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

        // Levé tlačítko je pro stavění/těžbu; mapou se táhne pravým nebo prostředním.
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
        int tileX = (int)MathF.Floor(world.X / TerrainRenderer.TileSize);
        int tileY = (int)MathF.Floor(world.Y / TerrainRenderer.TileSize);

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
                (building.X + def.FootprintWidth * 0.5f) * TerrainRenderer.TileSize,
                (building.Y + def.FootprintHeight * 0.5f) * TerrainRenderer.TileSize);
            _particles.SpawnBurst(center, new Color(205, 195, 175), 14, 50f, 170f); // prach dopadu
            _particles.SpawnBurst(center, def.MapColor.ToXna(), 6, 40f, 120f);
        }

        _sounds.PlayPlace();
        _knownBuildingCount = buildings.Length;
    }

    /// <summary>
    /// Ruční těžba: klik na strom/kámen — surovina, popup, třísky, zvuk; strom se
    /// zmenšuje a po pár klicích spadne s velkým efektem (anticipace + payoff).
    /// </summary>
    private void UpdateHarvest(bool mouseOverUi)
    {
        if (_selectedBuilding >= 0 || mouseOverUi || !_input.WasLeftPressed)
        {
            return;
        }

        var world = _camera.ScreenToWorld(_input.MousePosition.ToVector2());
        int tileX = (int)MathF.Floor(world.X / TerrainRenderer.TileSize);
        int tileY = (int)MathF.Floor(world.Y / TerrainRenderer.TileSize);

        // Klik na budovu ji rozklikne (detail + vylepšení) — až pak řeším těžbu.
        if (_simulation.TryGetBuildingAt(tileX, tileY, out int buildingIndex))
        {
            _screens.Push(new BuildingInfoScreen(_screens, _simulation, buildingIndex));
            return;
        }

        if (!_simulation.TryHarvest(tileX, tileY, out int resourceIndex, out int amount))
        {
            return;
        }

        var content = _screens.Content;
        var tileCenter = new Vector2((tileX + 0.5f) * TerrainRenderer.TileSize, (tileY + 0.5f) * TerrainRenderer.TileSize);
        var resourceColor = content.Resources[resourceIndex].MapColor.ToXna();
        var biomeColor = content.Biomes[_simulation.BiomeAt(tileX, tileY)].MapColor.ToXna();

        _floatingText.Add(tileCenter, PopupText(resourceIndex, amount), resourceColor);
        _sounds.PlayChop();

        bool felled = _harvestables.RegisterChop(tileX, tileY);
        if (felled)
        {
            // Payoff: strom spadl → velký výbuch třísek + žuchnutí.
            _particles.SpawnBurst(tileCenter, biomeColor, 26, 60f, 240f);
            _particles.SpawnBurst(tileCenter, resourceColor, 12, 45f, 160f);
            _sounds.PlayPlace();
        }
        else
        {
            _particles.SpawnBurst(tileCenter, biomeColor, 8, 45f, 150f);
            _particles.SpawnBurst(tileCenter, resourceColor, 4, 35f, 110f);
        }
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

    /// <summary>Najde poblíž počátku první suchou dlaždici, ať kamera nezačíná nad oceánem.</summary>
    private Vector2 FindStartFocus()
    {
        var content = _screens.Content;
        var buildings = _simulation.Buildings;
        if (buildings.Length > 0)
        {
            var b = buildings[0];
            return new Vector2((b.X + 0.5f) * TerrainRenderer.TileSize, (b.Y + 0.5f) * TerrainRenderer.TileSize);
        }

        for (int radius = 0; radius < 300; radius++)
        {
            for (int y = -radius; y <= radius; y++)
            {
                for (int x = -radius; x <= radius; x++)
                {
                    if (Math.Max(Math.Abs(x), Math.Abs(y)) != radius)
                    {
                        continue;
                    }

                    if (!content.Biomes[_simulation.BiomeAt(x, y)].IsWater)
                    {
                        return new Vector2((x + 0.5f) * TerrainRenderer.TileSize, (y + 0.5f) * TerrainRenderer.TileSize);
                    }
                }
            }
        }

        return Vector2.Zero;
    }

    /// <summary>Vyzvedne oznámení ze simulace (splněné úkoly, achievementy, milníky) a udělá z nich toasty.</summary>
    private void DrainNotifications()
    {
        var loc = _screens.Loc;
        while (_simulation.TryDequeueNotification(out var note))
        {
            string text = $"{loc[note.TitleKey]}: {loc[note.SubjectKey]}";
            _toasts.Add(text, NotificationColor(note.Kind));
            _sounds.PlayPlace();

            // Splněný úkol / Vzestup mění seznam aktivních cílů — přestav sledovač.
            if (note.Kind is NotificationKind.QuestCompleted or NotificationKind.Ascended)
            {
                _goalsDirty = true;
            }

            // Nový achievement → zapiš do účet-wide profilu (přežije i restart).
            if (note.Kind == NotificationKind.AchievementUnlocked)
            {
                SyncAchievements();
            }
        }
    }

    /// <summary>Zapíše nově odemčené achievementy do profilu a uloží ho (účet-wide).</summary>
    private void SyncAchievements()
    {
        var content = _screens.Content;
        var profile = _screens.Profile;
        bool changed = false;
        for (int i = 0; i < content.Achievements.Count; i++)
        {
            string id = content.Achievements[i].Id;
            if (_simulation.IsAchievementUnlocked(i) && !profile.UnlockedAchievements.Contains(id))
            {
                profile.UnlockedAchievements.Add(id);
                changed = true;
            }
        }

        if (changed)
        {
            _screens.SaveProfile();
        }
    }

    private static Color NotificationColor(NotificationKind kind) => kind switch
    {
        NotificationKind.QuestCompleted => new Color(120, 200, 140),
        NotificationKind.AchievementUnlocked => new Color(230, 200, 110),
        NotificationKind.Ascended => new Color(180, 140, 230),
        _ => new Color(96, 196, 220),
    };

    // ----- HUD -----

    private void BuildUi()
    {
        var content = _screens.Content;

        // Horní pruh: suroviny (ikony) + populace.
        var resourceBar = new HorizontalStackPanel { Spacing = 18 };
        _resourceLabels = new Label[content.Resources.Count];
        for (int i = 0; i < content.Resources.Count; i++)
        {
            var chip = new HorizontalStackPanel { Spacing = 6 };
            var icon = _screens.Sprites.Get($"icon.{content.Resources[i].Id}");
            if (icon is not null)
            {
                chip.Widgets.Add(UiFactory.Icon(icon, 20));
            }
            else
            {
                chip.Widgets.Add(new Panel
                {
                    Width = 14,
                    Height = 14,
                    VerticalAlignment = VerticalAlignment.Center,
                    Background = new SolidBrush(content.Resources[i].MapColor.ToXna()),
                });
            }

            _resourceLabels[i] = new Label { VerticalAlignment = VerticalAlignment.Center };
            chip.Widgets.Add(_resourceLabels[i]);
            resourceBar.Widgets.Add(chip);
        }

        _populationLabel = new Label { VerticalAlignment = VerticalAlignment.Center, TextColor = UiFactory.Accent };
        resourceBar.Widgets.Add(_populationLabel);

        var topLeft = UiFactory.DarkPanel(resourceBar);
        topLeft.HorizontalAlignment = HorizontalAlignment.Left;
        topLeft.VerticalAlignment = VerticalAlignment.Top;
        topLeft.Margin = new Thickness(10, 10, 0, 0);

        // Pravý horní roh: den/čas + dlaždice pod kurzorem.
        _dayLabel = new Label { TextColor = UiFactory.Accent };
        _cursorLabel = new Label { TextColor = Color.LightGray };
        var worldInfoStack = new VerticalStackPanel { Spacing = 3, HorizontalAlignment = HorizontalAlignment.Right };
        worldInfoStack.Widgets.Add(_dayLabel);
        worldInfoStack.Widgets.Add(_cursorLabel);
        var topRight = UiFactory.DarkPanel(worldInfoStack);
        topRight.HorizontalAlignment = HorizontalAlignment.Right;
        topRight.VerticalAlignment = VerticalAlignment.Top;
        topRight.Margin = new Thickness(0, 10, 10, 0);

        // Spodek uprostřed: stavební menu — nahoře záložky kategorií, pod nimi budovy dané kategorie.
        _statusLabel = new Label { HorizontalAlignment = HorizontalAlignment.Center };
        _buildCategoryPanel = new HorizontalStackPanel { Spacing = 6, HorizontalAlignment = HorizontalAlignment.Center };
        _buildItemsPanel = new HorizontalStackPanel { Spacing = 8, HorizontalAlignment = HorizontalAlignment.Center };

        var buildStack = new VerticalStackPanel { Spacing = 6, HorizontalAlignment = HorizontalAlignment.Center };
        buildStack.Widgets.Add(_statusLabel);
        buildStack.Widgets.Add(_buildCategoryPanel);
        buildStack.Widgets.Add(_buildItemsPanel);
        var bottomCenter = UiFactory.DarkPanel(buildStack);
        bottomCenter.HorizontalAlignment = HorizontalAlignment.Center;
        bottomCenter.VerticalAlignment = VerticalAlignment.Bottom;
        bottomCenter.Margin = new Thickness(0, 0, 0, 12);

        // Levý střed: sledovač úkolů (aktuální cíle + pokrok) — vede hráče hrou.
        _goalsPanel = new VerticalStackPanel { Spacing = 5 };
        var goalsBox = UiFactory.DarkPanel(_goalsPanel);
        goalsBox.HorizontalAlignment = HorizontalAlignment.Left;
        goalsBox.VerticalAlignment = VerticalAlignment.Center;
        goalsBox.Margin = new Thickness(10, 0, 0, 0);

        var root = new Panel();
        root.Widgets.Add(topLeft);
        root.Widgets.Add(topRight);
        root.Widgets.Add(goalsBox);
        root.Widgets.Add(bottomCenter);
        root.Widgets.Add(BuildToolButtons());

        _desktop = new Desktop { Root = root };
        _goalsDirty = true;
        RefreshBuildMenu();
        RefreshHudTexts();
    }

    /// <summary>Rychlé akce mapy vlevo dole: zpět na město, seznam osad, tech tree.</summary>
    private Widget BuildToolButtons()
    {
        var loc = _screens.Loc;
        var stack = new VerticalStackPanel { Spacing = 6 };
        stack.Widgets.Add(UiFactory.SmallButton(loc["hud.quests"],
            () => _screens.Push(new QuestsScreen(_screens, _simulation))));
        stack.Widgets.Add(UiFactory.SmallButton(loc["hud.backToCity"], RecenterOnCity));
        stack.Widgets.Add(UiFactory.SmallButton(loc["hud.settlements"],
            () => _screens.Push(new SettlementsScreen(_screens, _simulation, _camera))));
        if (_screens.Content.Techs.Count > 0)
        {
            stack.Widgets.Add(UiFactory.SmallButton(loc["hud.tech"],
                () => _screens.Push(new TechScreen(_screens, _simulation))));
        }

        stack.Widgets.Add(UiFactory.SmallButton(loc["hud.ascend"],
            () => _screens.Push(new AscensionScreen(_screens, _simulation))));
        stack.Widgets.Add(UiFactory.SmallButton(loc["hud.achievements"],
            () => _screens.Push(new AchievementsScreen(_screens, _simulation))));

        var panel = UiFactory.DarkPanel(stack);
        panel.HorizontalAlignment = HorizontalAlignment.Left;
        panel.VerticalAlignment = VerticalAlignment.Bottom;
        panel.Margin = new Thickness(10, 0, 0, 12);
        return panel;
    }

    /// <summary>Přebuduje stavební menu podle aktuálně odemčených/stavitelných budov.</summary>
    private void RefreshBuildMenu()
    {
        var categories = BuildableCategories();
        if (categories.Count == 0)
        {
            _buildCategoryPanel.Widgets.Clear();
            _buildItemsPanel.Widgets.Clear();
            return;
        }

        if (!categories.Contains(_selectedCategory))
        {
            _selectedCategory = categories[0];
        }

        PopulateCategoryTabs(categories);
        PopulateBuildItems();
    }

    /// <summary>Kategorie, ve kterých je aspoň jedna stavitelná budova (v pořadí prvního výskytu).</summary>
    private List<string> BuildableCategories()
    {
        var content = _screens.Content;
        var result = new List<string>();
        for (int i = 0; i < content.Buildings.Count; i++)
        {
            if (!_simulation.IsBuildingBuildable(i))
            {
                continue;
            }

            string category = content.Buildings[i].Category;
            if (!result.Contains(category))
            {
                result.Add(category);
            }
        }

        return result;
    }

    private void PopulateCategoryTabs(IReadOnlyList<string> categories)
    {
        var loc = _screens.Loc;
        _buildCategoryPanel.Widgets.Clear();
        foreach (string category in categories)
        {
            bool active = category == _selectedCategory;
            var button = new Button
            {
                Content = new Label
                {
                    Text = loc[$"category.{category}"],
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    TextColor = active ? Color.White : Color.LightGray,
                },
                Padding = new Thickness(12, 4),
                Background = new SolidBrush(active ? new Color(60, 110, 130, 235) : new Color(38, 48, 64, 235)),
            };
            string captured = category;
            button.Click += (_, _) =>
            {
                _selectedCategory = captured;
                PopulateCategoryTabs(categories);
                PopulateBuildItems();
            };
            _buildCategoryPanel.Widgets.Add(button);
        }
    }

    private void PopulateBuildItems()
    {
        var content = _screens.Content;
        _buildItemsPanel.Widgets.Clear();
        _buildButtons.Clear();
        for (int i = 0; i < content.Buildings.Count; i++)
        {
            if (_simulation.IsBuildingBuildable(i) && content.Buildings[i].Category == _selectedCategory)
            {
                _buildItemsPanel.Widgets.Add(BuildingButton(i));
            }
        }

        RefreshBuildAffordability();
    }

    /// <summary>
    /// Zvýrazní stavební tlačítka podle toho, jestli na budovu hráč má — zelená
    /// cena a plné tlačítko = ano, červená a ztlumené = ne. Volá se každý snímek,
    /// suroviny se mění.
    /// </summary>
    private void RefreshBuildAffordability()
    {
        foreach (var (defIndex, button, priceLabel) in _buildButtons)
        {
            bool affordable = _simulation.CanAfford(defIndex);
            priceLabel.TextColor = affordable ? new Color(150, 220, 150) : new Color(232, 120, 110);
            button.Background = new SolidBrush(affordable ? new Color(38, 48, 64, 235) : new Color(30, 34, 42, 170));
        }
    }

    /// <summary>Přestaví sledovač úkolů (aktivní cíle) — volá se, jen když se stav změní.</summary>
    private void RebuildGoals()
    {
        var loc = _screens.Loc;
        var content = _screens.Content;
        _goalsPanel.Widgets.Clear();
        _goalSlots.Clear();
        _goalsPanel.Widgets.Add(new Label { Text = loc["hud.quests"], TextColor = UiFactory.Accent });

        int shown = 0;
        for (int i = 0; i < content.Quests.Count && shown < 3; i++)
        {
            if (_simulation.IsQuestCompleted(i))
            {
                continue;
            }

            AddGoalSlot(loc[content.Quests[i].NameKey], content.Quests[i].Condition);
            shown++;
        }

        if (shown < 3)
        {
            long target = _simulation.DynamicQuestTarget;
            var dyn = content.QuestsDynamic;
            AddGoalSlot(loc.Format("quest.dynamic", target),
                new GoalCondition(dyn.BaseCondition.Kind, dyn.BaseCondition.Param, target));
        }
    }

    private void AddGoalSlot(string name, GoalCondition condition)
    {
        var slot = new VerticalStackPanel { Spacing = 1 };
        slot.Widgets.Add(new Label { Text = name });
        var progress = new Label { TextColor = new Color(150, 220, 150) };
        slot.Widgets.Add(progress);
        _goalsPanel.Widgets.Add(slot);
        _goalSlots.Add((condition, progress));
    }

    /// <summary>Přebuduje sledovač jen při změně; jinak jen aktualizuje čísla pokroku.</summary>
    private void UpdateGoals()
    {
        if (_goalsDirty)
        {
            RebuildGoals();
            _goalsDirty = false;
        }

        foreach (var (condition, progress) in _goalSlots)
        {
            long current = Math.Min(_simulation.EvaluateMetric(condition.Kind, condition.Param), condition.Target);
            progress.Text = $"{current} / {condition.Target}";
        }
    }

    /// <summary>„Zpět na město": vycentruje kameru na těžiště zástavby (nebo start, když nic nestojí).</summary>
    private void RecenterOnCity()
    {
        var buildings = _simulation.Buildings;
        if (buildings.Length == 0)
        {
            _camera.CenterOn(FindStartFocus(), MathF.Max(_camera.Zoom, 1.8f));
            return;
        }

        var sum = Vector2.Zero;
        for (int i = 0; i < buildings.Length; i++)
        {
            sum += new Vector2(buildings[i].X + 0.5f, buildings[i].Y + 0.5f);
        }

        var world = sum / buildings.Length * TerrainRenderer.TileSize;
        _camera.CenterOn(world, MathF.Max(_camera.Zoom, 1.8f));
    }

    /// <summary>Tlačítko budovy: ikona + jméno + cena z definice (žádné texty natvrdo).</summary>
    private Button BuildingButton(int defIndex)
    {
        var loc = _screens.Loc;
        var content = _screens.Content;
        var def = content.Buildings[defIndex];

        var caption = new VerticalStackPanel { Spacing = 2, HorizontalAlignment = HorizontalAlignment.Center };
        var sprite = _screens.Sprites.Get($"building.{def.Id}");
        if (sprite is not null)
        {
            caption.Widgets.Add(UiFactory.Icon(sprite, 28));
        }

        caption.Widgets.Add(new Label
        {
            Text = loc[def.NameKey],
            HorizontalAlignment = HorizontalAlignment.Center,
        });
        var priceLabel = new Label
        {
            Text = CostFormat.Line(content, loc, def.BuildCost),
            TextColor = Color.Gray,
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        caption.Widgets.Add(priceLabel);

        var button = new Button
        {
            Content = caption,
            Padding = new Thickness(10, 6),
            Background = new SolidBrush(new Color(38, 48, 64, 235)),
        };
        button.Click += (_, _) => _selectedBuilding = _selectedBuilding == defIndex ? -1 : defIndex;
        _buildButtons.Add((defIndex, button, priceLabel));
        return button;
    }

    private void RefreshHudTexts()
    {
        var loc = _screens.Loc;

        for (int i = 0; i < _resourceLabels.Length; i++)
        {
            _resourceLabels[i].Text = $"{(long)_simulation.GetResource(i)}/{(long)_simulation.GetStorageCap(i)}";
        }

        _populationLabel.Text = loc.Format("hud.population", (long)_simulation.Population, _simulation.HousingCapacity);

        double hours = _simulation.TimeOfDay01 * 24.0;
        _dayLabel.Text = loc.Format("hud.day", _simulation.DayNumber, (int)hours, (int)((hours - (int)hours) * 60));

        UpdateCursorLabel();
        UpdateStatusLabel();
        RefreshBuildAffordability();
        UpdateGoals();
    }

    private void UpdateCursorLabel()
    {
        var world = _camera.ScreenToWorld(_input.MousePosition.ToVector2());
        int tileX = (int)MathF.Floor(world.X / TerrainRenderer.TileSize);
        int tileY = (int)MathF.Floor(world.Y / TerrainRenderer.TileSize);

        // Nekonečná mapa — každá dlaždice má biom.
        var biome = _screens.Content.Biomes[_simulation.BiomeAt(tileX, tileY)];
        _cursorLabel.Text = _screens.Loc.Format("hud.cursor", tileX, tileY, _screens.Loc[biome.NameKey]);
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
        PlacementResult.Occupied => "build.error.occupied",
        PlacementResult.WrongBiome => "build.error.wrongBiome",
        PlacementResult.NotEnoughResources => "build.error.resources",
        _ => "build.title",
    };
}
