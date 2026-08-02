using CivDle.Core.Save;
using CivDle.Core.World;
using CivDle.Input;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Myra.Graphics2D;
using Myra.Graphics2D.UI;

namespace CivDle.Screens;

/// <summary>
/// Moje časosběry: sbírka uložených běhů, přehratelná z hlavního menu.
///
/// <para>Proč to ve hře je: časosběr se ukládá při Vzestupu automaticky, ale
/// suvenýr, ke kterému se nedá vrátit, není suvenýr. Tohle je police, na
/// které minulá města stojí — i po smazání savu, i po deseti Vzestupech.</para>
///
/// <para>Terén se pro přehrávku rekonstruuje ze seedu a presetu uložených
/// v souboru, stejně jako při načtení savu — soubor sám nese jen barevné
/// buňky zástavby.</para>
/// </summary>
public sealed class TimelapseListScreen : IScreen
{
    private readonly ScreenManager _screens;
    private readonly InputManager _input = new();
    private Desktop _desktop = null!;

    /// <summary>Načtené časosběry (cesta + obsah), nejnovější první.</summary>
    private readonly List<(string Path, SavedTimelapse Timelapse)> _items = new();

    public TimelapseListScreen(ScreenManager screens)
    {
        _screens = screens;

        // Načíst jednou při otevření: souborů jsou jednotky až desítky a jsou
        // malé; poškozené TryLoad tiše vynechá, ať jeden vadný nezboří seznam.
        foreach (string path in screens.Saves.Timelapses.ListFiles())
        {
            if (screens.Saves.Timelapses.TryLoad(path) is { } timelapse)
            {
                _items.Add((path, timelapse));
            }
        }

        BuildUi();
        _screens.Loc.LanguageChanged += BuildUi;
        _screens.UiSettingsChanged += BuildUi;
    }

    public bool IsOverlay => true;

    public void OnActivated() => _input.Resync();

    public void Update(GameTime gameTime)
    {
        _input.Update();
        if (_input.WasPressed(Keys.Escape))
        {
            _screens.Pop();
        }
    }

    public void Draw(GameTime gameTime)
    {
        var viewport = _screens.GraphicsDevice.Viewport;
        var spriteBatch = _screens.SpriteBatch;
        spriteBatch.Begin();
        spriteBatch.Draw(_screens.WhitePixel, new Rectangle(0, 0, viewport.Width, viewport.Height), Color.Black * 0.55f);
        spriteBatch.End();

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

        var layout = new VerticalStackPanel
        {
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        layout.Widgets.Add(new Label
        {
            Text = loc["timelapse.collection"],
            HorizontalAlignment = HorizontalAlignment.Center,
            TextColor = new Color(255, 226, 150),
        });

        if (_items.Count == 0)
        {
            layout.Widgets.Add(new Label
            {
                Text = loc["timelapse.collectionEmpty"],
                Wrap = true,
                Width = 460,
                HorizontalAlignment = HorizontalAlignment.Center,
                TextColor = new Color(150, 160, 175),
            });
        }

        var list = new VerticalStackPanel { Spacing = 6 };
        foreach (var (path, timelapse) in _items)
        {
            list.Widgets.Add(Row(path, timelapse));
        }

        layout.Widgets.Add(new ScrollViewer { Content = list, Height = 380, Width = 520 });
        layout.Widgets.Add(UiFactory.MenuButton(loc["panel.close"], _screens.Pop));

        var panel = UiFactory.DarkPanel(layout);
        panel.HorizontalAlignment = HorizontalAlignment.Center;
        panel.VerticalAlignment = VerticalAlignment.Center;

        var root = new Panel();
        root.Widgets.Add(panel);
        _desktop = _screens.NewDesktop(root);
    }

    private Widget Row(string path, SavedTimelapse timelapse)
    {
        var loc = _screens.Loc;
        var last = timelapse.History.FrameAt(timelapse.History.Count - 1);

        var row = new VerticalStackPanel
        {
            Spacing = 3,
            Width = 492,
            Padding = new Thickness(12, 8),
            Background = new Myra.Graphics2D.Brushes.SolidBrush(new Color(28, 32, 40, 235)),
        };
        row.Widgets.Add(new Label
        {
            Text = timelapse.SavedAtUtc.ToLocalTime().ToString("d. M. yyyy H:mm"),
            TextColor = new Color(255, 226, 150),
        });
        row.Widgets.Add(new Label
        {
            Text = loc.Format(
                "timelapse.caption",
                DurationFormat.Human(last.Seconds),
                CivDle.Core.Numbers.Format(last.Population),
                last.Buildings),
            TextColor = Color.LightGray,
        });

        var buttons = new HorizontalStackPanel { Spacing = 8 };
        buttons.Widgets.Add(UiFactory.SmallButton(loc["timelapse.play"], () => Play(timelapse)));
        buttons.Widgets.Add(UiFactory.SmallButton(loc["timelapse.delete"], () => Delete(path)));
        row.Widgets.Add(buttons);
        return row;
    }

    private void Play(SavedTimelapse timelapse)
    {
        // Terén ze seedu a presetu, jako při načtení savu. Zaniklý preset
        // nahradí první z dat — přehrávka nad trochu jiným pozadím je lepší
        // než žádná.
        var worldGen = _screens.Content.WorldGen;
        var preset = worldGen.Presets.FirstOrDefault(p => p.Id == timelapse.PresetId) ?? worldGen.Presets[0];
        var terrain = new ProceduralTerrain(_screens.Content.Biomes, preset, timelapse.Seed);

        _screens.Push(new TimelapseScreen(_screens, timelapse.History, terrain, timelapse.Seed));
    }

    private void Delete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"Časosběr se nepovedlo smazat: {ex.Message}");
        }

        _items.RemoveAll(item => item.Path == path);
        BuildUi();
    }
}
