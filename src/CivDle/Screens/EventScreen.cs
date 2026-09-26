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
/// slavnost… Hráč vybere jednu možnost; co se pak stane (cena, zisk, dočasný
/// dozvuk), řeší simulace — obrazovka jen ukáže předem, co která volba udělá.
/// Nedostupná volba (chybí suroviny) je ztlumená. Simulace stojí.
/// </summary>
public sealed class EventScreen : IScreen
{
    private readonly ScreenManager _screens;
    private readonly Simulation _simulation;
    private readonly int _eventIndex;
    private readonly EventDef _event;
    private readonly InputManager _input = new();
    private Desktop _desktop = null!;

    public EventScreen(ScreenManager screens, Simulation simulation, int eventIndex)
    {
        _screens = screens;
        _simulation = simulation;
        _eventIndex = eventIndex;
        _event = screens.Content.Events[eventIndex];
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

        for (int i = 0; i < _event.Choices.Count; i++)
        {
            layout.Widgets.Add(ChoiceButton(i));
        }

        var panel = UiFactory.DarkPanel(layout);
        panel.HorizontalAlignment = HorizontalAlignment.Center;
        panel.VerticalAlignment = VerticalAlignment.Center;

        var root = new Panel();
        root.Widgets.Add(panel);
        _desktop = _screens.NewDesktop(root);
    }

    private Widget ChoiceButton(int choiceIndex)
    {
        var loc = _screens.Loc;
        var choice = _event.Choices[choiceIndex];
        var content = new VerticalStackPanel
        {
            Spacing = 2,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        content.Widgets.Add(new Label
        {
            Text = loc[choice.LabelKey],
            HorizontalAlignment = HorizontalAlignment.Center,
        });

        // Co volba udělá, pod popiskem — bez toho se hráč rozhoduje naslepo.
        string summary = EventChoiceSummary.Line(_screens.Content, loc, choice);
        if (summary.Length > 0)
        {
            content.Widgets.Add(new Label
            {
                Text = summary,
                Wrap = true,
                Width = 320,
                HorizontalAlignment = HorizontalAlignment.Center,
                TextColor = Color.LightGray,
            });
        }

        var button = new Button
        {
            Content = content,
            Width = 340,
            Padding = new Thickness(10, 6),
            HorizontalAlignment = HorizontalAlignment.Center,
            Background = new PanelBrush(UiPalette.Panel),
            Enabled = _simulation.CanChooseEventOption(_eventIndex, choiceIndex),
        };
        button.Click += (_, _) =>
        {
            if (_simulation.TryChooseEventOption(_eventIndex, choiceIndex))
            {
                _screens.Pop();
            }
        };
        return button;
    }
}
