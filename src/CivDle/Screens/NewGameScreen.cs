using CivDle.Core.Sim;
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
    private readonly Desktop _desktop;
    private readonly TextBox _seedBox;
    private readonly Label _sizeLabel;
    private readonly Label _presetLabel;
    private int _sizeIndex;
    private int _presetIndex;

    public NewGameScreen(ScreenManager screens)
    {
        _screens = screens;
        var worldGen = screens.Content.WorldGen;
        _sizeIndex = worldGen.DefaultSizeIndex;
        _presetIndex = worldGen.DefaultPresetIndex;

        _seedBox = new TextBox
        {
            Text = SeedUtil.NewRandom().ToString(),
            Width = 220,
            VerticalAlignment = VerticalAlignment.Center,
        };
        _sizeLabel = new Label
        {
            Width = 220,
            TextAlign = FontStashSharp.RichText.TextHorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        _presetLabel = new Label
        {
            Width = 220,
            TextAlign = FontStashSharp.RichText.TextHorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        RefreshLabels();

        var layout = new VerticalStackPanel
        {
            Spacing = 14,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };

        layout.Widgets.Add(new Label
        {
            Text = "Nová hra",
            HorizontalAlignment = HorizontalAlignment.Center,
        });
        layout.Widgets.Add(new Label { Text = " " });
        layout.Widgets.Add(Row("Seed:",
            _seedBox,
            UiFactory.SmallButton("Náhodný", () => _seedBox.Text = SeedUtil.NewRandom().ToString())));
        layout.Widgets.Add(Row("Velikost světa:",
            UiFactory.SmallButton("<", () => CycleSize(-1)),
            _sizeLabel,
            UiFactory.SmallButton(">", () => CycleSize(+1))));
        layout.Widgets.Add(Row("Typ světa:",
            UiFactory.SmallButton("<", () => CyclePreset(-1)),
            _presetLabel,
            UiFactory.SmallButton(">", () => CyclePreset(+1))));
        layout.Widgets.Add(new Label { Text = " " });
        layout.Widgets.Add(UiFactory.MenuButton("Vytvořit svět", StartGame));
        layout.Widgets.Add(UiFactory.MenuButton("Zpět", _screens.Pop));

        _desktop = new Desktop { Root = layout };
    }

    public bool IsOverlay => false;

    public void Update(GameTime gameTime)
    {
    }

    public void Draw(GameTime gameTime) => _desktop.Render();

    public void Dispose()
    {
    }

    private static HorizontalStackPanel Row(string labelText, params Widget[] widgets)
    {
        var row = new HorizontalStackPanel
        {
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        row.Widgets.Add(new Label
        {
            Text = labelText,
            Width = 150,
            VerticalAlignment = VerticalAlignment.Center,
        });
        foreach (var widget in widgets)
        {
            row.Widgets.Add(widget);
        }

        return row;
    }

    private void CycleSize(int delta)
    {
        int count = _screens.Content.WorldGen.Sizes.Count;
        _sizeIndex = (_sizeIndex + delta + count) % count;
        RefreshLabels();
    }

    private void CyclePreset(int delta)
    {
        int count = _screens.Content.WorldGen.Presets.Count;
        _presetIndex = (_presetIndex + delta + count) % count;
        RefreshLabels();
    }

    private void RefreshLabels()
    {
        var size = _screens.Content.WorldGen.Sizes[_sizeIndex];
        var preset = _screens.Content.WorldGen.Presets[_presetIndex];
        _sizeLabel.Text = $"{size.Name} ({size.Width}×{size.Height})";
        _presetLabel.Text = preset.Name;
    }

    private void StartGame()
    {
        var content = _screens.Content;
        var size = content.WorldGen.Sizes[_sizeIndex];
        var preset = content.WorldGen.Presets[_presetIndex];
        long seed = SeedUtil.Parse(_seedBox.Text);

        // Generování je pro velikosti z dat otázka desítek ms — běží synchronně.
        var map = new MapGenerator().Generate(content, new WorldGenRequest(seed, size.Width, size.Height, preset));
        var simulation = new Simulation(map);
        var info = new WorldInfo(seed, size.Name, preset.Name);

        _screens.ReplaceAll(new GameplayScreen(_screens, simulation, info));
    }
}
