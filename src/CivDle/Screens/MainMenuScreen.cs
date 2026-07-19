using Microsoft.Xna.Framework;
using Myra.Graphics2D.UI;

namespace CivDle.Screens;

/// <summary>Hlavní menu: vstup do nové hry a ukončení.</summary>
public sealed class MainMenuScreen : IScreen
{
    private readonly Desktop _desktop;

    public MainMenuScreen(ScreenManager screens)
    {
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
            Text = "idle city-builder",
            HorizontalAlignment = HorizontalAlignment.Center,
            TextColor = Color.Gray,
        });
        layout.Widgets.Add(new Label { Text = " " });
        layout.Widgets.Add(UiFactory.MenuButton("Nová hra", () => screens.Push(new NewGameScreen(screens))));
        layout.Widgets.Add(UiFactory.MenuButton("Ukončit", screens.ExitGame));

        _desktop = new Desktop { Root = layout };
    }

    public bool IsOverlay => false;

    public void Update(GameTime gameTime)
    {
        // Interakci (klik na tlačítka) obsluhuje Myra uvnitř Desktop.Render().
    }

    public void Draw(GameTime gameTime) => _desktop.Render();

    public void Dispose()
    {
    }
}
