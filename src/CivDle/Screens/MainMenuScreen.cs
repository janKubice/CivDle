using Microsoft.Xna.Framework;
using Myra.Graphics2D.UI;

namespace CivDle.Screens;

/// <summary>Hlavní menu: nová hra, nastavení, ukončení. Po změně jazyka se přestaví.</summary>
public sealed class MainMenuScreen : IScreen
{
    private readonly ScreenManager _screens;
    private Desktop _desktop = null!;

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
        layout.Widgets.Add(UiFactory.MenuButton(loc["menu.newGame"], () => _screens.Push(new NewGameScreen(_screens))));
        layout.Widgets.Add(UiFactory.MenuButton(loc["menu.settings"], () => _screens.Push(new SettingsScreen(_screens))));
        layout.Widgets.Add(UiFactory.MenuButton(loc["menu.quit"], _screens.ExitGame));

        _desktop = new Desktop { Root = layout };
    }
}
