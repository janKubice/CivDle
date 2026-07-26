using CivDle.Core.Sim;
using CivDle.Input;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Myra.Graphics2D;
using Myra.Graphics2D.Brushes;
using Myra.Graphics2D.UI;

namespace CivDle.Screens;

/// <summary>
/// Detail budovy jako overlay: klik na budovu ji rozklikne, ukáže kategorii,
/// polohu a — je-li dostupná další úroveň — tlačítko na vylepšení (např. dům →
/// chalupa). Vylepšení proběhne na místě (stejný půdorys), panel se přestaví,
/// aby ukázal novou úroveň. Simulace mezitím stojí (jen vrchní obrazovka tiká).
/// </summary>
public sealed class BuildingInfoScreen : IScreen
{
    private readonly ScreenManager _screens;
    private readonly Simulation _simulation;
    private readonly int _buildingIndex;
    private readonly Action<int> _onStartMove;
    private readonly InputManager _input = new();
    private Desktop _desktop = null!;

    /// <param name="onStartMove">Zavolá se s indexem budovy, když hráč zvolí „Přesunout" (režim řídí herní obrazovka).</param>
    public BuildingInfoScreen(ScreenManager screens, Simulation simulation, int buildingIndex, Action<int> onStartMove)
    {
        _screens = screens;
        _simulation = simulation;
        _buildingIndex = buildingIndex;
        _onStartMove = onStartMove;
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
        var content = _screens.Content;
        ref readonly var instance = ref _simulation.Buildings[_buildingIndex];
        var def = content.Buildings[instance.DefIndex];

        var layout = new VerticalStackPanel
        {
            Spacing = 10,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };

        var header = new HorizontalStackPanel { Spacing = 8, HorizontalAlignment = HorizontalAlignment.Center };
        var icon = _screens.Sprites.Get($"building.{def.Id}");
        if (icon is not null)
        {
            header.Widgets.Add(UiFactory.Icon(icon, 34));
        }

        header.Widgets.Add(new Label
        {
            Text = loc[def.NameKey],
            VerticalAlignment = VerticalAlignment.Center,
        });
        layout.Widgets.Add(header);

        layout.Widgets.Add(new Label
        {
            Text = loc.Format("panel.building.info", loc[$"category.{def.Category}"], instance.X, instance.Y),
            TextColor = Color.LightGray,
            HorizontalAlignment = HorizontalAlignment.Center,
        });

        layout.Widgets.Add(UpgradeSection(instance.DefIndex));

        // Agency: přesunout jinam nebo zbourat (vrátí půl ceny). Obojí se ODEMYKÁ —
        // na začátku hráč jen staví, zásahy do hotového města přijdou později.
        var actions = new HorizontalStackPanel { Spacing = 8, HorizontalAlignment = HorizontalAlignment.Center };
        if (_simulation.IsFeatureUnlocked("move"))
        {
            actions.Widgets.Add(UiFactory.SmallButton(loc["building.move"], () =>
            {
                _onStartMove(_buildingIndex);
                _screens.Pop();
            }));
        }

        if (_simulation.IsFeatureUnlocked("demolish"))
        {
            actions.Widgets.Add(UiFactory.SmallButton(loc["building.demolish"], () =>
            {
                _simulation.TryDemolish(_buildingIndex);
                _screens.Pop();
            }));
        }

        if (actions.Widgets.Count > 0)
        {
            layout.Widgets.Add(actions);
        }

        layout.Widgets.Add(UiFactory.MenuButton(loc["panel.close"], _screens.Pop));

        var panel = UiFactory.DarkPanel(layout);
        panel.HorizontalAlignment = HorizontalAlignment.Center;
        panel.VerticalAlignment = VerticalAlignment.Center;

        var root = new Panel();
        root.Widgets.Add(panel);
        _desktop = new Desktop { Root = root };
    }

    private Widget UpgradeSection(int defIndex)
    {
        var loc = _screens.Loc;
        var content = _screens.Content;
        var def = content.Buildings[defIndex];

        var section = new VerticalStackPanel { Spacing = 5, HorizontalAlignment = HorizontalAlignment.Center };
        if (!def.HasUpgrade)
        {
            section.Widgets.Add(new Label
            {
                Text = loc["building.maxLevel"],
                TextColor = new Color(180, 190, 200),
                HorizontalAlignment = HorizontalAlignment.Center,
            });
            return section;
        }

        var next = content.Buildings[def.UpgradesToIndex];
        section.Widgets.Add(new Label
        {
            Text = loc.Format("panel.cost", CostFormat.Line(content, loc, def.UpgradeCost)),
            TextColor = Color.Gray,
            HorizontalAlignment = HorizontalAlignment.Center,
        });

        var button = new Button
        {
            Content = new Label
            {
                Text = loc.Format("building.upgrade", loc[next.NameKey]),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            },
            Padding = new Thickness(16, 6),
            HorizontalAlignment = HorizontalAlignment.Center,
            Background = new SolidBrush(new Color(48, 92, 72, 235)),
            Enabled = _simulation.CanUpgrade(_buildingIndex) == PlacementResult.Ok,
        };
        button.Click += (_, _) =>
        {
            if (_simulation.TryUpgradeBuilding(_buildingIndex) == PlacementResult.Ok)
            {
                BuildUi(); // budova je teď o úroveň výš — ukázat nový stav
            }
        };
        section.Widgets.Add(button);
        return section;
    }
}
