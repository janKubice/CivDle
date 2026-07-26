using CivDle.Core.Sim;
using CivDle.Input;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Myra.Graphics2D;
using Myra.Graphics2D.Brushes;
using Myra.Graphics2D.UI;

namespace CivDle.Screens;

/// <summary>
/// Vzestup (prestige) jako overlay — háček dema. Ukazuje body Vzestupu, podmínku
/// a strom trvalých upgradů (za body). Vzestup zresetuje éru, ale upgrady a body
/// zůstávají; teaser slibuje plnou verzi. Simulace mezitím stojí.
/// </summary>
public sealed class AscensionScreen : IScreen
{
    private readonly ScreenManager _screens;
    private readonly Simulation _simulation;
    private readonly InputManager _input = new();
    private Desktop _desktop = null!;

    public AscensionScreen(ScreenManager screens, Simulation simulation)
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
        spriteBatch.Draw(_screens.WhitePixel, new Rectangle(0, 0, viewport.Width, viewport.Height), Color.Black * 0.62f);
        spriteBatch.End();

        _desktop.Render();
    }

    public void Dispose() => _screens.Loc.LanguageChanged -= BuildUi;

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
            Text = loc["prestige.title"],
            HorizontalAlignment = HorizontalAlignment.Center,
            TextColor = new Color(180, 140, 230),
        });
        layout.Widgets.Add(new Label
        {
            Text = loc.Format("prestige.points", _simulation.PrestigePoints),
            HorizontalAlignment = HorizontalAlignment.Center,
        });
        layout.Widgets.Add(AscendAction());

        // Upgrady (strom) ve scrollu.
        var list = new VerticalStackPanel { Spacing = 8 };
        var upgrades = _screens.Content.PrestigeUpgrades;
        for (int i = 0; i < upgrades.Count; i++)
        {
            list.Widgets.Add(UpgradeRow(i));
        }

        layout.Widgets.Add(new ScrollViewer { Content = list, Height = 300, Width = 460 });

        layout.Widgets.Add(new Label
        {
            Text = loc["prestige.teaser"],
            Wrap = true,
            Width = 460,
            TextColor = new Color(150, 160, 175),
            HorizontalAlignment = HorizontalAlignment.Center,
        });
        layout.Widgets.Add(UiFactory.MenuButton(loc["panel.close"], _screens.Pop));

        var panel = UiFactory.DarkPanel(layout);
        panel.HorizontalAlignment = HorizontalAlignment.Center;
        panel.VerticalAlignment = VerticalAlignment.Center;

        var root = new Panel();
        root.Widgets.Add(panel);
        _desktop = new Desktop { Root = root };
    }

    private Widget AscendAction()
    {
        var loc = _screens.Loc;
        if (_simulation.CanAscend())
        {
            var button = new Button
            {
                Content = new Label
                {
                    Text = loc.Format("prestige.ascend", _simulation.PendingAscensionPoints()),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                },
                Padding = new Thickness(20, 8),
                HorizontalAlignment = HorizontalAlignment.Center,
                Background = new SolidBrush(new Color(120, 80, 170, 240)),
            };
            button.Click += (_, _) =>
            {
                if (_simulation.TryAscend() == PlacementResult.Ok)
                {
                    BuildUi(); // nová éra + víc bodů
                }
            };
            return button;
        }

        // Práh roste s každým Vzestupem — ukazuj ten AKTUÁLNÍ, ne základní z dat.
        long current = _simulation.AscensionProgress();
        long target = _simulation.AscensionRequirement();

        var pending = new VerticalStackPanel { Spacing = 4, HorizontalAlignment = HorizontalAlignment.Center };
        pending.Widgets.Add(new Label
        {
            Text = loc.Format("prestige.requirement", current, target),
            HorizontalAlignment = HorizontalAlignment.Center,
            TextColor = new Color(210, 170, 120),
        });

        var bar = new ProgressBar(412, 8);
        bar.SetProgress(target > 0 ? current / (double)target : 1.0);
        pending.Widgets.Add(bar.Root);
        return pending;
    }

    private Widget UpgradeRow(int upgradeIndex)
    {
        var loc = _screens.Loc;
        var upgrade = _screens.Content.PrestigeUpgrades[upgradeIndex];

        var row = new VerticalStackPanel
        {
            Spacing = 3,
            Width = 436,
            Padding = new Thickness(12, 8),
            Background = new SolidBrush(new Color(30, 26, 44, 235)),
        };
        row.Widgets.Add(new Label { Text = loc[upgrade.NameKey], TextColor = new Color(190, 160, 235) });
        row.Widgets.Add(new Label { Text = loc[upgrade.DescriptionKey], TextColor = Color.LightGray, Wrap = true });
        row.Widgets.Add(UpgradeAction(upgradeIndex));
        return row;
    }

    private Widget UpgradeAction(int upgradeIndex)
    {
        var loc = _screens.Loc;
        if (_simulation.IsUpgradePurchased(upgradeIndex))
        {
            return new Label { Text = loc["prestige.owned"], TextColor = Color.LightGreen };
        }

        var status = _simulation.CanBuyUpgrade(upgradeIndex);
        if (status == PlacementResult.NotUnlocked)
        {
            return new Label { Text = loc["prestige.locked"], TextColor = new Color(150, 150, 160) };
        }

        int cost = _screens.Content.PrestigeUpgrades[upgradeIndex].Cost;
        var button = new Button
        {
            Content = new Label
            {
                Text = loc.Format("prestige.buy", cost),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            },
            Padding = new Thickness(14, 5),
            HorizontalAlignment = HorizontalAlignment.Left,
            Background = new SolidBrush(new Color(72, 56, 110, 235)),
            Enabled = status == PlacementResult.Ok,
        };
        button.Click += (_, _) =>
        {
            if (_simulation.TryBuyUpgrade(upgradeIndex) == PlacementResult.Ok)
            {
                BuildUi();
            }
        };
        return button;
    }
}
