using CivDle.Core.Sim;
using CivDle.Core.World;
using CivDle.Core.WorldGen;
using Microsoft.Xna.Framework;
using Myra.Graphics2D.UI;

namespace CivDle.Screens;

/// <summary>
/// Nastavení nové hry: seed (číslo, text nebo prázdné = náhodný), velikost světa
/// a terénní preset. Nabídky se plní z data-driven katalogu (worldgen.json) —
/// obrazovka žádné hodnoty nezná natvrdo.
/// </summary>
public sealed class NewGameScreen : IScreen
{
    private readonly ScreenManager _screens;
    private Desktop _desktop = null!;
    private TextBox _seedBox = null!;
    private CycleSelector _preset = null!;
    private string _seedText;
    private int _presetIndex;

    public NewGameScreen(ScreenManager screens)
    {
        _screens = screens;
        var worldGen = screens.Content.WorldGen;
        _presetIndex = worldGen.DefaultPresetIndex;
        _seedText = SeedUtil.NewRandom().ToString();

        BuildUi();
        _screens.Loc.LanguageChanged += BuildUi;
        _screens.UiSettingsChanged += BuildUi;
    }

    public bool IsOverlay => false;

    public void Update(GameTime gameTime) => _screens.MenuBackground.Update(gameTime);

    public void Draw(GameTime gameTime)
    {
        _screens.MenuBackground.Draw(_screens.SpriteBatch);
        _desktop.Render();
    }

    public void Dispose()
    {
        _screens.Loc.LanguageChanged -= BuildUi;
        _screens.UiSettingsChanged -= BuildUi;
    }

    private void BuildUi()
    {
        var loc = _screens.Loc;
        var worldGen = _screens.Content.WorldGen;

        _seedBox = new TextBox
        {
            Text = _seedText,
            Width = 220,
            VerticalAlignment = VerticalAlignment.Center,
        };
        _seedBox.TextChanged += (_, _) => _seedText = _seedBox.Text ?? string.Empty;

        _preset = new CycleSelector(
            worldGen.Presets.Count,
            _presetIndex,
            i => loc[worldGen.Presets[i].NameKey]);
        _preset.SelectionChanged += i => _presetIndex = i;

        var layout = new VerticalStackPanel
        {
            Spacing = 14,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };

        layout.Widgets.Add(new Label
        {
            Text = loc["newgame.title"],
            HorizontalAlignment = HorizontalAlignment.Center,
        });
        layout.Widgets.Add(new Label { Text = " " });
        layout.Widgets.Add(UiFactory.Row(loc["newgame.seed"],
            _seedBox,
            UiFactory.SmallButton(loc["newgame.seedRandom"], () => _seedBox.Text = SeedUtil.NewRandom().ToString())));
        layout.Widgets.Add(UiFactory.Row(loc["newgame.worldType"], _preset.Widget));
        layout.Widgets.Add(new Label { Text = " " });
        layout.Widgets.Add(UiFactory.MenuButton(loc["newgame.create"], StartGame));
        layout.Widgets.Add(UiFactory.MenuButton(loc["newgame.back"], _screens.Pop));

        _desktop = _screens.NewDesktop(UiFactory.MenuBackdrop(layout));
    }

    private void StartGame()
    {
        var content = _screens.Content;
        var preset = content.WorldGen.Presets[_presetIndex];
        long seed = SeedUtil.Parse(_seedText);

        // Nekonečný terén: žádné generování mapy dopředu — počítá se on-demand,
        // takže „velikost světa" už nemá smysl. Do savu ukládáme jen ID pro
        // zpětnou kompatibilitu (výchozí velikost z katalogu).
        var terrain = new ProceduralTerrain(content.Biomes, preset, seed);
        var simulation = new Simulation(content, terrain, seed);
        string sizeId = content.WorldGen.Sizes[content.WorldGen.DefaultSizeIndex].Id;
        var info = new WorldInfo(seed, sizeId, preset.Id);

        _screens.ReplaceAll(new GameplayScreen(_screens, simulation, info));
    }
}
