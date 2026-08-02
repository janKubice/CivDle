using CivDle.Core.Sim;
using CivDle.Input;
using CivDle.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Myra.Graphics2D;
using Myra.Graphics2D.Brushes;
using Myra.Graphics2D.UI;

namespace CivDle.Screens;

/// <summary>
/// Rozklikávací seznam osad (overlay nad hrou): každá osada je řádek s jménem
/// a počtem budov; kliknutí přenese kameru na její těžiště. Užitečné, až jich
/// bude víc, než se vejde na obrazovku. Simulace mezitím stojí (jen vrchní
/// obrazovka tiká). Čistě čte simulaci, zapisuje jen do kamery (render vrstva).
/// </summary>
public sealed class SettlementsScreen : IScreen
{
    private const float JumpZoom = 2.2f;

    private readonly ScreenManager _screens;
    private readonly Simulation _simulation;
    private readonly Camera2D _camera;
    private readonly InputManager _input = new();
    private Desktop _desktop = null!;

    public SettlementsScreen(ScreenManager screens, Simulation simulation, Camera2D camera)
    {
        _screens = screens;
        _simulation = simulation;
        _camera = camera;
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
        var names = _screens.Content.SettlementNames;
        var settlements = _simulation.Settlements;

        var list = new VerticalStackPanel { Spacing = 6 };
        if (settlements.Count == 0)
        {
            list.Widgets.Add(new Label
            {
                Text = loc["panel.settlements.empty"],
                TextColor = Color.LightGray,
            });
        }
        else
        {
            for (int i = 0; i < settlements.Count; i++)
            {
                list.Widgets.Add(SettlementRow(settlements[i], names));
            }
        }

        AddNpcCities(list);

        var scroll = new ScrollViewer
        {
            Content = list,
            Height = 340,
            Width = 360,
        };

        var layout = new VerticalStackPanel
        {
            Spacing = 12,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        layout.Widgets.Add(new Label
        {
            Text = loc["hud.settlements"],
            HorizontalAlignment = HorizontalAlignment.Center,
        });
        layout.Widgets.Add(scroll);
        layout.Widgets.Add(UiFactory.MenuButton(loc["panel.close"], _screens.Pop));

        var panel = UiFactory.DarkPanel(layout);
        panel.HorizontalAlignment = HorizontalAlignment.Center;
        panel.VerticalAlignment = VerticalAlignment.Center;

        var root = new Panel();
        root.Widgets.Add(panel);
        _desktop = _screens.NewDesktop(root);
    }

    /// <summary>
    /// Sousedé pod vlastními sídly: cizí města, se kterými se obchoduje.
    /// Vztah je dlouhá veličina — roste desítky minut — takže patří sem, kde se
    /// hráč dívá na mapu jako celek, ne do rychlého HUD.
    /// </summary>
    /// <summary>
    /// Objevená cizí města: co s nimi hráč zažil a co s nimi může udělat.
    ///
    /// <para>Nahradilo to pevný seznam „sousedů". Soused byl řádek v tabulce,
    /// který nikde nestál — město je bod na mapě, ke kterému se dá dojít,
    /// postavit k němu cestu, obchodovat s ním, koupit ho, nebo ho prostě
    /// obestavět, až sroste s tvým.</para>
    ///
    /// <para>Ukazují se jen ta OBJEVENÁ: seznam nesmí prozradit, co je za mlhou.</para>
    /// </summary>
    private void AddNpcCities(VerticalStackPanel list)
    {
        var loc = _screens.Loc;
        if (!_simulation.NpcCitiesEnabled)
        {
            return;
        }

        list.Widgets.Add(new Label { Text = loc["npc.title"], TextColor = UiFactory.Accent });

        var catalog = _screens.Content.NpcCities;
        bool any = false;
        foreach (var city in _simulation.CitiesNear(
                     _simulation.CityCenterX, _simulation.CityCenterY, NpcCityMap.CellTiles * 3))
        {
            if (!_simulation.IsCityDiscovered(city))
            {
                continue;
            }

            any = true;
            list.Widgets.Add(CityRow(loc, catalog, city));
        }

        if (!any)
        {
            list.Widgets.Add(new Label
            {
                Text = loc["npc.none"],
                Wrap = true,
                Width = 420,
                TextColor = new Color(150, 160, 175),
            });
        }
    }

    private Widget CityRow(CivDle.Core.Content.Localization loc, CivDle.Core.Content.NpcCityCatalog catalog, NpcCity city)
    {
        var state = _simulation.NpcStateOf(city.Key);
        var archetype = catalog.Archetypes[city.ArchetypeIndex];

        var stack = new VerticalStackPanel { Spacing = 3, Width = 430 };
        stack.Widgets.Add(new Label
        {
            Text = catalog.Names[city.NameIndex % catalog.Names.Count] + " — " + loc[archetype.NameKey],
            TextColor = archetype.MapColor.ToXna(),
        });

        if (state.Absorbed)
        {
            stack.Widgets.Add(new Label { Text = loc["npc.joined"], TextColor = new Color(150, 220, 150) });
            return Framed(stack);
        }

        stack.Widgets.Add(new Label
        {
            Text = loc.Format("npc.relation", state.Relation) + "   " + loc.Format("npc.trades", state.Trades),
            TextColor = Color.LightGray,
        });

        stack.Widgets.Add(new Label
        {
            Text = state.RoadLinked ? loc["npc.linked"] : loc["npc.noLink"],
            TextColor = state.RoadLinked ? new Color(150, 220, 150) : new Color(235, 170, 110),
            Wrap = true,
        });

        var buttons = new HorizontalStackPanel { Spacing = 6 };
        buttons.Widgets.Add(UiFactory.SmallButton(loc["npc.gift"], () =>
        {
            _simulation.TryGiftCity(city.Key);
            BuildUi();
        }));

        if (!state.RoadLinked)
        {
            buttons.Widgets.Add(UiFactory.SmallButton(loc["npc.connect"], () =>
            {
                _simulation.TryConnectCity(city.Key);
                BuildUi();
            }));
        }

        if (state.Relation >= catalog.BuyRelation)
        {
            buttons.Widgets.Add(UiFactory.SmallButton(loc["npc.buy"], () =>
            {
                _simulation.TryBuyCity(city.Key);
                BuildUi();
            }));
        }
        else
        {
            stack.Widgets.Add(new Label
            {
                Text = loc.Format("npc.needRelation", catalog.BuyRelation),
                TextColor = new Color(160, 168, 184),
            });
        }

        stack.Widgets.Add(buttons);
        stack.Widgets.Add(new Label { Text = loc["npc.surround"], TextColor = new Color(140, 148, 165), Wrap = true });
        return Framed(stack);
    }

    private static Widget Framed(Widget content)
    {
        var panel = new Panel
        {
            Background = new Myra.Graphics2D.Brushes.SolidBrush(new Color(30, 34, 44, 230)),
            Padding = new Thickness(10, 8),
        };
        panel.Widgets.Add(content);
        return panel;
    }

    private Button SettlementRow(Settlement settlement, IReadOnlyList<string> names)
    {
        var loc = _screens.Loc;
        var caption = new VerticalStackPanel { Spacing = 2 };
        caption.Widgets.Add(new Label
        {
            Text = names[settlement.NameIndex],
            TextColor = UiFactory.Accent,
        });
        // Stupeň před počtem: „Městečko · 24 budov" je čitelnější než holé číslo
        // a je to jediné místo, kde hráč vidí hierarchii sídel pohromadě.
        var rank = _screens.Content.SettlementRanks.At(settlement.RankIndex);
        caption.Widgets.Add(new Label
        {
            Text = rank is null
                ? loc.Format("panel.settlements.count", settlement.BuildingCount)
                : loc.Format("panel.settlements.rank", loc[rank.NameKey], settlement.BuildingCount),
            TextColor = Color.Gray,
        });

        var button = new Button
        {
            Content = caption,
            Width = 336,
            Padding = new Thickness(12, 8),
            Background = new SolidBrush(new Color(38, 48, 64, 235)),
        };
        button.Click += (_, _) => JumpTo(settlement);
        return button;
    }

    private void JumpTo(Settlement settlement)
    {
        var world = new Vector2(settlement.CenterX * TerrainRenderer.TileSize, settlement.CenterY * TerrainRenderer.TileSize);
        _camera.CenterOn(world, JumpZoom);
        _screens.Pop();
    }
}
