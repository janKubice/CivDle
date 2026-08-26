using CivDle.Core.Content;
using CivDle.Core.Sim;
using CivDle.Input;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Myra.Graphics2D.UI;

namespace CivDle.Screens;

/// <summary>
/// Okno anomálie: co to je, co stojí se tam vypravit a co už se přivezlo.
///
/// <para>Proč to ve hře je: místa na mapě existovala dřív, chybělo <b>okno
/// s cenou a odměnou</b>. Bez něj je anomálie jen ozdoba; s ním je to
/// rozhodnutí — utratit teď za něco, co se možná vyplatí.</para>
///
/// <para>Odměna se schválně neukazuje předem. Kdyby hráč viděl, co ho čeká,
/// nešlo by o výpravu, ale o nákup. Ukáže se, až se výprava vrátí.</para>
///
/// <para>Vrstva: UI. Rozhodnutí („jedna výprava naráz", „máš na to")
/// jsou v simulaci.</para>
/// </summary>
public sealed class ExpeditionScreen : IScreen
{
    private const int PanelWidth = 620;

    private readonly ScreenManager _screens;
    private readonly Simulation _simulation;
    private readonly PointOfInterest _poi;
    private readonly InputManager _input = new();
    private Desktop _desktop = null!;

    public ExpeditionScreen(ScreenManager screens, Simulation simulation, PointOfInterest poi)
    {
        _screens = screens;
        _simulation = simulation;
        _poi = poi;
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
        var def = _screens.Content.PointsOfInterest[_poi.KindIndex];
        var layout = new VerticalStackPanel { Spacing = 8, Width = PanelWidth };

        layout.Widgets.Add(new Label
        {
            Text = loc[def.NameKey],
            TextColor = UiPalette.TextBright,
            HorizontalAlignment = HorizontalAlignment.Center,
        });

        layout.Widgets.Add(new Label
        {
            Text = loc[def.DescriptionKey],
            TextColor = UiPalette.Text,
            Wrap = true,
            Width = PanelWidth - 20,
        });

        layout.Widgets.Add(new Label
        {
            Text = CostFormat.Line(_screens.Content, loc, def.Cost)
                + "   " + DurationFormat.Human(def.DurationTicks / Simulation.TicksPerSecond),
            TextColor = UiPalette.Accent,
        });

        // Odměna zůstane utajená: kdyby ji hráč viděl, nebyla by to výprava,
        // ale nákup.
        layout.Widgets.Add(new Label { Text = loc["poi.reward"], TextColor = UiPalette.TextDim });

        AddAction(layout, def);
        AddRelics(layout);

        layout.Widgets.Add(UiFactory.SmallButton(loc["panel.close"], _screens.Pop));

        var panel = UiFactory.DarkPanel(layout);
        panel.HorizontalAlignment = HorizontalAlignment.Center;
        panel.VerticalAlignment = VerticalAlignment.Center;

        var root = new Panel();
        root.Widgets.Add(panel);
        _desktop = _screens.NewDesktop(root);
    }

    private void AddAction(VerticalStackPanel layout, PoiDef def)
    {
        var loc = _screens.Loc;

        if (_simulation.ExpeditionRunning)
        {
            layout.Widgets.Add(new Label
            {
                Text = loc.Format("poi.running", (int)Math.Round(_simulation.ExpeditionProgress * 100)),
                TextColor = UiPalette.Accent,
            });
            layout.Widgets.Add(new Label { Text = loc["poi.busy"], TextColor = UiPalette.Warn });
            return;
        }

        var check = _simulation.CanSendExpedition(_poi.X, _poi.Y);
        if (check == PlacementResult.NotEnoughResources)
        {
            layout.Widgets.Add(new Label { Text = loc["poi.tooPoor"], TextColor = UiPalette.Bad });
            return;
        }

        layout.Widgets.Add(UiFactory.SmallButton(loc["poi.send"], () =>
        {
            if (_simulation.TrySendExpedition(_poi.X, _poi.Y) == PlacementResult.Ok)
            {
                _screens.Pop();
            }
        }));
    }

    /// <summary>
    /// Co už se přivezlo. Je to tady, a ne na vlastní obrazovce, protože se
    /// relikvie sbírají právě tudy — a jinde by na ně hráč nenarazil.
    /// </summary>
    private void AddRelics(VerticalStackPanel layout)
    {
        var loc = _screens.Loc;
        var relics = _simulation.Relics;

        layout.Widgets.Add(new Label { Text = loc["poi.relics"], TextColor = UiPalette.TextBright });

        if (relics.Count == 0)
        {
            layout.Widgets.Add(new Label { Text = loc["poi.noRelics"], TextColor = UiPalette.TextDim });
            return;
        }

        var catalog = _screens.Content.PointsOfInterest.Relics;
        for (int i = 0; i < relics.Count; i++)
        {
            layout.Widgets.Add(new Label
            {
                Text = loc[catalog[relics[i]].NameKey],
                TextColor = UiPalette.Good,
            });
        }
    }
}
