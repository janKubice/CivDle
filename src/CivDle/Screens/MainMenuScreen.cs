using Microsoft.Xna.Framework;
using Myra.Graphics2D.UI;

namespace CivDle.Screens;

/// <summary>
/// Hlavní menu: pokračování uložené hry, nová hra, nastavení, ukončení.
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

    public void Update(GameTime gameTime)
    {
        // Interakci (klik na tlačítka) obsluhuje Myra uvnitř Desktop.Render().
    }

    public void Draw(GameTime gameTime) => _desktop.Render();

    public void Dispose()
    {
        _screens.Loc.LanguageChanged -= BuildUi;
    }

    private void BuildUi()
    {
        var loc = _screens.Loc;
        var layout = new VerticalStackPanel
        {
            Spacing = 10,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };

        layout.Widgets.Add(new Label
        {
            Text = "CivDle",
            HorizontalAlignment = HorizontalAlignment.Center,
        });
        layout.Widgets.Add(new Label
        {
            Text = loc["menu.subtitle"],
            HorizontalAlignment = HorizontalAlignment.Center,
            TextColor = Color.Gray,
        });
        layout.Widgets.Add(new Label { Text = " " });
        if (_screens.Saves.HasSave)
        {
            layout.Widgets.Add(UiFactory.MenuButton(loc["menu.continue"], ContinueGame));
        }

        layout.Widgets.Add(UiFactory.MenuButton(loc["menu.newGame"], () => _screens.Push(new NewGameScreen(_screens))));
        layout.Widgets.Add(UiFactory.MenuButton(loc["menu.settings"], () => _screens.Push(new SettingsScreen(_screens))));
        layout.Widgets.Add(UiFactory.MenuButton(loc["menu.quit"], _screens.ExitGame));

        if (_statusText is not null)
        {
            layout.Widgets.Add(new Label
            {
                Text = _statusText,
                TextColor = new Color(235, 120, 110),
                HorizontalAlignment = HorizontalAlignment.Center,
            });
        }

        _desktop = new Desktop { Root = layout };
    }

    private void ContinueGame()
    {
        var loaded = _screens.Saves.TryLoad(_screens.Content, out var error);
        if (loaded is null)
        {
            // Detail patří do logu, hráči stačí srozumitelná věta.
            if (error is not null)
            {
                Console.Error.WriteLine($"Načtení savu selhalo: {error}");
            }

            _statusText = _screens.Loc["menu.loadFailed"];
            BuildUi();
            return;
        }

        var info = new WorldInfo(loaded.Metadata.Seed, loaded.Metadata.SizeId, loaded.Metadata.PresetId);
        _screens.ReplaceAll(new GameplayScreen(_screens, loaded.Simulation, info));
    }
}
