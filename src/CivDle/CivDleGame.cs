using CivDle.Capture;
using CivDle.Core.Config;
using CivDle.Core.Content;
using CivDle.Core.Save;
using CivDle.Core.Sim;
using CivDle.Rendering.Sprites;
using CivDle.Screens;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Graphics;
using Myra;

namespace CivDle;

/// <summary>
/// Kompoziční kořen hry: okno, herní smyčka MonoGame, uživatelská nastavení
/// (grafika se aplikuje na GraphicsDeviceManager) a start aplikace. Při startu
/// načte herní obsah (fail-fast) a předá řízení zásobníku obrazovek — sám
/// žádnou herní logiku nedrží.
/// </summary>
public sealed class CivDleGame : Game
{
    private static readonly Color BackgroundColor = new(24, 26, 32);

    /// <summary>Rozlišení snímků do obchodu — Steam chce 1920×1080.</summary>
    private const int CaptureWidth = 1920;
    private const int CaptureHeight = 1080;

    private readonly GraphicsDeviceManager _graphics;
    private readonly SettingsStore _settingsStore;
    private readonly ProfileStore _profileStore;
    private readonly CaptureDirector? _capture;
    private readonly string? _capsuleDirectory;
    private readonly bool _smoke;
    private SmokeRun? _smokeRun;
    private GameplayScreen? _smokeScreen;
    private Simulation? _smokeSimulation;
    private ScreenManager? _screens;

    /// <param name="captureDirectory">
    /// Není-li null, hra se místo menu spustí v režimu focení do obchodu: sama si
    /// vypěstuje město, uloží sadu snímků do téhle složky a skončí.
    /// </param>
    /// <param name="capsuleDirectory">
    /// Není-li null, hra místo menu vyrobí obrázky do obchodu (header, library,
    /// ikonu) a skončí.
    /// </param>
    /// <param name="smoke">
    /// Projet všechny nástroje herní obrazovky a skončit. Chytá pády, které se
    /// projeví až po kliknutí na nástroj — testy simulace na ně nedosáhnou.
    /// </param>
    public CivDleGame(string? captureDirectory = null, string? capsuleDirectory = null, bool smoke = false)
    {
        _smoke = smoke;
        _capture = captureDirectory is null ? null : new CaptureDirector(captureDirectory);
        _capsuleDirectory = capsuleDirectory;
        _settingsStore = new SettingsStore(GetSettingsPath());
        Settings = _settingsStore.Load();
        _profileStore = new ProfileStore(GetProfilePath());
        Profile = _profileStore.Load();

        _graphics = new GraphicsDeviceManager(this);
        ApplyGraphicsToManager(Settings);
        Window.Title = "CivDle";
        Window.AllowUserResizing = true;
        IsMouseVisible = true;
    }

    /// <summary>Sdílený SpriteBatch pro všechny obrazovky.</summary>
    public SpriteBatch SpriteBatch { get; private set; } = null!;

    /// <summary>Sdílená 1×1 bílá textura na kreslení obdélníků (budovy, overlay…).</summary>
    public Texture2D WhitePixel { get; private set; } = null!;

    /// <summary>Procedurální sprity a ikony (suroviny, budovy, objekty, agenti).</summary>
    public SpriteLibrary Sprites { get; private set; } = null!;

    /// <summary>Aktuální uživatelská nastavení.</summary>
    public GameSettings Settings { get; private set; }

    /// <summary>Účet-wide profil hráče (odemčené achievementy).</summary>
    public PlayerProfile Profile { get; }

    /// <summary>Uloží profil (odemčené achievementy) na disk.</summary>
    public void SaveProfile() => _profileStore.Save(Profile);

    /// <summary>Uloží nová nastavení a hned aplikuje grafiku (jazyk řeší Localization).</summary>
    public void ApplySettings(GameSettings settings)
    {
        Settings = settings;
        _settingsStore.Save(settings);
        ApplyGraphicsToManager(settings);
        _graphics.ApplyChanges();
    }

