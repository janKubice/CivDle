using CivDle.Core.Config;
using CivDle.Core.Content;
using CivDle.Core.Save;
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

    private readonly GraphicsDeviceManager _graphics;
    private readonly SettingsStore _settingsStore;
    private ScreenManager? _screens;

    public CivDleGame()
    {
        _settingsStore = new SettingsStore(GetSettingsPath());
        Settings = _settingsStore.Load();

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

        // Data leží vedle binárky — funguje pro `dotnet run` i pro publish jedním exe.
        var content = new ContentLoader().LoadFrom(Path.Combine(AppContext.BaseDirectory, "data"));
        var localization = new Localization(content.Languages, Settings.Language);
        var saves = new SaveStore(Path.Combine(GetProfileDirectory(), "saves", "save.civdle"));

        _screens = new ScreenManager(this, content, localization, saves);
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
        _screens!.Update(gameTime);
        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(BackgroundColor);
        _screens!.Draw(gameTime);
        base.Draw(gameTime);
    }

    private void ApplyGraphicsToManager(GameSettings settings)
    {
        _graphics.PreferredBackBufferWidth = settings.ResolutionWidth;
        _graphics.PreferredBackBufferHeight = settings.ResolutionHeight;
        _graphics.SynchronizeWithVerticalRetrace = settings.VSync;
        // Borderless = fullscreen bez přepnutí režimu monitoru (rychlé Alt-Tab).
        _graphics.HardwareModeSwitch = settings.WindowMode == WindowMode.Fullscreen;
        _graphics.IsFullScreen = settings.WindowMode != WindowMode.Windowed;
        // Hlasitost je globální přes MonoGame — jeden zdroj pravdy pro všechny zvuky.
        SoundEffect.MasterVolume = settings.MasterVolume;
    }

    /// <summary>Nastavení patří do profilu uživatele — vedle exe nemusí být právo zápisu.</summary>
    private static string GetSettingsPath() => Path.Combine(GetProfileDirectory(), "settings.json");

    /// <summary>Složka profilu hry (nastavení, savy).</summary>
    private static string GetProfileDirectory() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "CivDle");
}
