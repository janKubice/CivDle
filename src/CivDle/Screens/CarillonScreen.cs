using CivDle.Core.Sim;
using CivDle.Input;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Myra.Graphics2D.UI;

namespace CivDle.Screens;

/// <summary>
/// Notový editor zvonohry: osm tónů, které město zahraje, když se slaví.
///
/// <para>Proč to ve hře je: idle hra je hodiny tiché práce s čísly, kde je
/// každé rozhodnutí buď lepší, nebo horší. Melodie je jediná věc, u které
/// <b>není správná odpověď</b> — a proto je jediná, která je hráčova.</para>
///
/// <para>Editor je záměrně hloupý: osm sloupců, v každém tlačítko, které tón
/// posune o stupeň výš a nahoře přeteče do pauzy. Notová osnova by byla
/// hezčí a nikdo by ji neuměl ovládat myší za tři vteřiny.</para>
///
/// <para>Vrstva: UI. Mění melodii v simulaci (to je hráčův příkaz) a přehrává
/// ji přes audio vrstvu; herní logiku nedrží.</para>
/// </summary>
public sealed class CarillonScreen : IScreen
{
    private const int PanelWidth = 620;

    private readonly ScreenManager _screens;
    private readonly Simulation _simulation;
    private readonly Audio.CarillonPlayer _player;
    private readonly InputManager _input = new();
    private Desktop _desktop = null!;

    public CarillonScreen(ScreenManager screens, Simulation simulation, Audio.CarillonPlayer player)
    {
        _screens = screens;
        _simulation = simulation;
        _player = player;
        BuildUi();
        _screens.Loc.LanguageChanged += BuildUi;
    }

    public bool IsOverlay => true;

    public void OnActivated() => _input.Resync();

    public void Update(GameTime gameTime)
    {
        _input.Update();

        // Ukázka běží i s otevřeným panelem — hráč musí slyšet, co skládá.
        _player.Update(gameTime.ElapsedGameTime.TotalSeconds, _simulation);

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
            Text = loc["carillon.title"],
            TextColor = UiPalette.TextBright,
            HorizontalAlignment = HorizontalAlignment.Center,
        });

        layout.Widgets.Add(new Label
        {
            Text = loc["carillon.desc"],
            TextColor = UiPalette.Text,
            Wrap = true,
            Width = PanelWidth - 20,
        });

        var notes = new HorizontalStackPanel
        {
            Spacing = 6,
            HorizontalAlignment = HorizontalAlignment.Center,
        };

        for (int i = 0; i < Carillon.NoteCount; i++)
        {
            notes.Widgets.Add(NoteButton(i));
        }

        layout.Widgets.Add(notes);

        var buttons = new HorizontalStackPanel
        {
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        buttons.Widgets.Add(UiFactory.SmallButton(loc["carillon.play"], PlayPreview));
        buttons.Widgets.Add(UiFactory.SmallButton(loc["carillon.reset"], () =>
        {
            _simulation.Carillon.Reset();
            BuildUi();
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

    /// <summary>
    /// Jeden tón. Klik ho posune o stupeň výš; z nejvyššího se přehoupne do
    /// pauzy a z pauzy zpátky na nejnižší — celý rozsah je tak na dosah bez
    /// druhého tlačítka.
    /// </summary>
    private Widget NoteButton(int index)
    {
        var carillon = _simulation.Carillon;
        int note = carillon[index];

        return UiFactory.SmallButton(
            note == Carillon.Rest ? _screens.Loc["carillon.rest"] : (note + 1).ToString(),
            () =>
            {
                carillon.Set(index, Next(note));

                // Ozve se hned po kliknutí: bez toho by hráč skládal poslepu
                // a poznal by výsledek až po celé melodii.
                _screens.Sounds.PlayBell(carillon[index]);
                BuildUi();
            });
    }

    private static int Next(int note) => note + 1 >= Carillon.Degrees ? Carillon.Rest : note + 1;

    private void PlayPreview() => _player.Play(_simulation.Carillon.Notes);
}
