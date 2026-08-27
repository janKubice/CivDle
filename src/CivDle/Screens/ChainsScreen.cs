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
/// Výrobní řetězce: odkud se která surovina bere a kam jde.
///
/// <para>Proč to ve hře je: budov je přes stovku a hráč, kterému dojdou prkna,
/// nemá jak zjistit, <b>co postavit</b>. Musel by procházet stavební menu
/// budovu po budově a číst recepty. Tohle je otočený pohled — začni u toho, co
/// potřebuješ.</para>
///
/// <para>Ukazují se i <b>zamčené</b> budovy, jen ztlumeně a s poznámkou. Skrýt
/// je by znamenalo odpovědět „tuhle surovinu nikdo nevyrábí", což je lež —
/// správná odpověď je „ještě jsi na to nepřišel".</para>
///
/// <para>Vrstva: UI nad odvozeným pohledem <see cref="ProductionChains"/>.
/// Nic nemění.</para>
/// </summary>
public sealed class ChainsScreen : IScreen
{
    private const int PanelWidth = 720;

    private readonly ScreenManager _screens;
    private readonly Simulation? _simulation;
    private readonly ProductionChains _chains;
    private readonly InputManager _input = new();
    private Desktop _desktop = null!;

    /// <summary>Kterou surovinu si hráč prohlíží; −1 = seznam.</summary>
    private int _selected = -1;

    /// <param name="simulation">
    /// Rozehraná hra, nebo <c>null</c> z hlavního menu. Slouží jen k tomu, aby
    /// se dalo říct „tohle už umíš" — bez ní se ukáže celý strom bez zámků.
    /// </param>
    public ChainsScreen(ScreenManager screens, Simulation? simulation = null)
    {
        _screens = screens;
        _simulation = simulation;
        _chains = new ProductionChains(screens.Content);
        BuildUi();
        _screens.Loc.LanguageChanged += BuildUi;
    }

    public bool IsOverlay => true;

    public void OnActivated() => _input.Resync();

    public void Update(GameTime gameTime)
    {
        _input.Update();
        if (!_input.WasPressed(Keys.Escape))
        {
            return;
        }

        // Escape vede o krok zpět, ne rovnou ven: hráč, který se proklikal do
        // suroviny, chce zpátky na seznam, ne do hry.
        if (_selected >= 0)
        {
            _selected = -1;
            BuildUi();
            return;
        }

        _screens.Pop();
    }

    public void Draw(GameTime gameTime) => _desktop.Render();

    public void Dispose() => _screens.Loc.LanguageChanged -= BuildUi;

    private void BuildUi()
    {
        var loc = _screens.Loc;
        var layout = new VerticalStackPanel { Spacing = 8, Width = PanelWidth };

        layout.Widgets.Add(new Label
        {
            Text = loc["chains.title"],
            TextColor = UiPalette.TextBright,
            HorizontalAlignment = HorizontalAlignment.Center,
        });

        layout.Widgets.Add(new Label
        {
            Text = loc[_selected < 0 ? "chains.hint" : "chains.backHint"],
            TextColor = UiPalette.TextDim,
            Wrap = true,
            Width = PanelWidth - 20,
        });

        var content = _selected < 0 ? ResourceList() : Detail(_selected);
        layout.Widgets.Add(new ScrollViewer { Content = content, Width = PanelWidth - 10, Height = 400 });

        var buttons = new HorizontalStackPanel { Spacing = 8, HorizontalAlignment = HorizontalAlignment.Center };
        if (_selected >= 0)
        {
            buttons.Widgets.Add(UiFactory.SmallButton(loc["chains.back"], () =>
            {
                _selected = -1;
                BuildUi();
            }));
        }

        buttons.Widgets.Add(UiFactory.SmallButton(loc["panel.close"], _screens.Pop));
        layout.Widgets.Add(buttons);

        var panel = UiFactory.DarkPanel(layout);
        panel.HorizontalAlignment = HorizontalAlignment.Center;
        panel.VerticalAlignment = VerticalAlignment.Center;

        var root = new Panel();
        root.Widgets.Add(panel);
        _desktop = _screens.NewDesktop(root);
    }

