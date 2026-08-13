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
    private int _uiScaleStep;
    private bool _reduceMotion;
    private bool _colorCues;
    private int _detailIndex;
    private int _captureIndex;
    private bool _captureStrip;
    private bool _shadows;

    /// <summary>Nabízená zvětšení UI (index = krok v přepínači).</summary>
    private static readonly float[] UiScales = { 0.8f, 0.9f, 1.0f, 1.15f, 1.3f, 1.45f, 1.6f };

    /// <summary>Stupně detailu v pořadí, v jakém je přepínač nabízí.</summary>
    private static readonly DetailQuality[] DetailSteps =
    {
        DetailQuality.Performance,
        DetailQuality.Balanced,
        DetailQuality.Detailed,
        DetailQuality.Maximum,
    };

    /// <summary>Stupně rozlišení focení v pořadí, v jakém je přepínač nabízí.</summary>
    private static readonly CaptureResolution[] CaptureSteps =
    {
        CaptureResolution.Hd1080,
        CaptureResolution.Qhd1440,
        CaptureResolution.Uhd4K,
    };

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
        _uiScaleStep = NearestUiScaleStep(settings.SafeUiScale);
        _reduceMotion = settings.ReduceMotion;
        _colorCues = settings.ColorCues;
        _detailIndex = Math.Max(0, Array.IndexOf(DetailSteps, settings.Detail));
        _captureIndex = Math.Max(0, Array.IndexOf(CaptureSteps, settings.CaptureResolution));
        _captureStrip = settings.CaptureStrip;
        _shadows = settings.Shadows;

        BuildUi();
        _screens.Loc.LanguageChanged += BuildUi;
        _screens.UiSettingsChanged += BuildUi;
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

        _screens.RenderDesktop(this, _desktop);
    }

    public void Dispose()
    {
        _screens.Loc.LanguageChanged -= BuildUi;
        _screens.UiSettingsChanged -= BuildUi;
    }

    private void BuildUi()
    {
        var loc = _screens.Loc;

        var language = new CycleSelector(
            loc.Languages.Count, _languageIndex, i => LanguageLabel(loc.Languages[i]));
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

        var uiScale = new CycleSelector(
            UiScales.Length, _uiScaleStep, i => $"{UiScales[i] * 100f:0} %");
        uiScale.SelectionChanged += i => _uiScaleStep = i;

        var reduceMotion = new CycleSelector(2, _reduceMotion ? 0 : 1, i => loc[i == 0 ? "common.on" : "common.off"]);
        reduceMotion.SelectionChanged += i => _reduceMotion = i == 0;

        var colorCues = new CycleSelector(2, _colorCues ? 0 : 1, i => loc[i == 0 ? "common.on" : "common.off"]);
        colorCues.SelectionChanged += i => _colorCues = i == 0;

        // Detail při oddálení: popisek pod přepínačem se mění hned, ať hráč
        // vidí, co si vybral, dřív než dá Použít a bude to zkoušet naslepo.
        var detailHint = new Label
        {
            Text = loc[DetailHintKey(_detailIndex)],
            TextColor = Color.LightGray,
            Wrap = true,
            Width = 420,
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        var detail = new CycleSelector(
            DetailSteps.Length, _detailIndex, i => loc[DetailNameKey(i)]);
        detail.SelectionChanged += i =>
        {
            _detailIndex = i;
            detailHint.Text = loc[DetailHintKey(i)];
        };

        var shadows = new CycleSelector(2, _shadows ? 0 : 1, i => loc[i == 0 ? "common.on" : "common.off"]);
        shadows.SelectionChanged += i => _shadows = i == 0;

        // Focení a natáčení: rozlišení nezávisí na okně, proužek se dá vypnout.
        // Obojí je nastavení „jak vypadá to, co z hry odejde ven".
        var captureHint = new Label
        {
            Text = loc["settings.capture.hint"],
            TextColor = UiPalette.TextDim,
            Wrap = true,
            Width = 420,
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        var capture = new CycleSelector(
            CaptureSteps.Length, _captureIndex, i => loc[CaptureNameKey(i)]);
        capture.SelectionChanged += i => _captureIndex = i;

        var captureStrip = new CycleSelector(2, _captureStrip ? 0 : 1, i => loc[i == 0 ? "common.on" : "common.off"]);
        captureStrip.SelectionChanged += i => _captureStrip = i == 0;

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
        layout.Widgets.Add(UiFactory.Row(loc["settings.detail"], detail.Widget));
        layout.Widgets.Add(detailHint);
        layout.Widgets.Add(new Label { Text = " " });
        layout.Widgets.Add(new Label
        {
            Text = loc["settings.accessibility"],
            TextColor = UiFactory.Accent,
            HorizontalAlignment = HorizontalAlignment.Center,
        });
        layout.Widgets.Add(UiFactory.Row(loc["settings.uiScale"], uiScale.Widget));
        layout.Widgets.Add(UiFactory.Row(loc["settings.reduceMotion"], reduceMotion.Widget));
        layout.Widgets.Add(UiFactory.Row(loc["settings.colorCues"], colorCues.Widget));
        layout.Widgets.Add(UiFactory.Row(loc["settings.shadows"], shadows.Widget));
        layout.Widgets.Add(UiFactory.Row(loc["settings.capture"], capture.Widget));
        layout.Widgets.Add(captureHint);
        layout.Widgets.Add(UiFactory.Row(loc["settings.captureStrip"], captureStrip.Widget));
        layout.Widgets.Add(new Label { Text = " " });
        layout.Widgets.Add(UiFactory.MenuButton(loc["settings.apply"], Apply));
        layout.Widgets.Add(UiFactory.MenuButton(loc["settings.back"], _screens.Pop));

        _desktop = _screens.NewDesktop(UiFactory.MenuBackdrop(layout));
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
            UiScale = UiScales[_uiScaleStep],
            ReduceMotion = _reduceMotion,
            ColorCues = _colorCues,
            Detail = DetailSteps[_detailIndex],
            CaptureResolution = CaptureSteps[_captureIndex],
            CaptureStrip = _captureStrip,
            Shadows = _shadows,
        };

        _screens.ApplySettings(settings);
        // Změna jazyka rozešle event — tahle i spodní obrazovky se přestaví.
        _screens.Loc.SetLanguage(settings.Language);
    }

    /// <summary>Název stupně detailu v nabídce.</summary>
    /// <summary>Klíč překladu pro stupeň rozlišení focení.</summary>
    private static string CaptureNameKey(int step) =>
        $"settings.capture.{CaptureSteps[step].ToString().ToLowerInvariant()}";

    private static string DetailNameKey(int step) => $"settings.detail.{DetailSteps[step].ToString().ToLowerInvariant()}";

    /// <summary>Jednořádkové vysvětlení, co stupeň udělá.</summary>
    private static string DetailHintKey(int step) => DetailNameKey(step) + ".hint";

    /// <summary>Nejbližší nabízený krok zvětšení k uložené hodnotě (soubor mohl někdo ručně upravit).</summary>
    /// <summary>
    /// Jméno jazyka v nabídce; u nedodělaných překladů i procento pokrytí.
    ///
    /// <para>Bez toho by hráč přepnul na částečný jazyk a divil se, proč je
    /// půlka hry pořád v základním jazyce — takhle to ví předem.</para>
    /// </summary>
    private static string LanguageLabel(CivDle.Core.Content.LanguageDef language) =>
        language.IsComplete ? language.NativeName : $"{language.NativeName} ({language.Coverage * 100:0} %)";

    private static int NearestUiScaleStep(float scale)
    {
        int best = 0;
        for (int i = 1; i < UiScales.Length; i++)
        {
            if (MathF.Abs(UiScales[i] - scale) < MathF.Abs(UiScales[best] - scale))
            {
                best = i;
            }
        }

        return best;
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
