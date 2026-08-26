using CivDle.Core.Sim;
using CivDle.Input;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Myra.Graphics2D.UI;

namespace CivDle.Screens;

/// <summary>
/// Významné osobnosti města: kdo právě žije, co dokud žije zlepšuje, a na koho
/// už město jen vzpomíná.
///
/// <para>Proč vlastní obrazovka: bonus osobnosti je jediný ve hře, který
/// <b>sám od sebe zmizí</b>. Bez místa, kde je vidět „zbývá 20 %", by hráči
/// jednoho dne klesla výroba a neměl by kde zjistit proč — a toast, který
/// přeletěl před hodinou, mu to nepřipomene.</para>
///
/// <para>Vrstva: čte simulaci, nic v ní nemění.</para>
/// </summary>
public sealed class FiguresScreen : IScreen
{
    private const int PanelWidth = 560;

    private readonly ScreenManager _screens;
    private readonly Simulation _simulation;
    private readonly InputManager _input = new();
    private Desktop _desktop = null!;

    public FiguresScreen(ScreenManager screens, Simulation simulation)
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

    public void Draw(GameTime gameTime) => _desktop.Render();

    public void Dispose() => _screens.Loc.LanguageChanged -= BuildUi;

    private void BuildUi()
    {
        var loc = _screens.Loc;
        var figures = _simulation.Figures;
        var catalog = _screens.Content.Figures;

        var layout = new VerticalStackPanel { Spacing = 8, Width = PanelWidth };
        layout.Widgets.Add(new Label
        {
            Text = loc["figures.title"],
            TextColor = UiPalette.TextBright,
            HorizontalAlignment = HorizontalAlignment.Center,
        });

        if (figures.Remembered.Count == 0)
        {
            layout.Widgets.Add(new Label
            {
                Text = loc["figures.empty"],
                TextColor = UiPalette.Text,
                Wrap = true,
                Width = PanelWidth - 20,
            });

            Finish(layout);
            return;
        }

        var list = new VerticalStackPanel { Spacing = 6, Width = PanelWidth - 20 };

        // Žijící napřed: jen u nich se dá s bonusem ještě něco naplánovat.
        var living = figures.Living;
        for (int i = 0; i < living.Count; i++)
        {
            var def = catalog[living[i].FigureIndex];
            int percent = (int)Math.Round(figures.LifeLeft(living[i], _simulation.TickCount) * 100);
            list.Widgets.Add(Row(
                loc[$"figure.{def.Id}"],
                loc[$"figure.{def.Id}.desc"],
                $"{loc["figures.alive"]} — {loc.Format("figures.lifeLeft", percent)}",
                UiPalette.Good));
        }

        // Mrtví zůstávají v seznamu: socha na mapě je jejich, a hráč má právo
        // vědět, čí je.
        var remembered = figures.Remembered;
        for (int i = 0; i < remembered.Count; i++)
        {
            if (IsAlive(living, remembered[i]))
            {
                continue;
            }

            var def = catalog[remembered[i]];
            list.Widgets.Add(Row(
                loc[$"figure.{def.Id}"],
                loc[$"figure.{def.Id}.desc"],
                loc["figures.remembered"],
                UiPalette.TextDim));
        }

        layout.Widgets.Add(new ScrollViewer
        {
            Content = list,
            Width = PanelWidth - 10,
            Height = 320,
        });

        Finish(layout);
    }

    private static bool IsAlive(IReadOnlyList<LivingFigure> living, int figureIndex)
    {
        for (int i = 0; i < living.Count; i++)
        {
            if (living[i].FigureIndex == figureIndex)
            {
                return true;
            }
        }

        return false;
    }

    private static Widget Row(string name, string description, string status, Color statusColor)
    {
        var row = new VerticalStackPanel { Spacing = 2, Width = PanelWidth - 40 };
        row.Widgets.Add(new Label { Text = name, TextColor = UiPalette.TextBright });
        row.Widgets.Add(new Label
        {
            Text = description,
            TextColor = UiPalette.Text,
            Wrap = true,
            Width = PanelWidth - 60,
        });
        row.Widgets.Add(new Label { Text = status, TextColor = statusColor });
        return row;
    }

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