    protected override void LoadContent()
    {
        SpriteBatch = new SpriteBatch(GraphicsDevice);
        WhitePixel = new Texture2D(GraphicsDevice, 1, 1);
        WhitePixel.SetData(new[] { Color.White });
        Sprites = new SpriteLibrary(GraphicsDevice);
        MyraEnvironment.Game = this;

        // Tooltipy Myry se kreslí u kurzoru — hráč nemusí očima skákat na spodní
        // lištu, aby se dozvěděl, co tlačítko dělá. Krátká prodleva, ať se bublina
        // neplete při rychlém přejetí lišty.
        MyraEnvironment.TooltipDelayInMs = 260;
        MyraEnvironment.TooltipOffset = new Point(16, 18);

        // Data leží vedle binárky — funguje pro `dotnet run` i pro publish jedním exe.
        var content = new ContentLoader().LoadFrom(Path.Combine(AppContext.BaseDirectory, "data"));
        // Obchod je anglicky: snímky do Steamu musí být v jazyce, kterému rozumí
        // každý, kdo si stránku otevře — ne v tom, který má vývojář nastavený.
        bool storeMode = _capture is not null || _capsuleDirectory is not null || _smoke;
        var localization = new Localization(content.Languages, storeMode ? "en" : Settings.Language);
        var saves = new SaveStore(Path.Combine(GetProfileDirectory(), "saves", "save.civdle"));

        var screens = new ScreenManager(this, content, localization, saves);
        if (_capsuleDirectory is not null)
        {
            var scene = CityFixture.Grow(content, seed: 20260728, minutes: 14);
            CityFixture.TickUntilPostcardMoment(scene, content, from: 0.40, to: 0.58);
            new CapsuleDirector(_capsuleDirectory).RenderAll(screens, scene);
            Exit();
            return; // _screens zůstává null — Update ani Draw už nemají co dělat
        }

        _screens = screens;
        if (_smoke)
        {
            _smokeSimulation = SmokeRun.BuildScene(screens);
            _smokeScreen = new GameplayScreen(screens, _smokeSimulation, new WorldInfo(20260728, "medium", "continents"));
            _smokeScreen.FocusForCapture(
                new Vector2(
                    (_smokeSimulation.CityCenterX + 0.5f) * Rendering.TerrainRenderer.TileSize,
                    (_smokeSimulation.CityCenterY + 0.5f) * Rendering.TerrainRenderer.TileSize),
                zoom: 2.5f);
            _screens.ReplaceAll(_smokeScreen);
            _smokeRun = new SmokeRun();
            return;
        }

        if (_capture is not null)
        {
            _screens.ReplaceAll(_capture.PrepareNextShot(_screens));
            return;
        }

        _screens.ReplaceAll(new MainMenuScreen(_screens));
    }

    protected override void UnloadContent()
    {
        WhitePixel.Dispose();
        Sprites.Dispose();
        base.UnloadContent();
    }

    protected override void Update(GameTime gameTime)
    {
        if (_smokeRun is not null)
        {
            var run = _smokeRun;
            _smokeRun = null; // jen jednou; případná výjimka probublá do crash.log
            run.Run(_screens!, _smokeScreen!, _smokeSimulation!, gameTime);
            Exit();
            return;
        }

        if (_screens is null)
        {
            return; // režim kapslí: hotovo v LoadContent, hra se zavírá
        }

        _screens.Update(gameTime);
        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(BackgroundColor);
        if (_screens is null)
        {
            return;
        }

        _screens.Draw(gameTime);
        base.Draw(gameTime);

        if (_capture is null)
        {
            return;
        }

        // Snímek se čte až po vykreslení celého rámce — jinak by v něm chyběl HUD.
        if (_capture.AfterDraw(GraphicsDevice))
        {
            if (_capture.Finished)
            {
                Exit();
                return;
            }

            _screens.ReplaceAll(_capture.PrepareNextShot(_screens));
        }
    }

    private void ApplyGraphicsToManager(GameSettings settings)
    {
        // Focení do obchodu má pevné rozlišení: Steam chce 1920×1080 a snímky
        // nesmí záviset na tom, jak měl kdo naposled nastavené okno.
        bool capturing = _capture is not null || _capsuleDirectory is not null || _smoke;
        _graphics.PreferredBackBufferWidth = capturing ? CaptureWidth : settings.ResolutionWidth;
        _graphics.PreferredBackBufferHeight = capturing ? CaptureHeight : settings.ResolutionHeight;
        _graphics.SynchronizeWithVerticalRetrace = settings.VSync;
        // Borderless = fullscreen bez přepnutí režimu monitoru (rychlé Alt-Tab).
        _graphics.HardwareModeSwitch = settings.WindowMode == WindowMode.Fullscreen;
        _graphics.IsFullScreen = !capturing && settings.WindowMode != WindowMode.Windowed;
        // Hlasitost je globální přes MonoGame — jeden zdroj pravdy pro všechny zvuky.
        SoundEffect.MasterVolume = settings.MasterVolume;
    }

    /// <summary>Nastavení patří do profilu uživatele — vedle exe nemusí být právo zápisu.</summary>
    private static string GetSettingsPath() => Path.Combine(GetProfileDirectory(), "settings.json");

    /// <summary>Profil (achievementy) patří do profilu uživatele, mimo per-hra save.</summary>
    private static string GetProfilePath() => Path.Combine(GetProfileDirectory(), "profile.json");

    /// <summary>Složka profilu hry (nastavení, savy).</summary>
    private static string GetProfileDirectory() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "CivDle");
}
