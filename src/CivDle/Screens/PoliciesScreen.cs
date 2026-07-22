using CivDle.Core.Sim;
using CivDle.Input;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Myra.Graphics2D;
using Myra.Graphics2D.Brushes;
using Myra.Graphics2D.UI;

namespace CivDle.Screens;

/// <summary>
/// Politiky růstu jako overlay (automatizace, stupeň 4): seznam pravidel
/// s přepínačem zap/vyp. Zapnutá politika hned mění chování auto-stavby
/// a plnění zón. Simulace mezitím stojí.
/// </summary>
public sealed class PoliciesScreen : IScreen
{
    private readonly ScreenManager _screens;
    private readonly Simulation _simulation;
    private readonly InputManager _input = new();
    private Desktop _desktop = null!;

    public PoliciesScreen(ScreenManager screens, Simulation simulation)
    {
        _screens = screens;
        _simulation = simulation;
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
        var viewport = _screens.GraphicsDevice.Viewport;
        var spriteBatch = _screens.SpriteBatch;
        spriteBatch.Begin();
        spriteBatch.Draw(_screens.WhitePixel, new Rectangle(0, 0, viewport.Width, viewport.Height), Color.Black * 0.55f);
        spriteBatch.End();

        _desktop.Render();
    }

    public void Dispose() => _screens.Loc.LanguageChanged -= BuildUi;

    private void BuildUi()
    {
        var loc = _screens.Loc;
        var policies = _screens.Content.Policies;

        var list = new VerticalStackPanel { Spacing = 8 };
        for (int i = 0; i < policies.Count; i++)
        {
            list.Widgets.Add(Row(i));
        }

        var layout = new VerticalStackPanel
        {
            Spacing = 12,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        layout.Widgets.Add(new Label { Text = loc["policy.title"], HorizontalAlignment = HorizontalAlignment.Center });
        layout.Widgets.Add(new ScrollViewer { Content = list, Height = 360, Width = 460 });
        layout.Widgets.Add(UiFactory.MenuButton(loc["panel.close"], _screens.Pop));

        var panel = UiFactory.DarkPanel(layout);
        panel.HorizontalAlignment = HorizontalAlignment.Center;
        panel.VerticalAlignment = VerticalAlignment.Center;

        var root = new Panel();
        root.Widgets.Add(panel);
        _desktop = new Desktop { Root = root };
    }

    private Widget Row(int index)
    {
        var loc = _screens.Loc;
        var policy = _screens.Content.Policies[index];
        bool active = _simulation.IsPolicyActive(index);

        var row = new VerticalStackPanel
        {
            Spacing = 4,
            Width = 436,
            Padding = new Thickness(12, 8),
            Background = new SolidBrush(active ? new Color(28, 46, 30, 235) : new Color(26, 30, 38, 235)),
        };
        row.Widgets.Add(new Label
        {
            Text = loc[policy.NameKey],
            TextColor = active ? new Color(150, 220, 150) : new Color(200, 205, 215),
        });
        row.Widgets.Add(new Label { Text = loc[policy.DescriptionKey], TextColor = Color.LightGray, Wrap = true });
        row.Widgets.Add(UiFactory.SmallButton(active ? loc["policy.on"] : loc["policy.off"], () =>
        {
            _simulation.TogglePolicy(index);
            BuildUi(); // překresli, ať se přepínač i barva hned obnoví
        }));
        return row;
    }
}
