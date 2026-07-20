using CivDle.Core.Sim;
using CivDle.Input;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Myra.Graphics2D;
using Myra.Graphics2D.Brushes;
using Myra.Graphics2D.UI;

namespace CivDle.Screens;

/// <summary>
/// Tech tree jako overlay: seznam technologií z data-driven katalogu
/// (<c>tech.json</c>). Každá řádka ukazuje jméno, popis, cenu a stav —
/// hotovo / zamčeno prerekvizitou / lze vyzkoumat. Výzkum odečte suroviny
/// a odemkne budovy; panel se přestaví, aby ukázal nový stav. Simulace stojí.
/// </summary>
public sealed class TechScreen : IScreen
{
    private readonly ScreenManager _screens;
    private readonly Simulation _simulation;
    private readonly InputManager _input = new();
    private Desktop _desktop = null!;

    public TechScreen(ScreenManager screens, Simulation simulation)
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
        var techs = _screens.Content.Techs;

        var list = new VerticalStackPanel { Spacing = 8 };
        for (int i = 0; i < techs.Count; i++)
        {
            list.Widgets.Add(TechRow(i));
        }

        var scroll = new ScrollViewer
        {
            Content = list,
            Height = 380,
            Width = 440,
        };

        var layout = new VerticalStackPanel
        {
            Spacing = 12,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        layout.Widgets.Add(new Label
        {
            Text = loc["hud.tech"],
            HorizontalAlignment = HorizontalAlignment.Center,
        });
        layout.Widgets.Add(scroll);
        layout.Widgets.Add(UiFactory.MenuButton(loc["panel.close"], _screens.Pop));

        var panel = UiFactory.DarkPanel(layout);
        panel.HorizontalAlignment = HorizontalAlignment.Center;
        panel.VerticalAlignment = VerticalAlignment.Center;

        var root = new Panel();
        root.Widgets.Add(panel);
        _desktop = new Desktop { Root = root };
    }

    private Widget TechRow(int techIndex)
    {
        var loc = _screens.Loc;
        var content = _screens.Content;
        var tech = content.Techs[techIndex];

        var row = new VerticalStackPanel
        {
            Spacing = 3,
            Width = 416,
            Padding = new Thickness(12, 8),
            Background = new SolidBrush(new Color(28, 36, 50, 235)),
        };
        row.Widgets.Add(new Label
        {
            Text = loc[tech.NameKey],
            TextColor = UiFactory.Accent,
        });
        row.Widgets.Add(new Label
        {
            Text = loc[tech.DescriptionKey],
            TextColor = Color.LightGray,
            Wrap = true,
        });
        row.Widgets.Add(new Label
        {
            Text = loc.Format("panel.cost", CostFormat.Line(content, loc, tech.Cost)),
            TextColor = Color.Gray,
        });
        row.Widgets.Add(TechAction(techIndex));
        return row;
    }

    private Widget TechAction(int techIndex)
    {
        var loc = _screens.Loc;
        if (_simulation.IsTechResearched(techIndex))
        {
            return new Label { Text = loc["tech.researched"], TextColor = Color.LightGreen };
        }

        var status = _simulation.CanResearch(techIndex);
        if (status == PlacementResult.NotUnlocked)
        {
            return new Label { Text = loc["tech.locked"], TextColor = new Color(210, 170, 120) };
        }

        var button = new Button
        {
            Content = new Label
            {
                Text = loc["tech.research"],
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            },
            Padding = new Thickness(14, 5),
            HorizontalAlignment = HorizontalAlignment.Left,
            Background = new SolidBrush(new Color(48, 92, 72, 235)),
            Enabled = status == PlacementResult.Ok,
        };
        button.Click += (_, _) =>
        {
            if (_simulation.TryResearch(techIndex) == PlacementResult.Ok)
            {
                BuildUi(); // stav se změnil — odemčené budovy, splněné prereky
            }
        };
        return button;
    }
}
