using CivDle.Core.Content;
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
/// Obrazovka jednoho cizího města: kdo to je, jak si stojíte a co se s tím dá dělat.
///
/// <para>Proč vznikla: diplomacie se dřív mačkala do řádku v seznamu sídel. Pár
/// řádků a tři tlačítka v pruhu 430 px — nedalo se to přečíst a hlavně to
/// neodpovídalo tomu, jak se s městem zachází. Město je místo, ne položka
/// seznamu, takže se na něj dá i <b>kliknout na mapě</b> a otevře se tohle.</para>
///
/// <para>Vrstva: čte simulaci a posílá do ní příkazy hráče (dar, cesta, odkup).
/// Kameru posouvá jen na požádání („ukaž mi ho").</para>
/// </summary>
public sealed class CityScreen : IScreen
{
    private const float JumpZoom = 2.2f;
    private const int PanelWidth = 620;

    private readonly ScreenManager _screens;
    private readonly Simulation _simulation;
    private readonly Camera2D _camera;
    private readonly NpcCity _city;
    private readonly InputManager _input = new();
    private Desktop _desktop = null!;

    public CityScreen(ScreenManager screens, Simulation simulation, Camera2D camera, NpcCity city)
    {
        _screens = screens;
        _simulation = simulation;
        _camera = camera;
        _city = city;
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
        spriteBatch.Draw(_screens.WhitePixel, new Rectangle(0, 0, viewport.Width, viewport.Height), Color.Black * 0.6f);
        spriteBatch.End();

        _screens.RenderDesktop(this, _desktop);
    }

    public void Dispose()
    {
        _screens.Loc.LanguageChanged -= BuildUi;
        _screens.UiSettingsChanged -= BuildUi;
    }

    /// <summary>Jméno města ze společné množiny jmen sídel.</summary>
    public static string NameOf(GameContent content, in NpcCity city) =>
        content.SettlementNames[city.NameIndex % content.SettlementNames.Count];

    private void BuildUi()
    {
        var loc = _screens.Loc;
        var content = _screens.Content;
        var catalog = content.NpcCities;
        var archetype = catalog.Archetypes[_city.ArchetypeIndex];
        var state = _simulation.NpcStateOf(_city.Key);

        var layout = new VerticalStackPanel { Spacing = 10, Width = PanelWidth };

        layout.Widgets.Add(new Label
        {
            Text = NameOf(content, _city),
            TextColor = archetype.MapColor.ToXna(),
            HorizontalAlignment = HorizontalAlignment.Center,
        });
        layout.Widgets.Add(new Label
        {
            Text = loc[archetype.NameKey],
            TextColor = Color.LightGray,
            HorizontalAlignment = HorizontalAlignment.Center,
        });

        if (state.Absorbed)
        {
            layout.Widgets.Add(Note(loc["npc.joined"], new Color(150, 220, 150)));
            Finish(layout);
            return;
        }

        layout.Widgets.Add(StatusPanel(loc, catalog, state));
        layout.Widgets.Add(ActionsPanel(loc, catalog, state));

        // Obestavění je tichá cesta, o které se hráč jinak nedozví — patří sem,
        // ne do nápovědy někde stranou.
        layout.Widgets.Add(Note(loc["npc.surround"], new Color(150, 158, 175)));
        Finish(layout);
    }

