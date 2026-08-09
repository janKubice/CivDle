using CivDle.Core.Content;
using CivDle.Core.Sim;
using CivDle.Input;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Myra.Graphics2D;
using Myra.Graphics2D.Brushes;
using Myra.Graphics2D.UI;

namespace CivDle.Screens;

/// <summary>
/// Náhodná událost s volbami (mikro-rozhodnutí) jako overlay: kupec, učenec,
/// slavnost… Hráč vybere jednu možnost (cena se odečte, zisk přičte). Přidává
/// agenci a variabilitu; nedostupná volba (chybí suroviny) je ztlumená. Simulace stojí.
/// </summary>
public sealed class EventScreen : IScreen
{
    private readonly ScreenManager _screens;
    private readonly Simulation _simulation;
    private readonly EventDef _event;
    private readonly InputManager _input = new();
    private Desktop _desktop = null!;

    public EventScreen(ScreenManager screens, Simulation simulation, EventDef gameEvent)
    {
        _screens = screens;
        _simulation = simulation;
        _event = gameEvent;
        BuildUi();
        _screens.Loc.LanguageChanged += BuildUi;
        _screens.UiSettingsChanged += BuildUi;
    }

    public bool IsOverlay => true;

    public void OnActivated() => _input.Resync();

    public void Update(GameTime gameTime) => _input.Update();

    public void Draw(GameTime gameTime)
    {
        var viewport = _screens.GraphicsDevice.Viewport;
        var spriteBatch = _screens.SpriteBatch;
        spriteBatch.Begin();
        spriteBatch.Draw(_screens.WhitePixel, new Rectangle(0, 0, viewport.Width, viewport.Height), Color.Black * 0.62f);
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
            Spacing = 10,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        layout.Widgets.Add(new Label
        {
            Text = loc[_event.NameKey],
            HorizontalAlignment = HorizontalAlignment.Center,
            TextColor = UiFactory.Accent,
        });
        layout.Widgets.Add(new Label
        {
            Text = loc[_event.DescriptionKey],
            Wrap = true,
            Width = 420,
            HorizontalAlignment = HorizontalAlignment.Center,
            TextColor = Color.LightGray,
        });

        foreach (var choice in _event.Choices)
        {
            layout.Widgets.Add(ChoiceButton(choice));
        }

        var panel = UiFactory.DarkPanel(layout);
        panel.HorizontalAlignment = HorizontalAlignment.Center;
        panel.VerticalAlignment = VerticalAlignment.Center;

        var root = new Panel();
        root.Widgets.Add(panel);
        _desktop = _screens.NewDesktop(root);
    }

    private Widget ChoiceButton(EventChoiceDef choice)
    {
        var loc = _screens.Loc;
        var button = new Button
        {
            Content = new Label
            {
                Text = loc[choice.LabelKey],
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            },
            Width = 340,
            Padding = new Thickness(10, 6),
            HorizontalAlignment = HorizontalAlignment.Center,
            Background = new SolidBrush(UiPalette.Panel),
            Enabled = CanAfford(choice),
        };
        button.Click += (_, _) =>
        {
            Apply(choice);
            _screens.Pop();
        };
        return button;
    }

    private bool CanAfford(EventChoiceDef choice)
    {
        foreach (var cost in choice.Cost)
        {
            if (_simulation.GetResource(cost.ResourceIndex) < cost.Amount)
            {
                return false;
            }
        }

        return true;
    }

    private void Apply(EventChoiceDef choice)
    {
        foreach (var cost in choice.Cost)
        {
            _simulation.AddResource(cost.ResourceIndex, -cost.Amount);
        }

        foreach (var gain in choice.Gain)
        {
            _simulation.AddResource(gain.ResourceIndex, gain.Amount);
        }
    }
}
