using CivDle.Core;
using CivDle.Core.Sim;
using CivDle.Input;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Myra.Graphics2D.UI;

namespace CivDle.Screens;

/// <summary>
/// Velké dílo — obrazovka, kam hráč sype přebytky.
///
/// <para>Ukazuje rozestavěný stupeň, kolik čeho chybí, a u každé suroviny
/// tlačítko „vsypat vše". Vkládá se ručně a po surovinách schválně: je to
/// rozhodnutí („tohle mi přebývá"), ne automatická daň z produkce, které by si
/// hráč nevšiml.</para>
///
/// <para>Vrstva: jen volá příkazy simulace, žádnou logiku nedrží.</para>
/// </summary>
public sealed class GrandWorkScreen : IScreen
{
    private const int PanelWidth = 620;

    private readonly ScreenManager _screens;
    private readonly Simulation _simulation;
    private readonly InputManager _input = new();
    private Desktop _desktop = null!;

    public GrandWorkScreen(ScreenManager screens, Simulation simulation)
    {
        _screens = screens;
        _simulation = simulation;
        BuildUi();
        _screens.Loc.LanguageChanged += BuildUi;
    }

    public bool IsOverlay => true;

    public void OnActivated() => _input.Resync();

    private void BuildUi()
    {
        var loc = _screens.Loc;
        var content = _screens.Content;
        var layout = new VerticalStackPanel { Spacing = 10, Width = PanelWidth };

        layout.Widgets.Add(new Label
        {
            Text = loc["grandwork.title"],
            TextColor = new Color(240, 205, 110),
            HorizontalAlignment = HorizontalAlignment.Center,
        });

        if (!_simulation.GrandWorkAvailable)
        {
            layout.Widgets.Add(Note(loc["grandwork.locked"], new Color(200, 170, 130)));
            Finish(layout);
            return;
        }

        layout.Widgets.Add(new Label
        {
            Text = loc.Format("grandwork.stage", _simulation.GrandWorkStage + 1),
            HorizontalAlignment = HorizontalAlignment.Center,
        });
        layout.Widgets.Add(Note(loc["grandwork.desc"], Color.LightGray));

        var bar = new ProgressBar(PanelWidth - 40, height: 12);
        bar.SetProgress(_simulation.GrandWorkProgress01());
        layout.Widgets.Add(bar.Root);

        // Řádek na surovinu: kolik chybí a čím to zasypat.
        foreach (var need in _simulation.GrandWorkCost())
        {
            int resourceIndex = need.ResourceIndex;
            double remaining = _simulation.GrandWorkRemaining(resourceIndex);
            double have = _simulation.GetResource(resourceIndex);

            var row = new HorizontalStackPanel { Spacing = 10, Width = PanelWidth - 40 };
            row.Widgets.Add(new Label
            {
                Text = $"{loc[content.Resources[resourceIndex].NameKey]}  "
                    + $"{Numbers.Format(need.Amount - remaining)} / {Numbers.Format(need.Amount)}",
                TextColor = remaining <= 0 ? new Color(150, 220, 150) : Color.White,
                VerticalAlignment = VerticalAlignment.Center,
            });

            if (remaining > 0)
            {
                var button = UiFactory.SmallButton(loc["grandwork.invest"], () =>
                {
                    _simulation.InvestInGrandWork(resourceIndex);
                    BuildUi();
                });

                // Zhasnuté tlačítko říká „nemáš co vsypat" beze slov.
                button.Enabled = have > 0;
                row.Widgets.Add(button);
            }

            layout.Widgets.Add(row);
        }

        Finish(layout);
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

    private Label Note(string text, Color color) => new()
    {
        Text = text,
        TextColor = color,
        Wrap = true,
        Width = PanelWidth - 20,
    };

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
}