    private void Finish(VerticalStackPanel layout)
    {
        var loc = _screens.Loc;
        var buttons = new HorizontalStackPanel
        {
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        buttons.Widgets.Add(UiFactory.SmallButton(loc["npc.showMe"], () =>
        {
            _camera.CenterOn(
                new Vector2(_city.X * TerrainRenderer.TileSize, _city.Y * TerrainRenderer.TileSize), JumpZoom);
            _screens.Pop();
        }));
        buttons.Widgets.Add(UiFactory.SmallButton(loc["panel.close"], _screens.Pop));
        layout.Widgets.Add(buttons);

        var panel = UiFactory.DarkPanel(layout);
        panel.HorizontalAlignment = HorizontalAlignment.Center;
        panel.VerticalAlignment = VerticalAlignment.Center;

        var root = new Panel();
        root.Widgets.Add(panel);
        _desktop = _screens.NewDesktop(root);
    }

    /// <summary>Vztah, obchody a spojení — tři čísla, na kterých diplomacie stojí.</summary>
    private Widget StatusPanel(Localization loc, NpcCityCatalog catalog, NpcCityState state)
    {
        var stack = new VerticalStackPanel { Spacing = 6, Width = PanelWidth - 40 };

        stack.Widgets.Add(new Label
        {
            Text = loc.Format("npc.relation", state.Relation),
            TextColor = Color.White,
        });

        // Pruh vztahu: číslo řekne kolik, pruh řekne „jak daleko ještě k odkupu".
        var bar = new ProgressBar(PanelWidth - 40, height: 10);
        bar.SetProgress(state.Relation / (double)Simulation.MaxRelation);
        stack.Widgets.Add(bar.Root);

        stack.Widgets.Add(new Label
        {
            Text = loc.Format("npc.trades", state.Trades),
            TextColor = Color.LightGray,
        });

        stack.Widgets.Add(new Label
        {
            Text = state.RoadLinked ? loc["npc.linked"] : loc["npc.noLink"],
            TextColor = state.RoadLinked ? new Color(150, 220, 150) : new Color(235, 170, 110),
            Wrap = true,
            Width = PanelWidth - 40,
        });

        if (state.Relation < catalog.BuyRelation)
        {
            stack.Widgets.Add(new Label
            {
                Text = loc.Format("npc.needRelation", catalog.BuyRelation),
                TextColor = new Color(170, 178, 195),
                Wrap = true,
                Width = PanelWidth - 40,
            });
        }

        return Framed(stack);
    }

    /// <summary>Co se dá udělat — s cenou u každé akce, ať se nemusí hádat.</summary>
    private Widget ActionsPanel(Localization loc, NpcCityCatalog catalog, NpcCityState state)
    {
        var content = _screens.Content;
        var stack = new VerticalStackPanel { Spacing = 8, Width = PanelWidth - 40 };

        stack.Widgets.Add(Action(
            loc["npc.gift"], CostFormat.Line(content, loc, catalog.GiftCost),
            () => _simulation.TryGiftCity(_city.Key)));

        if (!state.RoadLinked)
        {
            stack.Widgets.Add(Action(
                loc["npc.connect"], CostFormat.Line(content, loc, catalog.RoadCost),
                () => _simulation.TryConnectCity(_city.Key)));
        }

        if (state.Relation >= catalog.BuyRelation)
        {
            stack.Widgets.Add(Action(
                loc["npc.buy"], CostFormat.Line(content, loc, catalog.BuyCost),
                () => _simulation.TryBuyCity(_city.Key)));
        }

        return Framed(stack);
    }

    /// <summary>Řádek akce: tlačítko vlevo, cena vedle. Po kliknutí se panel překreslí.</summary>
    private Widget Action(string label, string cost, Action perform)
    {
        var row = new HorizontalStackPanel { Spacing = 10 };
        row.Widgets.Add(UiFactory.SmallButton(label, () =>
        {
            perform();
            BuildUi();
        }));
        row.Widgets.Add(new Label
        {
            Text = cost,
            TextColor = Color.Gray,
            VerticalAlignment = VerticalAlignment.Center,
        });
        return row;
    }

    private Label Note(string text, Color color) => new()
    {
        Text = text,
        TextColor = color,
        Wrap = true,
        Width = PanelWidth - 20,
        HorizontalAlignment = HorizontalAlignment.Center,
    };

    private static Widget Framed(Widget content)
    {
        var panel = new Panel
        {
            Background = new SolidBrush(new Color(30, 34, 44, 230)),
            Padding = new Thickness(14, 12),
        };
        panel.Widgets.Add(content);
        return panel;
    }
}
