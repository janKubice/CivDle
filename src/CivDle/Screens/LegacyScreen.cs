using CivDle.Core;
using CivDle.Core.Sim;
using CivDle.Input;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Myra.Graphics2D;
using Myra.Graphics2D.Brushes;
using Myra.Graphics2D.UI;

namespace CivDle.Screens;

/// <summary>
/// Odkaz — druhá prestižní vrstva jako overlay.
///
/// <para>Záměrně je oddělený od obrazovky Vzestupu: kdyby byl jen dalším
/// panelem vedle, splynul by s ním a hráč by nepoznal, že je to hlubší řez.
/// Text i potvrzení na dvě kliknutí říkají nahlas, co se smaže.</para>
///
/// <para>Vrstva: jen volá příkazy simulace, žádnou logiku nedrží.</para>
/// </summary>
public sealed class LegacyScreen : IScreen
{
    private const int PanelWidth = 480;

    private readonly ScreenManager _screens;
    private readonly Simulation _simulation;
    private readonly InputManager _input = new();
    private Desktop _desktop = null!;

    /// <summary>Čeká tlačítko na potvrzení? (Nevratný krok na dvě kliknutí.)</summary>
    private bool _confirming;

    public LegacyScreen(ScreenManager screens, Simulation simulation)
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

        _screens.RenderDesktop(this, _desktop);
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
            Text = loc["legacy.title"],
            HorizontalAlignment = HorizontalAlignment.Center,
            TextColor = UiPalette.TextBright,
        });

        if (!_simulation.LegacyAvailable)
        {
            layout.Widgets.Add(Note(loc["legacy.locked"], UiPalette.TextBright));
            Finish(layout);
            return;
        }

        layout.Widgets.Add(Note(loc["legacy.desc"], Color.LightGray));

        bool hasPoints = _simulation.LegacyPoints > 0;
        layout.Widgets.Add(new Label
        {
            Text = loc.Format("legacy.points", _simulation.LegacyPoints),
            HorizontalAlignment = HorizontalAlignment.Center,
            TextColor = hasPoints ? UiPalette.TextBright : Color.LightGray,
        });

        if (_simulation.LegacyDepth > 0)
        {
            layout.Widgets.Add(new Label
            {
                Text = loc.Format("legacy.depth", _simulation.LegacyDepth),
                HorizontalAlignment = HorizontalAlignment.Center,
                TextColor = UiPalette.Text,
            });
        }

        if (hasPoints)
        {
            layout.Widgets.Add(Note(loc["legacy.spendNow"], UiPalette.Good));
        }

        layout.Widgets.Add(LeaveAction());

        var list = new VerticalStackPanel { Spacing = 8 };
        var upgrades = _screens.Content.LegacyUpgrades;
        for (int i = 0; i < upgrades.Count; i++)
        {
            list.Widgets.Add(UpgradeRow(i));
        }

        layout.Widgets.Add(new ScrollViewer { Content = list, Height = 300, Width = PanelWidth });
        layout.Widgets.Add(UiFactory.MenuButton(loc["panel.close"], _screens.Pop));
        Finish(layout);
    }

    private void Finish(VerticalStackPanel layout)
    {
        if (!_simulation.LegacyAvailable)
        {
            layout.Widgets.Add(UiFactory.MenuButton(_screens.Loc["panel.close"], _screens.Pop));
        }

        var panel = UiFactory.DarkPanel(layout);
        panel.HorizontalAlignment = HorizontalAlignment.Center;
        panel.VerticalAlignment = VerticalAlignment.Center;

        var root = new Panel();
        root.Widgets.Add(panel);
        _desktop = _screens.NewDesktop(root);
    }

    private Label Note(string text, Color color) => new()
    {
        Text = text,
        TextColor = color,
        Wrap = true,
        Width = PanelWidth,
    };

    private Widget LeaveAction()
    {
        var loc = _screens.Loc;
        if (!_simulation.CanLeaveLegacy())
        {
            _confirming = false;

            long current = _simulation.LegacyProgress();
            long target = _simulation.LegacyRequirement();

            var pending = new VerticalStackPanel { Spacing = 4, HorizontalAlignment = HorizontalAlignment.Center };
            pending.Widgets.Add(new Label
            {
                Text = loc.Format("legacy.requirement", current, target),
                HorizontalAlignment = HorizontalAlignment.Center,
                TextColor = UiPalette.TextDim,
            });

            var bar = new ProgressBar(PanelWidth - 40, 8);
            bar.SetProgress(target > 0 ? current / (double)target : 1.0);
            pending.Widgets.Add(bar.Root);
            return pending;
        }

        var ready = new VerticalStackPanel { Spacing = 8, HorizontalAlignment = HorizontalAlignment.Center };
        ready.Widgets.Add(PreviewPanel());

        var button = new Button
        {
            Content = new Label
            {
                Text = _confirming
                    ? loc["legacy.confirm"]
                    : loc.Format("legacy.leave", _simulation.PendingLegacyPoints()),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            },
            Padding = new Thickness(20, 8),
            HorizontalAlignment = HorizontalAlignment.Center,
            Background = new SolidBrush(_confirming
                ? UiPalette.PanelBad
                : UiPalette.PanelBad),
        };

        // Dvě kliknutí: Odkaz maže víc než Vzestup a vrátit se nedá.
        button.Click += (_, _) =>
        {
            if (!_confirming)
            {
                _confirming = true;
                BuildUi();
                return;
            }

            _confirming = false;
            if (_simulation.TryLeaveLegacy() == PlacementResult.Ok)
            {
                BuildUi();
            }
        };

        ready.Widgets.Add(button);
        return ready;
    }

    /// <summary>Rozvaha před nevratným krokem: co přibude, co zmizí, co zůstane.</summary>
    private Widget PreviewPanel()
    {
        var loc = _screens.Loc;
        var stack = new VerticalStackPanel
        {
            Spacing = 4,
            Width = PanelWidth - 24,
            Padding = new Thickness(12, 8),
            Background = new SolidBrush(UiPalette.Panel),
        };

        long points = _simulation.PendingLegacyPoints();
        stack.Widgets.Add(new Label
        {
            Text = loc.Format("legacy.preview.gain", points, _simulation.LegacyPoints + points),
            TextColor = UiPalette.TextBright,
            Wrap = true,
        });

        int upgradeLevels = 0;
        for (int i = 0; i < _screens.Content.PrestigeUpgrades.Count; i++)
        {
            upgradeLevels += _simulation.UpgradeLevel(i);
        }

        stack.Widgets.Add(new Label
        {
            Text = loc.Format("legacy.preview.loses",
                _simulation.AscensionLevel, _simulation.PrestigePoints, upgradeLevels),
            TextColor = UiPalette.Warn,
            Wrap = true,
        });

        stack.Widgets.Add(new Label
        {
            Text = loc["legacy.preview.keeps"],
            TextColor = UiPalette.Good,
            Wrap = true,
        });

        stack.Widgets.Add(new Label
        {
            Text = loc.Format("legacy.preview.next", _simulation.LegacyRequirement()),
            TextColor = UiPalette.TextDim,
            Wrap = true,
        });

        return stack;
    }

    private Widget UpgradeRow(int upgradeIndex)
    {
        var loc = _screens.Loc;
        var upgrade = _screens.Content.LegacyUpgrades[upgradeIndex];

        var row = new VerticalStackPanel
        {
            Spacing = 3,
            Width = PanelWidth - 24,
            Padding = new Thickness(12, 8),
            Background = new SolidBrush(UiPalette.Panel),
        };
        row.Widgets.Add(new Label { Text = loc[upgrade.NameKey], TextColor = UiPalette.TextBright });
        row.Widgets.Add(new Label { Text = loc[upgrade.DescriptionKey], TextColor = Color.LightGray, Wrap = true });

        // Úroveň se ukazuje vždy, i u nekoupených: hráč tak hned vidí, že jde
        // o opakovatelný upgrade, a ne o jednorázový uzel.
        row.Widgets.Add(new Label
        {
            Text = loc.Format("legacy.level", _simulation.LegacyLevel(upgradeIndex), upgrade.MaxLevel),
            TextColor = UiPalette.TextDim,
        });

        row.Widgets.Add(UpgradeAction(upgradeIndex));
        return row;
    }

    private Widget UpgradeAction(int upgradeIndex)
    {
        var loc = _screens.Loc;
        if (_simulation.IsLegacyUpgradeMaxed(upgradeIndex))
        {
            return new Label { Text = loc["legacy.maxed"], TextColor = Color.LightGreen };
        }

        var status = _simulation.CanBuyLegacyUpgrade(upgradeIndex);
        if (status == PlacementResult.NotUnlocked)
        {
            return new Label { Text = loc["legacy.lockedUpgrade"], TextColor = UiPalette.TextDim };
        }

        var button = new Button
        {
            Content = new Label
            {
                Text = loc.Format("legacy.buy", Numbers.Format(_simulation.LegacyCost(upgradeIndex))),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            },
            Padding = new Thickness(14, 5),
            HorizontalAlignment = HorizontalAlignment.Left,
            Background = new SolidBrush(UiPalette.Panel),
            Enabled = status == PlacementResult.Ok,
        };
        button.Click += (_, _) =>
        {
            if (_simulation.TryBuyLegacyUpgrade(upgradeIndex) == PlacementResult.Ok)
            {
                BuildUi();
            }
        };
        return button;
    }
}
