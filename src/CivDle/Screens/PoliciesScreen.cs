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
        layout.Widgets.Add(GovernorSection());
        layout.Widgets.Add(new ScrollViewer { Content = list, Height = 360, Width = 460 });
        layout.Widgets.Add(UiFactory.MenuButton(loc["panel.close"], _screens.Pop));

        var panel = UiFactory.DarkPanel(layout);
        panel.HorizontalAlignment = HorizontalAlignment.Center;
        panel.VerticalAlignment = VerticalAlignment.Center;

        var root = new Panel();
        root.Widgets.Add(panel);
        _desktop = _screens.NewDesktop(root);
    }

    /// <summary>
    /// Guvernérova správa vylepšení: dokud ji hráč neodemkne technologií, vidí jen
    /// zámek (automatizace se odemyká, není výchozí). Po odemčení si nastaví míru —
    /// od „vypnuto" po „vše, svižně".
    /// </summary>
    private Widget GovernorSection()
    {
        var loc = _screens.Loc;
        var box = new VerticalStackPanel
        {
            Spacing = 5,
            Width = 436,
            Padding = new Thickness(12, 8),
            Background = new SolidBrush(UiPalette.Panel),
        };
        box.Widgets.Add(new Label { Text = loc["hud.governor"], TextColor = UiFactory.Accent });

        if (!_simulation.IsGovernorUnlocked)
        {
            box.Widgets.Add(new Label { Text = loc["governor.locked"], TextColor = UiPalette.TextDim, Wrap = true });
            return box;
        }

        box.Widgets.Add(new Label { Text = loc["governor.desc"], TextColor = Color.LightGray, Wrap = true });

        // Tempo výstavby černé na bílém: zrychlení z upgradů je jinak vidět jen
        // „nějak rychleji" a hráč nepozná, jestli se koupený bonus vůbec projevil.
        box.Widgets.Add(new Label
        {
            Text = loc.Format(
                "governor.pace",
                (_simulation.AutoBuildInterval / (double)Simulation.TicksPerSecond).ToString("0.#"),
                _simulation.AutoBuildBudget),
            TextColor = UiPalette.Accent,
        });

        box.Widgets.Add(new Label
        {
            Text = loc.Format("governor.level", loc[$"governor.level{_simulation.AutoUpgradeLevel}"]),
            TextColor = _simulation.AutoUpgradeLevel > 0 ? UiPalette.Good : Color.LightGray,
        });

        // Stupně jako řada tlačítek — aktuální je zvýrazněný.
        var levels = new HorizontalStackPanel { Spacing = 6 };
        for (int level = 0; level <= Simulation.MaxAutoUpgradeLevel; level++)
        {
            int captured = level;
            bool active = _simulation.AutoUpgradeLevel == level;
            bool unlocked = level <= _simulation.MaxUnlockedAutoUpgradeLevel;
            var button = new Button
            {
                Content = new Label
                {
                    Text = unlocked ? level.ToString() : "×",
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    TextColor = active ? Color.White
                        : unlocked ? UiPalette.Text : UiPalette.TextDim,
                },
                Width = 44,
                Height = 32,
                Background = new SolidBrush(active ? UiPalette.PanelGood
                    : unlocked ? UiPalette.Panel : UiPalette.Panel),
            };
            button.Click += (_, _) =>
            {
                _simulation.SetAutoUpgradeLevel(captured);
                BuildUi();
            };
            levels.Widgets.Add(button);
        }

        box.Widgets.Add(levels);

        // U zamčeného stupně musí být vidět, ČÍM se odemkne — jinak vypadá
        // přeškrtnuté tlačítko jako rozbitá hra.
        if (_simulation.MaxUnlockedAutoUpgradeLevel < Simulation.MaxAutoUpgradeLevel)
        {
            box.Widgets.Add(new Label
            {
                Text = loc[_simulation.MaxUnlockedAutoUpgradeLevel < 2 ? "governor.locked2" : "governor.locked3"],
                TextColor = UiPalette.TextDim,
                Wrap = true,
            });
        }

        box.Widgets.Add(AddReserve(loc));

        // Automatické slučování je vlastní přepínač, ne další stupeň: mění půdorys
        // města a je nevratné, takže se nemá zapnout jen posunutím míry vylepšování.
        box.Widgets.Add(new Label { Text = " " });
        if (!_simulation.IsAutoMergeUnlocked)
        {
            box.Widgets.Add(new Label
            {
                Text = loc["governor.mergeLocked"],
                TextColor = UiPalette.TextDim,
                Wrap = true,
            });
            return box;
        }

        box.Widgets.Add(new Label { Text = loc["governor.mergeDesc"], TextColor = Color.LightGray, Wrap = true });
        var mergeToggle = UiFactory.SmallButton(
            loc.Format("governor.merge", loc[_simulation.AutoMerge ? "common.on" : "common.off"]),
            () =>
            {
                _simulation.SetAutoMerge(!_simulation.AutoMerge);
                BuildUi();
            },
            loc["tip.governorMerge"]);
        box.Widgets.Add(mergeToggle);
        return box;
    }

    /// <summary>
    /// Rezerva surovin: kolik guvernér nesmí utratit. Je to jediné nastavení,
    /// které hráči vrací kontrolu nad tím, co si schoval — bez něj si automatiku
    /// dřív nebo později vypne.
    /// </summary>
    private Widget AddReserve(CivDle.Core.Content.Localization loc)
    {
        var box = new VerticalStackPanel { Spacing = 4 };
        box.Widgets.Add(new Label { Text = " " });

        if (!_simulation.IsGovernorReserveUnlocked)
        {
            box.Widgets.Add(new Label
            {
                Text = loc["governor.reserveLocked"],
                TextColor = UiPalette.TextDim,
                Wrap = true,
            });
            return box;
        }

        box.Widgets.Add(new Label { Text = loc["governor.reserveDesc"], TextColor = Color.LightGray, Wrap = true });
        box.Widgets.Add(new Label
        {
            Text = loc.Format("governor.reserve", (int)Math.Round(_simulation.GovernorReserve * 100)),
            TextColor = UiPalette.Good,
        });

        var steps = new HorizontalStackPanel { Spacing = 6 };
        foreach (int percent in new[] { 0, 10, 25, 50, 75 })
        {
            int captured = percent;
            bool active = (int)Math.Round(_simulation.GovernorReserve * 100) == percent;
            var button = new Button
            {
                Content = new Label
                {
                    Text = percent + " %",
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    TextColor = active ? Color.White : UiPalette.Text,
                },
                Width = 58,
                Height = 32,
                Background = new SolidBrush(active ? UiPalette.PanelGood : UiPalette.Panel),
            };
            button.Click += (_, _) =>
            {
                _simulation.SetGovernorReserve(captured / 100.0);
                BuildUi();
            };
            steps.Widgets.Add(button);
        }

        box.Widgets.Add(steps);
        return box;
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
            Background = new SolidBrush(active ? UiPalette.Panel : UiPalette.Panel),
        };
        row.Widgets.Add(new Label
        {
            Text = loc[policy.NameKey],
            TextColor = active ? UiPalette.Good : UiPalette.Text,
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
