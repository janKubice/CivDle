using CivDle.Core.Sim;
using CivDle.Input;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Myra.Graphics2D.Brushes;
using Myra.Graphics2D.UI;

namespace CivDle.Screens;

/// <summary>
/// Guvernér jednoho sídla: co se v <b>tomhle</b> městě smí stavět.
///
/// <para>Proč to ve hře je: plán guvernéra byl jeden pro celou říši, takže
/// „hornické město" a „obilnice" se nedaly mít zároveň — každý zákaz platil
/// všude. Tohle je ta půlka, která z říše dělá říši a ne jedno velké město
/// rozstříkané po mapě.</para>
///
/// <para>Vlastní plán <b>nahradí</b> říšský, nesčítá se s ním; dokud si ho
/// sídlo nevezme, jede podle říšského a nic se pro něj nemění.</para>
///
/// <para>Vrstva: UI. Rozhodnutí („podle kterého plánu se staví na téhle
/// dlaždici") je v simulaci.</para>
/// </summary>
public sealed class SettlementPlanScreen : IScreen
{
    private const int PanelWidth = 560;

    private readonly ScreenManager _screens;
    private readonly Simulation _simulation;
    private readonly int _nameIndex;
    private readonly InputManager _input = new();
    private Desktop _desktop = null!;

    public SettlementPlanScreen(ScreenManager screens, Simulation simulation, int nameIndex)
    {
        _screens = screens;
        _simulation = simulation;
        _nameIndex = nameIndex;
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

    public void Draw(GameTime gameTime) => _desktop.Render();

    public void Dispose() => _screens.Loc.LanguageChanged -= BuildUi;

    private void BuildUi()
    {
        var loc = _screens.Loc;
        bool own = _simulation.HasOwnPlan(_nameIndex);
        var layout = new VerticalStackPanel { Spacing = 8, Width = PanelWidth };

        layout.Widgets.Add(new Label
        {
            Text = _screens.Content.SettlementNames[_nameIndex],
            TextColor = UiPalette.TextBright,
            HorizontalAlignment = HorizontalAlignment.Center,
        });

        layout.Widgets.Add(new Label
        {
            Text = loc[own ? "settlementPlan.own" : "settlementPlan.empire"],
            TextColor = own ? UiPalette.Good : UiPalette.Text,
            Wrap = true,
            Width = PanelWidth - 20,
        });

        if (!own)
        {
            layout.Widgets.Add(UiFactory.SmallButton(loc["settlementPlan.take"], () =>
            {
                _simulation.GiveOwnPlan(_nameIndex);
                BuildUi();
            }));

            Finish(layout);
            return;
        }

        layout.Widgets.Add(new Label
        {
            Text = loc["settlementPlan.hint"],
            TextColor = UiPalette.TextDim,
            Wrap = true,
            Width = PanelWidth - 20,
        });

        layout.Widgets.Add(CategoryRows());

        // Vrácení pod říšský plán je vedle kategorií schválně: je to nejčastější
        // věc, kterou hráč po experimentování chce, a schovaná by znamenala
        // ručně překlikat všechny kategorie zpátky.
        layout.Widgets.Add(UiFactory.SmallButton(loc["settlementPlan.drop"], () =>
        {
            _simulation.DropOwnPlan(_nameIndex);
            BuildUi();
        }));

        Finish(layout);
    }

    private Widget CategoryRows()
    {
        var loc = _screens.Loc;
        var plan = _simulation.PlanForSettlement(_nameIndex);

        var box = new VerticalStackPanel { Spacing = 5 };
        var row = new HorizontalStackPanel { Spacing = 6 };
        int inRow = 0;

        foreach (string category in AutoBuildCategories())
        {
            string captured = category;
            bool allowed = plan.AllowsCategory(category);
            var button = new Button
            {
                Content = new Label
                {
                    Text = loc[$"category.{category}"],
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    TextColor = allowed ? Color.White : UiPalette.TextDim,
                },
                Width = 132,
                Height = 30,
                Background = new SolidBrush(allowed ? UiPalette.PanelGood : UiPalette.Panel),
            };
            button.Click += (_, _) =>
            {
                var current = _simulation.PlanForSettlement(_nameIndex);
                current.SetCategoryAllowed(captured, !current.AllowsCategory(captured));
                BuildUi();
            };

            row.Widgets.Add(button);
            if (++inRow % 3 == 0)
            {
                box.Widgets.Add(row);
                row = new HorizontalStackPanel { Spacing = 6 };
            }
        }

        if (inRow % 3 != 0)
        {
            box.Widgets.Add(row);
        }

        return box;
    }

    /// <summary>
    /// Kategorie z dat, ne pevný seznam — jinak by budovy z modů neměly jak se
    /// do nabídky dostat.
    /// </summary>
    private IEnumerable<string> AutoBuildCategories() =>
        _screens.Content.Buildings.All
            .Where(b => b.AutoBuild)
            .Select(b => b.Category)
            .Distinct()
            .OrderBy(c => c, StringComparer.Ordinal);

    private void Finish(VerticalStackPanel layout)
    {
        layout.Widgets.Add(UiFactory.SmallButton(_screens.Loc["panel.close"], _screens.Pop));

        var panel = UiFactory.DarkPanel(layout);
        panel.HorizontalAlignment = HorizontalAlignment.Center;
        panel.VerticalAlignment = VerticalAlignment.Center;

        var root = new Panel();
        root.Widgets.Add(panel);
        _desktop = _screens.NewDesktop(root);
    }
}
