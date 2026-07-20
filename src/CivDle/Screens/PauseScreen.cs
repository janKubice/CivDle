using CivDle.Input;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Myra.Graphics2D.UI;

namespace CivDle.Screens;

/// <summary>
/// Pauzovací menu jako overlay nad hrou: ztmaví běžící scénu a nabídne návrat
/// do hry, nastavení, hlavní menu, nebo ukončení. Simulace stojí automaticky —
/// ScreenManager aktualizuje jen vrchní obrazovku.
/// </summary>
public sealed class PauseScreen : IScreen
{
    private readonly ScreenManager _screens;
    private readonly InputManager _input = new();
    private Desktop _desktop = null!;

    public PauseScreen(ScreenManager screens)
    {
        _screens = screens;
        BuildUi();
        _screens.Loc.LanguageChanged += BuildUi;
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
        // Ztmavení hry pod menu — hra zůstává vidět, ale je jasné, že stojí.
        var viewport = _screens.GraphicsDevice.Viewport;
        var spriteBatch = _screens.SpriteBatch;
        spriteBatch.Begin();
        spriteBatch.Draw(_screens.WhitePixel, new Rectangle(0, 0, viewport.Width, viewport.Height), Color.Black * 0.55f);
        spriteBatch.End();

        _desktop.Render();
    }

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
            Text = loc["pause.title"],
            HorizontalAlignment = HorizontalAlignment.Center,
        });
        layout.Widgets.Add(new Label { Text = " " });
        layout.Widgets.Add(UiFactory.MenuButton(loc["pause.resume"], _screens.Pop));
        layout.Widgets.Add(UiFactory.MenuButton(loc["pause.settings"], () => _screens.Push(new SettingsScreen(_screens))));
        layout.Widgets.Add(UiFactory.MenuButton(loc["pause.mainMenu"], () => _screens.ReplaceAll(new MainMenuScreen(_screens))));
        layout.Widgets.Add(UiFactory.MenuButton(loc["pause.quit"], _screens.ExitGame));

        _desktop = new Desktop { Root = layout };
    }
}
