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
/// Modlitby: hráč vybere prosbu, nastaví sílu a obětuje víru.
///
/// <para>Rozhodnutí, které tahle obrazovka nabízí, je jediné svého druhu v celé
/// hře: <b>jistá drobnost, nebo drahý hazard</b>. Proto je u každého stupně
/// vidět cena i šance vedle sebe — bez těch dvou čísel by volba síly byla
/// jen posuvník bez významu.</para>
///
/// <para>Modlitba mířená na místo se nepronese odsud: obrazovka se zavře
/// a hráč klikne na mapu. Cílit naslepo z menu by znamenalo přivolat meteorit
/// někam, kam hráč nevidí.</para>
/// </summary>
public sealed class PrayerScreen : IScreen
{
    private readonly ScreenManager _screens;
    private readonly Simulation _simulation;
    private readonly InputManager _input = new();

    /// <summary>Zavolá se s indexem modlitby a silou, když si má hráč vybrat cíl na mapě.</summary>
    private readonly Action<int, int> _startTargeting;

    private Desktop _desktop = null!;
    private int _strength = 1;

    public PrayerScreen(ScreenManager screens, Simulation simulation, Action<int, int> startTargeting)
    {
        _screens = screens;
        _simulation = simulation;
        _startTargeting = startTargeting;
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
        var faith = _screens.Content.Faith;

        var layout = new VerticalStackPanel
        {
            Spacing = 10,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        layout.Widgets.Add(new Label
        {
            Text = loc["faith.title"],
            HorizontalAlignment = HorizontalAlignment.Center,
            TextColor = UiPalette.TextBright,
        });

        if (!_simulation.FaithEnabled)
        {
            layout.Widgets.Add(new Label { Text = loc["faith.locked"], Wrap = true, Width = 420 });
            layout.Widgets.Add(UiFactory.MenuButton(loc["panel.close"], _screens.Pop));
            Finish(layout);
            return;
        }

        layout.Widgets.Add(StrengthRow(loc));

        var list = new VerticalStackPanel { Spacing = 8 };
        for (int i = 0; i < faith.Prayers.Count; i++)
        {
            list.Widgets.Add(PrayerRow(loc, faith, i));
        }

        layout.Widgets.Add(new ScrollViewer { Content = list, Height = 420, Width = 520 });
        layout.Widgets.Add(UiFactory.MenuButton(loc["panel.close"], _screens.Pop));
        Finish(layout);
    }

    private void Finish(Widget layout)
    {
        var panel = UiFactory.DarkPanel(layout);
        panel.HorizontalAlignment = HorizontalAlignment.Center;
        panel.VerticalAlignment = VerticalAlignment.Center;

        var root = new Panel();
        root.Widgets.Add(panel);
        _desktop = _screens.NewDesktop(root);
    }

    /// <summary>Volba síly — společná pro všechny modlitby, ať se nemusí opakovat u každé.</summary>
    private Widget StrengthRow(Localization loc)
    {
        var row = new HorizontalStackPanel { Spacing = 6, HorizontalAlignment = HorizontalAlignment.Center };
        row.Widgets.Add(new Label
        {
            Text = loc.Format("faith.strength", _strength),
            VerticalAlignment = VerticalAlignment.Center,
            TextColor = UiPalette.Text,
        });

        for (int level = 1; level <= Simulation.MaxPrayerStrength; level++)
        {
            int captured = level;
            bool active = _strength == level;
            var button = new Button
            {
                Content = new Label
                {
                    Text = level.ToString(),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    TextColor = active ? Color.White : UiPalette.Text,
                },
                Width = 40,
                Height = 30,
                Background = new SolidBrush(active ? UiPalette.Panel : UiPalette.Panel),
            };
            button.Click += (_, _) =>
            {
                _strength = captured;
                BuildUi();
            };
            row.Widgets.Add(button);
        }

        return row;
    }

    private Widget PrayerRow(Localization loc, FaithCatalog faith, int index)
    {
        var prayer = faith.Prayers[index];
        int cost = prayer.CostAt(_strength);
        bool affordable = _simulation.GetResource(faith.FaithResourceIndex) >= cost;

        var stack = new VerticalStackPanel { Spacing = 4, Width = 470 };
        stack.Widgets.Add(new Label { Text = loc[prayer.NameKey], TextColor = UiPalette.TextBright });
        stack.Widgets.Add(new Label { Text = loc[prayer.DescriptionKey], TextColor = Color.LightGray, Wrap = true });

        // Cena a šance vedle sebe — to je celé to rozhodnutí, a musí být vidět
        // dřív, než hráč klikne.
        stack.Widgets.Add(new Label
        {
            Text = loc.Format("faith.chance", (int)Math.Round(prayer.ChanceAt(_strength) * 100), cost),
            TextColor = affordable ? UiPalette.Good : UiPalette.Warn,
        });

        if (affordable)
        {
            stack.Widgets.Add(UiFactory.SmallButton(loc["faith.pray"], () => Pray(index, prayer)));
        }

        var panel = new Panel
        {
            Background = new SolidBrush(UiPalette.Panel),
            Border = new SolidBrush(UiPalette.TextBright * 0.5f),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(10, 8),
            Tooltip = loc["tip.faith"],
        };
        panel.Widgets.Add(stack);
        return panel;
    }

    private void Pray(int index, PrayerDef prayer)
    {
        if (prayer.NeedsTarget)
        {
            // Cíl si hráč ukáže na mapě — přivolat meteorit naslepo z menu by
            // byla past, ne rozhodnutí.
            _screens.Pop();
            _startTargeting(index, _strength);
            return;
        }

        _simulation.TryPray(index, _strength, _simulation.CityCenterX, _simulation.CityCenterY);
        BuildUi();
    }
}
