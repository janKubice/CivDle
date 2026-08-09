using CivDle.Core.Sim;
using CivDle.Input;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Myra.Graphics2D;
using Myra.Graphics2D.Brushes;
using Myra.Graphics2D.UI;

namespace CivDle.Screens;

/// <summary>
/// Volby jako overlay: kandidátka programů, hráč vybere jeden a ten dává městu
/// bonus po celé volební období.
///
/// <para>Zavřít se dá i bez volby — volby běží na pozadí a když si hráč nevybere,
/// rozhodne se to za něj. Idle hra nesmí čekat na klik.</para>
/// </summary>
public sealed class ElectionScreen : IScreen
{
    private readonly ScreenManager _screens;
    private readonly Simulation _simulation;
    private readonly InputManager _input = new();
    private Desktop _desktop = null!;

    public ElectionScreen(ScreenManager screens, Simulation simulation)
    {
        _screens = screens;
        _simulation = simulation;
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
        var elections = _screens.Content.Elections;

        var layout = new VerticalStackPanel
        {
            Spacing = 10,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        layout.Widgets.Add(new Label
        {
            Text = loc["election.title"],
            TextColor = UiFactory.Accent,
            HorizontalAlignment = HorizontalAlignment.Center,
        });
        layout.Widgets.Add(new Label
        {
            Text = loc.Format("election.subtitle", elections.TermDays),
            TextColor = UiPalette.Text,
            HorizontalAlignment = HorizontalAlignment.Center,
        });

        // Stav: kdo vládne teď a kolik zbývá — hráč se sem podívá i mimo volby.
        layout.Widgets.Add(new Label
        {
            Text = _simulation.HasElected
                ? loc.Format("election.current", loc[elections.Candidates[_simulation.ElectedCandidate].NameKey])
                : loc["election.none"],
            TextColor = UiPalette.Text,
            HorizontalAlignment = HorizontalAlignment.Center,
        });
        layout.Widgets.Add(new Label
        {
            Text = loc.Format("election.daysLeft", _simulation.DaysUntilElection),
            TextColor = Color.LightGray,
            HorizontalAlignment = HorizontalAlignment.Center,
        });
        layout.Widgets.Add(new Label { Text = " " });

        for (int slot = 0; slot < _simulation.BallotSize; slot++)
        {
            layout.Widgets.Add(CandidateRow(slot));
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

    /// <summary>Jeden program na kandidátce: jméno, slib a tlačítko zvolit.</summary>
    private Widget CandidateRow(int slot)
    {
        var loc = _screens.Loc;
        int candidateIndex = _simulation.BallotAt(slot);
        var candidate = _screens.Content.Elections.Candidates[candidateIndex];
        bool elected = _simulation.ElectedCandidate == candidateIndex;

        var stack = new VerticalStackPanel { Spacing = 4, Width = 420 };
        stack.Widgets.Add(new Label
        {
            Text = loc[candidate.NameKey],
            TextColor = elected ? UiPalette.Good : UiPalette.TextBright,
        });
        stack.Widgets.Add(new Label
        {
            Text = loc[candidate.DescriptionKey],
            TextColor = UiPalette.Text,
            Wrap = true,
        });

        if (!elected)
        {
            stack.Widgets.Add(UiFactory.SmallButton(loc["hud.election"], () =>
            {
                _simulation.ElectCandidate(candidateIndex);
                BuildUi();
            }, loc["tip.election"]));
        }

        var panel = new Panel
        {
            Background = new SolidBrush(elected ? UiPalette.Panel : UiPalette.PanelDeep),
            Border = new SolidBrush(elected ? UiPalette.Good : UiFactory.Accent * 0.55f),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(10, 8),
        };
        panel.Widgets.Add(stack);
        return panel;
    }
}