    /// <summary>Všechny suroviny jako tlačítka.</summary>
    private Widget ResourceList()
    {
        var loc = _screens.Loc;
        var list = new VerticalStackPanel { Spacing = 4, Width = PanelWidth - 20 };
        var resources = _screens.Content.Resources;

        for (int i = 0; i < resources.Count; i++)
        {
            int captured = i;
            var row = new HorizontalStackPanel { Spacing = 8 };

            var icon = _screens.Sprites.Get($"icon.{resources[i].Id}");
            if (icon is not null)
            {
                row.Widgets.Add(UiFactory.Icon(icon, 20));
            }

            row.Widgets.Add(new Label
            {
                Text = loc[resources[i].NameKey],
                TextColor = UiPalette.Text,
                VerticalAlignment = VerticalAlignment.Center,
                Width = 200,
            });

            // Rovnou v seznamu je vidět, jestli se surovina vyrábí, nebo sbírá
            // — je to nejčastější otázka a nemá kvůli ní být potřeba proklik.
            row.Widgets.Add(new Label
            {
                Text = _chains.IsRaw(captured)
                    ? loc["chains.raw"]
                    : loc.Format("chains.madeBy", _chains.ProducersOf(captured).Count),
                TextColor = _chains.IsRaw(captured) ? UiPalette.Accent : UiPalette.TextDim,
                VerticalAlignment = VerticalAlignment.Center,
                Width = 220,
            });

            row.Widgets.Add(UiFactory.SmallButton(loc["chains.show"], () =>
            {
                _selected = captured;
                BuildUi();
            }));

            list.Widgets.Add(row);
        }

        return list;
    }

    /// <summary>Detail jedné suroviny: kdo ji dělá, z čeho, a kdo ji spotřebuje.</summary>
    private Widget Detail(int resourceIndex)
    {
        var loc = _screens.Loc;
        var content = _screens.Content;
        var box = new VerticalStackPanel { Spacing = 6, Width = PanelWidth - 20 };

        box.Widgets.Add(new Label
        {
            Text = loc[content.Resources[resourceIndex].NameKey],
            TextColor = UiPalette.TextBright,
        });

        if (_chains.IsRaw(resourceIndex))
        {
            box.Widgets.Add(new Label
            {
                Text = loc["chains.rawHint"],
                TextColor = UiPalette.Accent,
                Wrap = true,
                Width = PanelWidth - 60,
            });
        }
        else
        {
            var ingredients = _chains.IngredientsOf(resourceIndex, content);
            if (ingredients.Count > 0)
            {
                box.Widgets.Add(new Label
                {
                    Text = loc.Format(
                        "chains.needs",
                        string.Join(", ", ingredients.Select(r => loc[content.Resources[r].NameKey]))),
                    TextColor = UiPalette.Warn,
                    Wrap = true,
                    Width = PanelWidth - 60,
                });
            }

            AddSteps(box, loc["chains.producers"], _chains.ProducersOf(resourceIndex));
        }

        AddSteps(box, loc["chains.consumers"], _chains.ConsumersOf(resourceIndex));
        return box;
    }

    private void AddSteps(VerticalStackPanel box, string caption, IReadOnlyList<ChainStep> steps)
    {
        var loc = _screens.Loc;
        box.Widgets.Add(new Label { Text = caption, TextColor = UiPalette.Accent });

        if (steps.Count == 0)
        {
            box.Widgets.Add(new Label { Text = loc["chains.none"], TextColor = UiPalette.TextDim });
            return;
        }

        foreach (var step in steps)
        {
            var def = _screens.Content.Buildings[step.BuildingIndex];

            // Zamčené se ukazují ztlumeně, ne skrytě: skrýt je by znamenalo
            // odpovědět „tuhle surovinu nikdo nevyrábí", což je lež.
            bool known = _simulation is null || _simulation.IsBuildingUnlocked(step.BuildingIndex);

            var row = new VerticalStackPanel
            {
                Spacing = 2,
                Width = PanelWidth - 60,
                Padding = new Thickness(10, 5),
                Background = new SolidBrush(UiPalette.Panel),
            };

            row.Widgets.Add(new Label
            {
                Text = loc[def.NameKey],
                TextColor = known ? UiPalette.Text : UiPalette.TextDim,
            });

            row.Widgets.Add(new Label
            {
                Text = loc.Format("chains.rate", $"{step.PerSecond:0.##}"),
                TextColor = UiPalette.TextDim,
            });

            if (!known)
            {
                row.Widgets.Add(new Label { Text = loc["chains.locked"], TextColor = UiPalette.Warn });
            }

            box.Widgets.Add(row);
        }
    }
}
