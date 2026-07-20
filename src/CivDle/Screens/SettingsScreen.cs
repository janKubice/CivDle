using CivDle.Core.Config;
using Microsoft.Xna.Framework;
using Myra.Graphics2D.UI;

namespace CivDle.Screens;

/// <summary>
/// Nastavení hry: jazyk (z data/lang) a grafika (rozlišení, režim okna, VSync).
/// Změny se aplikují až tlačítkem Použít — uloží se na disk, grafika se přepne
/// hned a jazyk rozešle event, na který se obrazovky přestaví.
/// </summary>
public sealed class SettingsScreen : IScreen
{
    private static readonly (int Width, int Height)[] BaseResolutions =
    {
        (1280, 720),
        (1600, 900),
        (1920, 1080),
        (2560, 1440),
        (3840, 2160),
    };

    private readonly ScreenManager _screens;
    private readonly bool _showBackground;
    private readonly List<(int Width, int Height)> _resolutions;
    private Desktop _desktop = null!;
    private int _languageIndex;
    private int _resolutionIndex;
    private int _windowModeIndex;
    private bool _vsync;
    private int _volumeStep;

    /// <param name="showBackground">
    /// True v menu (kreslí živé město na pozadí); false z pauzy ve hře, kde by
    /// pod hrou neměla běžet druhá simulace.
    /// </param>
    public SettingsScreen(ScreenManager screens, bool showBackground = true)
    {
        _screens = screens;
        _showBackground = showBackground;
        var settings = screens.Settings;

        _resolutions = BaseResolutions.ToList();
        if (!_resolutions.Contains((settings.ResolutionWidth, settings.ResolutionHeight)))
        {
            // Uživatelovo (třeba ručně editované) rozlišení v nabídce nesmí zmizet.
            _resolutions.Insert(0, (settings.ResolutionWidth, settings.ResolutionHeight));
        }

        _languageIndex = IndexOfLanguage(settings.Language);
        _resolutionIndex = _resolutions.IndexOf((settings.ResolutionWidth, settings.ResolutionHeight));
        _windowModeIndex = (int)settings.WindowMode;
        _vsync = settings.VSync;
        _volumeStep = Math.Clamp((int)MathF.Round(settings.MasterVolume * 10f), 0, 10);

        BuildUi();
        _screens.Loc.LanguageChanged += BuildUi;
    }

    public bool IsOverlay => false;

    public void Update(GameTime gameTime)
    {
        if (_showBackground)
        {
            _screens.MenuBackground.Update(gameTime);
        }
    }

    public void Draw(GameTime gameTime)
    {
        if (_showBackground)
        {
            _screens.MenuBackground.Draw(_screens.SpriteBatch);
        }

        _desktop.Render();
    }

    public void Dispose()
    {
        _screens.Loc.LanguageChanged -= BuildUi;
    }

    private void BuildUi()
    {
        var loc = _screens.Loc;

        var language = new CycleSelector(
            loc.Languages.Count, _languageIndex, i => loc.Languages[i].NativeName);
        language.SelectionChanged += i => _languageIndex = i;

        var resolution = new CycleSelector(
            _resolutions.Count, _resolutionIndex,
            i => $"{_resolutions[i].Width} × {_resolutions[i].Height}");
        resolution.SelectionChanged += i => _resolutionIndex = i;

        var windowModeKeys = new[]
        {
            "settings.windowMode.windowed",
            "settings.windowMode.borderless",
            "settings.windowMode.fullscreen",
        };
        var windowMode = new CycleSelector(
            windowModeKeys.Length, _windowModeIndex, i => loc[windowModeKeys[i]]);
        windowMode.SelectionChanged += i => _windowModeIndex = i;

        var vsync = new CycleSelector(2, _vsync ? 0 : 1, i => loc[i == 0 ? "common.on" : "common.off"]);
        vsync.SelectionChanged += i => _vsync = i == 0;

        // Hlasitost v krocích po 10 % (0–100 %).
        var volume = new CycleSelector(11, _volumeStep, i => i == 0 ? loc["common.off"] : $"{i * 10} %");
        volume.SelectionChanged += i => _volumeStep = i;

        var layout = new VerticalStackPanel
        {
            Spacing = 14,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };

        layout.Widgets.Add(new Label
        {
            Text = loc["settings.title"],
            HorizontalAlignment = HorizontalAlignment.Center,
        });
        layout.Widgets.Add(new Label { Text = " " });
        layout.Widgets.Add(UiFactory.Row(loc["settings.language"], language.Widget));
        layout.Widgets.Add(UiFactory.Row(loc["settings.resolution"], resolution.Widget));
        layout.Widgets.Add(UiFactory.Row(loc["settings.windowMode"], windowMode.Widget));
        layout.Widgets.Add(UiFactory.Row(loc["settings.vsync"], vsync.Widget));
        layout.Widgets.Add(UiFactory.Row(loc["settings.sound"], volume.Widget));
        layout.Widgets.Add(new Label { Text = " " });
        layout.Widgets.Add(UiFactory.MenuButton(loc["settings.apply"], Apply));
        layout.Widgets.Add(UiFactory.MenuButton(loc["settings.back"], _screens.Pop));

        _desktop = new Desktop { Root = layout };
    }

    private void Apply()
    {
        var resolution = _resolutions[_resolutionIndex];
        var settings = new GameSettings
        {
            Language = _screens.Loc.Languages[_languageIndex].Id,
            ResolutionWidth = resolution.Width,
            ResolutionHeight = resolution.Height,
            WindowMode = (WindowMode)_windowModeIndex,
            VSync = _vsync,
            MasterVolume = _volumeStep / 10f,
        };

        _screens.ApplySettings(settings);
        // Změna jazyka rozešle event — tahle i spodní obrazovky se přestaví.
        _screens.Loc.SetLanguage(settings.Language);
    }

    private int IndexOfLanguage(string id)
    {
        var languages = _screens.Loc.Languages;
        for (int i = 0; i < languages.Count; i++)
        {
            if (languages[i].Id == id)
            {
                return i;
            }
        }

        return 0;
    }
}
