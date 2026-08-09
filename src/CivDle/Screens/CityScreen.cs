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

    /// <summary>Odkud může cesta vyjít: hráčova sídla, seřazená od nejbližšího k městu.</summary>
    private readonly List<(string Name, int X, int Y)> _origins = new();
    private int _originIndex;

    /// <summary>Poslední pokus o cestu selhal (mezi sídlem a městem není kudy).</summary>
    private bool _connectFailed;

    public CityScreen(ScreenManager screens, Simulation simulation, Camera2D camera, NpcCity city)
    {
        _screens = screens;
        _simulation = simulation;
        _camera = camera;
        _city = city;
        CollectOrigins();
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

    /// <summary>
    /// Sesbírá, odkud může cesta vyjít.
    ///
    /// <para>Dřív cesta k městu vůbec nevznikla — zaplatilo se a jen se nastavil
    /// příznak. Teď se opravdu staví, a tím pádem záleží, odkud: silnice přes
    /// půl říše z náhodného konce mapy není to, co hráč chtěl. Nejbližší sídlo
    /// je předvolba, protože je to skoro vždycky ta správná odpověď.</para>
    /// </summary>
    private void CollectOrigins()
    {
        var content = _screens.Content;
        var names = content.SettlementNames;
        var ranks = content.SettlementRanks;
        var loc = _screens.Loc;

        foreach (var settlement in _simulation.Settlements)
        {
            string name = names[settlement.NameIndex % names.Count];
            if (ranks.At(settlement.RankIndex) is { } rank)
            {
                name = $"{name} · {loc[rank.NameKey]}";
            }

            _origins.Add((name, (int)settlement.CenterX, (int)settlement.CenterY));
        }

        // Bez rozpoznaného sídla (úplný začátek) zůstává jediná volba: střed
        // města. Prázdná nabídka by tlačítko zbytečně zamkla.
        if (_origins.Count == 0)
        {
            _origins.Add((loc["npc.connectFromCentre"], _simulation.CityCenterX, _simulation.CityCenterY));
        }

        _origins.Sort((a, b) => Distance(a).CompareTo(Distance(b)));

        long Distance((string Name, int X, int Y) origin)
        {
            long dx = origin.X - _city.X;
            long dy = origin.Y - _city.Y;
            return (dx * dx) + (dy * dy);
        }
    }

    /// <summary>Postaví cestu z vybraného sídla a zapamatuje si, jestli to vyšlo.</summary>
    private void StartConnect()
    {
        var origin = _origins[Math.Clamp(_originIndex, 0, _origins.Count - 1)];
        _connectFailed = _simulation.TryConnectCity(_city.Key, origin.X, origin.Y) == DiplomacyResult.NoRoute;
    }

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
            layout.Widgets.Add(Note(loc["npc.joined"], UiPalette.Good));
            Finish(layout);
            return;
        }

        layout.Widgets.Add(StatusPanel(loc, catalog, state));
        layout.Widgets.Add(ActionsPanel(loc, catalog, state));

        // Obestavění je tichá cesta, o které se hráč jinak nedozví — patří sem,
        // ne do nápovědy někde stranou.
        layout.Widgets.Add(Note(
            loc.Format("npc.surround", _simulation.SurroundBuildingsFor(_city.Key)),
            UiPalette.TextDim));
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
            TextColor = state.RoadLinked ? UiPalette.Good : UiPalette.Warn,
            Wrap = true,
            Width = PanelWidth - 40,
        });

        if (state.Relation < catalog.BuyRelation)
        {
            stack.Widgets.Add(new Label
            {
                Text = loc.Format("npc.needRelation", catalog.BuyRelation),
                TextColor = UiPalette.Text,
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

        // Ceny čtou ze simulace, ne z katalogu: u většího města jsou vyšší
        // a hráč musí vidět tu skutečnou, ne základní z dat.
        stack.Widgets.Add(Action(
            loc["npc.gift"], _simulation.GiftCostFor(_city.Key),
            () => _simulation.TryGiftCity(_city.Key)));

        if (!state.RoadLinked)
        {
            stack.Widgets.Add(Action(
                loc["npc.connect"], catalog.RoadCost, StartConnect));

            // Odkud cesta povede. Nabídne se, teprve když je z čeho vybírat —
            // s jediným sídlem je otázka zbytečná.
            if (_origins.Count > 1)
            {
                stack.Widgets.Add(new Label
                {
                    Text = loc["npc.connectFrom"],
                    TextColor = UiPalette.Text,
                    Wrap = true,
                    Width = PanelWidth - 40,
                });

                var row = new HorizontalStackPanel { Spacing = 6 };
                for (int i = 0; i < _origins.Count; i++)
                {
                    int index = i;
                    var button = UiFactory.SmallButton(_origins[i].Name, () =>
                    {
                        _originIndex = index;
                        BuildUi();
                    });

                    // Zvolený zdroj se zvýrazní — bez toho hráč neví, co je
                    // vlastně nastavené, dokud cestu nepostaví.
                    if (index == _originIndex)
                    {
                        button.Background = new SolidBrush(UiPalette.PanelAccent);
                    }

                    row.Widgets.Add(button);
                }

                stack.Widgets.Add(row);
            }

            if (_connectFailed)
            {
                stack.Widgets.Add(new Label
                {
                    Text = loc["npc.noRoute"],
                    TextColor = UiPalette.Bad,
                    Wrap = true,
                    Width = PanelWidth - 40,
                });
            }
        }

        if (state.Relation >= catalog.BuyRelation)
        {
            stack.Widgets.Add(Action(
                loc["npc.buy"], _simulation.BuyCostFor(_city.Key),
                () => _simulation.TryBuyCity(_city.Key)));
        }

        return Framed(stack);
    }

    /// <summary>
    /// Řádek akce: tlačítko vlevo, cena vedle. Po kliknutí se panel překreslí.
    ///
    /// <para>Cena i tlačítko <b>mění barvu podle toho, jestli na ni hráč má</b>.
    /// Dřív byla cena vždycky šedá a jediný způsob, jak zjistit, že dar nejde,
    /// bylo kliknout a sledovat, že se nic nestalo.</para>
    /// </summary>
    private Widget Action(string label, IReadOnlyList<ResourceAmount> cost, Action perform)
    {
        bool affordable = _simulation.CanAfford(cost);

        var row = new HorizontalStackPanel { Spacing = 10 };
        var button = UiFactory.SmallButton(label, () =>
        {
            perform();
            BuildUi();
        });
        button.Enabled = affordable;
        row.Widgets.Add(button);

        row.Widgets.Add(new Label
        {
            Text = CostFormat.Line(_screens.Content, _screens.Loc, cost),
            TextColor = affordable ? UiPalette.Good : UiPalette.Bad,
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
            Background = new SolidBrush(UiPalette.Panel),
            Padding = new Thickness(14, 12),
        };
        panel.Widgets.Add(content);
        return panel;
    }
}
