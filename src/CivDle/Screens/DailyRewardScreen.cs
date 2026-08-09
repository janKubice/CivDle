using CivDle.Core.Content;
using CivDle.Input;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Myra.Graphics2D.UI;

namespace CivDle.Screens;

/// <summary>
/// Denní odměna (overlay po vstupu do hry): série po sobě jdoucích dní a získané
/// suroviny. Retenční háček — důvod otevřít hru každý den. Simulace pod ní stojí.
/// </summary>
public sealed class DailyRewardScreen : IScreen
{
    private readonly ScreenManager _screens;
    private readonly int _streak;
    private readonly IReadOnlyList<ResourceAmount> _reward;
    private readonly InputManager _input = new();
    private Desktop _desktop = null!;

    public DailyRewardScreen(ScreenManager screens, int streak, IReadOnlyList<ResourceAmount> reward)
    {
        _screens = screens;
        _streak = streak;
        _reward = reward;
        BuildUi();
        _screens.Loc.LanguageChanged += BuildUi;
        _screens.UiSettingsChanged += BuildUi;
    }

    public bool IsOverlay => true;

    public void OnActivated() => _input.Resync();

    public void Update(GameTime gameTime)
    {
        _input.Update();
        if (_input.WasPressed(Keys.Escape) || _input.WasPressed(Keys.Enter) || _input.WasPressed(Keys.Space))
        {
            _screens.Pop();
        }
    }

    public void Draw(GameTime gameTime)
    {
        var viewport = _screens.GraphicsDevice.Viewport;
        var spriteBatch = _screens.SpriteBatch;
        spriteBatch.Begin();
        spriteBatch.Draw(_screens.WhitePixel, new Rectangle(0, 0, viewport.Width, viewport.Height), Color.Black * 0.6f);
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
        var content = _screens.Content;

        var layout = new VerticalStackPanel
        {
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        layout.Widgets.Add(new Label
        {
            Text = loc["daily.title"],
            HorizontalAlignment = HorizontalAlignment.Center,
            TextColor = UiPalette.TextBright,
        });
        layout.Widgets.Add(new Label
        {
            Text = loc.Format("daily.streak", _streak),
            HorizontalAlignment = HorizontalAlignment.Center,
            TextColor = Color.LightGray,
        });

        foreach (var amount in _reward)
        {
            layout.Widgets.Add(new Label
            {
                Text = $"+{amount.Amount} {loc[content.Resources[amount.ResourceIndex].NameKey]}",
                HorizontalAlignment = HorizontalAlignment.Center,
                TextColor = UiPalette.Good,
            });
        }

        layout.Widgets.Add(new Label { Text = " " });
        layout.Widgets.Add(UiFactory.MenuButton(loc["panel.close"], _screens.Pop));

        var panel = UiFactory.DarkPanel(layout);
        panel.HorizontalAlignment = HorizontalAlignment.Center;
        panel.VerticalAlignment = VerticalAlignment.Center;

        var root = new Panel();
        root.Widgets.Add(panel);
        _desktop = _screens.NewDesktop(root);
    }
}
