using CivDle.Core.Content;
using CivDle.Core.Save;
using CivDle.Core.Sim;
using CivDle.Core.WorldGen;
using CivDle.Input;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Myra.Graphics2D.UI;

namespace CivDle.Screens;

/// <summary>
/// Nová hra+ po otevření brány (endgame.md, C3): nový svět s pravidlem výzvy
/// a vším, co hráč nasbíral v profilu — odměny výzev, rekordy, achievementy.
///
/// <para><b>Město první kapitoly se neztratí.</b> Hlavní slot savu je jeden;
/// nový svět ho přepíše. Proto se rozehraná hra napřed uloží a zkopíruje do
/// archivu, odkud ji jde z hlavního menu kdykoli vrátit. Bez toho by „nová
/// hra+" znamenala „smaž si město, na kterém jsi strávil stovky hodin".</para>
///
/// <para>Vrstva: UI. Svět skládá <see cref="ScenarioWorld.CreateFree"/> —
/// týž, který ho po načtení savu složí znovu.</para>
/// </summary>
public sealed class NewGamePlusScreen : IScreen
{
    private const int PanelWidth = 640;

    private readonly ScreenManager _screens;
    private readonly Simulation _current;
    private readonly WorldInfo _currentInfo;
    private readonly InputManager _input = new();
    private readonly HashSet<ScenarioRule> _chosen = new();
    private Desktop _desktop = null!;
    private string? _status;

    public NewGamePlusScreen(ScreenManager screens, Simulation current, WorldInfo currentInfo)
    {
        _screens = screens;
        _current = current;
        _currentInfo = currentInfo;
        BuildUi();
        _screens.Loc.LanguageChanged += BuildUi;
    }

    public bool IsOverlay => false;

    public void OnActivated() => _input.Resync();

    public void Update(GameTime gameTime)
    {
        _input.Update();
        if (_input.WasPressed(Keys.Escape))
        {
            _screens.Pop();
        }
    }

    public void Draw(GameTime gameTime) => _desktop.Render();

    public void Dispose() => _screens.Loc.LanguageChanged -= BuildUi;

    private void BuildUi()
    {
        var loc = _screens.Loc;
        var layout = new VerticalStackPanel { Spacing = 10, Width = PanelWidth };
        layout.Widgets.Add(new Label
        {
            Text = loc["ngplus.title"],
            TextColor = UiPalette.TextBright,
            HorizontalAlignment = HorizontalAlignment.Center,
        });
        layout.Widgets.Add(new Label { Text = loc["ngplus.desc"], TextColor = UiPalette.Text, Wrap = true, Width = PanelWidth - 20 });
        layout.Widgets.Add(new Label { Text = loc["ngplus.rules"], TextColor = UiPalette.Accent });

        foreach (var rule in WorldRules.NewGamePlusChoices)
        {
            bool on = _chosen.Contains(rule);
            string text = (on ? "✓ " : "   ") + loc[ScenariosScreen.RuleKey(rule)];
            layout.Widgets.Add(UiFactory.SmallButton(text, () =>
            {
                if (!_chosen.Remove(rule))
                {
                    _chosen.Add(rule);
                }

                BuildUi();
            }));
        }

        layout.Widgets.Add(UiFactory.MenuButton(loc["ngplus.start"], Start));
        layout.Widgets.Add(UiFactory.MenuButton(loc["newgame.back"], _screens.Pop));
        if (_status is not null)
        {
            layout.Widgets.Add(new Label { Text = _status, TextColor = UiPalette.Bad, Wrap = true, Width = PanelWidth - 20 });
        }

        _desktop = _screens.NewDesktop(UiFactory.MenuBackdrop(layout));
    }

    /// <summary>
    /// Uloží a archivuje rozehrané město, pak založí nový svět. Když se
    /// archiv nepovede, nový svět se nezaloží — raději nic než přijít o město.
    /// </summary>
    private void Start()
    {
        var content = _screens.Content;
        var now = DateTime.UtcNow;
        _screens.Saves.TrySave(_current, new SaveMetadata(_currentInfo.Seed, _currentInfo.SizeId, _currentInfo.PresetId, now));
        if (_screens.Saves.TryArchive(CityName(), now) is null)
        {
            _status = _screens.Loc["ngplus.archiveFailed"];
            BuildUi();
            return;
        }

        int biome = _chosen.Contains(ScenarioRule.SingleBiome) ? content.Biomes.IndexOf("desert") : -1;
        var rules = _chosen.Count == 0
            ? WorldRules.None
            : new WorldRules(WorldRules.NewGamePlusChoices.Where(_chosen.Contains).ToList(), biome);

        long seed = SeedUtil.NewRandom();
        int presetIndex = content.WorldGen.DefaultPresetIndex;
        var simulation = ScenarioWorld.CreateFree(content, seed, presetIndex, rules);
        string sizeId = content.WorldGen.Sizes[content.WorldGen.DefaultSizeIndex].Id;
        var info = new WorldInfo(seed, sizeId, content.WorldGen.Presets[presetIndex].Id);

        _screens.ReplaceAll(new LoadingScreen(
            _screens, "loading.newWorld", _ => new GameplayScreen(_screens, simulation, info)));
    }

    /// <summary>Jméno největšího sídla — pod ním se město v archivu pozná.</summary>
    private string CityName()
    {
        var names = _screens.Content.SettlementNames;
        var biggest = _current.Settlements.OrderByDescending(s => s.BuildingCount).FirstOrDefault();
        return names.Count > 0 && _current.Settlements.Count > 0 ? names[biggest.NameIndex % names.Count] : "mesto";
    }
}
