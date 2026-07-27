using CivDle.Audio;
using CivDle.Core.Content;
using CivDle.Core.Save;
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
    private readonly ZoneRenderer _zoneRenderer;
    private readonly LandmarkRenderer _landmarkRenderer;
    private readonly UfoRenderer _ufoRenderer;
    private readonly WeatherRenderer _weatherRenderer;
    private readonly BuildingRenderer _buildingRenderer;
    private readonly LightsRenderer _lightsRenderer;
    private readonly FaunaSystem _fauna;
    private readonly AgentSystem _agents;
    private readonly InputManager _input = new();
    private readonly FixedStepLoop _simLoop = new(Simulation.TicksPerSecond);
    private readonly ParticleSystem _particles = new();
    private readonly FloatingTextRenderer _floatingText = new();
    private readonly GameSounds _sounds = new();
    private readonly AmbientMusic _ambient = new();
    private readonly AmbientSoundscape _soundscape;
    private readonly MinimapRenderer _minimap;
    private readonly ToastRenderer _toasts;
    private readonly CityScaleRenderer _cityScale;
    private readonly VignetteRenderer _vignette;
    private readonly BubbleSystem _bubbles;
    private readonly GoldenSpawnSystem _golden;
    private readonly DiscoveryRenderer _discoveries;
    private readonly SpriteFontBase _popupFont;
    private readonly Dictionary<int, string> _popupTextCache = new();

    private Desktop _desktop = null!;
    private Label[] _resourceLabels = Array.Empty<Label>();
    private Label[] _resourceRateLabels = Array.Empty<Label>();
    private Widget[] _resourceChips = Array.Empty<Widget>();
    private int _knownResourceCount;
    private double[] _ratePrev = Array.Empty<double>();
    private double[] _perSecond = Array.Empty<double>();
    private float _rateTimer;
    private Label _populationLabel = null!;
    private Label _idleLabel = null!;
    private Label _eraLabel = null!;
    private Label _eraNextLabel = null!;
    private Label _tierLabel = null!;
    private Label _powerLabel = null!;
    private Label _weatherLabel = null!;
    private Label _dayLabel = null!;
    private Label _cursorLabel = null!;
    private Label _happinessLabel = null!;
    private Label _statusLabel = null!;
    private HorizontalStackPanel _buildCategoryPanel = null!;
    private HorizontalStackPanel _buildItemsPanel = null!;
    private string _selectedCategory = string.Empty;
    private readonly List<(int DefIndex, Button Button, Label PriceLabel)> _buildButtons = new();
    private ObjectiveTracker _objectives = null!;
    private readonly Queue<IScreen> _pendingIntros = new(); // uvítací overlaye (offline, denní odměna)
    private readonly Random _eventRng = new();
    private float _eventTimer;
    private Label _festivalLabel = null!;
    private Button _festivalButton = null!;
    private Button _buildMenuButton = null!;
    private Widget _buildMenuPanel = null!;
    private Widget _statusPanel = null!;
    private bool _buildMenuOpen;
    private int _unlockedFeatureCount = -1;

    /// <summary>Jak dlouho musí kurzor stát nad dlaždicí, než vyskočí bublina.</summary>
    private const float TileTooltipDelaySeconds = 0.45f;
    private int _hoverTileX = int.MinValue;
    private int _hoverTileY = int.MinValue;
    private float _hoverSeconds;

    /// <summary>Jak často se hra uloží sama (sekundy). Idle hru nesmí sežrat pád.</summary>
    private const float AutosaveIntervalSeconds = 120f;
    private float _autosaveTimer = AutosaveIntervalSeconds;

    /// <summary>Šířka pruhu pokroku ve sledovači úkolů (v pixelech).</summary>

    /// <summary>Nástroje mapy (stavba, sázení, zóny, přesun) i s jejich stavem.</summary>
    private readonly MapTools _tools;
    private int _knownBuildingCount;

    /// <param name="savedAtUtc">Čas uložení načtené hry — spustí offline dohon; <c>null</c> = nová hra.</param>
    public GameplayScreen(ScreenManager screens, Simulation simulation, WorldInfo info, DateTime? savedAtUtc = null)
    {
        _screens = screens;
        _simulation = simulation;
        _info = info;
        // Už odemčené achievementy z profilu, ať se v téhle hře nespouštějí znovu.
        _simulation.SeedUnlockedAchievements(screens.Profile.UnlockedAchievements);

        // Offline postup: dožeň čas od uložení a připrav uvítací souhrn.
        if (savedAtUtc is { } savedAt)
        {
            var summary = OfflineProgress.Apply(_simulation, savedAt, DateTime.UtcNow);
            SyncAchievements(); // co se odemklo offline, zapiš do profilu
            if (summary.Worthwhile)
            {
                _pendingIntros.Enqueue(new OfflineSummaryScreen(_screens, summary));
            }
        }

        GrantDailyReward();
        screens.DisposeMenuBackground(); // pod hrou už netiká ukázkové město z menu

        _terrainRenderer = new TerrainRenderer(screens.GraphicsDevice, screens.Content.Biomes, info.Seed);
        _decorationRenderer = new DecorationRenderer(screens.WhitePixel, screens.Content, info.Seed);
        _harvestables = new HarvestableRenderer(screens.Sprites, screens.Content);
        _roadRenderer = new RoadRenderer(screens.WhitePixel, screens.Content);
        _zoneRenderer = new ZoneRenderer(screens.WhitePixel, screens.Content);
        _landmarkRenderer = new LandmarkRenderer(screens.WhitePixel, screens.Content);
        _ufoRenderer = new UfoRenderer(screens.WhitePixel);
        _soundscape = new AmbientSoundscape(screens.Content);
        _tools = new MapTools(simulation, _camera, _input, screens.Content);
        _weatherRenderer = new WeatherRenderer(screens.WhitePixel, screens.Content);
        _buildingRenderer = new BuildingRenderer(screens.WhitePixel, screens.Content, screens.Sprites);
        _lightsRenderer = new LightsRenderer(screens.WhitePixel, screens.Content);
        _fauna = new FaunaSystem(screens.Content);
        _agents = new AgentSystem(screens.Content, screens.Sprites);
        _minimap = new MinimapRenderer(screens.GraphicsDevice, screens.Content.Biomes, screens.WhitePixel);
        _vignette = new VignetteRenderer(screens.GraphicsDevice);
        _bubbles = new BubbleSystem(screens.Sprites, screens.Content);
        _golden = new GoldenSpawnSystem(screens.Sprites, screens.Content);
        _discoveries = new DiscoveryRenderer(screens.Sprites);
        _popupFont = Stylesheet.Current.LabelStyle.Font;
        _toasts = new ToastRenderer(screens.WhitePixel, _popupFont);
        _cityScale = new CityScaleRenderer(screens.WhitePixel, _popupFont);

        var viewport = screens.GraphicsDevice.Viewport;
        _camera.SetViewport(viewport.Width, viewport.Height);
        _camera.CenterOn(FindStartFocus(), zoom: 2.2f);
        _knownBuildingCount = simulation.Buildings.Length; // načtená hra: bez juice za staré budovy

        _ratePrev = new double[_simulation.ResourceCount];
        _perSecond = new double[_simulation.ResourceCount];
        for (int i = 0; i < _ratePrev.Length; i++)
        {
            _ratePrev[i] = _simulation.GetResource(i);
        }

        BuildUi();
        _screens.Loc.LanguageChanged += BuildUi;
        _screens.UiSettingsChanged += BuildUi;
        _ambient.Play(); // klidná smyčka pro relaxační jádro
        _eventTimer = NextEventGap();
    }

    public bool IsOverlay => false;

    public void OnActivated()
    {
        _input.Resync();
        // Návrat z overlaye (výzkum, detail budovy, Vzestup) mohl odemknout budovy
        // nebo zresetovat éru — stavební menu přebuduj a srovnej počítadlo budov,
        // ať po Vzestupu (reset na 0 budov) nevystřelí prach za „nové" budovy.
        RefreshBuildMenu();
        _objectives.MarkDirty();
        ApplyMotionSettings(); // hráč se mohl vrátit z nastavení
        _knownBuildingCount = _simulation.Buildings.Length;
    }

    public void Update(GameTime gameTime)
    {
        // Uvítací overlaye (offline souhrn, denní odměna) postupně na první snímky.
        if (_pendingIntros.Count > 0)
        {
            _screens.Push(_pendingIntros.Dequeue());
            return;
        }

        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
        _input.Update();

        // Kulisa podle biomu a počasí — atmosféra stála skoro jen na obraze.
        _soundscape.Update(dt, _simulation);
        _hoverSeconds += dt;

        // Pravidelný autosave: idle hra běží hodiny, ztratit ji kvůli pádu
        // nebo zavření okna je to nejhorší, co se může stát.
        _autosaveTimer -= dt;
        if (_autosaveTimer <= 0f)
        {
            _autosaveTimer = AutosaveIntervalSeconds;
            SaveGame();
        }

        var viewport = _screens.GraphicsDevice.Viewport;
        _camera.SetViewport(viewport.Width, viewport.Height);

        if (_input.WasPressed(Keys.Escape) && !_tools.CancelTopmost())
        {
            if (_buildMenuOpen)
            {
                SetBuildMenuOpen(false);
            }
            else
            {
                _screens.Push(new PauseScreen(_screens, _simulation, _info));
                return;
            }
        }

        bool mouseOverUi = _desktop.IsMouseOverGUI;
        UpdateCamera(dt, mouseOverUi);

        // Nástroj si vstup buď vezme (staví, maluje, přesouvá), nebo ho pustí dál
        // na ruční těžbu — jediné místo, kde se to rozhoduje.
        if (!_tools.Update(mouseOverUi))
        {
            UpdateHarvest(mouseOverUi);
        }

        int ticks = _simLoop.Advance(gameTime.ElapsedGameTime.TotalSeconds);
        for (int i = 0; i < ticks; i++)
        {
            _simulation.Tick();
        }

        UpdateEventScheduler(dt);
        SampleRates(dt);

        EmitNewBuildingJuice();
        _harvestables.Update(dt);
        _particles.Update(dt);
        _floatingText.Update(dt);
        // Při velkém oddálení chodce/faunu neaktualizuj — nespawnovali by se přes
        // obří viditelnou plochu (a stejně se nekreslí; z výšky vidíš hustotu).
        if (_camera.Zoom >= CityScaleRenderer.ThresholdZoom)
        {
            _fauna.Update(dt, _camera, _simulation);
            _agents.Update(dt, _camera, _simulation);
            _bubbles.Update(dt, _simulation);
            _golden.Update(dt, _camera, _simulation);
            _discoveries.Update(dt);
        }

        _weatherRenderer.Update(dt, _simulation, _screens.GraphicsDevice.Viewport);
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
        _zoneRenderer.Draw(spriteBatch, _camera, _simulation); // tint zón na zemi, pod budovami
        // Landmarky jen zblízka (LOD): z výšky jsou stejně pod rozlišením a dotaz
        // na desítky tisíc dlaždic by zbytečně žral snímky.
        if (_camera.Zoom >= LandmarkRenderer.MinZoom)
        {
            _landmarkRenderer.Draw(spriteBatch, _camera, _simulation);
        }

        // Velké oddálení → agregátní pohled na měřítko (hustota + populace) místo
        // drobných jednotlivců (game-feel-wow: „koukni, jak to vyrostlo").
        if (_camera.Zoom >= CityScaleRenderer.ThresholdZoom)
        {
            _harvestables.Draw(spriteBatch, _camera, _simulation);
            _discoveries.Draw(spriteBatch, _camera, _simulation);
            _roadRenderer.Draw(spriteBatch, _camera, _simulation);
            _buildingRenderer.Draw(spriteBatch, _camera, _simulation);
            _agents.Draw(spriteBatch, _camera);
            _fauna.Draw(spriteBatch, _screens.WhitePixel, _camera);
            _bubbles.Draw(spriteBatch, _camera);
            _golden.Draw(spriteBatch, _camera);
        }
        else
        {
            _roadRenderer.Draw(spriteBatch, _camera, _simulation); // cesty dávají kontext i z výšky
            _cityScale.Draw(spriteBatch, _screens.GraphicsDevice.Viewport, _camera, _simulation);
        }

        // UFO letí nad vším na mapě — je to událost, ne kulisa.
        _ufoRenderer.Draw(spriteBatch, _camera, _simulation, (float)gameTime.TotalGameTime.TotalSeconds);

        _particles.Draw(spriteBatch, _screens.WhitePixel, _camera);

        // Den/noc: ztmavení scény a pak aditivní světla, ať září skrz tmu.
        double timeOfDay = _simulation.TimeOfDay01;
        DayNightCycle.DrawOverlay(
            spriteBatch, _screens.WhitePixel, _screens.GraphicsDevice.Viewport,
            _screens.Content.Gameplay.DayNight, timeOfDay);
        _lightsRenderer.Draw(spriteBatch, _camera, _simulation, DayNightCycle.NightFactor(timeOfDay));

        // Duch pod kurzorem — co přesně se kreslí, ví MapTools; obrazovka to jen zobrazí.
        if (_tools.GhostVisible && _tools.SelectedBuilding >= 0)
        {
            var def = _screens.Content.Buildings[_tools.SelectedBuilding];
            _buildingRenderer.DrawGhost(
                spriteBatch, _camera, def, _tools.GhostX, _tools.GhostY, _tools.GhostResult == PlacementResult.Ok);
        }
        else if (_tools.MoveGhostActive && _tools.MovingBuildingIndex < _simulation.Buildings.Length)
        {
            var def = _screens.Content.Buildings[_simulation.Buildings[_tools.MovingBuildingIndex].DefIndex];
            _buildingRenderer.DrawGhost(
                spriteBatch, _camera, def, _tools.MoveGhostX, _tools.MoveGhostY,
                _tools.MoveGhostResult == PlacementResult.Ok);
        }

        if (_tools.PlantGhostActive && _screens.Sprites.Get("node.tree") is { } plantSprite)
        {
            const int ts = TerrainRenderer.TileSize;
            var tint = (_tools.PlantGhostResult == PlacementResult.Ok
                ? new Color(120, 240, 140)
                : new Color(240, 110, 100)) * 0.7f;
            spriteBatch.Begin(samplerState: SamplerState.PointClamp, transformMatrix: _camera.Transform);
            spriteBatch.Draw(plantSprite, new Rectangle(_tools.PlantGhostX * ts, _tools.PlantGhostY * ts, ts, ts), tint);
            spriteBatch.End();
        }

        if (_tools.TerraformGhostActive)
        {
            const int ts = TerrainRenderer.TileSize;
            var tint = (_tools.TerraformGhostResult == PlacementResult.Ok
                ? new Color(140, 230, 200)
                : new Color(240, 110, 100)) * 0.55f;
            spriteBatch.Begin(samplerState: SamplerState.PointClamp, transformMatrix: _camera.Transform);
            spriteBatch.Draw(_screens.WhitePixel,
                new Rectangle(_tools.TerraformGhostX * ts, _tools.TerraformGhostY * ts, ts, ts), tint);
            spriteBatch.End();
        }

        if (_tools.ZonePreviewActive)
        {
            var preview = _tools.ZonePreview;
            _zoneRenderer.DrawPreview(spriteBatch, _camera, _tools.ZonePaintTypeIndex,
                preview.X, preview.Y, preview.Width, preview.Height);
        }

        _weatherRenderer.Draw(spriteBatch, _screens.GraphicsDevice.Viewport); // závoj + srážky nad scénou
        _vignette.Draw(spriteBatch, _screens.GraphicsDevice.Viewport); // decentní sevření pohledu, pod HUD
        _desktop.Render();

        // Minimapa, popupy a toasty až nad UI — hráč je nesmí přehlédnout.
        _minimap.Draw(spriteBatch, _screens.GraphicsDevice.Viewport, _camera, _simulation);
        _floatingText.Draw(spriteBatch, _camera, _popupFont);
        DrawSettlementLabels(spriteBatch);
        DrawTileTooltip(spriteBatch);
        _toasts.Draw(spriteBatch, _screens.GraphicsDevice.Viewport);
    }

    /// <summary>
    /// Bublina u kurzoru s tím, co je na dlaždici pod myší: terén, a hlavně jméno
    /// zvláštnosti (prastarý strom, žíla, skrýš). Bez toho hráč koukal na „nějaké
    /// kostičky" a neměl jak zjistit, co to je.
    /// </summary>
    private void DrawTileTooltip(SpriteBatch spriteBatch)
    {
        if (_desktop.IsMouseOverGUI || _camera.Zoom < LandmarkRenderer.MinZoom)
        {
            return;
        }

        var world = _camera.ScreenToWorld(_input.MousePosition.ToVector2());
        int tileX = (int)MathF.Floor(world.X / TerrainRenderer.TileSize);
        int tileY = (int)MathF.Floor(world.Y / TerrainRenderer.TileSize);

        // Bublina vyskočí, teprve když kurzor chvíli stojí nad TOUŽ dlaždicí.
        // Bez prodlevy poskakovala po celé mapě při každém pohnutí myší a byla
        // spíš na obtíž než k užitku.
        if (tileX != _hoverTileX || tileY != _hoverTileY)
        {
            _hoverTileX = tileX;
            _hoverTileY = tileY;
            _hoverSeconds = 0f;
            return;
        }

        if (_hoverSeconds < TileTooltipDelaySeconds)
        {
            return;
        }

        var loc = _screens.Loc;
        var content = _screens.Content;
        string title = loc[content.Biomes[_simulation.BiomeAt(tileX, tileY)].NameKey];
        string? body = null;
        Color? accent = null;

        int landmark = _simulation.LandmarkAt(tileX, tileY);
        if (landmark >= 0)
        {
            title = loc[content.Landmarks[landmark].NameKey];
            body = loc["tip.landmark"];
            accent = new Color(255, 215, 120);
        }
        else if (_simulation.IsDiscoveryTile(tileX, tileY) && !_simulation.IsDiscoveryClaimed(tileX, tileY))
        {
            title = loc["tip.discovery"];
            body = loc["tip.discovery.desc"];
            accent = new Color(160, 220, 255);
        }
        else if (_simulation.TryGetPlantedNode(tileX, tileY, out int plantedResource))
        {
            title = loc[content.Resources[plantedResource].NameKey];
            body = loc["tip.planted"];
            accent = new Color(150, 230, 150);
        }

        HoverTooltip.Draw(spriteBatch, _screens.WhitePixel, _popupFont,
            _screens.GraphicsDevice.Viewport, _input.MousePosition, title, body, accent);
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
        _screens.UiSettingsChanged -= BuildUi;
        _terrainRenderer.Dispose();
        _minimap.Dispose();
        _vignette.Dispose();
        _ambient.Dispose();
        _soundscape.Stop();
        _soundscape.Dispose();
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
        if (_tools.SelectedBuilding >= 0 || mouseOverUi || !_input.WasLeftPressed)
        {
            return;
        }

        var world = _camera.ScreenToWorld(_input.MousePosition.ToVector2());
        int tileX = (int)MathF.Floor(world.X / TerrainRenderer.TileSize);
        int tileY = (int)MathF.Floor(world.Y / TerrainRenderer.TileSize);

        // Sběrné bubliny a zlaté spawny mají přednost před budovou i těžbou.
        if (_bubbles.TryCollect(world, _simulation, out int bubbleRes, out int bubbleAmt, out var bubblePos))
        {
            CollectFeedback(bubbleRes, bubbleAmt, bubblePos);
            return;
        }

        if (_golden.TryCollect(world, _simulation, out int goldRes, out int goldAmt, out var goldPos))
        {
            CollectFeedback(goldRes, goldAmt, goldPos);
            return;
        }

        // Skrýš na mapě (objevování) — klik ji vyzvedne.
        if (_simulation.TryClaimDiscovery(tileX, tileY, out int discRes, out int discAmt))
        {
            var center = new Vector2((tileX + 0.5f) * TerrainRenderer.TileSize, (tileY + 0.5f) * TerrainRenderer.TileSize);
            CollectFeedback(discRes, discAmt, center);
            return;
        }

        // Klik na budovu ji rozklikne (detail + vylepšení + přesun/demolice).
        if (_simulation.TryGetBuildingAt(tileX, tileY, out int buildingIndex))
        {
            _screens.Push(new BuildingInfoScreen(_screens, _simulation, buildingIndex, _tools.StartMove));
            return;
        }

        // Klik na obyvatele → bublina s myšlenkou (svět reaguje na dotek).
        if (_agents.TryPokeAgent(world, TerrainRenderer.TileSize * 0.6f, out var agentPos))
        {
            string key = $"citizen.thought{1 + _eventRng.Next(5)}";
            _floatingText.Add(agentPos - new Vector2(0f, TerrainRenderer.TileSize * 0.4f), _screens.Loc[key], Color.White);
            return;
        }

        if (!_simulation.TryHarvest(tileX, tileY, out int resourceIndex, out int amount, out var outcome))
        {
            return;
        }

        var content = _screens.Content;
        var tileCenter = new Vector2((tileX + 0.5f) * TerrainRenderer.TileSize, (tileY + 0.5f) * TerrainRenderer.TileSize);
        var resourceColor = content.Resources[resourceIndex].MapColor.ToXna();
        var biomeColor = content.Biomes[_simulation.BiomeAt(tileX, tileY)].MapColor.ToXna();

        switch (outcome)
        {
            case HarvestOutcome.Jackpot:
                // Úlovek života (velryba, obří žíla): největší oslava, jakou hra má —
                // je to vzácnost odemčená až Vzestupem, musí to být poznat.
                _floatingText.Add(tileCenter, _screens.Loc.Format("hud.jackpot", amount), new Color(120, 235, 255));
                _particles.SpawnBurst(tileCenter, new Color(120, 235, 255), 48, 80f, 340f);
                _particles.SpawnBurst(tileCenter, new Color(255, 255, 255), 24, 40f, 180f);
                _sounds.PlayChime();
                break;

            case HarvestOutcome.Crit:
                // Krit: zlatý velký popup + extra jiskry + cinknutí — aktivní klikání se vyplatí.
                _floatingText.Add(tileCenter, _screens.Loc.Format("hud.crit", amount), new Color(255, 215, 80));
                _particles.SpawnBurst(tileCenter, new Color(255, 215, 80), 20, 60f, 200f);
                _sounds.PlayChime();
                break;

            default:
                _floatingText.Add(tileCenter, PopupText(resourceIndex, amount), resourceColor);
                break;
        }

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

    /// <summary>Zpětná vazba na posbíranou bublinu / zlatý spawn: zlatý popup, jiskry, cinknutí.</summary>
    private void CollectFeedback(int resourceIndex, int amount, Vector2 worldPos)
    {
        var color = new Color(255, 224, 130);
        _floatingText.Add(worldPos, $"+{amount} {_screens.Loc[_screens.Content.Resources[resourceIndex].NameKey]}", color);
        _particles.SpawnBurst(worldPos, color, 16, 55f, 190f);
        _sounds.PlayChime();
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
            _sounds.PlayChime(); // dobrá zpráva → příjemné cinknutí

            // Splněný úkol / Vzestup mění seznam aktivních cílů — přestav sledovač.
            if (note.Kind is NotificationKind.QuestCompleted or NotificationKind.Ascended)
            {
                _objectives.MarkDirty();
            }

            // Nový achievement → zapiš do účet-wide profilu (přežije i restart).
            if (note.Kind == NotificationKind.AchievementUnlocked)
            {
                SyncAchievements();
            }
        }
    }

    /// <summary>Občas spustí náhodnou událost s volbami (mikro-rozhodnutí).</summary>
    private void UpdateEventScheduler(float dt)
    {
        if (_screens.Content.Events.Count == 0)
        {
            return;
        }

        _eventTimer -= dt;
        if (_eventTimer > 0f)
        {
            return;
        }

        _eventTimer = NextEventGap();
        if (PickEligibleEvent() is { } chosen)
        {
            _screens.Push(new EventScreen(_screens, _simulation, chosen));
        }
    }

    /// <summary>
    /// Vybere náhodnou událost, na kterou už město dorostlo. Bez filtru by nabízel
    /// kupec ocel osadě, která ještě neumí bronz — a nabídka, kterou hráč nemůže
    /// využít, je horší než žádná událost.
    ///
    /// <para>Reservoir sampling: rovnoměrný výběr jedním průchodem, bez pomocného
    /// seznamu (událostí jsou desítky a tohle běží jednou za ~10 minut, ale je to
    /// stejně krátké jako alokovat).</para>
    /// </summary>
    private EventDef? PickEligibleEvent()
    {
        var events = _screens.Content.Events;
        EventDef? chosen = null;
        int seen = 0;
        for (int i = 0; i < events.Count; i++)
        {
            var candidate = events[i];
            if (candidate.Requirement is { } requirement
                && _simulation.EvaluateMetric(requirement.Kind, requirement.Param) < requirement.Target)
            {
                continue;
            }

            seen++;
            if (_eventRng.Next(seen) == 0)
            {
                chosen = candidate;
            }
        }

        return chosen;
    }

    /// <summary>
    /// Rozestup náhodných událostí. Záměrně řídký: událost má být milé vyrušení,
    /// ne přerušování každou minutu — vyskakovací okno bere hráči kontrolu.
    /// </summary>
    /// <summary>
    /// Rozestup náhodných událostí. Původní ~4–7 min bylo na relaxační hru moc —
    /// vyskakovací okno každou chvíli ruší, místo aby bylo zpestřením.
    /// </summary>
    private float NextEventGap() => 540f + (float)_eventRng.NextDouble() * 420f; // ~9–16 min

    /// <summary>Jednou za sekundu spočítá čistý přírůstek surovin za sekundu (HUD ticker).</summary>
    private void SampleRates(float dt)
    {
        _rateTimer += dt;
        if (_rateTimer < 1f)
        {
            return;
        }

        for (int i = 0; i < _perSecond.Length; i++)
        {
            double now = _simulation.GetResource(i);
            _perSecond[i] = (now - _ratePrev[i]) / _rateTimer;
            _ratePrev[i] = now;
        }

        _rateTimer = 0f;
    }

    /// <summary>Vyhodnotí a udělí denní odměnu (účet-wide, roste se sérií dní).</summary>
    private void GrantDailyReward()
    {
        var profile = _screens.Profile;
        var now = DateTime.UtcNow;
        var result = DailyReward.Evaluate(_screens.Content.Gameplay.DailyReward, profile.LastDailyRewardDate, profile.DailyStreak, now);
        if (!result.Due || result.Reward.Count == 0)
        {
            return;
        }

        foreach (var amount in result.Reward)
        {
            _simulation.AddResource(amount.ResourceIndex, amount.Amount);
        }

        profile.LastDailyRewardDate = DailyReward.TodayKey(now);
        profile.DailyStreak = result.Streak;
        _screens.SaveProfile();
        _pendingIntros.Enqueue(new DailyRewardScreen(_screens, result.Streak, result.Reward));
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

        // Horní pruh: suroviny (ikony) + zásoba/kapacita + přírůstek za sekundu.
        var resourceBar = new HorizontalStackPanel { Spacing = 18 };
        _resourceLabels = new Label[content.Resources.Count];
        _resourceRateLabels = new Label[content.Resources.Count];
        _resourceChips = new Widget[content.Resources.Count];
        for (int i = 0; i < content.Resources.Count; i++)
        {
            var chip = new HorizontalStackPanel { Spacing = 5 };
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
            _resourceRateLabels[i] = new Label { VerticalAlignment = VerticalAlignment.Center, TextColor = new Color(120, 190, 130) };
            chip.Widgets.Add(_resourceRateLabels[i]);
            chip.Tooltip = ResourceTooltip(i);
            // Neznámá surovina se v pruhu vůbec neukáže — hra nesmí prozrazovat
            // obsah, ke kterému se hráč ještě nedostal (odhalí se získáním).
            chip.Visible = _simulation.IsResourceKnown(i);
            _resourceChips[i] = chip;
            resourceBar.Widgets.Add(chip);
        }

        _populationLabel = new Label { VerticalAlignment = VerticalAlignment.Center, TextColor = UiFactory.Accent };
        resourceBar.Widgets.Add(_populationLabel);

        // Nevyužité budovy se musí ohlásit: bez dělníků nevyrábějí a hráč by jinak
        // jen viděl, že mu stavění přestalo něco přinášet, aniž by věděl proč.
        _idleLabel = new Label
        {
            VerticalAlignment = VerticalAlignment.Center,
            TextColor = new Color(240, 180, 90),
            Tooltip = _screens.Loc["tip.idleBuildings"],
        };
        resourceBar.Widgets.Add(_idleLabel);

        var topLeft = UiFactory.DarkPanel(resourceBar);
        topLeft.HorizontalAlignment = HorizontalAlignment.Left;
        topLeft.VerticalAlignment = VerticalAlignment.Top;
        topLeft.Margin = new Thickness(10, 10, 0, 0);

        // Pravý horní roh: éra + den/čas + dlaždice pod kurzorem.
        var loc = _screens.Loc;
        _eraLabel = new Label { TextColor = new Color(210, 185, 120), HorizontalAlignment = HorizontalAlignment.Right, Tooltip = loc["tip.era"] };
        _eraNextLabel = new Label { TextColor = new Color(160, 150, 120), HorizontalAlignment = HorizontalAlignment.Right, Tooltip = loc["tip.eraNext"] };
        _tierLabel = new Label { TextColor = new Color(190, 160, 230), HorizontalAlignment = HorizontalAlignment.Right, Tooltip = loc["tip.tier"] };
        _powerLabel = new Label { TextColor = new Color(120, 200, 240), HorizontalAlignment = HorizontalAlignment.Right, Tooltip = loc["tip.power"] };
        _weatherLabel = new Label { TextColor = new Color(170, 200, 220), HorizontalAlignment = HorizontalAlignment.Right, Tooltip = loc["tip.weather"] };
        _happinessLabel = new Label { HorizontalAlignment = HorizontalAlignment.Right, Tooltip = loc["tip.happiness"] };
        _dayLabel = new Label { TextColor = UiFactory.Accent, Tooltip = loc["tip.day"] };
        _cursorLabel = new Label { TextColor = Color.LightGray };
        var worldInfoStack = new VerticalStackPanel { Spacing = 3, HorizontalAlignment = HorizontalAlignment.Right };
        worldInfoStack.Widgets.Add(_eraLabel);
        worldInfoStack.Widgets.Add(_eraNextLabel);
        worldInfoStack.Widgets.Add(_tierLabel);
        worldInfoStack.Widgets.Add(_powerLabel);
        worldInfoStack.Widgets.Add(_weatherLabel);
        if (_screens.Content.Gameplay.Happiness.IsEnabled)
        {
            worldInfoStack.Widgets.Add(_happinessLabel);
        }
        worldInfoStack.Widgets.Add(_dayLabel);
        worldInfoStack.Widgets.Add(_cursorLabel);
        var topRight = UiFactory.DarkPanel(worldInfoStack);
        topRight.HorizontalAlignment = HorizontalAlignment.Right;
        topRight.VerticalAlignment = VerticalAlignment.Top;
        topRight.Margin = new Thickness(0, 10, 10, 0);

        // Stavební menu je VYSKAKOVACÍ: spodek obrazovky má zůstat úzký proužek,
        // katalog budov vyjede nad ním až na kliknutí (a zase se zavře).
        _buildCategoryPanel = new HorizontalStackPanel { Spacing = 6, HorizontalAlignment = HorizontalAlignment.Center };
        _buildItemsPanel = new HorizontalStackPanel { Spacing = 8, HorizontalAlignment = HorizontalAlignment.Center };

        var buildStack = new VerticalStackPanel { Spacing = 6, HorizontalAlignment = HorizontalAlignment.Center };
        buildStack.Widgets.Add(_buildCategoryPanel);
        buildStack.Widgets.Add(_buildItemsPanel);
        _buildMenuPanel = UiFactory.DarkPanel(buildStack);
        _buildMenuPanel.HorizontalAlignment = HorizontalAlignment.Center;
        _buildMenuPanel.Visible = _buildMenuOpen;

        // Stavový řádek zůstává vidět i se zavřeným menu — nese hlášky režimů
        // („sázíš", „sem to nejde") a bez nich by hráč tápal.
        _statusLabel = new Label { HorizontalAlignment = HorizontalAlignment.Center };
        _statusPanel = UiFactory.DarkPanel(_statusLabel);
        _statusPanel.HorizontalAlignment = HorizontalAlignment.Center;

        // Levý střed: „co teď" — hlavní cíl s návodem, pod ním ostatní úkoly.
        _objectives = new ObjectiveTracker(_screens, _simulation, FocusOn, SkipGuide);

        // Spodní blok zdola nahoru: proužek tlačítek, nad ním stav, a úplně nahoře
        // vyskakovací katalog budov. Nahoře na obrazovce zůstávají suroviny (vlevo)
        // a stav světa (vpravo) — spodek patří ovládání.
        var bottomBar = new VerticalStackPanel
        {
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(0, 0, 0, 12),
        };
        bottomBar.Widgets.Add(_buildMenuPanel);
        bottomBar.Widgets.Add(_statusPanel);
        bottomBar.Widgets.Add(BuildToolButtons());

        var root = new Panel();
        root.Widgets.Add(topLeft);
        root.Widgets.Add(topRight);
        root.Widgets.Add(_objectives.Root);
        root.Widgets.Add(bottomBar);

        _desktop = _screens.NewDesktop(root);
        ApplyMotionSettings();
        RefreshBuildMenu();
        RefreshHudTexts();
    }

    /// <summary>
    /// Lišta rychlých akcí (úkoly, sázení, zóny, výzkum, guvernér, Vzestup…).
    /// Vodorovný pruh DOLE nad stavebním menu — spodek obrazovky patří akcím,
    /// horní okraj zůstává na suroviny a stav světa.
    /// </summary>
    private Widget BuildToolButtons()
    {
        var loc = _screens.Loc;
        var stack = new HorizontalStackPanel { Spacing = 6, HorizontalAlignment = HorizontalAlignment.Center };

        // „Stavět" vytáhne katalog budov NAD lištu — spodek obrazovky tak zůstává
        // úzký proužek, ne trvale rozložené menu přes půl mapy.
        _buildMenuButton = UiFactory.SmallButton(loc["hud.build"], ToggleBuildMenu, loc["tip.build"]);
        stack.Widgets.Add(_buildMenuButton);

        stack.Widgets.Add(UiFactory.SmallButton(loc["hud.quests"],
            () => _screens.Push(new QuestsScreen(_screens, _simulation)), loc["tip.quests"]));

        // Každá funkce se objeví, teprve až si ji hráč odemkne (data/features.json).
        if (_simulation.IsFeatureUnlocked("plant"))
        {
            stack.Widgets.Add(UiFactory.SmallButton(loc["hud.plant"], _tools.TogglePlant, loc["tip.plant"]));
        }

        // Zóny (automatizace): jedno tlačítko na typ; klik = malovat, další klik na stejný = ven.
        var zoneTypes = _simulation.IsFeatureUnlocked("zones")
            ? _screens.Content.ZoneTypes
            : null;
        for (int z = 0; zoneTypes is not null && z < zoneTypes.Count; z++)
        {
            int typeIndex = z;
            stack.Widgets.Add(UiFactory.SmallButton(loc[zoneTypes[z].NameKey],
                () => _tools.ToggleZone(typeIndex), ZoneTooltip(zoneTypes[z])));
        }
        // Přetváření krajiny: jedno tlačítko na zásah, odemyká se výzkumem.
        var terraform = _simulation.IsFeatureUnlocked("terraform") ? _screens.Content.Terraform : null;
        for (int a = 0; terraform is not null && a < terraform.Count; a++)
        {
            int actionIndex = a;
            var action = terraform[a];
            if (_simulation.CanTerraform(actionIndex, 0, 0) == PlacementResult.NotUnlocked)
            {
                continue; // technologie ještě není — nástroj se vůbec neukáže
            }

            stack.Widgets.Add(UiFactory.SmallButton(loc[action.NameKey],
                () => _tools.ToggleTerraform(actionIndex),
                loc[action.DescriptionKey] + '\n' + loc.Format("panel.cost",
                    CostFormat.Line(_screens.Content, loc, action.Cost))));
        }

        stack.Widgets.Add(UiFactory.SmallButton(loc["hud.backToCity"], RecenterOnCity, loc["tip.backToCity"]));

        if (_simulation.IsFeatureUnlocked("settlements"))
        {
            stack.Widgets.Add(UiFactory.SmallButton(loc["hud.settlements"],
                () => _screens.Push(new SettlementsScreen(_screens, _simulation, _camera)), loc["tip.settlements"]));
        }

        if (_screens.Content.Techs.Count > 0 && _simulation.IsFeatureUnlocked("research"))
        {
            stack.Widgets.Add(UiFactory.SmallButton(loc["hud.tech"],
                () => _screens.Push(new TechScreen(_screens, _simulation)), loc["tip.tech"]));
        }

        if (_screens.Content.Policies.Count > 0 && _simulation.IsFeatureUnlocked("governor"))
        {
            stack.Widgets.Add(UiFactory.SmallButton(loc["hud.governor"],
                () => _screens.Push(new PoliciesScreen(_screens, _simulation)), loc["tip.governor"]));
        }

        if (_simulation.IsFeatureUnlocked("ascend"))
        {
            stack.Widgets.Add(UiFactory.SmallButton(loc["hud.ascend"],
                () => _screens.Push(new AscensionScreen(_screens, _simulation)), loc["tip.ascend"]));
        }

        stack.Widgets.Add(UiFactory.SmallButton(loc["hud.achievements"],
            () => _screens.Push(new AchievementsScreen(_screens, _simulation)), loc["tip.achievements"]));

        // Slavnost: aktivní boost na kliknutí (stav se přepisuje v RefreshHudTexts).
        _festivalLabel = new Label
        {
            Text = loc["hud.festival"],
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        _festivalButton = new Button
        {
            Content = _festivalLabel,
            Height = 36,
            Padding = new Thickness(12, 0),
            Background = new SolidBrush(new Color(150, 90, 60, 235)),
            // Slavnost bez vysvětlení byla záhada — tooltip říká násobič i délku z dat.
            Tooltip = loc.Format("tip.festival",
                _screens.Content.Gameplay.Boost.Multiplier.ToString("0.#"),
                _screens.Content.Gameplay.Boost.DurationSeconds,
                _screens.Content.Gameplay.Boost.CooldownSeconds),
        };
        _festivalButton.Click += (_, _) => _simulation.TryStartBoost();
        if (_simulation.IsFeatureUnlocked("festival"))
        {
            stack.Widgets.Add(_festivalButton);
        }

        var panel = UiFactory.DarkPanel(stack);
        panel.HorizontalAlignment = HorizontalAlignment.Center;
        return panel;
    }

    /// <summary>
    /// Otevře/zavře vyskakovací katalog budov. Zavřením se ruší i rozestavěná
    /// volba — jinak by hráči zůstal duch budovy pod kurzorem bez menu, ze
    /// kterého vznikl.
    /// </summary>
    private void ToggleBuildMenu()
    {
        SetBuildMenuOpen(!_buildMenuOpen);
    }

    private void SetBuildMenuOpen(bool open)
    {
        _buildMenuOpen = open;
        _buildMenuPanel.Visible = open;
        _tools.Clear(); // otevření i zavření menu vždy začíná s čistým stolem
    }

    /// <summary>
    /// Popisek suroviny u kurzoru: kdo ji vyrábí a kdo ji spotřebovává. Skládá se
    /// z definic budov, takže nová surovina v JSON dostane vysvětlení sama.
    /// </summary>
    private string ResourceTooltip(int resourceIndex)
    {
        var loc = _screens.Loc;
        var content = _screens.Content;
        var producers = new List<string>();
        var consumers = new List<string>();
        for (int i = 0; i < content.Buildings.Count; i++)
        {
            if (content.Buildings[i].Recipe is not { } recipe)
            {
                continue;
            }

            string name = loc[content.Buildings[i].NameKey];
            if (recipe.Outputs.Any(o => o.ResourceIndex == resourceIndex)) producers.Add(name);
            if (recipe.Inputs.Any(o => o.ResourceIndex == resourceIndex)) consumers.Add(name);
        }

        var text = new System.Text.StringBuilder(loc[content.Resources[resourceIndex].NameKey]);
        if (producers.Count > 0)
        {
            text.Append('\n').Append(loc.Format("tip.resource.producedBy", string.Join(", ", producers.Take(6))));
        }

        if (consumers.Count > 0)
        {
            text.Append('\n').Append(loc.Format("tip.resource.usedBy", string.Join(", ", consumers.Take(6))));
        }

        return text.ToString();
    }

    /// <summary>Popisek typu zóny: čím ji automat zaplňuje (z priority v datech).</summary>
    private string ZoneTooltip(ZoneTypeDef zone)
    {
        var loc = _screens.Loc;
        var content = _screens.Content;
        string buildings = string.Join(", ", zone.BuildingIndices.Select(b => loc[content.Buildings[b].NameKey]));
        return loc.Format("tip.zone", buildings);
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
        var loc = _screens.Loc;
        var content = _screens.Content;
        // Se zapnutými barevnými vodítky nese „mám / nemám" i značka před cenou,
        // aby to nezáviselo jen na rozlišení zelené a červené.
        bool cues = _screens.Settings.ColorCues;
        foreach (var (defIndex, button, priceLabel) in _buildButtons)
        {
            bool affordable = _simulation.CanAfford(defIndex);
            string price = CostFormat.Line(content, loc, content.Buildings[defIndex].BuildCost);
            priceLabel.Text = cues ? loc[affordable ? "cue.yes" : "cue.no"] + ' ' + price : price;
            priceLabel.TextColor = affordable ? new Color(150, 220, 150) : new Color(232, 120, 110);
            button.Background = new SolidBrush(affordable ? new Color(38, 48, 64, 235) : new Color(30, 34, 42, 170));
        }
    }

    /// <summary>
    /// Uloží hru na pozadí (autosave). Selhání se schválně nehlásí vyskakovacím
    /// oknem — je to tichá pojistka, ne akce hráče; ruční uložení v pauze dál
    /// výsledek hlásí.
    /// </summary>
    private void SaveGame() =>
        _screens.Saves.TrySave(_simulation, new SaveMetadata(_info.Seed, _info.SizeId, _info.PresetId, DateTime.UtcNow));

    /// <summary>Promítne přístupnostní volbu „omezit pohyb" do vizuálních efektů.</summary>
    private void ApplyMotionSettings()
    {
        bool motion = !_screens.Settings.ReduceMotion;
        _particles.Enabled = motion;
        _floatingText.Enabled = motion;
    }

    /// <summary>
    /// Řádek „kam to celé směřuje": příští éra a technologie, která ji otevře.
    /// Samotné jméno aktuální éry je jen údaj — tohle z něj dělá směr, aby hráč
    /// věděl, že hra někam vede (zpětná vazba „nemám jasný cíl").
    /// </summary>
    private string NextEraLine(int currentEraIndex)
    {
        var eras = _screens.Content.Eras;
        int currentOrder = currentEraIndex >= 0 ? eras[currentEraIndex].Order : int.MinValue;

        EraDef? next = null;
        foreach (var era in eras.All)
        {
            if (era.Order > currentOrder && (next is null || era.Order < next.Order))
            {
                next = era;
            }
        }

        // Poslední éra (nebo éra bez technologie) už nikam neukazuje — pak radši nic.
        if (next is null || next.UnlockTechId.Length == 0
            || !_screens.Content.Techs.TryIndexOf(next.UnlockTechId, out int techIndex))
        {
            return string.Empty;
        }

        var loc = _screens.Loc;
        return loc.Format("hud.eraNext", loc[next.NameKey], loc[_screens.Content.Techs[techIndex].NameKey]);
    }

    /// <summary>
    /// Tlačítko „Ukaž mi" u hlavního cíle: otevře to, co krok potřebuje. Data
    /// říkají jen „tohle je cíl", překlad na obrazovku/nástroj patří sem —
    /// obsah tak nemusí vědět nic o UI.
    /// </summary>
    private void FocusOn(FocusHint focus)
    {
        switch (focus.Kind)
        {
            case FocusKind.Map:
                RecenterOnCity();
                break;

            case FocusKind.Build:
                SetBuildMenuOpen(true);
                _selectedCategory = _screens.Content.Buildings[focus.BuildingIndex].Category;
                RefreshBuildMenu();
                // Rovnou i vybrat: hráč tak jen klikne do mapy a je hotovo.
                if (_simulation.IsBuildingBuildable(focus.BuildingIndex))
                {
                    _tools.ToggleBuilding(focus.BuildingIndex);
                }

                break;

            case FocusKind.Tool when focus.Target == "plant":
                SetBuildMenuOpen(false);
                _tools.TogglePlant();
                break;

            case FocusKind.Screen when focus.Target == "tech":
                _screens.Push(new TechScreen(_screens, _simulation));
                break;

            case FocusKind.Screen when focus.Target == "quests":
                _screens.Push(new QuestsScreen(_screens, _simulation));
                break;

            case FocusKind.Screen when focus.Target == "ascend":
                _screens.Push(new AscensionScreen(_screens, _simulation));
                break;
        }
    }

    /// <summary>Vypne průvodce natrvalo; hlavním cílem se stane první nesplněný úkol.</summary>
    private void SkipGuide()
    {
        _simulation.SkipTutorial();
        _objectives.MarkDirty();
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
            // Popisek u kurzoru se skládá z definice — nová budova v JSON má
            // vysvětlení hned, bez ručně psaného textu.
            Tooltip = BuildingSummary.Describe(content, loc, def),
        };
        button.Click += (_, _) => _tools.ToggleBuilding(defIndex);
        _buildButtons.Add((defIndex, button, priceLabel));
        return button;
    }

    private void RefreshHudTexts()
    {
        var loc = _screens.Loc;

        // Tlačítko „Stavět" drží stav otevřeného menu, ať je vidět, co je zapnuté.
        _buildMenuButton.Background = new SolidBrush(_buildMenuOpen
            ? new Color(60, 110, 130, 235)
            : new Color(38, 48, 64, 235));

        // Odemčená funkce musí být vidět hned, ne až po restartu. Přestavba je drahá,
        // ale nastane jen ve chvíli odemčení (pár × za hru), ne každý snímek.
        int unlocked = _simulation.UnlockedFeatureCount;
        if (unlocked != _unlockedFeatureCount)
        {
            bool first = _unlockedFeatureCount < 0;
            _unlockedFeatureCount = unlocked;
            if (!first)
            {
                BuildUi();
                return; // UI se právě přestavělo — popisky doplní příští snímek
            }
        }

        // Nově získaná surovina se v pruhu odhalí (a jen tehdy se sahá na Visible).
        int known = _simulation.KnownResourceCount;
        if (known != _knownResourceCount)
        {
            _knownResourceCount = known;
            for (int i = 0; i < _resourceChips.Length; i++)
            {
                _resourceChips[i].Visible = _simulation.IsResourceKnown(i);
            }
        }

        for (int i = 0; i < _resourceLabels.Length; i++)
        {
            if (!_simulation.IsResourceKnown(i))
            {
                continue; // neznámou surovinu není co počítat ani kreslit
            }

            double amount = _simulation.GetResource(i);
            double cap = _simulation.GetStorageCap(i);
            _resourceLabels[i].Text = CivDle.Core.Numbers.FormatRatio(amount, cap);
            // Přeteklý sklad zežloutne (výzva rozšířit), jinak neutrální.
            _resourceLabels[i].TextColor = amount >= cap - 0.5 ? new Color(240, 200, 90) : Color.White;

            // Ticker přírůstku za sekundu (jen znatelný nárůst).
            double rate = i < _perSecond.Length ? _perSecond[i] : 0.0;
            _resourceRateLabels[i].Text = rate >= 0.05 ? $"+{CivDle.Core.Numbers.Format(rate)}/s" : string.Empty;
        }

        _idleLabel.Text = _simulation.IdleBuildings > 0
            ? loc.Format("hud.idleBuildings", _simulation.IdleBuildings)
            : string.Empty;

        _populationLabel.Text = loc.Format("hud.population",
            CivDle.Core.Numbers.Format(_simulation.Population), CivDle.Core.Numbers.Format(_simulation.HousingCapacity));

        var eras = _screens.Content.Eras;
        int eraIndex = _simulation.CurrentEraIndex;
        _eraLabel.Text = eraIndex >= 0 ? loc.Format("hud.era", loc[eras[eraIndex].NameKey]) : string.Empty;
        _eraNextLabel.Text = NextEraLine(eraIndex);

        // Měřítko (stupeň Vzestupu): jméno + strop; u stropu zežloutne jako pobídka k Vzestupu.
        var tiers = _screens.Content.AscensionTiers;
        int tierIndex = _simulation.CurrentTierIndex;
        if (tierIndex >= 0)
        {
            double cap = _simulation.PopulationCap;
            // Jen jméno měřítka. Strop je vnitřní balanční číslo — hráči nic neříká,
            // a když se blíží, pozná to tak, že populace přestane růst.
            _tierLabel.Text = loc.Format("hud.tier", loc[tiers[tierIndex].NameKey]);
            _tierLabel.TextColor = _simulation.Population >= cap - 0.5
                ? new Color(240, 200, 90)
                : new Color(190, 160, 230);
        }
        else
        {
            _tierLabel.Text = string.Empty;
        }

        // Počasí: ambientní jen informuje, extrémní varuje (oranžově) i s odpočtem —
        // hráč má vědět, proč mu zrovna teď klesla výroba.
        int weatherIndex = _simulation.CurrentWeatherIndex;
        if (weatherIndex >= 0)
        {
            string weatherName = loc[_screens.Content.Weather[weatherIndex].NameKey];
            if (_simulation.IsExtremeWeather)
            {
                _weatherLabel.Text = loc.Format("hud.weatherExtreme", weatherName,
                    (int)MathF.Ceiling((float)_simulation.WeatherSecondsRemaining));
                _weatherLabel.TextColor = new Color(240, 170, 80);
            }
            else
            {
                _weatherLabel.Text = loc.Format("hud.weather", weatherName);
                _weatherLabel.TextColor = new Color(170, 200, 220);
            }
        }
        else
        {
            _weatherLabel.Text = string.Empty;
        }

        // Spokojenost: barva nese stav, ať se to dá číst koutkem oka.
        if (_screens.Content.Gameplay.Happiness.IsEnabled)
        {
            double happiness = _simulation.Happiness;
            _happinessLabel.Text = loc.Format("hud.happiness", (int)Math.Round(happiness * 100));
            _happinessLabel.TextColor = happiness >= 0.75 ? new Color(150, 220, 150)
                : happiness >= 0.45 ? new Color(230, 210, 130)
                : new Color(235, 140, 120);
        }

        // Rozvodná síť: zobraz se až když má město spotřebiče; červená = nedostatek.
        if (_simulation.TotalPowerDemand > 0)
        {
            _powerLabel.Text = loc.Format("hud.power", _simulation.TotalPowerSupply, _simulation.TotalPowerDemand);
            _powerLabel.TextColor = _simulation.TotalPowerSupply < _simulation.TotalPowerDemand
                ? new Color(235, 120, 90)
                : new Color(120, 200, 240);
        }
        else
        {
            _powerLabel.Text = string.Empty;
        }

        double hours = _simulation.TimeOfDay01 * 24.0;
        _dayLabel.Text = loc.Format("hud.day", _simulation.DayNumber, (int)hours, (int)((hours - (int)hours) * 60));

        UpdateCursorLabel();
        UpdateStatusLabel();
        RefreshBuildAffordability();
        _objectives.Update();
        UpdateFestivalButton();
    }

    /// <summary>Přepíše popisek a dostupnost tlačítka Slavnost podle stavu boostu.</summary>
    private void UpdateFestivalButton()
    {
        var loc = _screens.Loc;
        if (_simulation.IsBoostActive)
        {
            _festivalLabel.Text = loc.Format("hud.festivalActive", (int)MathF.Ceiling((float)_simulation.BoostSecondsRemaining));
            _festivalButton.Enabled = false;
        }
        else if (!_simulation.CanStartBoost)
        {
            _festivalLabel.Text = loc.Format("hud.festivalCooldown", (int)MathF.Ceiling((float)_simulation.BoostCooldownSecondsRemaining));
            _festivalButton.Enabled = false;
        }
        else
        {
            _festivalLabel.Text = loc["hud.festival"];
            _festivalButton.Enabled = true;
        }
    }

    private void UpdateCursorLabel()
    {
        var world = _camera.ScreenToWorld(_input.MousePosition.ToVector2());
        int tileX = (int)MathF.Floor(world.X / TerrainRenderer.TileSize);
        int tileY = (int)MathF.Floor(world.Y / TerrainRenderer.TileSize);

        // Jen jméno terénu pod kurzorem — souřadnice dlaždice jsou ladicí údaj,
        // hráči nic neříkají a jen zabírají místo v HUD.
        var biome = _screens.Content.Biomes[_simulation.BiomeAt(tileX, tileY)];
        _cursorLabel.Text = _screens.Loc[biome.NameKey];
    }

    private void UpdateStatusLabel()
    {
        var loc = _screens.Loc;

        // Proužek se stavem se ukazuje, jen když má co říct — prázdný panel
        // uprostřed spodku obrazovky by byl jen šum.
        _statusPanel.Visible = _tools.AnyActive;
        if (!_statusPanel.Visible)
        {
            return;
        }

        if (_tools.TerraformIndex >= 0)
        {
            _statusLabel.Text = loc[_screens.Content.Terraform[_tools.TerraformIndex].NameKey];
            _statusLabel.TextColor = new Color(140, 230, 200);
            return;
        }

        if (_tools.PlantMode)
        {
            _statusLabel.Text = loc["hud.planting"];
            _statusLabel.TextColor = UiFactory.Accent;
            return;
        }

        if (_tools.ZoneMode)
        {
            _statusLabel.Text = loc[_screens.Content.ZoneTypes[_tools.ZonePaintTypeIndex].NameKey];
            _statusLabel.TextColor = UiFactory.Accent;
            return;
        }

        if (_tools.MovingBuildingIndex >= 0)
        {
            _statusLabel.Text = loc["hud.moving"];
            _statusLabel.TextColor = UiFactory.Accent;
            return;
        }

        var def = _screens.Content.Buildings[_tools.SelectedBuilding];
        if (_tools.GhostVisible && _tools.GhostResult != PlacementResult.Ok)
        {
            _statusLabel.Text = loc[ErrorKey(_tools.GhostResult)];
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
        PlacementResult.NeedsWaterAccess => "build.error.waterAccess",
        _ => "build.title",
    };
}
