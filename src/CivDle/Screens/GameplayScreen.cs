using System.Text;
using CivDle.Audio;
using CivDle.Core.Config;
using CivDle.Core.Content;
using CivDle.Core.Save;
using CivDle.Core.Platform;
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
    private readonly PollutionRenderer _pollutionRenderer;
    private readonly DistrictRenderer _districtRenderer;
    private readonly LandmarkRenderer _landmarkRenderer;
    private readonly UfoRenderer _ufoRenderer;
    private readonly WeatherRenderer _weatherRenderer;
    private readonly BuildingRenderer _buildingRenderer;
    private readonly LightsRenderer _lightsRenderer;
    private readonly FaunaSystem _fauna;
    private readonly TrafficSystem _traffic;
    private readonly AgentSystem _agents;
    private readonly InputManager _input = new();
    private readonly FixedStepLoop _simLoop = new(Simulation.TicksPerSecond);

    /// <summary>Pauza / 1× / 2× / 4×. Ovládání času, ne vlastnost simulace.</summary>
    private readonly GameSpeed _speed = new();

    /// <summary>Velké „×N" nahoře — kolikrát je civilizace silnější než na startu.</summary>
    private MightBanner _might = null!;

    /// <summary>Tlačítko rychlosti — popisek se přepisuje podle stavu.</summary>
    private Button? _speedButton;
    private readonly ParticleSystem _particles = new();
    private readonly FloatingTextRenderer _floatingText = new();
    private CityPulseRenderer _cityPulse = null!;
    private RollingNumbers _rolling = null!;
    private readonly CelebrationRenderer _celebration = new();
    private readonly FireworksRenderer _fireworks = new();
    private readonly LaserRenderer _laser = new();
    private readonly SpectacleRenderer _spectacles;

    /// <summary>
    /// Fotorežim: HUD zmizí a zůstane jen město. Existuje proto, že hráč chce
    /// ukázat, co postavil — ne rozdělané menu a lištu tlačítek.
    /// </summary>
    private bool _photoMode;

    /// <summary>Kolik sekund zbývá do dalšího zásahu těžebního paprsku.</summary>
    private float _laserCooldown;
    private readonly GameSounds _sounds;
    private readonly AmbientMusic _ambient = new();
    private readonly AmbientSoundscape _soundscape;
    private readonly MinimapRenderer _minimap;
    private readonly ToastRenderer _toasts;
    private readonly CityScaleRenderer _cityScale;
    private readonly VignetteRenderer _vignette;

    /// <summary>Závoj přes neprozkoumaný svět — kreslí se až nad mapou a budovami.</summary>
    private readonly FogRenderer _fogRenderer;
    private readonly NpcCityRenderer _npcCityRenderer;
    private readonly BubbleSystem _bubbles;
    private readonly CaravanSystem _caravans;
    private readonly GoldenSpawnSystem _golden;
    private readonly DiscoveryRenderer _discoveries;
    private readonly SpriteFontBase _popupFont;
    private readonly Dictionary<int, string> _popupTextCache = new();

    private Desktop _desktop = null!;
    /// <summary>Řádky, do kterých se skládají známé suroviny.</summary>
    private VerticalStackPanel _resourceRows = null!;

    /// <summary>Odhad šířky jedné položky suroviny (ikona + zásoba + přírůstek).</summary>
    private const int ChipWidth = 200;

    /// <summary>
    /// Přeskládá známé suroviny do řádků podle šířky okna.
    ///
    /// <para>Volá se jen při objevení nové suroviny (pár × za hru), ne každý
    /// snímek — přestavět pár widgetů je levné, dělat to nepřetržitě ne.</para>
    /// </summary>
    private void RepackResourceChips()
    {
        _resourceRows.Widgets.Clear();

        int perRow = Math.Max(4, (_screens.GraphicsDevice.Viewport.Width - 80) / ChipWidth);
        HorizontalStackPanel? row = null;
        int inRow = 0;

        for (int i = 0; i < _resourceChips.Length; i++)
        {
            if (!_simulation.IsResourceKnown(i))
            {
                continue; // neznámá surovina se v pruhu vůbec neukáže
            }

            if (row is null || inRow == perRow)
            {
                row = new HorizontalStackPanel { Spacing = 14 };
                _resourceRows.Widgets.Add(row);
                inRow = 0;
            }

            _resourceChips[i].Visible = true;
            row.Widgets.Add(_resourceChips[i]);
            inRow++;
        }
    }

    /// <summary>Rezervovaná šířka pro „zásoba/kapacita" — drží horní lištu v klidu.</summary>
    private const int AmountLabelWidth = 96;

    /// <summary>Rezervovaná šířka pro „+x/s" — ticker se objevuje a mizí, lišta se hýbat nesmí.</summary>
    private const int RateLabelWidth = 56;

    private Label[] _resourceLabels = Array.Empty<Label>();
    private Label[] _resourceRateLabels = Array.Empty<Label>();
    private Widget[] _resourceChips = Array.Empty<Widget>();
    private int _knownResourceCount;
    private double[] _ratePrev = Array.Empty<double>();
    private double[] _perSecond = Array.Empty<double>();
    private float _rateTimer;

    /// <summary>Kolik sekund ještě mlčet o přetékajícím skladu dané suroviny.</summary>
    private float[] _fullStorageCooldown = Array.Empty<float>();
    private Label _populationLabel = null!;
    private Label _idleLabel = null!;
    private Label _eraLabel = null!;
    private Label _eraNextLabel = null!;
    private Label _tierLabel = null!;
    private Label _powerLabel = null!;
    private Label _weatherLabel = null!;
    private Label _seasonLabel = null!;
    private Label _toolsLabel = null!;
    private Label _pollutionLabel = null!;

    /// <summary>Poslední ohlášené období — změna se hlásí jednou, ne každý snímek.</summary>
    private int _lastSeasonIndex = -1;
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

    /// <summary>
    /// Vybírá, co hráči ukázat a kdy. Nahradil pevný časovač událostí — obsah tím
    /// začal reagovat na to, co se ve městě zrovna děje.
    /// </summary>
    private ContentDirector _director = null!;
    private Button _festivalButton = null!;
    private Button _buildMenuButton = null!;

    /// <summary>Tlačítko zakázek; <c>null</c>, když jsou v datech vypnuté.</summary>
    private Button? _contractsButton;

    /// <summary>
    /// Odehrané sekundy od posledního zápisu do kroniky. Sčítá se v reálném čase
    /// (ne herním) — hráč se ptá „kolik hodin jsem v tom nechal", ne kolik tiků
    /// odtikala simulace.
    /// </summary>
    private double _unsavedPlaySeconds;
    private Widget _buildMenuPanel = null!;
    private Widget _statusPanel = null!;
    private HorizontalStackPanel _roadModePanel = null!;
    private Button _roadAddButton = null!;
    private Button _roadEraseButton = null!;

    /// <summary>Přepínač násobiče hromadné stavby (×1, ×5, ×25) a jeho tlačítka.</summary>
    private HorizontalStackPanel _batchPanel = null!;
    private Button[] _batchButtons = Array.Empty<Button>();
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

    /// <summary>
    /// Modlitba, která čeká na ukázání cíle na mapě (−1 = žádná). Cílit se musí
    /// na mapě, ne v menu — přivolat meteorit naslepo by byla past.
    /// </summary>
    private int _pendingPrayer = -1;

    private int _pendingPrayerStrength = 1;
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
        _pollutionRenderer = new PollutionRenderer(screens.WhitePixel, screens.Content);
        _landmarkRenderer = new LandmarkRenderer(screens.WhitePixel, screens.Content, screens.Sprites);
        _ufoRenderer = new UfoRenderer(screens.WhitePixel);
        _sounds = screens.Sounds;
        _soundscape = new AmbientSoundscape(screens.Content);
        _tools = new MapTools(simulation, _camera, _input, screens.Content);
        _weatherRenderer = new WeatherRenderer(screens.WhitePixel, screens.Content);
        _cityPulse = new CityPulseRenderer(screens.WhitePixel, screens.Content);
        _rolling = new RollingNumbers(screens.Content.Resources.Count);
        _rolling.SnapTo(simulation.GetResource); // na startu (i po načtení savu) žádné dojíždění
        _buildingRenderer = new BuildingRenderer(screens.WhitePixel, screens.Content, screens.Sprites);
        _lightsRenderer = new LightsRenderer(screens.WhitePixel, screens.Content);
        _fauna = new FaunaSystem(screens.Content);
        _traffic = new TrafficSystem(screens.Content);
        _spectacles = new SpectacleRenderer(screens.Content);
        _agents = new AgentSystem(screens.Content, screens.Sprites);
        _minimap = new MinimapRenderer(screens.GraphicsDevice, screens.Content.Biomes, screens.WhitePixel);
        _vignette = new VignetteRenderer(screens.GraphicsDevice);
        _fogRenderer = new FogRenderer(screens.WhitePixel);
        _bubbles = new BubbleSystem(screens.Sprites, screens.Content);
        _caravans = new CaravanSystem(screens.Sprites, screens.Content);
        _golden = new GoldenSpawnSystem(screens.Sprites, screens.Content);
        _discoveries = new DiscoveryRenderer(screens.Sprites);
        // Pozor na pořadí: všechno pod tímhle řádkem si font drží, takže se to
        // nesmí vytvářet dřív, než je načtený (jinak null uvnitř rendereru).
        _popupFont = Stylesheet.Current.LabelStyle.Font;
        _might = new MightBanner(screens.WhitePixel, _popupFont, screens.Loc);
        _toasts = new ToastRenderer(screens.WhitePixel, _popupFont);
        _cityScale = new CityScaleRenderer(screens.WhitePixel, _popupFont);
        _districtRenderer = new DistrictRenderer(screens.WhitePixel, screens.Content, screens.Loc, _popupFont);
        _npcCityRenderer = new NpcCityRenderer(screens.WhitePixel, screens.Content, screens.Loc, _popupFont);

        var viewport = screens.GraphicsDevice.Viewport;
        _camera.SetViewport(viewport.Width, viewport.Height);
        _camera.CenterOn(FindStartFocus(), zoom: 2.2f);
        _knownBuildingCount = simulation.Buildings.Length; // načtená hra: bez juice za staré budovy

        _ratePrev = new double[_simulation.ResourceCount];
        _perSecond = new double[_simulation.ResourceCount];
        _fullStorageCooldown = new float[_simulation.ResourceCount];
        for (int i = 0; i < _ratePrev.Length; i++)
        {
            _ratePrev[i] = _simulation.GetResource(i);
        }

        BuildUi();
        _screens.Loc.LanguageChanged += BuildUi;
        _screens.UiSettingsChanged += BuildUi;
        _ambient.Play(); // klidná smyčka pro relaxační jádro
        _director = new ContentDirector(screens.Content, _simulation.Seed);
        RefreshChallengeDay();
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
        _input.Update(dt);

        // Kulisa podle biomu a počasí — atmosféra stála skoro jen na obraze.
        _soundscape.Update(dt, _simulation);
        _hoverSeconds += dt;
        _unsavedPlaySeconds += dt;

        // Pravidelný autosave: idle hra běží hodiny, ztratit ji kvůli pádu
        // nebo zavření okna je to nejhorší, co se může stát.
        RefreshChallengeDay();
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

        // F11 schová HUD, F12 uloží sdílitelnou kartu. Obojí je „ukaž to
        // ostatním", proto vedle sebe.
        if (_input.WasPressed(Keys.F11))
        {
            _photoMode = !_photoMode;
            _tools.Clear(); // s nástrojem v ruce by ve fotce zůstal duch budovy
        }

        if (_input.WasPressed(Keys.F12))
        {
            SaveShareCard();
        }

        // Ctrl+Shift+D: ladicí menu. Schválně zkratka bez tlačítka kdekoli
        // v nabídce — je to nástroj na testování pozdní hry, ne herní obsah,
        // a hráč, který ho nehledá, na něj nemá narazit.
        if (_input.WasPressed(Keys.D)
            && (_input.IsDown(Keys.LeftControl) || _input.IsDown(Keys.RightControl))
            && (_input.IsDown(Keys.LeftShift) || _input.IsDown(Keys.RightShift)))
        {
            _screens.Push(new DebugScreen(_screens, _simulation, _camera));
            return;
        }

        // Start otevře pauzu, Y stavební menu — bez nich by ovladač uměl jen
        // chodit po mapě.
        if (_input.WasPadPressed(GamePadMap.Pause))
        {
            _screens.Push(new PauseScreen(_screens, _simulation, _info));
            return;
        }

        if (_input.WasPadPressed(GamePadMap.BuildMenu))
        {
            ToggleBuildMenu();
        }

        // Tab přepíná násobič hromadné stavby — ruka zůstává u WASD a nemusí
        // pro ×25 přes celou obrazovku na tlačítko.
        if (_input.WasPressed(Keys.Tab) && _tools.SelectedBuilding >= 0)
        {
            _tools.CycleBatchSize();
        }

        bool mouseOverUi = _desktop.IsMouseOverGUI;
        UpdateCamera(dt, mouseOverUi);

        // Modlitba čekající na cíl má přednost přede vším ostatním — hráč právě
        // ukazuje, kam má dopadnout. Musí se vyřídit DŘÍV než nástroje a laser:
        // ty klik spolknou a modlitba by zmizela do prázdna (přesně tenhle bug
        // dělal z meteoritu tlačítko, po kterém se nic nedělo).
        if (!ResolvePendingPrayer(mouseOverUi)
            && !_tools.Update(mouseOverUi))
        {
            // Nástroj si vstup buď vezme (staví, maluje, přesouvá), nebo ho pustí
            // dál na ruční těžbu — jediné místo, kde se to rozhoduje.
            UpdateHarvest(dt, mouseOverUi);
        }

        int ticks = _simLoop.Advance(_speed.Scale(gameTime.ElapsedGameTime.TotalSeconds));
        for (int i = 0; i < ticks; i++)
        {
            _simulation.Tick();
        }

        UpdateDirector(dt);
        SampleRates(dt);

        // Svět běží rychlostí, kterou hráč zvolil — a při pauze STOJÍ. Chodci,
        // auta a dým jsou součástí toho světa, ne ozdoba nad ním: když se
        // zastaví simulace a lidé si chodí dál, pauza nevypadá jako pauza.
        float worldDt = dt * (float)_speed.Multiplier;

        EmitNewBuildingJuice();
        _harvestables.Update(worldDt);
        _particles.Update(worldDt);
        _floatingText.Update(worldDt);
        LaunchFireworksForMilestones();
        _cityPulse.Update(worldDt, _simulation);
        _fireworks.Update(worldDt);
        _laser.Update(worldDt);
        _celebration.Update(worldDt);

        // Rolující čísla v liště jdou naopak reálným časem: dojíždějí k hodnotě,
        // která už platí, a při pauze by zamrzla na půl cesty.
        _rolling.Update(dt, _simulation.GetResource);

        // Při velkém oddálení chodce/faunu neaktualizuj — nespawnovali by se přes
        // obří viditelnou plochu (a stejně se nekreslí; z výšky vidíš hustotu).
        if (_camera.Zoom >= CityScaleRenderer.ThresholdZoom)
        {
            _fauna.Update(worldDt, _camera, _simulation);
            _traffic.Update(worldDt, _camera, _simulation);
            _agents.Update(worldDt, _camera, _simulation);
            _bubbles.Update(worldDt, _simulation);
            UpdateCaravan(worldDt);
            _golden.Update(worldDt, _camera, _simulation);
            _discoveries.Update(worldDt);
        }

        _buildingRenderer.Update(worldDt); // balony nad kotvišti se houpou
        _weatherRenderer.Update(worldDt, _simulation, _screens.GraphicsDevice.Viewport);
        _minimap.Update(dt, _camera, _simulation);
        _might.Update(dt, _simulation);
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
        _districtRenderer.Draw(spriteBatch, _camera, _simulation); // tvář čtvrtí, taky na zemi
        // Landmarky jen zblízka (LOD): z výšky jsou stejně pod rozlišením a dotaz
        // na desítky tisíc dlaždic by zbytečně žral snímky.
        if (_camera.Zoom >= LandmarkRenderer.MinZoom)
        {
            _landmarkRenderer.Draw(spriteBatch, _camera, _simulation);
        }

        // Cizí města pod hráčovou zástavbou: svět, do kterého hráč přišel, má
        // ležet POD tím, co postavil. Mlha se kreslí až úplně nakonec, takže
        // neobjevené město zůstane schované samo od sebe.
        _npcCityRenderer.Draw(spriteBatch, _camera, _simulation);

        // Velké oddálení → agregátní pohled na měřítko (hustota + populace) místo
        // drobných jednotlivců (game-feel-wow: „koukni, jak to vyrostlo").
        if (_camera.Zoom >= CityScaleRenderer.ThresholdZoom)
        {
            _harvestables.Draw(spriteBatch, _camera, _simulation);
            _discoveries.Draw(spriteBatch, _camera, _simulation);
            _roadRenderer.Draw(spriteBatch, _camera, _simulation);
            // Provoz patří NAD silnici a POD budovy — auto má zajet za dům, ne přes něj.
            _traffic.Draw(spriteBatch, _screens.WhitePixel, _camera, DayNightCycle.NightFactor(_simulation.TimeOfDay01));
            _buildingRenderer.Draw(spriteBatch, _camera, _simulation);
            _agents.Draw(spriteBatch, _camera);
            _fauna.Draw(spriteBatch, _screens.WhitePixel, _camera);
            _bubbles.Draw(spriteBatch, _camera);
            _caravans.Draw(spriteBatch, _camera);
            _golden.Draw(spriteBatch, _camera);
            _spectacles.Draw(spriteBatch, _screens.WhitePixel, _camera, _simulation);
            _fireworks.Draw(spriteBatch, _screens.WhitePixel, _camera);
            _laser.Draw(spriteBatch, _screens.WhitePixel, _camera);
        }
        else
        {
            _roadRenderer.Draw(spriteBatch, _camera, _simulation); // cesty dávají kontext i z výšky
            _cityScale.Draw(spriteBatch, _screens.GraphicsDevice.Viewport, _camera, _simulation);
        }

        // Závoj zamoření nad městem, ale pod událostmi a efekty: špína leží
        // na krajině, nemá zakrývat, co se zrovna děje.
        _pollutionRenderer.Draw(spriteBatch, _camera, _simulation);

        // Cedule čtvrtí až nad zástavbu — jméno místa má být čitelné i tam,
        // kde je pod ním nejhustěji postaveno.
        _districtRenderer.DrawLabels(spriteBatch, _camera, _simulation);

        // UFO letí nad vším na mapě — je to událost, ne kulisa.
        _ufoRenderer.Draw(spriteBatch, _camera, _simulation, (float)gameTime.TotalGameTime.TotalSeconds);

        _particles.Draw(spriteBatch, _screens.WhitePixel, _camera);

        // Odezva na práci simulace (jiskry výroby, naskakující stavby) nad mapou,
        // ale pod denním/nočním překryvem — patří do světa, ne do UI.
        spriteBatch.Begin(samplerState: SamplerState.PointClamp, transformMatrix: _camera.Transform);
        _cityPulse.Draw(spriteBatch);
        spriteBatch.End();

        // Nádech období pod den/noc — hráč pozná zimu dřív, než se podívá do HUD.
        DayNightCycle.DrawSeasonTint(
            spriteBatch, _screens.WhitePixel, _screens.GraphicsDevice.Viewport, _simulation.CurrentSeason);

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

            // Hromadná stavba: duch je celý plán, ne jedna budova. Hráč tak vidí
            // dopředu i to, kde mu dojdou suroviny (červené kusy se nepostaví).
            var plan = _tools.BulkPlan;
            if (plan.Count > 0)
            {
                for (int i = 0; i < plan.Count; i++)
                {
                    _buildingRenderer.DrawGhost(spriteBatch, _camera, def, plan[i].X, plan[i].Y, plan[i].WillBuild);
                }
            }
            else
            {
                _buildingRenderer.DrawGhost(
                    spriteBatch, _camera, def, _tools.GhostX, _tools.GhostY, _tools.GhostResult == PlacementResult.Ok);
            }
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
            DrawTileOverlay(spriteBatch, plantSprite, _tools.PlantGhostX, _tools.PlantGhostY, 1,
                (_tools.PlantGhostResult == PlacementResult.Ok
                    ? new Color(120, 240, 140)
                    : new Color(240, 110, 100)) * 0.7f);
        }

        if (_tools.TerraformGhostActive)
        {
            DrawTileOverlay(spriteBatch, _screens.WhitePixel, _tools.TerraformGhostX, _tools.TerraformGhostY, 1,
                (_tools.TerraformGhostResult == PlacementResult.Ok
                    ? new Color(140, 230, 200)
                    : new Color(240, 110, 100)) * 0.55f);
        }

        // V režimu slučování se rozsvítí VŠECHNY bloky, které jdou sloučit —
        // hráč jinak musí objíždět město myší a hádat, kde se čtyři stejné domy
        // potkaly. Kreslí se jen to, co je vidět, a jen v tomhle režimu.
        if (_tools.MergeMode)
        {
            DrawMergeCandidates(spriteBatch);
        }

        // Náhled slučování: obtáhne celý čtverec 2×2, ne jednu dlaždici — hráč
        // musí vidět, které čtyři budovy zmizí.
        if (_tools.MergeGhostActive)
        {
            DrawTileOverlay(spriteBatch, _screens.WhitePixel, _tools.MergeGhostX, _tools.MergeGhostY, 2,
                (_tools.MergeGhostResult == PlacementResult.Ok
                    ? new Color(150, 235, 150)
                    : new Color(235, 120, 110)) * 0.45f);
        }

        if (_tools.RoadGhostActive)
        {
            // Celá tažená trasa, ne jen dlaždice pod kurzorem — hráč vidí ulici,
            // kterou postaví, ještě než pustí tlačítko.
            foreach (var (pathX, pathY) in _tools.RoadGhostPath)
            {
                DrawTileOverlay(spriteBatch, _screens.WhitePixel, pathX, pathY, 1,
                    (_tools.RoadGhostErasing ? new Color(240, 190, 90) : new Color(150, 230, 160)) * 0.45f);
            }

            DrawTileOverlay(spriteBatch, _screens.WhitePixel, _tools.RoadGhostX, _tools.RoadGhostY, 1,
                (_tools.RoadGhostResult != PlacementResult.Ok
                    ? new Color(235, 120, 110)
                    : _tools.RoadGhostErasing ? new Color(240, 190, 90) : new Color(200, 200, 190)) * 0.55f);
        }

        if (_tools.ZonePreviewActive)
        {
            var preview = _tools.ZonePreview;
            _zoneRenderer.DrawPreview(spriteBatch, _camera, _tools.ZonePaintTypeIndex,
                preview.X, preview.Y, preview.Width, preview.Height);
        }

        _weatherRenderer.Draw(spriteBatch, _screens.GraphicsDevice.Viewport); // závoj + srážky nad scénou
        // Mlha až nad mapou i zástavbou: schovat musí i to, co v neprozkoumaném
        // stojí. Ve fotorežimu se vypíná — na snímek do obchodu patří svět, ne tma.
        if (!_captureMode)
        {
            _fogRenderer.Draw(spriteBatch, _camera, _simulation.Fog);
        }

        _vignette.Draw(spriteBatch, _screens.GraphicsDevice.Viewport); // decentní sevření pohledu, pod HUD

        // Fotorežim: všechno od téhle chvíle je HUD, a ten se do fotky nehodí.
        // Hra pod ním běží dál — je to jen jiný pohled, ne pauza.
        if (_photoMode)
        {
            // Jediná věta, která zůstane: bez ní hráč neví, jak HUD vrátit.
            // Do sdílené karty se nedostane, ta se kreslí zvlášť.
            string hint = _screens.Loc["hud.photoMode"];
            var size = _popupFont.MeasureString(hint);
            var viewport = _screens.GraphicsDevice.Viewport;
            spriteBatch.Begin();
            spriteBatch.DrawString(
                _popupFont, hint,
                new Vector2((viewport.Width - size.X) * 0.5f, viewport.Height - size.Y - 18f),
                Color.White * 0.35f);
            spriteBatch.End();
            return;
        }

        _screens.RenderDesktop(this, _desktop);

        // Minimapa, popupy a toasty až nad UI — hráč je nesmí přehlédnout.
        _minimap.Draw(spriteBatch, _screens.GraphicsDevice.Viewport, _camera, _simulation);
        _might.Draw(spriteBatch, _screens.GraphicsDevice.Viewport, _simulation);
        _floatingText.Draw(spriteBatch, _camera, _popupFont);
        DrawSettlementLabels(spriteBatch);
        DrawTileTooltip(spriteBatch);
        _toasts.Draw(spriteBatch, _screens.GraphicsDevice.Viewport);

        // Oslava milníku úplně navrchu — je to ta nejdůležitější zpráva na obrazovce.
        spriteBatch.Begin();
        _celebration.Draw(spriteBatch, _screens.WhitePixel, _popupFont, _screens.GraphicsDevice.Viewport);
        spriteBatch.End();
    }

    /// <summary>
    /// Uloží sdílitelnou kartu a řekne hráči kam. Bez té hlášky by obrázek
    /// vznikl někde, kde ho nikdo nenajde.
    /// </summary>
    private void SaveShareCard()
    {
        try
        {
            string path = new Capture.ShareCard(_screens).Save(_simulation, _camera, _screens.Saves.ShareDirectory);
            _toasts.Add(_screens.Loc.Format("share.saved", Path.GetFileName(path)), new Color(255, 226, 150));
        }
        catch (IOException error)
        {
            // Plný disk ani zamčená složka nemají shodit rozehranou hru.
            _toasts.Add(_screens.Loc.Format("share.failed", error.Message), new Color(235, 120, 110));
        }
    }

    /// <summary>
    /// Bublina u kurzoru s tím, co je na dlaždici pod myší: terén, a hlavně jméno
    /// zvláštnosti (prastarý strom, žíla, skrýš). Bez toho hráč koukal na „nějaké
    /// kostičky" a neměl jak zjistit, co to je.
    /// </summary>
    private void DrawTileTooltip(SpriteBatch spriteBatch)
    {
        // Bublina u kurzoru je ve snímku pro obchod nežádoucí ze dvou důvodů:
        // ukazuje kurzor, který na statickém obrázku nikdo nedrží, a často nese
        // hlášku o problému („nemá tu kdo pracovat") — tedy přesně to, co na
        // reklamní obrázek nepatří.
        if (_captureMode)
        {
            return;
        }

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

        // Stojící budova má přednost přede vším ostatním na dlaždici: hráč na ni
        // najel právě proto, že se diví, proč nic nedělá.
        if (_simulation.TryGetBuildingAt(tileX, tileY, out int hoveredBuilding)
            && StallText(_simulation.Buildings[hoveredBuilding].Stall) is { } stallKey)
        {
            var def = content.Buildings[_simulation.Buildings[hoveredBuilding].DefIndex];
            HoverTooltip.Draw(spriteBatch, _screens.WhitePixel, _popupFont,
                _screens.GraphicsDevice.Viewport, _input.MousePosition,
                loc[def.NameKey], loc[stallKey],
                BuildingRenderer.StallColor(_simulation.Buildings[hoveredBuilding].Stall));
            return;
        }

        if (TryLandmarkUnder(tileX, tileY, out int landmark))
        {
            var def = content.Landmarks[landmark];
            title = loc[def.NameKey];

            // Co z něj je, když je z čeho — u sbíratelného místa je to ta
            // informace, kvůli které na něj hráč najel.
            body = def.ClickYield is { } yield
                ? loc.Format("tip.landmark.yield",
                    yield.Amount, loc[content.Resources[yield.ResourceIndex].NameKey])
                : loc["tip.landmark"];
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

    /// <summary>
    /// Landmark pod kurzorem — i když hráč míří vedle jeho kotevní dlaždice.
    ///
    /// <para>Velké landmarky (vrak, ruiny) se kreslí přes 2×2, ale v simulaci
    /// sedí na jedné dlaždici. Bez tohohle šlo popisek vyvolat jen z té jedné
    /// a hráč měl dojem, že na ně najet nejde.</para>
    /// </summary>
    private bool TryLandmarkUnder(int tileX, int tileY, out int landmark)
    {
        landmark = _simulation.LandmarkAt(tileX, tileY);
        if (landmark >= 0)
        {
            return true;
        }

        for (int dy = -1; dy <= 1; dy++)
        {
            for (int dx = -1; dx <= 1; dx++)
            {
                int index = _simulation.LandmarkAt(tileX + dx, tileY + dy);

                // Sousední landmark se počítá, jen když sem svým půdorysem
                // opravdu dosáhne — jinak by popisek vyskakoval i vedle malých.
                if (index >= 0 && _screens.Content.Landmarks[index].Footprint > 1)
                {
                    landmark = index;
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Objevené cizí město pod dlaždicí. Trefa se počítá na celé jeho zástavbě,
    /// ne jen na středu — hráč míří na město, ne na jeden pixel. Zástavbu zná
    /// simulace (jsou to skutečné budovy a ulice), takže se jen zeptáme jí.
    /// </summary>
    private bool TryGetCityAt(int tileX, int tileY, out NpcCity city) =>
        _simulation.TryNpcCityAt(tileX, tileY, out city);

    /// <summary>
    /// Lokalizační klíč vysvětlení, proč budova stojí; <c>null</c> = pracuje.
    /// Rozestavěná budova se nehlásí — lešení a pruh postupu mluví samy.
    /// </summary>
    internal static string? StallText(BuildingStall stall) => stall switch
    {
        BuildingStall.NoWorkers => "stall.noWorkers",
        BuildingStall.MissingInput => "stall.missingInput",
        BuildingStall.NoTerrain => "stall.noTerrain",
        _ => null,
    };

    /// <summary>Jmenovky osad ve screen-space nad těžištěm shluku (orientace na mapě, fáze 4).</summary>
    private void DrawSettlementLabels(SpriteBatch spriteBatch)
    {
        var settlements = _simulation.Settlements;
        if (settlements.Count == 0)
        {
            return;
        }

        var names = _screens.Content.SettlementNames;
        var ranks = _screens.Content.SettlementRanks;
        var loc = _screens.Loc;
        spriteBatch.Begin();
        for (int i = 0; i < settlements.Count; i++)
        {
            var settlement = settlements[i];

            // Jméno i stupeň: „Zkouškovice · Městečko". Bez stupně je na mapě
            // vidět jen jméno a hráč nepozná, jestli roste — a přesně tohle
            // z hromady budov dělá místo.
            string name = names[settlement.NameIndex];
            if (ranks.At(settlement.RankIndex) is { } rank)
            {
                name = $"{name} · {loc[rank.NameKey]}";
            }

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
        }

        // Levý stick jede plynule, ne po krocích — proto se přičítá až po
        // normalizaci klávesnice, aby si poloviční výchylka udržela poloviční
        // rychlost.
        move += _input.PadCameraMove;
        if (move != Vector2.Zero)
        {
            // Dělení zoomem: posun je konstantní v pixelech obrazovky, ne světa.
            _camera.PanWorld(move * (PanSpeed * dt / _camera.Zoom));
        }

        // Spouště přibližují a oddalují ke středu obrazovky — palcem se kolečko
        // myši nahradit nedá.
        float padZoom = _input.PadZoomFactor(dt);
        if (padZoom != 1f)
        {
            var viewport = _screens.GraphicsDevice.Viewport;
            _camera.ZoomAt(new Vector2(viewport.Width * 0.5f, viewport.Height * 0.5f), padZoom);
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
    /// Odpálí ohňostroj nad každým milníkem, který simulace ohlásila.
    ///
    /// <para>Musí běžet DŘÍV, než frontu vybere pulz města — ten ji vyprazdňuje.
    /// Obrazovka je jediné místo, které o obou efektech ví, takže se tady
    /// rozhoduje, ne uvnitř některého z nich (SRP).</para>
    /// </summary>
    private void LaunchFireworksForMilestones()
    {
        var queue = _simulation.VisualEvents;
        for (int i = 0; i < queue.Count; i++)
        {
            if (queue[i].Kind != VisualEventKind.MilestoneReached)
            {
                continue;
            }

            var center = new Vector2(
                (queue[i].X + 0.5f) * TerrainRenderer.TileSize,
                (queue[i].Y + 0.5f) * TerrainRenderer.TileSize);

            // Seed z místa a času: dvě salvy po sobě vypadají jinak, ale tatáž
            // salva se při přehrání chová stejně (žádné mihotání mezi snímky).
            _fireworks.Burst(center, HashCode.Combine(queue[i].X, queue[i].Y, _simulation.TickCount));
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
    /// Těžba orbitálním paprskem: dokud hráč drží tlačítko, sbírá se v pravidelném
    /// rytmu pod kurzorem (a v poloměru z dat).
    ///
    /// <para>Vrací true, když paprsek vstup spotřeboval — herní obrazovka pak
    /// neřeší klik. Nespotřebuje ho hned na první stisk: první klik musí projít
    /// běžnou cestou, aby šlo pořád rozkliknout budovu nebo sebrat bublinu.</para>
    /// </summary>
    private bool UpdateLaser(float dt, int tileX, int tileY)
    {
        var config = _screens.Content.Gameplay.Laser;
        if (!_simulation.LaserUnlocked || !_input.IsLeftDown || _input.WasLeftPressed)
        {
            _laserCooldown = 0f;
            return false;
        }

        _laser.Aim(new Vector2(
            (tileX + 0.5f) * TerrainRenderer.TileSize,
            (tileY + 0.5f) * TerrainRenderer.TileSize));

        _laserCooldown -= dt;
        if (_laserCooldown > 0f)
        {
            return true;
        }

        _laserCooldown = (float)config.SecondsPerHarvest;

        // Poloměr se prochází celý, ale úroda se hlásí jen tam, kde opravdu byla —
        // jinak by paprsek nad holou plání sypal popupy do prázdna.
        int radius = config.RadiusTiles;
        for (int dy = -radius; dy <= radius; dy++)
        {
            for (int dx = -radius; dx <= radius; dx++)
            {
                if (_simulation.TryHarvest(tileX + dx, tileY + dy, out int resourceIndex, out int amount, out _))
                {
                    LaserHitFeedback(tileX + dx, tileY + dy, resourceIndex, amount);
                }
            }
        }

        return true;
    }

    /// <summary>
    /// Zpětná vazba na jeden zásah paprsku — schválně střídmější než u kliku:
    /// při osmi zásazích za sekundu by plné popupy a cinkání byly nepoužitelné.
    /// </summary>
    private void LaserHitFeedback(int tileX, int tileY, int resourceIndex, int amount)
    {
        var center = new Vector2(
            (tileX + 0.5f) * TerrainRenderer.TileSize,
            (tileY + 0.5f) * TerrainRenderer.TileSize);
        var resourceColor = _screens.Content.Resources[resourceIndex].MapColor.ToXna();

        _particles.SpawnBurst(center, resourceColor, 4, 40f, 130f);
        _harvestables.RegisterChop(tileX, tileY);

        // Číslo jen občas: text u každého zásahu by se slil v nečitelnou kaši.
        if (_eventRng.Next(4) == 0)
        {
            _floatingText.Add(center, PopupText(resourceIndex, amount), resourceColor);
        }
    }

    /// <summary>
    /// Ruční těžba: klik na strom/kámen — surovina, popup, třísky, zvuk; strom se
    /// zmenšuje a po pár klicích spadne s velkým efektem (anticipace + payoff).
    /// </summary>
    private void UpdateHarvest(float dt, bool mouseOverUi)
    {
        if (_tools.SelectedBuilding >= 0 || mouseOverUi)
        {
            _laser.Clear();
            return;
        }

        var world = _camera.ScreenToWorld(_input.MousePosition.ToVector2());
        int tileX = (int)MathF.Floor(world.X / TerrainRenderer.TileSize);
        int tileY = (int)MathF.Floor(world.Y / TerrainRenderer.TileSize);

        // Držené tlačítko + odemčený laser = paprsek. Klik zůstává klikem, takže
        // se ani po odemčení nezmění to, co hráč umí — jen přibude, co dokáže
        // držením.
        if (UpdateLaser(dt, tileX, tileY))
        {
            return;
        }

        if (!_input.WasLeftPressed)
        {
            return;
        }

        // Sběrné bubliny a zlaté spawny mají přednost před budovou i těžbou.
        // Karavana má přednost před těžbou i bublinou — je na ní vidět, že se
        // na ni klika, a hráč po ní klika záměrně.
        if (_caravans.TryEscort(world, out var caravanPos))
        {
            _floatingText.Add(caravanPos - new Vector2(0f, TerrainRenderer.TileSize * 0.5f),
                _screens.Loc["hud.escort"], new Color(255, 220, 140));
            _particles.SpawnBurst(caravanPos, new Color(255, 220, 140), 8, 40f, 130f);
            return;
        }

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

        // Klik na cizí město otevře jeho obrazovku. Je před budovou schválně:
        // město je celek, a když na něj hráč klikne, chce jednat s ním — ne
        // rozklikávat jeden jeho dům.
        if (TryGetCityAt(tileX, tileY, out var clickedCity))
        {
            _screens.Push(new CityScreen(_screens, _simulation, _camera, clickedCity));
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

        // Kombo: série rychlých kliků zvedá výnos. Ukazuje se až od druhého
        // v řadě — u prvního by „×1" jen mátlo. Číslo letí nad popupem výnosu,
        // aby bylo vidět, že to spolu souvisí.
        int streak = _simulation.ComboStreak;
        if (streak > 1)
        {
            _floatingText.Add(
                tileCenter - new Vector2(0f, TerrainRenderer.TileSize * 0.7f),
                _screens.Loc.Format("hud.combo", streak),
                Color.Lerp(new Color(255, 235, 180), new Color(255, 140, 60), Math.Min(1f, (streak - 1) / 10f)));
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

    /// <summary>
    /// Roční období v HUD. Barvu si nese období samo (nádech z dat), takže se
    /// dá číst koutkem oka; zima bez dřeva navíc zčervená a řekne proč — mrznoucí
    /// město je jediná situace, kdy období hráče skutečně brzdí.
    /// </summary>
    private void UpdateSeasonLabel(Localization loc)
    {
        if (_simulation.CurrentSeason is not { } season)
        {
            _seasonLabel.Text = string.Empty;
            return;
        }

        bool freezing = season.NeedsHeating && !_simulation.HasFuelForHeating;
        _seasonLabel.Text = loc.Format("hud.season", loc[season.NameKey]);
        _seasonLabel.TextColor = freezing ? new Color(235, 140, 120) : season.TintColor.ToXna();
        _seasonLabel.Tooltip = freezing
            ? loc["hud.season.freezing"]
            : loc.Format("hud.season.tip", loc[season.NameKey], loc[season.DescriptionKey]);

        // Změna období je událost, ne stav — ohlásí se jednou, když nastane.
        int index = _simulation.CurrentSeasonIndex;
        if (_lastSeasonIndex >= 0 && index != _lastSeasonIndex)
        {
            _toasts.Add(loc.Format("toast.season", loc[season.NameKey]), season.TintColor.ToXna());
        }

        _lastSeasonIndex = index;
    }

    /// <summary>Spokojenost rozepsaná na sčítance („Základ 60 · služby +25 · přelidnění −12").</summary>
    private string DescribeHappiness(Localization loc)
    {
        var parts = _simulation.HappinessParts;
        string text = loc.Format("hud.happinessParts",
            Points(parts.Base), Points(parts.Services), Points(parts.Crowding));

        // Vláda se zmiňuje, jen když nějaká je — jinak by to byl řádek o nule.
        if (Math.Abs(parts.Government) > 0.0005)
        {
            text += loc.Format("hud.happinessGovernment", Points(parts.Government));
        }

        // Totéž smog: dokud se nic nekazí, ať zbytečně neplete.
        if (Math.Abs(parts.Pollution) > 0.0005)
        {
            text += loc.Format("hud.happinessPollution", Points(parts.Pollution));
        }

        return text;
    }

    /// <summary>Položka spokojenosti jako body se znaménkem (0.25 → „+25").</summary>
    private static string Points(double value)
    {
        int points = (int)Math.Round(value * 100);
        return points > 0 ? $"+{points}" : points.ToString();
    }

    /// <summary>
    /// Vybavenost nástroji v HUD. Ukazuje se, teprve až hráč nějaké nástroje má —
    /// dřív by to byl řádek o mechanice, kterou ještě nepotkal.
    /// </summary>
    private void UpdateToolsLabel(Localization loc)
    {
        var tools = _screens.Content.Gameplay.Tools;
        if (!tools.IsEnabled || !_simulation.IsResourceKnown(tools.ResourceIndex))
        {
            _toolsLabel.Text = string.Empty;
            return;
        }

        double coverage = _simulation.ToolCoverage;
        _toolsLabel.Text = loc.Format("hud.tools", (int)Math.Round(coverage * 100));
        _toolsLabel.TextColor = coverage >= 0.75 ? new Color(150, 220, 150)
            : coverage >= 0.35 ? new Color(230, 210, 130)
            : new Color(200, 195, 180);
    }

    /// <summary>
    /// Znečištění v HUD. Objeví se, teprve až se něco pokazí — dokud je vzduch
    /// čistý, není o čem mluvit a bronzová doba nemá na obrazovce řádek o smogu,
    /// který ještě nikdo nevyrobil.
    /// </summary>
    private void UpdatePollutionLabel(Localization loc)
    {
        double severity = _simulation.AirPollutionSeverity;
        if (!_simulation.PollutionEnabled || severity < 0.01)
        {
            _pollutionLabel.Text = string.Empty;
            return;
        }

        _pollutionLabel.Text = loc.Format("hud.pollution", (int)Math.Round(severity * 100));
        _pollutionLabel.TextColor = severity >= 0.6 ? new Color(228, 120, 100)
            : severity >= 0.3 ? new Color(230, 190, 120)
            : new Color(180, 190, 170);
    }

    /// <summary>
    /// Tlačítko zakázek: kolik jich visí a jestli je něco k odevzdání.
    ///
    /// <para>Zelená je celé to „pojď se podívat" — hráč nemusí nic otevírat, aby
    /// věděl, že si může přijít pro odměnu. Bez toho by nástěnka existovala jen
    /// pro toho, kdo si na ni vzpomene.</para>
    /// </summary>
    private void UpdateContractsButton(Localization loc)
    {
        if (_contractsButton is null)
        {
            return;
        }

        int offers = 0;
        int ready = 0;
        for (int slot = 0; slot < _simulation.ContractSlots.Length; slot++)
        {
            if (!_simulation.ContractSlots[slot].IsActive)
            {
                continue;
            }

            offers++;
            if (_simulation.CanFulfilContract(slot))
            {
                ready++;
            }
        }

        if (_contractsButton.Content is not Label label)
        {
            return;
        }

        label.Text = ready > 0
            ? $"{loc["hud.contracts"]} {ready}/{offers} ✓"
            : $"{loc["hud.contracts"]} {offers}";
        label.TextColor = ready > 0 ? new Color(150, 220, 150) : Color.White;
    }

    /// <summary>Vyzvedne oznámení ze simulace (splněné úkoly, achievementy, milníky) a udělá z nich toasty.</summary>
    private void DrainNotifications()
    {
        var loc = _screens.Loc;
        while (_simulation.TryDequeueNotification(out var note))
        {
            string subject = note.HasSubjectArg ? loc.Format(note.SubjectKey, note.SubjectArg) : loc[note.SubjectKey];
            if (!_captureMode)
            {
                _toasts.Add($"{loc[note.TitleKey]}: {subject}", NotificationColor(note.Kind));
                _sounds.PlayChime(); // dobrá zpráva → příjemné cinknutí
            }

            // Milník a Vzestup dostanou navíc oslavu přes obrazovku. Tisící
            // obyvatel se nemá ztratit v rohu vedle „sklad je plný".
            if (!_captureMode
                && note.Kind is NotificationKind.Milestone
                    or NotificationKind.Ascended
                    or NotificationKind.BuildingMilestone)
            {
                _celebration.Show(subject, NotificationColor(note.Kind));
            }

            // Div světa se stavěl minuty — dokončení nesmí skončit jako řádek
            // v rohu vedle „sklad je plný".
            if (!_captureMode && note.TitleKey == "toast.wonderDone")
            {
                _celebration.Show(subject, new Color(240, 200, 90));
            }

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
            else if (note.TitleKey == "toast.challenge")
            {
                _screens.Profile.ChallengesCompleted++;
                _screens.SaveProfile();
            }
        }
    }

    /// <summary>
    /// Nechá ředitele rozhodnout, jestli je zrovna něco na řadě — a zobrazí to.
    ///
    /// <para>Obrazovka jen prezentuje; <b>co</b> a <b>kdy</b> řeší
    /// <see cref="ContentDirector"/> v Core, aby se to dalo testovat bez grafiky
    /// (CLAUDE.md, vrstvy).</para>
    /// </summary>
    private void UpdateDirector(float dt)
    {
        // Při focení a při běžícím overlayi ředitele umlčíme: vyskakovací okno
        // přes screenshot je poslední, co kdo chce, a toast přes menu taky ne.
        if (_captureMode)
        {
            return;
        }

        var decision = _director.Advance(_simulation, dt);
        switch (decision.Cue)
        {
            case DirectorCue.Event when decision.EventIndex >= 0:
                _screens.Push(new EventScreen(
                    _screens, _simulation, _screens.Content.Events[decision.EventIndex]));
                break;

            case DirectorCue.Hint:
                _toasts.Add(_screens.Loc[decision.HintKey], new Color(210, 200, 150));
                break;
        }
    }

    /// <summary>Jak často se odečítá přírůstek surovin (s). Krátký vzorek + vyhlazení.</summary>
    private const float RateSampleSeconds = 0.25f;

    /// <summary>
    /// Jak rychle vyhlazená rychlost dojíždí k naměřené (0–1 na vzorek). Nízké
    /// číslo = klidný ukazatel, který neposkakuje po každém dokončeném cyklu.
    /// </summary>
    private const double RateSmoothing = 0.25;

    /// <summary>
    /// Přírůstek surovin za sekundu pro HUD.
    ///
    /// <para>Dřív se odečítalo jednou za sekundu a číslo skákalo: výroba je po
    /// dávkách, takže se v jedné sekundě dokončilo pět cyklů a v další žádný.
    /// Teď se vzorkuje čtyřikrát častěji a hodnota se k naměřené jen přibližuje —
    /// ukazatel tím dýchá místo blikání a dá se z něj číst trend.</para>
    /// </summary>
    private void SampleRates(float dt)
    {
        _rateTimer += dt;
        if (_rateTimer < RateSampleSeconds)
        {
            return;
        }

        for (int i = 0; i < _perSecond.Length; i++)
        {
            double now = _simulation.GetResource(i);
            double measured = (now - _ratePrev[i]) / _rateTimer;
            _perSecond[i] += (measured - _perSecond[i]) * RateSmoothing;
            _ratePrev[i] = now;
        }

        _rateTimer = 0f;
        WarnAboutFullStorage(dt);
    }

    /// <summary>
    /// Upozorní, že sklad přetéká a výroba propadá. Idle konvence je nechat
    /// přebytek propadnout bez trestu — jenže pak se hráč nedozví, že už hodinu
    /// vyrábí do prázdna. Hláška chodí nejvýš jednou za
    /// <see cref="FullStorageCooldownSeconds"/> a jen u surovin, které opravdu
    /// tečou; jinak by z ní byl otravný budík.
    /// </summary>
    private void WarnAboutFullStorage(float dt)
    {
        _ = dt;
        for (int i = 0; i < _perSecond.Length; i++)
        {
            if (_fullStorageCooldown[i] > 0)
            {
                _fullStorageCooldown[i] -= RateSampleSeconds;
                continue;
            }

            if (!_simulation.IsResourceKnown(i) || _perSecond[i] <= 0.01)
            {
                continue;
            }

            double cap = _simulation.GetStorageCap(i);
            if (cap <= 0 || _simulation.GetResource(i) < cap - 0.001)
            {
                continue;
            }

            _fullStorageCooldown[i] = FullStorageCooldownSeconds;
            if (_captureMode)
            {
                continue;
            }

            _toasts.Add(
                _screens.Loc.Format("toast.storageFull", _screens.Loc[_screens.Content.Resources[i].NameKey]),
                new Color(230, 200, 120));
        }
    }

    /// <summary>Jak dlouho mlčet o jedné a téže přetékající surovině (s).</summary>
    private const float FullStorageCooldownSeconds = 120f;

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

                // Tentýž okamžik ohlas i platformě. Steam si drží vlastní stav,
                // takže bez tohohle by se achievement odemkl ve hře, ale ve
                // Steamu ne — a hráč by ho tam marně hledal.
                _screens.Platform.UnlockAchievement(PlatformCatalog.AchievementApiName(id));
                changed = true;
            }
        }

        if (changed)
        {
            _screens.SaveProfile();

            // Statistiky se posílají spolu s achievementy: obojí se mění zřídka
            // a Steam stejně ukládá dávkově, takže je to jeden zápis navíc.
            PlatformCatalog.PushStats(_screens.Platform, _simulation);
            PlatformCatalog.PushScores(_screens.Platform, _simulation);
            _screens.Platform.Flush();
        }
    }

    /// <summary>
    /// Zapíše do kroniky, co tahle hra dokázala (rekordy a zastavěné biomy).
    /// Volá se spolu s autosavem, ne každý snímek — profil se ukládá na disk
    /// a rekord se stejně mění zřídka.
    /// </summary>
    private void SyncChronicle()
    {
        var profile = _screens.Profile;
        var eras = _screens.Content.Eras;
        int eraIndex = _simulation.CurrentEraIndex;

        bool changed = profile.RecordRun(new RunRecord(
            _simulation.Population,
            _simulation.Buildings.Length,
            _simulation.AscensionLevel,
            _simulation.Settlements.Count,
            eraIndex >= 0 && eraIndex < eras.Count ? eras[eraIndex].Order : -1,
            eraIndex >= 0 && eraIndex < eras.Count ? eras[eraIndex].Id : string.Empty,
            _simulation.ContractsCompleted,
            _simulation.WondersCompleted,
            _simulation.TickCount / Simulation.TicksPerSecond));

        // Odehraný čas se sčítá po přírůstcích — celek by se každým uložením
        // připočetl znovu.
        if (_unsavedPlaySeconds > 0)
        {
            profile.AddPlaytime(_unsavedPlaySeconds);
            _unsavedPlaySeconds = 0;
            changed = true;
        }

        var biomes = _screens.Content.Biomes;
        for (int i = 0; i < biomes.Count; i++)
        {
            if (_simulation.HasSettledBiome(i) && profile.RecordBiome(biomes[i].Id))
            {
                changed = true;
            }
        }

        if (changed)
        {
            _screens.SaveProfile();
        }
    }

    /// <summary>
    /// Odpověď na modlitbu. Ticho musí být vidět stejně jasně jako zázrak —
    /// jinak by hráč nevěděl, jestli se vůbec něco stalo, a měl by pocit, že
    /// hra spolkla suroviny.
    /// </summary>
    private void ShowPrayerOutcome(PrayerOutcome outcome, Vector2 world)
    {
        var loc = _screens.Loc;
        if (outcome == PrayerOutcome.Answered)
        {
            _toasts.Add(loc["toast.prayerAnswered"], new Color(255, 226, 150));
            _floatingText.Add(world, loc["toast.prayerAnswered"], new Color(255, 226, 150));
            _particles.SpawnBurst(world, new Color(255, 226, 150), 20, 60f, 190f);
            _sounds.PlayChime();
            return;
        }

        if (outcome == PrayerOutcome.Unanswered)
        {
            _toasts.Add(loc["toast.prayerUnanswered"], new Color(150, 155, 170));
            _floatingText.Add(world, loc["toast.prayerUnanswered"], new Color(150, 155, 170));
        }
    }

    private static Color NotificationColor(NotificationKind kind) => kind switch
    {
        NotificationKind.QuestCompleted => new Color(120, 200, 140),
        NotificationKind.ContractReady => new Color(150, 220, 150), // stejná zelená jako „✓" na tlačítku zakázek
        NotificationKind.AchievementUnlocked => new Color(230, 200, 110),
        NotificationKind.Ascended => new Color(180, 140, 230),
        NotificationKind.BuildingMilestone => new Color(255, 214, 120), // barva ohňostroje
        _ => new Color(96, 196, 220),
    };

    // ----- HUD -----

    private void BuildUi()
    {
        var content = _screens.Content;

        // Horní pruh: suroviny (ikony) + zásoba/kapacita + přírůstek za sekundu.
        //
        // Zabalený do řádků, ne jeden dlouhý pruh: surovin je devatenáct a v jedné
        // řadě by při plné hře přetekly z obrazovky ven. Do řádků se skládají jen
        // ty ZNÁMÉ, takže se v pruhu nedělají díry po neobjevených.
        _resourceRows = new VerticalStackPanel { Spacing = 4 };
        var resourceBar = new VerticalStackPanel { Spacing = 4 };
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

            // Pevná šířka obou popisků. Bez ní se pruh při každé změně čísla
            // přeskládal — „9/1000" je užší než „10/1000", takže se posunulo
            // úplně všechno vpravo od té suroviny. Rezervované místo znamená,
            // že se čísla mění uvnitř svého okénka a lišta stojí.
            _resourceLabels[i] = new Label
            {
                VerticalAlignment = VerticalAlignment.Center,
                MinWidth = AmountLabelWidth,
            };
            chip.Widgets.Add(_resourceLabels[i]);
            _resourceRateLabels[i] = new Label
            {
                VerticalAlignment = VerticalAlignment.Center,
                TextColor = new Color(120, 190, 130),
                MinWidth = RateLabelWidth,
            };
            chip.Widgets.Add(_resourceRateLabels[i]);
            chip.Tooltip = ResourceTooltip(i);
            // Neznámá surovina se v pruhu vůbec neukáže — hra nesmí prozrazovat
            // obsah, ke kterému se hráč ještě nedostal (odhalí se získáním).
            chip.Visible = _simulation.IsResourceKnown(i);
            _resourceChips[i] = chip;
        }

        resourceBar.Widgets.Add(_resourceRows);
        RepackResourceChips();

        _populationLabel = new Label
        {
            VerticalAlignment = VerticalAlignment.Center,
            TextColor = UiFactory.Accent,
            MinWidth = AmountLabelWidth, // ať rostoucí počet obyvatel netlačí hlášku vedle sebe
        };

        // Lidé a nečinné budovy na vlastním řádku pod surovinami — jsou to jiná
        // čísla než sklad a v zabaleném pruhu by jinak plavaly kdekoli.
        var summaryRow = new HorizontalStackPanel { Spacing = 14 };
        summaryRow.Widgets.Add(_populationLabel);

        // Nevyužité budovy se musí ohlásit: bez dělníků nevyrábějí a hráč by jinak
        // jen viděl, že mu stavění přestalo něco přinášet, aniž by věděl proč.
        _idleLabel = new Label
        {
            VerticalAlignment = VerticalAlignment.Center,
            TextColor = new Color(240, 180, 90),
            Tooltip = _screens.Loc["tip.idleBuildings"],
        };
        summaryRow.Widgets.Add(_idleLabel);
        resourceBar.Widgets.Add(summaryRow);

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
        _seasonLabel = new Label { HorizontalAlignment = HorizontalAlignment.Right };
        _toolsLabel = new Label { HorizontalAlignment = HorizontalAlignment.Right, Tooltip = loc["tip.tools"] };
        _pollutionLabel = new Label { HorizontalAlignment = HorizontalAlignment.Right, Tooltip = loc["tip.pollution"] };
        _happinessLabel = new Label { HorizontalAlignment = HorizontalAlignment.Right, Tooltip = loc["tip.happiness"] };
        _dayLabel = new Label { TextColor = UiFactory.Accent, Tooltip = loc["tip.day"] };
        _cursorLabel = new Label { TextColor = Color.LightGray };
        var worldInfoStack = new VerticalStackPanel { Spacing = 3, HorizontalAlignment = HorizontalAlignment.Right };
        worldInfoStack.Widgets.Add(_eraLabel);
        worldInfoStack.Widgets.Add(_eraNextLabel);
        worldInfoStack.Widgets.Add(_tierLabel);
        worldInfoStack.Widgets.Add(_powerLabel);
        worldInfoStack.Widgets.Add(_weatherLabel);
        if (_screens.Content.Seasons.IsEnabled)
        {
            worldInfoStack.Widgets.Add(_seasonLabel);
        }

        if (_screens.Content.Gameplay.Tools.IsEnabled)
        {
            worldInfoStack.Widgets.Add(_toolsLabel);
        }
        if (_screens.Content.Gameplay.Pollution.IsEnabled)
        {
            worldInfoStack.Widgets.Add(_pollutionLabel);
        }
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
        _statusLabel = new Label { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };

        // Silnice: „stavět" a „bourat" jsou dva režimy jednoho nástroje, ne dvě
        // tlačítka v liště. Přepínač je proto tady — objeví se, teprve když ho
        // hráč potřebuje, a jinak nezabírá místo.
        _roadAddButton = UiFactory.SmallButton("+", () => _tools.SetRoadErasing(false), loc["tip.road"]);
        _roadEraseButton = UiFactory.SmallButton("−", () => _tools.SetRoadErasing(true), loc["tip.roadErase"]);
        _roadModePanel = new HorizontalStackPanel { Spacing = 4, VerticalAlignment = VerticalAlignment.Center };
        _roadModePanel.Widgets.Add(_roadAddButton);
        _roadModePanel.Widgets.Add(_roadEraseButton);
        _roadModePanel.Visible = false;

        // Násobič hromadné stavby: stejné místo jako přepínač silnic — objeví se,
        // teprve když hráč něco staví, a jinak liště nepřekáží.
        _batchPanel = new HorizontalStackPanel { Spacing = 4, VerticalAlignment = VerticalAlignment.Center };
        var sizes = _tools.BatchSizes;
        _batchButtons = new Button[sizes.Count];
        for (int i = 0; i < sizes.Count; i++)
        {
            int size = sizes[i];
            _batchButtons[i] = UiFactory.SmallButton($"×{size}", () => _tools.SetBatchSize(size), loc["tip.batch"]);
            _batchPanel.Widgets.Add(_batchButtons[i]);
        }

        _batchPanel.Visible = false;

        var statusRow = new HorizontalStackPanel
        {
            Spacing = 10,
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        statusRow.Widgets.Add(_statusLabel);
        statusRow.Widgets.Add(_roadModePanel);
        statusRow.Widgets.Add(_batchPanel);

        _statusPanel = UiFactory.DarkPanel(statusRow);
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

        // Sledovač úkolů do snímku pro obchod nepatří: „Zasaď svůj první strom"
        // udělá ze zralého města tutoriál. Ve hře je naopak to nejdůležitější,
        // takže se vynechává jen při focení.
        if (!_captureMode)
        {
            root.Widgets.Add(_objectives.Root);
        }

        root.Widgets.Add(bottomBar);
        root.Widgets.Add(BuildScreenButtons());

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
    /// <summary>Tlačítko s vybraným druhem k zasazení; null = druhy nejsou.</summary>
    private Widget? _plantSpeciesButton;

    /// <summary>Popisek „Sázet: Háj" — jméno druhu přichází z dat, ne z kódu.</summary>
    private string PlantSpeciesLabel()
    {
        var species = _simulation.SelectedPlantSpecies;
        return species is null
            ? _screens.Loc["hud.plant"]
            : _screens.Loc.Format("hud.plantSpecies", _screens.Loc[species.NameKey]);
    }

    /// <summary>Přepne druh a rovnou zapne sázení — hráč chtěl sázet, ne listovat.</summary>
    private void CyclePlantSpecies()
    {
        _simulation.CyclePlantSpecies();
        if (_plantSpeciesButton is Button button && button.Content is Label label)
        {
            label.Text = PlantSpeciesLabel();
        }

        if (!_tools.PlantMode)
        {
            _tools.TogglePlant();
        }
    }

    /// <summary>Modlitba čeká na ukázání cíle na mapě; další klik ji pronese.</summary>
    private void StartPrayerTargeting(int prayerIndex, int strength)
    {
        _pendingPrayer = prayerIndex;
        _pendingPrayerStrength = strength;
        _tools.Clear(); // rozestavěná budova by cíl přebila
        _toasts.Add(_screens.Loc["faith.pickTarget"], new Color(255, 226, 150));
    }

    /// <summary>
    /// Vyřídí modlitbu čekající na cíl. Vrací <c>true</c>, když si klik vzala —
    /// pak už ho nikdo jiný nedostane.
    /// </summary>
    private bool ResolvePendingPrayer(bool mouseOverUi)
    {
        if (_pendingPrayer < 0)
        {
            return false;
        }

        // Pravý klik (nebo Esc) modlitbu zruší, ať hráč nezůstane v pasti
        // s kurzorem, který dělá něco jiného, než čeká.
        if (_input.WasRightPressed || _input.WasPressed(Keys.Escape))
        {
            _pendingPrayer = -1;
            _toasts.Add(_screens.Loc["faith.targetCancelled"], new Color(150, 155, 170));
            return true;
        }

        if (mouseOverUi || !_input.WasLeftPressed)
        {
            return true; // cíl se pořád vybírá — klik nikam jinam nepustíme
        }

        var world = _camera.ScreenToWorld(_input.MousePosition.ToVector2());
        int tileX = (int)MathF.Floor(world.X / TerrainRenderer.TileSize);
        int tileY = (int)MathF.Floor(world.Y / TerrainRenderer.TileSize);

        string effect = _screens.Content.Faith.Prayers[_pendingPrayer].Effect;
        var outcome = _simulation.TryPray(_pendingPrayer, _pendingPrayerStrength, tileX, tileY);
        _pendingPrayer = -1;

        ShowPrayerOutcome(outcome, world);
        if (outcome == PrayerOutcome.Answered)
        {
            ShowStrikeImpact(effect, world);
        }

        return true;
    }

    /// <summary>
    /// Podívaná po vyslyšené ráně. Bez ní vypadal i úspěšný meteorit na prázdné
    /// pláni jako by se nestalo nic — a hráč neměl jak poznat, že modlitba vyšla.
    /// </summary>
    private void ShowStrikeImpact(string effect, Vector2 world)
    {
        switch (effect)
        {
            case "smite_meteor":
                _particles.SpawnBurst(world, new Color(255, 150, 60), 60, 90f, 420f);
                _particles.SpawnBurst(world, new Color(70, 60, 55), 40, 40f, 220f);
                _fireworks.Burst(world, HashCode.Combine((int)world.X, (int)world.Y));
                _sounds.PlayPlace();
                break;

            case "smite_flood":
                _particles.SpawnBurst(world, new Color(110, 180, 235), 60, 60f, 320f);
                _particles.SpawnBurst(world, new Color(200, 230, 245), 30, 30f, 160f);
                _sounds.PlayPlace();
                break;

            case "smite_blight":
                _particles.SpawnBurst(world, new Color(150, 130, 70), 45, 30f, 200f);
                _sounds.PlayPlace();
                break;

            case "bless_regrow":
                _particles.SpawnBurst(world, new Color(120, 220, 120), 45, 40f, 240f);
                break;

            case "bless_reveal":
                _particles.SpawnBurst(world, new Color(170, 220, 255), 40, 60f, 300f);
                break;

            case "bless_festival":
                _fireworks.Burst(world, HashCode.Combine((int)world.X, (int)world.Y));
                break;
        }
    }

    /// <summary>Ikona z knihovny; <c>null</c>, když ji knihovna nezná.</summary>
    private Texture2D? Ico(string key) => _screens.Sprites.Get(key);

    /// <summary>
    /// Panel s ikonami v mřížce o pevné šířce.
    ///
    /// <para>Pevná šířka je tu podstatná, ne kosmetická: roztažitelný panel si
    /// v Myře ukousl víc plochy, než na kolik bylo vidět, a hit-test pak bral
    /// kliknutí mapě pod sebou — hráč nemohl postavit silnici a nevěděl proč.</para>
    /// </summary>
    private static Grid IconGrid(int columns)
    {
        var grid = new Grid
        {
            ColumnSpacing = 6,
            RowSpacing = 6,
            Width = columns * (UiFactory.IconButtonSize + 6),
        };

        for (int i = 0; i < columns; i++)
        {
            grid.ColumnsProportions.Add(new Proportion(ProportionType.Auto));
        }

        return grid;
    }

    /// <summary>Posadí tlačítko do mřížky na dané pořadí (řádky se dopočítají).</summary>
    private static void Place(Grid grid, Widget widget, int index, int columns)
    {
        Grid.SetColumn(widget, index % columns);
        Grid.SetRow(widget, index / columns);
        grid.Widgets.Add(widget);
    }

    /// <summary>
    /// Pravý dolní blok: ovládání času a odbočky do obrazovek, v mřížce ikon
    /// nad minimapou.
    ///
    /// <para>Rozdělení HUD je podle toho, co tlačítko dělá: <b>dole nástroje</b>
    /// (mění mapu), <b>vpravo odbočky</b> (otevírají okno). Předchozí pokus byl
    /// jeden dlouhý sloupec textových tlačítek — bylo jich dvanáct pod sebou,
    /// zabíral půl obrazovky a nešlo v něm nic najít.</para>
    /// </summary>
    private Widget BuildScreenButtons()
    {
        var loc = _screens.Loc;
        const int columns = 4;
        var grid = IconGrid(columns);
        int slot = 0;

        _speedButton = UiFactory.ToolButton(Ico("ui.play"), loc["tip.speed"], () =>
        {
            _speed.Next();
            RefreshHudTexts();
        }, "1x");
        Place(grid, _speedButton, slot++, columns);

        Place(grid, UiFactory.ToolButton(
            Ico("ui.home"), loc["hud.backToCity"] + '\n' + loc["tip.backToCity"], RecenterOnCity), slot++, columns);

        if (_simulation.IsFeatureUnlocked("settlements"))
        {
            Place(grid, UiFactory.ToolButton(
                Ico("ui.settlements"), loc["hud.settlements"] + '\n' + loc["tip.settlements"],
                () => _screens.Push(new SettlementsScreen(_screens, _simulation, _camera))), slot++, columns);
        }

        Place(grid, UiFactory.ToolButton(
            Ico("ui.quests"), loc["hud.quests"] + '\n' + loc["tip.quests"],
            () => _screens.Push(new QuestsScreen(_screens, _simulation))), slot++, columns);

        // Zakázky mají vlastní tlačítko, i když bydlí na obrazovce úkolů: je to
        // nejkratší smyčka ve hře a schovaná o dvě kliknutí by zanikla.
        if (_simulation.ContractsEnabled)
        {
            _contractsButton = UiFactory.ToolButton(
                Ico("ui.contracts"), loc["hud.contracts"] + '\n' + loc["tip.contracts"],
                () => _screens.Push(new QuestsScreen(_screens, _simulation)));
            Place(grid, _contractsButton, slot++, columns);
        }

        if (_screens.Content.Techs.Count > 0 && _simulation.IsFeatureUnlocked("research"))
        {
            Place(grid, UiFactory.ToolButton(
                Ico("ui.tech"), loc["hud.tech"] + '\n' + loc["tip.tech"],
                () => _screens.Push(new TechScreen(_screens, _simulation))), slot++, columns);
        }

        if (_screens.Content.Policies.Count > 0 && _simulation.IsFeatureUnlocked("governor"))
        {
            Place(grid, UiFactory.ToolButton(
                Ico("ui.governor"), loc["hud.governor"] + '\n' + loc["tip.governor"],
                () => _screens.Push(new PoliciesScreen(_screens, _simulation))), slot++, columns);
        }

        if (_simulation.IsFeatureUnlocked("ascend"))
        {
            Place(grid, UiFactory.ToolButton(
                Ico("ui.ascend"), loc["hud.ascend"] + '\n' + loc["tip.ascend"],
                () => _screens.Push(new AscensionScreen(_screens, _simulation, _info))), slot++, columns);
        }

        // Velké dílo se v liště objeví, teprve až je čím sypat — dřív by to byla
        // nabídka na něco, co hráč nemá jak použít.
        if (_simulation.GrandWorkAvailable)
        {
            Place(grid, UiFactory.ToolButton(
                Ico("ui.ascend"), loc["hud.grandwork"] + '\n' + loc["grandwork.desc"],
                () => _screens.Push(new GrandWorkScreen(_screens, _simulation))), slot++, columns);
        }

        // Odkaz se ukáže až po prvním Vzestupu — vrstva nad mechanikou, kterou
        // hráč ještě nezná, by byla jen matoucí tlačítko navíc.
        if (_simulation.LegacyAvailable)
        {
            Place(grid, UiFactory.ToolButton(
                Ico("ui.ascend"), loc["hud.legacy"] + '\n' + loc["legacy.desc"],
                () => _screens.Push(new LegacyScreen(_screens, _simulation))), slot++, columns);
        }

        if (_simulation.HistoryEnabled)
        {
            Place(grid, UiFactory.ToolButton(
                Ico("ui.stats"), loc["hud.stats"] + '\n' + loc["tip.stats"],
                () => _screens.Push(new StatsScreen(_screens, _simulation.History))), slot++, columns);
        }

        Place(grid, UiFactory.ToolButton(
            Ico("ui.trophy"), loc["hud.achievements"] + '\n' + loc["tip.achievements"],
            () => _screens.Push(new AchievementsScreen(_screens, _simulation))), slot++, columns);

        Place(grid, UiFactory.ToolButton(
            Ico("ui.stats"), loc["hud.leaderboards"] + '\n' + loc["board.title"],
            () => _screens.Push(new LeaderboardScreen(_screens, _simulation))), slot++, columns);

        if (_simulation.IsFeatureUnlocked("elections") && _screens.Content.Elections.IsEnabled)
        {
            Place(grid, UiFactory.ToolButton(
                Ico("ui.vote"), loc["hud.election"] + '\n' + loc["tip.election"],
                () => _screens.Push(new ElectionScreen(_screens, _simulation))), slot++, columns);
        }

        Place(grid, UiFactory.ToolButton(
            Ico("ui.chronicle"), loc["menu.chronicle"] + '\n' + loc["tip.chronicle"],
            () => _screens.Push(new ChronicleScreen(_screens))), slot, columns);

        var panel = UiFactory.DarkPanel(grid);
        panel.HorizontalAlignment = HorizontalAlignment.Right;
        panel.VerticalAlignment = VerticalAlignment.Bottom;
        panel.Margin = new Thickness(0, 0, 12, MinimapRenderer.ReservedHeight);
        return panel;
    }

    /// <summary>
    /// Dolní lišta: nástroje, které mění mapu. Jedna řada ikon, ne řádek slov —
    /// z názvů „Silnice / Sloučit / Rezidenční / Zavlažit" byla přes půl
    /// obrazovky dlouhá věta, ve které se nedalo nic najít.
    /// </summary>
    private Widget BuildToolButtons()
    {
        var loc = _screens.Loc;
        var row = new HorizontalStackPanel { Spacing = 6, HorizontalAlignment = HorizontalAlignment.Center };

        // „Stavět" vytáhne katalog budov NAD lištu — spodek obrazovky tak zůstává
        // úzký proužek, ne trvale rozložené menu přes půl mapy.
        _buildMenuButton = UiFactory.ToolButton(Ico("ui.build"),
            loc["hud.build"] + '\n' + loc["tip.build"] + '\n' + loc["tip.bulkBuild"], ToggleBuildMenu);
        row.Widgets.Add(_buildMenuButton);

        // Každá funkce se objeví, teprve až si ji hráč odemkne (data/features.json).
        if (_simulation.IsFeatureUnlocked("plant"))
        {
            row.Widgets.Add(UiFactory.ToolButton(
                Ico("ui.plant"), loc["hud.plant"] + '\n' + loc["tip.plant"], _tools.TogglePlant));

            // Druhý knoflík vedle: čím se sází. Přepínat druh schovaným klikem do
            // téhož tlačítka by znamenalo, že hráč nemá jak zjistit, že jich je víc.
            if (_screens.Content.Gameplay.Planting.Species.Count > 1)
            {
                _plantSpeciesButton = UiFactory.SmallButton(
                    PlantSpeciesLabel(), CyclePlantSpecies, loc["tip.plantSpecies"]);
                row.Widgets.Add(_plantSpeciesButton);
            }
        }

        // Silnice: tvar sítě má být na hráči — auto-silnice řeší jen nutné napojení.
        if (_simulation.IsFeatureUnlocked("roads"))
        {
            row.Widgets.Add(UiFactory.ToolButton(
                Ico("ui.road"), loc["hud.road"] + '\n' + loc["tip.road"], _tools.ToggleRoad));
        }

        // Slučování bloků 2×2 v jednu velkou budovu.
        if (_simulation.IsFeatureUnlocked("merge"))
        {
            row.Widgets.Add(UiFactory.ToolButton(
                Ico("ui.demolish"), loc["hud.merge"] + '\n' + loc["tip.merge"], _tools.ToggleMerge));
        }

        // Zóny (automatizace): jedno tlačítko na typ; klik = malovat, další klik
        // na stejný = ven. Ikona je jedna, barvu nese bublina s názvem zóny.
        var zoneTypes = _simulation.IsFeatureUnlocked("zones") ? _screens.Content.ZoneTypes : null;
        for (int z = 0; zoneTypes is not null && z < zoneTypes.Count; z++)
        {
            int typeIndex = z;
            row.Widgets.Add(UiFactory.ToolButton(
                Ico("ui.zone"), loc[zoneTypes[z].NameKey] + '\n' + ZoneTooltip(zoneTypes[z]),
                () => _tools.ToggleZone(typeIndex)));
        }

        // Víra: modlitby jsou vlastní obrazovka, protože nesou volbu síly
        // a čísla (cena vs. šance), která se do lišty nevejdou.
        if (_simulation.FaithEnabled)
        {
            row.Widgets.Add(UiFactory.ToolButton(
                Ico("ui.faith"), loc["hud.faith"] + '\n' + loc["tip.faith"],
                () => _screens.Push(new PrayerScreen(_screens, _simulation, StartPrayerTargeting))));
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

            row.Widgets.Add(UiFactory.ToolButton(
                Ico("ui.terraform"),
                loc[action.NameKey] + '\n' + loc[action.DescriptionKey] + '\n'
                    + loc.Format("panel.cost", CostFormat.Line(_screens.Content, loc, action.Cost)),
                () => _tools.ToggleTerraform(actionIndex)));
        }

        // Slavnost: aktivní boost na kliknutí (stav se přepisuje v RefreshHudTexts).
        _festivalButton = UiFactory.ToolButton(
            Ico("ui.festival"),
            loc["hud.festival"] + '\n' + loc.Format("tip.festival",
                _screens.Content.Gameplay.Boost.Multiplier.ToString("0.#"),
                _screens.Content.Gameplay.Boost.DurationSeconds,
                _screens.Content.Gameplay.Boost.CooldownSeconds),
            () => _simulation.TryStartBoost());
        _festivalButton.Background = new SolidBrush(new Color(150, 90, 60, 235));
        if (_simulation.IsFeatureUnlocked("festival"))
        {
            row.Widgets.Add(_festivalButton);
        }

        var panel = UiFactory.DarkPanel(row);
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
            // Cena je bílá, dokud na ni hráč má, a červená, když ne. Zelená je
            // vyhrazená VÝROBĚ — dokud byla cena taky zelená, splývalo v dlaždici
            // „co to stojí" s „co to dělá".
            priceLabel.TextColor = affordable ? new Color(225, 228, 235) : new Color(232, 120, 110);
            button.Background = new SolidBrush(affordable ? new Color(38, 48, 64, 235) : new Color(30, 34, 42, 170));
        }
    }

    /// <summary>
    /// Uloží hru na pozadí (autosave). Selhání se schválně nehlásí vyskakovacím
    /// oknem — je to tichá pojistka, ne akce hráče; ruční uložení v pauze dál
    /// výsledek hlásí.
    /// </summary>
    private void SaveGame()
    {
        SyncChronicle();
        _screens.Saves.TrySave(_simulation, new SaveMetadata(_info.Seed, _info.SizeId, _info.PresetId, DateTime.UtcNow));
    }

    /// <summary>
    /// Řekne simulaci, jaký je dnes den (UTC). Simulace si na hodiny nesahá sama,
    /// aby zůstala deterministická — datum je vstup jako každý jiný. Změna dne
    /// uvnitř vydá novou sadu denních výzev; volání ve stejný den nic nedělá,
    /// takže se to může klidně ptát každý snímek.
    /// </summary>
    private void RefreshChallengeDay() =>
        _simulation.SetChallengeDay(DailyReward.TodayKey(DateTime.UtcNow));

    /// <summary>
    /// Posune karavanu a vyplatí ji, když dorazí. Odemyká se stejnou funkcí jako
    /// ruční silnice — bez sítě není kudy jezdit.
    /// </summary>
    private void UpdateCaravan(float dt)
    {
        if (!_simulation.IsFeatureUnlocked("roads"))
        {
            return;
        }

        _caravans.Update(dt, _simulation);
        if (_caravans.TryCollectArrival(
            _simulation, out int resourceIndex, out int amount, out var position, out long cityKey))
        {
            // Výplatu i vztah řeší simulace — obrazovka jen hlásí, že karavana
            // dojela, a ukáže výsledek (CLAUDE.md, vrstvy).
            int paid = _simulation.CompleteCaravan(cityKey, resourceIndex, amount);
            CollectFeedback(resourceIndex, paid, position);
        }
    }

    /// <summary>Promítne přístupnostní volbu „omezit pohyb" do vizuálních efektů.</summary>
    private void ApplyMotionSettings()
    {
        bool motion = !_screens.Settings.ReduceMotion;
        _particles.Enabled = motion;
        _floatingText.Enabled = motion;
        _cityPulse.Enabled = motion;
        _celebration.Enabled = motion;
        _fireworks.Enabled = motion;
        _laser.Enabled = motion;
        if (!motion)
        {
            _fireworks.Clear();
        }

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
                _screens.Push(new AscensionScreen(_screens, _simulation, _info));
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
            Text = loc.Format("hud.build.cost", CostFormat.Line(content, loc, def.BuildCost)),
            TextColor = new Color(225, 228, 235),
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        caption.Widgets.Add(priceLabel);

        // Co budova DĚLÁ a co k tomu POTŘEBUJE, přímo pod ikonu. Bublina to říká
        // taky, ale hráč vybírá z řady ikon a nemá jak poznat, čím se liší, dokud
        // na každou zvlášť nenajede.
        string effect = BuildingSummary.Effect(content, loc, def);
        if (effect.Length > 0)
        {
            caption.Widgets.Add(new Label
            {
                Text = effect,
                TextColor = new Color(140, 210, 150),
                HorizontalAlignment = HorizontalAlignment.Center,
            });
        }

        string needs = BuildingSummary.Needs(content, loc, def);
        if (needs.Length > 0)
        {
            caption.Widgets.Add(new Label
            {
                Text = loc.Format("hud.build.needs", needs),
                TextColor = new Color(220, 180, 120),
                HorizontalAlignment = HorizontalAlignment.Center,
            });
        }

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

        // Rychlost času: ikona i barva. Pauza svítí, ať je na první pohled jasné,
        // že hra stojí a nečeká se marně na to, až něco doroste.
        if (_speedButton is not null)
        {
            var icon = Ico(_speed.IsPaused ? "ui.pause" : _speed.Multiplier > 1.0 ? "ui.fast" : "ui.play");
            if (icon is not null)
            {
                _speedButton.Content = UiFactory.Icon(icon, UiFactory.IconButtonSize - 14);
                _speedButton.Content.HorizontalAlignment = HorizontalAlignment.Center;
                _speedButton.Content.VerticalAlignment = VerticalAlignment.Center;
            }

            _speedButton.Tooltip = _screens.Loc["tip.speed"] + '\n' + _speed.Label;
            _speedButton.Background = new SolidBrush(_speed.IsPaused
                ? new Color(150, 90, 60, 235)
                : new Color(38, 48, 64, 235));
        }

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
            RepackResourceChips();
        }

        for (int i = 0; i < _resourceLabels.Length; i++)
        {
            if (!_simulation.IsResourceKnown(i))
            {
                continue; // neznámou surovinu není co počítat ani kreslit
            }

            double amount = _simulation.GetResource(i);
            double cap = _simulation.GetStorageCap(i);

            // Vypisuje se DOJÍŽDĚJÍCÍ hodnota, ne skutečná — číslo se plynule
            // dotáčí nahoru místo skoku. Barva a strop se přitom řídí skutečnou
            // hodnotou, aby varování o plném skladu nepřišlo se zpožděním.
            _resourceLabels[i].Text = CivDle.Core.Numbers.FormatRatio(_rolling.Shown(i), cap);

            // Přeteklý sklad zežloutne (výzva rozšířit); přírůstek krátce rozsvítí.
            var baseColor = amount >= cap - 0.5 ? new Color(240, 200, 90) : Color.White;
            _resourceLabels[i].TextColor = Color.Lerp(baseColor, new Color(190, 255, 190), _rolling.Flash(i));

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

        UpdateSeasonLabel(loc);
        UpdateToolsLabel(loc);
        UpdatePollutionLabel(loc);
        UpdateContractsButton(loc);

        // Spokojenost: barva nese stav, ať se to dá číst koutkem oka.
        if (_screens.Content.Gameplay.Happiness.IsEnabled)
        {
            double happiness = _simulation.Happiness;
            _happinessLabel.Text = loc.Format("hud.happiness", (int)Math.Round(happiness * 100));
            _happinessLabel.TextColor = happiness >= 0.75 ? new Color(150, 220, 150)
                : happiness >= 0.45 ? new Color(230, 210, 130)
                : new Color(235, 140, 120);

            // Rozpad v bublině: spokojenost je jediná vrstva, kde se dá udělat
            // chyba, takže hráč musí vidět, KVŮLI ČEMU je zrovna taková. Jedno
            // číslo, které samo klesne, je nespravedlivé.
            _happinessLabel.Tooltip = DescribeHappiness(loc);
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

    /// <summary>
    /// Přepíše bublinu a dostupnost tlačítka Slavnost podle stavu boostu.
    ///
    /// <para>Odpočet je teď v bublině, ne v popisku: tlačítko nese ikonu a číslo
    /// by se do něj nevešlo. Zhasnuté tlačítko říká „teď ne" i beze slov, na
    /// „za jak dlouho" stačí najet.</para>
    /// </summary>
    private void UpdateFestivalButton()
    {
        var loc = _screens.Loc;
        if (_simulation.IsBoostActive)
        {
            _festivalButton.Tooltip = loc.Format("hud.festivalActive",
                (int)MathF.Ceiling((float)_simulation.BoostSecondsRemaining));
            _festivalButton.Enabled = false;
        }
        else if (!_simulation.CanStartBoost)
        {
            _festivalButton.Tooltip = loc.Format("hud.festivalCooldown",
                (int)MathF.Ceiling((float)_simulation.BoostCooldownSecondsRemaining));
            _festivalButton.Enabled = false;
        }
        else
        {
            _festivalButton.Tooltip = loc["hud.festival"];
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
        _roadModePanel.Visible = _tools.RoadToolActive;
        if (_tools.RoadToolActive)
        {
            // Aktivní režim je vidět na tlačítku, ne jen v textu — přepínač má
            // sám ukazovat, v čem zrovna jsi.
            _roadAddButton.Background = new SolidBrush(_tools.RoadEraseMode ? UiFactory.ButtonFill : UiFactory.Accent);
            _roadEraseButton.Background = new SolidBrush(_tools.RoadEraseMode ? UiFactory.Accent : UiFactory.ButtonFill);
        }

        // Násobič dává smysl jen u stavby budov — u silnic, zón ani přesunu ne.
        _batchPanel.Visible = _tools.SelectedBuilding >= 0 && _screens.Content.Gameplay.BulkBuild.HasBatches;
        if (_batchPanel.Visible)
        {
            var sizes = _tools.BatchSizes;
            int affordable = _tools.AffordableCount;
            for (int i = 0; i < _batchButtons.Length; i++)
            {
                bool active = sizes[i] == _tools.BatchSize;
                _batchButtons[i].Background = new SolidBrush(active ? UiFactory.Accent : UiFactory.ButtonFill);

                // Zšedne, na co hráč nemá — ať se nediví, že ×25 položí tři kusy.
                _batchButtons[i].Opacity = affordable >= sizes[i] ? 1f : 0.45f;
            }
        }

        if (!_statusPanel.Visible)
        {
            return;
        }

        if (_tools.MergeMode)
        {
            // Bez bloku pod kurzorem se řekne proč — jinak hráč klika naprázdno
            // a neví, jestli je rozbitý nástroj, nebo jeho zástavba.
            _statusLabel.Text = _tools.MergeGhostActive ? loc["status.merge"] : loc["hud.mergeHint"];
            _statusLabel.TextColor = _tools.MergeGhostActive
                ? new Color(150, 220, 150)
                : new Color(220, 190, 120);
            return;
        }

        if (_tools.RoadToolActive)
        {
            _statusLabel.Text = loc[_tools.RoadEraseMode ? "status.roadErase" : "status.road"];
            _statusLabel.TextColor = _tools.RoadEraseMode
                ? new Color(240, 190, 90)
                : new Color(210, 205, 190);
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
            _statusLabel.Text = PlantSpeciesLabel();
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

        // Při tažení je nejdůležitější číslo to, kolik kusů z toho opravdu vznikne
        // — hráč tak vidí dopředu, kde mu dojdou suroviny nebo místo.
        if (_tools.BulkPlan.Count > 0)
        {
            _statusLabel.Text = loc.Format("build.bulkCount", _tools.BulkBuildable, _tools.BulkPlan.Count);
            _statusLabel.TextColor = _tools.BulkBuildable > 0 ? Color.White : new Color(235, 120, 110);
            return;
        }

        if (_tools.GhostVisible && _tools.GhostResult != PlacementResult.Ok)
        {
            _statusLabel.Text = loc[ErrorKey(_tools.GhostResult)];
            _statusLabel.TextColor = new Color(235, 120, 110);
        }
        else
        {
            _statusLabel.Text = loc.Format("build.placing", loc[def.NameKey]) + PlacementHints(loc, def);
            _statusLabel.TextColor = Color.White;
        }
    }

    /// <summary>
    /// Zvýrazní bloky 2×2, které jdou sloučit.
    ///
    /// <para>Prochází jen budovy ve výřezu a bere z nich vždy tu levou horní
    /// (aby se každý blok nakreslil jednou). Běží výhradně v režimu slučování,
    /// takže mimo něj nestojí nic.</para>
    /// </summary>
    private void DrawMergeCandidates(SpriteBatch spriteBatch)
    {
        const int ts = TerrainRenderer.TileSize;
        var (min, max) = _camera.VisibleWorldBounds();
        var buildings = _simulation.Buildings;

        spriteBatch.Begin(samplerState: SamplerState.PointClamp, transformMatrix: _camera.Transform);
        for (int i = 0; i < buildings.Length; i++)
        {
            ref readonly var building = ref buildings[i];
            int x = building.X * ts;
            int y = building.Y * ts;
            if (x + ts < min.X || x > max.X || y + ts < min.Y || y > max.Y)
            {
                continue;
            }

            if (!_simulation.TryFindMergeGroup(building.X, building.Y, out var group)
                || group.X != building.X || group.Y != building.Y)
            {
                continue; // kreslí jen levý horní roh bloku, ať se nepřekrývá čtyřikrát
            }

            bool affordable = _simulation.CanMerge(group) == PlacementResult.Ok;
            var tint = (affordable ? new Color(150, 235, 150) : new Color(220, 200, 120)) * 0.28f;
            spriteBatch.Draw(_screens.WhitePixel, new Rectangle(x, y, ts * 2, ts * 2), tint);
        }

        spriteBatch.End();
    }

    /// <summary>
    /// Nakreslí barevný čtverec přes dlaždice mapy — náhled nástroje pod kurzorem.
    ///
    /// <para>Existuje kvůli konkrétnímu pádu: náhledy silnice a slučování volaly
    /// <c>spriteBatch.Draw</c> bez <c>Begin()</c>, protože se psaly po vzoru
    /// sousedního bloku, kde <c>Begin()</c> zůstal o pár řádků výš. Hra spadla
    /// hned, jak hráč nástroj zapnul. Metoda, která si dávku otevře i zavře sama,
    /// tuhle třídu chyb odstraňuje.</para>
    /// </summary>
    private void DrawTileOverlay(SpriteBatch spriteBatch, Texture2D texture, int tileX, int tileY, int tiles, Color tint)
    {
        const int ts = TerrainRenderer.TileSize;
        spriteBatch.Begin(samplerState: SamplerState.PointClamp, transformMatrix: _camera.Transform);
        spriteBatch.Draw(texture, new Rectangle(tileX * ts, tileY * ts, ts * tiles, ts * tiles), tint);
        spriteBatch.End();
    }

    /// <summary>
    /// Zapne nástroj pro smoke test (<c>--smoke</c>). Nástroje jinak zapíná
    /// jen kliknutí na tlačítko, které se bez okna a myši nedá simulovat.
    /// </summary>
    internal void ActivateToolForSmoke(Capture.SmokeTool tool)
    {
        _tools.Clear();
        switch (tool)
        {
            case Capture.SmokeTool.Road: _tools.ToggleRoad(); break;
            case Capture.SmokeTool.RoadErase:
                _tools.ToggleRoad();
                _tools.SetRoadErasing(true);
                break;
            case Capture.SmokeTool.Merge: _tools.ToggleMerge(); break;
            case Capture.SmokeTool.Plant: _tools.TogglePlant(); break;
        }
    }

    /// <summary>
    /// Postaví kameru pro režim focení do obchodu (<c>--capture</c>). Existuje
    /// proto, aby snímky procházely skutečným renderem hry — vyfotit jde jen to,
    /// na co se dá namířit.
    /// </summary>
    internal void FocusForCapture(Vector2 world, float zoom)
    {
        _camera.SetViewport(_screens.GraphicsDevice.Viewport.Width, _screens.GraphicsDevice.Viewport.Height);
        _camera.CenterOn(world, zoom);

        // Uvítací okna (denní odměna, souhrn offline) patří hráči, ne fotografovi —
        // na snímku by jen zakryly město.
        _pendingIntros.Clear();

        // Hromada toastů z rychle nasimulovaného města taky ne: hráč je vidí po
        // jednom, jak přicházejí, ne jako zeď přes půl obrazovky.
        while (_simulation.TryDequeueNotification(out _))
        {
        }

        _toasts.Clear();
        _objectives.MarkDirty();
        _captureMode = true;
    }

    /// <summary>
    /// Focení do obchodu: oznámení se zpracují (achievementy se zapíšou), ale
    /// toasty a oslavy se nekreslí. Zrychlená simulace jich vyrobí desítky naráz
    /// a na snímku by z nich byla zeď přes půl obrazovky.
    /// </summary>
    private bool _captureMode;

    /// <summary>
    /// Živé dopady místa pod kurzorem („+18 % za okolí", „svoz 60 %"). Bez tohohle
    /// by se hráč o obou pravidlech nedozvěděl — projeví se až v číslech za deset
    /// minut. Ukazuje se jen to, co zrovna něco dělá; mlčení znamená „na tomhle
    /// místě nic zvláštního".
    /// </summary>
    private string PlacementHints(Localization loc, BuildingDef def)
    {
        if (!_tools.GhostVisible || (def.Recipe is null && !def.TakesTimeToBuild))
        {
            return string.Empty;
        }

        var hints = new StringBuilder();

        if (def.TakesTimeToBuild)
        {
            hints.Append("  ").Append(loc.Format("tip.build.buildTime", DurationFormat.FromTicks(def.BuildTicks)));
        }

        if (def.HasAdjacencyBonus)
        {
            double bonus = _simulation.AdjacencyMultiplierAt(_tools.SelectedBuilding, _tools.GhostX, _tools.GhostY) - 1.0;
            if (bonus > 0)
            {
                hints.Append("  ").Append(loc.Format("build.adjacencyBonus", BuildingSummary.Percent(bonus)));
            }
        }

        double haul = def.Recipe is null ? 1.0 : _simulation.HaulMultiplierAt(_tools.GhostX, _tools.GhostY);
        if (haul < 0.995)
        {
            hints.Append("  ").Append(loc.Format("build.haulPenalty", BuildingSummary.Percent(haul)));
        }

        return hints.ToString();
    }

    private static string ErrorKey(PlacementResult result) => result switch
    {
        PlacementResult.Occupied => "build.error.occupied",
        PlacementResult.WrongBiome => "build.error.wrongBiome",
        PlacementResult.NotEnoughResources => "build.error.resources",
        PlacementResult.NeedsWaterAccess => "build.error.waterAccess",
        PlacementResult.SettlementTooSmall => "build.error.settlementTooSmall",
        _ => "build.title",
    };
}
