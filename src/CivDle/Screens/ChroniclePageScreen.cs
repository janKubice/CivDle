using CivDle.Core.Sim;
using CivDle.Input;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Myra.Graphics2D.UI;

namespace CivDle.Screens;

/// <summary>
/// Kronika běhu: pár vět o tom, jak město vzniklo — a tlačítko, které z nich
/// udělá obrázek.
///
/// <para>Proč to není součást statistik: graf odpovídá na „kolik", kronika na
/// „co se stalo". Hráč, který chce ukázat kamarádovi svoje město, nechce
/// poslat histogram.</para>
///
/// <para>Věty skládá <see cref="ChronicleWriter"/> v simulaci; tahle obrazovka
/// je jen přeloží a vykreslí. Text i obrázek jdou přes tentýž
/// <see cref="Capture.ChronicleText"/>, aby se nerozešly.</para>
///
/// <para>Vrstva: UI. Do simulace nesahá.</para>
/// </summary>
public sealed class ChroniclePageScreen : IScreen
{
    private const int PanelWidth = 720;

    private readonly ScreenManager _screens;
    private readonly Simulation _simulation;
    private readonly IReadOnlyList<ChronicleLine> _lines;
    private readonly InputManager _input = new();
    private Desktop _desktop = null!;

    private string _note = string.Empty;
    private Color _noteColor = UiPalette.Text;

    public ChroniclePageScreen(ScreenManager screens, Simulation simulation)
    {
        _screens = screens;
        _simulation = simulation;
        _lines = ChronicleWriter.Write(simulation.History, screens.Content.Chronicle);
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
        var layout = new VerticalStackPanel { Spacing = 10, Width = PanelWidth };

        layout.Widgets.Add(new Label
        {
            Text = loc["chronicle.page.title"],
            TextColor = UiPalette.TextBright,
            HorizontalAlignment = HorizontalAlignment.Center,
        });

        if (_lines.Count == 0)
        {
            layout.Widgets.Add(new Label
            {
                Text = loc["chronicle.page.empty"],
                TextColor = UiPalette.Text,
                Wrap = true,
                Width = PanelWidth - 20,
            });

            Finish(layout);
            return;
        }

        var list = new VerticalStackPanel { Spacing = 6, Width = PanelWidth - 20 };
        for (int i = 0; i < _lines.Count; i++)
        {
            list.Widgets.Add(new Label
            {
                Text = Capture.ChronicleText.Of(_lines[i], loc, _screens.Content),
                TextColor = UiPalette.Text,
                Wrap = true,
                Width = PanelWidth - 40,
            });
        }

        layout.Widgets.Add(new ScrollViewer { Content = list, Width = PanelWidth - 10, Height = 300 });
        layout.Widgets.Add(UiFactory.SmallButton(loc["chronicle.page.save"], SavePage));

        if (_note.Length > 0)
        {
            layout.Widgets.Add(new Label
            {
                Text = _note,
                TextColor = _noteColor,
                Wrap = true,
                Width = PanelWidth - 20,
            });
        }

        Finish(layout);
    }

    private void SavePage()
    {
        var loc = _screens.Loc;
        try
        {
            string path = new Capture.ChroniclePage(_screens).Save(
                _simulation.History, _lines, CityName(), _screens.Saves.ShareDirectory);
            _note = loc.Format("chronicle.page.saved", Path.GetFileName(path));
            _noteColor = UiPalette.Good;
        }
        catch (IOException)
        {
            // Plný disk ani zamčená složka nemají shodit rozehranou hru.
            _note = loc["chronicle.page.saveFailed"];
            _noteColor = UiPalette.Bad;
        }

        BuildUi();
    }

    private string CityName()
    {
        var settlements = _simulation.Settlements;
        return settlements.Count > 0
            ? _screens.Content.SettlementNames[settlements[0].NameIndex]
            : _screens.Loc["share.unnamed"];
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
