using CivDle.Core.Content;
using CivDle.Core.Sim;
using CivDle.Input;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Myra.Graphics2D.UI;

namespace CivDle.Screens;

/// <summary>
/// Konec ukázky: co hráč postavil, jak to rostlo a co ho čeká dál.
///
/// <para><b>Proč zrovna tady:</b> ukázka musela někde skončit a nabízely se dvě
/// místa. Strop obyvatel je zeď — hráč narazí, nic nedostane a poslední, co si
/// odnese, je šedý nápis, že dál to nejde. Druhý Vzestup je opak: je to
/// <b>odměna</b>, na kterou si došel, a je to zároveň ta nejlepší chvíle
/// říct „a takhle to pokračuje".</para>
///
/// <para><b>Vzestup se opravdu provede.</b> Hráč si ho zasloužil a body
/// dostane; tahle obrazovka přijde až po něm. Sebrat mu odměnu a místo ní dát
/// nabídku ke koupi by z konce ukázky udělalo účet, ne tečku.</para>
///
/// <para><b>Nezavírá hru.</b> Kdo chce, zavře kartu a hraje dál do stropu —
/// demo, které po sobě zabouchne dveře, si nikdo nepamatuje v dobrém.</para>
///
/// <para>Vrstva: čte hotová čísla ze simulace a z obsahu, nic nepočítá ani
/// nemění.</para>
/// </summary>
public sealed class DemoFinaleScreen : IScreen
{
    private readonly ScreenManager _screens;
    private readonly RunSummary _summary;
    private readonly DemoScope _scope;
    private readonly InputManager _input = new();
    private Desktop _desktop = null!;

    public DemoFinaleScreen(ScreenManager screens, RunSummary summary)
    {
        _screens = screens;
        _summary = summary;
        _scope = DemoScope.Measure(screens.Content);
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

        // Tmavší závoj než u běžné bilance: tohle je tečka za celou ukázkou,
        // ne mezizastávka mezi dvěma běhy.
        spriteBatch.Draw(
            _screens.WhitePixel,
            new Rectangle(0, 0, viewport.Width, viewport.Height),
            Color.Black * 0.82f);
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

        var layout = new VerticalStackPanel
        {
            Spacing = 6,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };

        layout.Widgets.Add(Heading(loc["demo.finale.title"], UiPalette.Accent));
        layout.Widgets.Add(Dim(loc["demo.finale.subtitle"]));
        layout.Widgets.Add(new Label { Text = " " });

        // Nejdřív jeho město, teprve potom nabídka. Obráceně by to byl leták.
        layout.Widgets.Add(Heading(loc["demo.finale.yourCity"], UiPalette.TextBright));
        layout.Widgets.Add(Line(loc.Format(
            "run.peak", CivDle.Core.Numbers.Format(_summary.PeakPopulation))));
        layout.Widgets.Add(Line(loc.Format("run.buildings", _summary.Buildings)));
        if (_summary.Techs > 0)
        {
            layout.Widgets.Add(Line(loc.Format("run.techs", _summary.Techs)));
        }

        layout.Widgets.Add(Line(loc.Format(
            "run.duration", DurationFormat.Human(_summary.DurationSeconds))));

        layout.Widgets.Add(new Label { Text = " " });
        layout.Widgets.Add(Heading(loc["demo.finale.whatsNext"], UiPalette.TextBright));
        layout.Widgets.Add(Line(loc.Format("demo.finale.techs", _scope.Techs, _scope.TotalTechs)));
        layout.Widgets.Add(Line(loc.Format(
            "demo.finale.buildings", _scope.Buildings, _scope.TotalBuildings)));
        layout.Widgets.Add(Line(loc["demo.finale.layers"]));
        layout.Widgets.Add(Dim(loc["demo.ascendEnd"]));

        layout.Widgets.Add(new Label { Text = " " });

        // Časosběr se při Vzestupu právě uložil do sbírky — je to ta nejsilnější
        // vzpomínka, jakou si hráč z ukázky může odnést, takže se nabízí jako
        // první tlačítko.
        layout.Widgets.Add(UiFactory.MenuButton(
            loc["demo.finale.timelapse"], () => _screens.Push(new TimelapseListScreen(_screens))));
        layout.Widgets.Add(UiFactory.MenuButton(loc["demo.finale.keepPlaying"], _screens.Pop));

        var panel = UiFactory.DarkPanel(layout);
        panel.HorizontalAlignment = HorizontalAlignment.Center;
        panel.VerticalAlignment = VerticalAlignment.Center;

        var root = new Panel();
        root.Widgets.Add(panel);
        _desktop = _screens.NewDesktop(root);
    }

    private static Label Heading(string text, Color color) => new()
    {
        Text = text,
        HorizontalAlignment = HorizontalAlignment.Center,
        TextColor = color,
    };

    private static Label Line(string text) => new()
    {
        Text = text,
        HorizontalAlignment = HorizontalAlignment.Center,
        TextColor = Color.LightGray,
    };

    private static Label Dim(string text) => new()
    {
        Text = text,
        HorizontalAlignment = HorizontalAlignment.Center,
        TextColor = UiPalette.TextDim,
    };
}
