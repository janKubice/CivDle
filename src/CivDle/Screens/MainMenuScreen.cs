using CivDle.Core.Content;
using Microsoft.Xna.Framework;
using Myra.Graphics2D;
using Myra.Graphics2D.UI;

namespace CivDle.Screens;

/// <summary>
/// Hlavní menu nad živým městem na pozadí: velký animovaný titul, tlačítka
/// (pokračovat / nová hra / nastavení / konec) a rolovací vývojový deník.
/// Po změně jazyka se přestaví.
/// </summary>
public sealed class MainMenuScreen : IScreen
{
    private readonly ScreenManager _screens;
    private Desktop _desktop = null!;
    private string? _statusText;

    public MainMenuScreen(ScreenManager screens)
    {
        _screens = screens;
        BuildUi();
        _screens.Loc.LanguageChanged += BuildUi;
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
    }

    private void BuildUi()
    {
        var loc = _screens.Loc;
        var root = new Panel();

        // Levý sloupec: titul + tlačítka v panelu.
        var buttons = new VerticalStackPanel { Spacing = 10 };
        buttons.Widgets.Add(new Label
        {
            Text = "C I V D L E",
            HorizontalAlignment = HorizontalAlignment.Center,
            TextColor = UiFactory.Accent,
        });
        buttons.Widgets.Add(new Label
        {
            Text = loc["menu.subtitle"],
            HorizontalAlignment = HorizontalAlignment.Center,
            TextColor = Color.LightGray,
        });
        buttons.Widgets.Add(new Label { Text = " " });
        if (_screens.Saves.HasSave)
        {
            buttons.Widgets.Add(UiFactory.MenuButton(loc["menu.continue"], ContinueGame));
        }

        buttons.Widgets.Add(UiFactory.MenuButton(loc["menu.newGame"], () => _screens.Push(new NewGameScreen(_screens))));
        buttons.Widgets.Add(UiFactory.MenuButton(loc["menu.settings"], () => _screens.Push(new SettingsScreen(_screens))));
        buttons.Widgets.Add(UiFactory.MenuButton(loc["menu.quit"], _screens.ExitGame));

        if (_statusText is not null)
        {
            buttons.Widgets.Add(new Label
            {
                Text = _statusText,
                TextColor = new Color(235, 120, 110),
                HorizontalAlignment = HorizontalAlignment.Center,
            });
        }

        var buttonPanel = UiFactory.DarkPanel(buttons);
        buttonPanel.HorizontalAlignment = HorizontalAlignment.Center;
        buttonPanel.VerticalAlignment = VerticalAlignment.Center;
        root.Widgets.Add(buttonPanel);

        // Pravý dolní roh: rolovací devlog.
        if (_screens.Content.Devlog.Count > 0)
        {
            var devlogPanel = BuildDevlog(loc);
            devlogPanel.HorizontalAlignment = HorizontalAlignment.Right;
            devlogPanel.VerticalAlignment = VerticalAlignment.Bottom;
            devlogPanel.Margin = new Thickness(0, 0, 14, 14);
            root.Widgets.Add(devlogPanel);
        }

        _desktop = new Desktop { Root = root };
    }

    private Panel BuildDevlog(Localization loc)
    {
        var list = new VerticalStackPanel { Spacing = 8, Width = 300 };
        list.Widgets.Add(new Label { Text = loc["menu.devlog"], TextColor = UiFactory.Accent });

        foreach (var entry in _screens.Content.Devlog)
        {
            var block = new VerticalStackPanel { Spacing = 1 };
            block.Widgets.Add(new Label
            {
                Text = string.IsNullOrEmpty(entry.Date) ? entry.Version : $"{entry.Version}  ·  {entry.Date}",
                TextColor = new Color(210, 220, 235),
            });
            foreach (var line in entry.Lines)
            {
                block.Widgets.Add(new Label
                {
                    Text = $"• {line}",
                    TextColor = Color.LightGray,
                    Wrap = true,
                    Width = 280,
                });
            }

            list.Widgets.Add(block);
        }

        var scroll = new ScrollViewer
        {
            Content = list,
            Height = 220,
            Width = 300,
        };

        return UiFactory.DarkPanel(scroll);
    }

    private void ContinueGame()
    {
        var loaded = _screens.Saves.TryLoad(_screens.Content, out var error);
        if (loaded is null)
        {
            if (error is not null)
            {
                Console.Error.WriteLine($"Načtení savu selhalo: {error}");
            }

            _statusText = _screens.Loc["menu.loadFailed"];
            BuildUi();
            return;
        }

        var info = new WorldInfo(loaded.Metadata.Seed, loaded.Metadata.SizeId, loaded.Metadata.PresetId);
        _screens.ReplaceAll(new GameplayScreen(_screens, loaded.Simulation, info, loaded.Metadata.SavedAtUtc));
    }
}
