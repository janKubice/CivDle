using CivDle.Core.Sim;
using CivDle.Input;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Myra.Graphics2D.UI;

namespace CivDle.Screens;

/// <summary>
/// Uvítací obrazovka „co se dělo, cos byl pryč" (overlay po načtení hry): kolik
/// času uběhlo a co civilizace mezitím vyrobila/postavila. Idle retenční háček —
/// odměna za návrat. Simulace pod ní stojí, dokud hráč nepotvrdí.
/// </summary>
public sealed class OfflineSummaryScreen : IScreen
{
    private readonly ScreenManager _screens;
    private readonly OfflineSummary _summary;
    private readonly InputManager _input = new();
    private Desktop _desktop = null!;

    public OfflineSummaryScreen(ScreenManager screens, OfflineSummary summary)
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
        spriteBatch.Draw(_screens.WhitePixel, new Rectangle(0, 0, viewport.Width, viewport.Height), Color.Black * 0.6f);
        spriteBatch.End();

        _desktop.Render();
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
            Text = loc["offline.title"],
            HorizontalAlignment = HorizontalAlignment.Center,
            TextColor = UiFactory.Accent,
        });
        layout.Widgets.Add(new Label
        {
            Text = loc.Format("offline.away", DurationFormat.Human(_summary.CreditedSeconds)),
            HorizontalAlignment = HorizontalAlignment.Center,
            TextColor = Color.LightGray,
        });
        layout.Widgets.Add(new Label { Text = loc["offline.gains"], HorizontalAlignment = HorizontalAlignment.Center });

        if (_summary.PopulationGain >= 1)
        {
            layout.Widgets.Add(GainLabel(loc.Format("offline.population", CivDle.Core.Numbers.Format(_summary.PopulationGain))));
        }

        if (_summary.BuildingsGain > 0)
        {
            layout.Widgets.Add(GainLabel(loc.Format("offline.buildings", _summary.BuildingsGain)));
        }

        for (int i = 0; i < content.Resources.Count && i < _summary.ResourceGains.Length; i++)
        {
            double gain = _summary.ResourceGains[i];
            if (gain >= 1)
            {
                layout.Widgets.Add(GainLabel($"+{CivDle.Core.Numbers.Format(gain)} {loc[content.Resources[i].NameKey]}"));
            }
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

    private static Label GainLabel(string text) => new()
    {
        Text = text,
        HorizontalAlignment = HorizontalAlignment.Center,
        TextColor = new Color(150, 220, 150),
    };

}
