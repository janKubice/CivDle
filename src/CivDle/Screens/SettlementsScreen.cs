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

    /// <summary>Čísla k sídlům. Jeden seznam na celou obrazovku, přepisuje se při přestavbě.</summary>
    private readonly List<SettlementStat> _stats = new();

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

    public void OnActivated()
    {
        _input.Resync();
        BuildUi(); // návrat od guvernéra: sídlo mohlo dostat vlastní plán
    }

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
            // Čísla k sídlům se spočítají jedním průchodem zástavbou, ne jedním
            // na řádek — u říše o desítkách měst by to jinak bylo desetkrát
            // celé město.
            _simulation.DescribeSettlements(_stats);

            for (int i = 0; i < settlements.Count; i++)
            {
                list.Widgets.Add(SettlementRow(settlements[i], names, StatFor(settlements[i].NameIndex)));
            }
        }

        AddNpcCities(list);

        var scroll = new ScrollViewer
        {
            Content = list,
            // Původních 340×360 bylo na seznam měst s diplomacií málo: karty se
            // mačkaly a text se lámal doprostřed slova. Panel je teď tak velký,
            // jak velký ten obsah je.
            Height = 560,
            Width = 660,
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
                Width = 600,
                TextColor = UiPalette.TextDim,
            });
        }
    }

    /// <summary>
    /// Karta jednoho města: jméno, druh, vztah a stav spojení. Klik otevře
    /// <see cref="CityScreen"/>.
    ///
    /// <para>Diplomacie se dřív celá mačkala sem — tři tlačítka, čtyři řádky
    /// textu a pruh 430 px. Seznam má odpovědět na „koho znám a jak si stojíme",
    /// ne na „co teď udělám"; to druhé patří na obrazovku toho města.</para>
    /// </summary>
    private Widget CityRow(CivDle.Core.Content.Localization loc, CivDle.Core.Content.NpcCityCatalog catalog, NpcCity city)
    {
        var state = _simulation.NpcStateOf(city.Key);
        var archetype = catalog.Archetypes[city.ArchetypeIndex];

        var caption = new VerticalStackPanel { Spacing = 4 };
        caption.Widgets.Add(new Label
        {
            Text = CityScreen.NameOf(_screens.Content, city),
            TextColor = archetype.MapColor.ToXna(),
        });
        caption.Widgets.Add(new Label
        {
            Text = loc[archetype.NameKey],
            TextColor = Color.Gray,
        });

        if (state.Absorbed)
        {
            caption.Widgets.Add(new Label { Text = loc["npc.joined"], TextColor = UiPalette.Good });
        }
        else
        {
            caption.Widgets.Add(new Label
            {
                Text = loc.Format("npc.relation", state.Relation) + "   " + loc.Format("npc.trades", state.Trades),
                TextColor = Color.LightGray,
            });
            caption.Widgets.Add(new Label
            {
                Text = state.RoadLinked ? loc["npc.linked"] : loc["npc.noLink"],
                TextColor = state.RoadLinked ? UiPalette.Good : UiPalette.Warn,
            });
        }

        var button = new Button
        {
            Content = caption,
            Width = 600,
            Padding = new Thickness(14, 10),
            Background = new PanelBrush(UiPalette.Panel),
            Tooltip = loc["npc.openTip"],
        };
        button.Click += (_, _) => _screens.Push(new CityScreen(_screens, _simulation, _camera, city));
        return button;
    }

    private static Widget Framed(Widget content)
    {
        var panel = new Panel
        {
            Background = new PanelBrush(UiPalette.Panel),
            Padding = new Thickness(10, 8),
        };
        panel.Widgets.Add(content);
        return panel;
    }

    private Widget SettlementRow(Settlement settlement, IReadOnlyList<string> names, SettlementStat stat)
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

        // Řádek čísel: kolik se vejde lidí, kolik je práce, co se tam vyrábí.
        // Populaci na sídlo hra nezná (je to agregát pro celou říši), takže se
        // ukazuje kapacita — předstírat přesnost, kterou simulace nemá, by bylo
        // horší než ji neukázat.
        caption.Widgets.Add(new Label
        {
            Text = loc.Format(
                "panel.settlements.stats",
                CivDle.Core.Numbers.Format(stat.Housing),
                stat.Jobs,
                stat.Services),
            TextColor = UiPalette.Text,
        });

        if (stat.TopResourceIndex >= 0)
        {
            caption.Widgets.Add(new Label
            {
                Text = loc.Format(
                    "panel.settlements.makes",
                    loc[_screens.Content.Resources[stat.TopResourceIndex].NameKey]),
                TextColor = UiPalette.TextDim,
            });
        }

        var button = new Button
        {
            Content = caption,
            Width = 440,
            Padding = new Thickness(12, 8),
            Background = new PanelBrush(UiPalette.Panel),
        };
        button.Click += (_, _) => JumpTo(settlement);

        var row = new HorizontalStackPanel { Spacing = 8 };
        row.Widgets.Add(button);
        row.Widgets.Add(GovernorButton(settlement.NameIndex));
        return row;
    }

    /// <summary>
    /// Tlačítko „guvernér tohohle města".
    ///
    /// <para>Píše se do něj, jestli sídlo jede podle vlastního plánu, nebo podle
    /// říšského. Bez toho by hráč po nastavení jednoho města nepoznal, které
    /// z desítky ostatních má taky vlastní pravidla.</para>
    /// </summary>
    private Widget GovernorButton(int nameIndex)
    {
        var loc = _screens.Loc;
        bool own = _simulation.HasOwnPlan(nameIndex);

        return UiFactory.SmallButton(
            loc[own ? "panel.settlements.ownPlan" : "panel.settlements.empirePlan"],
            () => _screens.Push(new SettlementPlanScreen(_screens, _simulation, nameIndex)),
            loc["tip.settlementPlan"]);
    }

    private SettlementStat StatFor(int nameIndex)
    {
        for (int i = 0; i < _stats.Count; i++)
        {
            if (_stats[i].NameIndex == nameIndex)
            {
                return _stats[i];
            }
        }

        return new SettlementStat(nameIndex, 0, 0, 0, 0, -1);
    }

    private void JumpTo(Settlement settlement)
    {
        var world = new Vector2(settlement.CenterX * TerrainRenderer.TileSize, settlement.CenterY * TerrainRenderer.TileSize);
        _camera.CenterOn(world, JumpZoom);
        _screens.Pop();
    }
}
