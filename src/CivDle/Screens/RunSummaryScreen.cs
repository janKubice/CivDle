using CivDle.Core.Sim;
using CivDle.Input;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Myra.Graphics2D.UI;

namespace CivDle.Screens;

/// <summary>
/// Bilance doběhnutého běhu (overlay hned po Vzestupu): jak dlouho trval, kam
/// město došlo a jestli to bylo dál než minule.
///
/// <para>Proč to ve hře je: prestige bez ohlédnutí je jen tlačítko „smazat
/// město". Tohle z něj dělá tečku za kapitolou — a to „dál než minule" je celý
/// motor opakovaného hraní.</para>
///
/// <para>Vrstva: čte hotový <see cref="RunSummary"/> ze simulace a nic nepočítá
/// ani nemění (render → sim, ne obráceně).</para>
/// </summary>
public sealed class RunSummaryScreen : IScreen
{
    private readonly ScreenManager _screens;
    private readonly RunSummary _summary;
    private readonly InputManager _input = new();
    private Desktop _desktop = null!;

    public RunSummaryScreen(ScreenManager screens, RunSummary summary)
    {
        _screens = screens;
        _summary = summary;
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
        spriteBatch.Draw(_screens.WhitePixel, new Rectangle(0, 0, viewport.Width, viewport.Height), Color.Black * 0.68f);
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
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };

        layout.Widgets.Add(new Label
        {
            Text = loc.Format("run.title", _summary.Level),
            HorizontalAlignment = HorizontalAlignment.Center,
            TextColor = new Color(180, 140, 230),
        });
        layout.Widgets.Add(new Label
        {
            Text = loc.Format("run.duration", DurationFormat.Human(_summary.DurationSeconds)),
            HorizontalAlignment = HorizontalAlignment.Center,
            TextColor = Color.LightGray,
        });

        layout.Widgets.Add(Line(loc.Format("run.peak", CivDle.Core.Numbers.Format(_summary.PeakPopulation))));
        layout.Widgets.Add(Line(loc.Format("run.buildings", _summary.Buildings)));

        // Řádky, které by byly nuly, se vynechávají — „0 divů světa" není
        // informace, jen šum v okamžiku, který má být slavnostní.
        if (_summary.Techs > 0)
        {
            layout.Widgets.Add(Line(loc.Format("run.techs", _summary.Techs)));
        }

        if (_summary.Wonders > 0)
        {
            layout.Widgets.Add(Line(loc.Format("run.wonders", _summary.Wonders)));
        }

        layout.Widgets.Add(new Label
        {
            Text = loc.Format("run.points", CivDle.Core.Numbers.Format(_summary.PointsEarned)),
            HorizontalAlignment = HorizontalAlignment.Center,
            TextColor = new Color(190, 160, 235),
        });

        layout.Widgets.Add(RecordLine());
        layout.Widgets.Add(new Label { Text = " " });
        layout.Widgets.Add(UiFactory.MenuButton(loc["run.continue"], _screens.Pop));

        var panel = UiFactory.DarkPanel(layout);
        panel.HorizontalAlignment = HorizontalAlignment.Center;
        panel.VerticalAlignment = VerticalAlignment.Center;

        var root = new Panel();
        root.Widgets.Add(panel);
        _desktop = _screens.NewDesktop(root);
    }

    /// <summary>
    /// „Dál než minule", nebo o kolik chybělo. Tohle je ta věta, kvůli které
    /// hráč zkusí další běh — proto má vlastní barvu a stojí na konci.
    /// </summary>
    private Widget RecordLine()
    {
        var loc = _screens.Loc;
        if (_summary.PreviousBestPopulation <= 0)
        {
            return new Label
            {
                Text = loc["run.firstRun"],
                HorizontalAlignment = HorizontalAlignment.Center,
                TextColor = new Color(150, 160, 175),
            };
        }

        if (_summary.IsBestPopulation)
        {
            return new Label
            {
                Text = loc.Format("run.record", CivDle.Core.Numbers.Format(_summary.PreviousBestPopulation)),
                HorizontalAlignment = HorizontalAlignment.Center,
                TextColor = new Color(240, 205, 110),
            };
        }

        return new Label
        {
            Text = loc.Format("run.behindBest", CivDle.Core.Numbers.Format(_summary.PreviousBestPopulation)),
            HorizontalAlignment = HorizontalAlignment.Center,
            TextColor = new Color(150, 160, 175),
        };
    }

    private static Label Line(string text) => new()
    {
        Text = text,
        HorizontalAlignment = HorizontalAlignment.Center,
        TextColor = new Color(210, 205, 190),
    };
}
