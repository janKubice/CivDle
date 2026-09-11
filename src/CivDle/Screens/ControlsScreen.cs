using CivDle.Input;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Myra.Graphics2D;
using Myra.Graphics2D.Brushes;
using Myra.Graphics2D.UI;

namespace CivDle.Screens;

/// <summary>
/// Ovládání: co je na které klávese a jak si to přendat.
///
/// <para>Proč to ve hře je: klávesy byly natvrdo v kódu a nikde vypsané.
/// Hráč, který si nepamatuje, že inspektor hrdel je na B, ho nemá jak najít —
/// a kdo má jiné rozložení klávesnice než QWERTY, nemá jak si ho přendat.</para>
///
/// <para>Přemapování je „klikni a zmáčkni": žádné rozbalovací seznamy se
/// stovkou kláves, u kterých hráč hledá tu svou. Escape čekání zruší, protože
/// právě Escape se přemapovat nedá a je to jediná klávesa, kterou má každý
/// v ruce jistě.</para>
///
/// <para>Vrstva: UI. Mapu drží <see cref="KeyMap"/>, ukládá se do nastavení.</para>
/// </summary>
public sealed class ControlsScreen : IScreen
{
    private const int PanelWidth = 620;

    private readonly ScreenManager _screens;
    private readonly InputManager _input = new();
    private Desktop _desktop = null!;

    /// <summary>Na kterou akci se právě čeká stisk; <c>null</c> = na žádnou.</summary>
    private GameAction? _waitingFor;

    /// <summary>Poslední hláška (konflikt, změněno).</summary>
    private string _note = string.Empty;
    private Color _noteColor = UiPalette.Text;

    public ControlsScreen(ScreenManager screens)
    {
        _screens = screens;
        BuildUi();
        _screens.Loc.LanguageChanged += BuildUi;
    }

    public bool IsOverlay => true;

    public void OnActivated() => _input.Resync();

    public void Update(GameTime gameTime)
    {
        _input.Update();

        if (_waitingFor is { } action)
        {
            CaptureKeyFor(action);
            return;
        }

        if (_input.WasPressed(Keys.Escape))
        {
            _screens.Pop();
        }
    }

    /// <summary>Vezme první stisknutou klávesu a přiřadí ji akci.</summary>
    private void CaptureKeyFor(GameAction action)
    {
        foreach (var key in Keyboard.GetState().GetPressedKeys())
        {
            if (!_input.WasPressed(key))
            {
                continue;
            }

            if (key == Keys.Escape)
            {
                _waitingFor = null;
                Note("controls.cancelled", good: false);
                return;
            }

            // Obsazená klávesa se odmítne i s tím, kdo ji drží. Tiše přebít
            // jinou akci by znamenalo, že hráč přijde o zkratku, o které
            // nevěděl, že ji tím ztrácí.
            if (_screens.Keys.ConflictOf(key, action) is { } taken)
            {
                _waitingFor = null;
                _note = _screens.Loc.Format("controls.conflict", KeyName(key), _screens.Loc[LabelKey(taken)]);
                _noteColor = UiPalette.Warn;
                BuildUi();
                return;
            }

            _screens.Keys.Rebind(action, key);
            _screens.SaveKeyBindings();
            _waitingFor = null;
            Note("controls.changed", good: true);
            return;
        }
    }

    public void Draw(GameTime gameTime) => _desktop.Render();

    public void Dispose() => _screens.Loc.LanguageChanged -= BuildUi;

    private void BuildUi()
    {
        var loc = _screens.Loc;
        var layout = new VerticalStackPanel { Spacing = 8, Width = PanelWidth };

        layout.Widgets.Add(new Label
        {
            Text = loc["controls.title"],
            TextColor = UiPalette.TextBright,
            HorizontalAlignment = HorizontalAlignment.Center,
        });

        layout.Widgets.Add(new Label
        {
            Text = loc["controls.hint"],
            TextColor = UiPalette.TextDim,
            Wrap = true,
            Width = PanelWidth - 20,
        });

        var list = new VerticalStackPanel { Spacing = 4, Width = PanelWidth - 20 };
        foreach (var action in Enum.GetValues<GameAction>())
        {
            list.Widgets.Add(Row(action));
        }

        // Myš a ovladač se nepřemapovávají, ale hráč musí vědět, že existují —
        // jinak nezjistí, že se kolečkem přibližuje a pravým se ruší nástroj.
        list.Widgets.Add(new Label { Text = " " });
        list.Widgets.Add(new Label { Text = loc["controls.fixed"], TextColor = UiPalette.Accent });
        foreach (string key in FixedControls)
        {
            list.Widgets.Add(new Label
            {
                Text = loc[key],
                TextColor = UiPalette.Text,
                Wrap = true,
                Width = PanelWidth - 60,
            });
        }

        layout.Widgets.Add(new ScrollViewer { Content = list, Width = PanelWidth - 10, Height = 360 });

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

        var buttons = new HorizontalStackPanel { Spacing = 8, HorizontalAlignment = HorizontalAlignment.Center };
        buttons.Widgets.Add(UiFactory.SmallButton(loc["controls.resetAll"], () =>
        {
            _screens.Keys.ResetAll();
            _screens.SaveKeyBindings();
            Note("controls.reset", good: true);
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

    private Widget Row(GameAction action)
    {
        var loc = _screens.Loc;
        var row = new HorizontalStackPanel
        {
            Spacing = 8,
            Width = PanelWidth - 50,
            Padding = new Thickness(8, 4),
            Background = new PanelBrush(UiPalette.Panel),
        };

        row.Widgets.Add(new Label
        {
            Text = loc[LabelKey(action)],
            TextColor = UiPalette.Text,
            VerticalAlignment = VerticalAlignment.Center,
            Width = 300,
        });

        string caption = _waitingFor == action
            ? loc["controls.press"]
            : KeyCaption(action);

        var button = UiFactory.SmallButton(caption, () =>
        {
            _waitingFor = action;
            _note = string.Empty;
            _input.Resync(); // klik nesmí sám sebe sebrat jako novou klávesu
            BuildUi();
        });
        row.Widgets.Add(button);

        // „Výchozí" jen u přemapovaných: u ostatních by to bylo tlačítko,
        // které nic nedělá.
        if (_screens.Keys.KeyFor(action) != KeyMap.DefaultFor(action))
        {
            row.Widgets.Add(UiFactory.SmallButton(loc["controls.reset"], () =>
            {
                _screens.Keys.Reset(action);
                _screens.SaveKeyBindings();
                BuildUi();
            }));
        }

        return row;
    }

    /// <summary>Klávesa akce i s alternativou („W nebo ↑").</summary>
    private string KeyCaption(GameAction action)
    {
        string primary = KeyName(_screens.Keys.KeyFor(action));
        return _screens.Keys.AlternateFor(action) is { } alternate
            ? _screens.Loc.Format("controls.orKey", primary, KeyName(alternate))
            : primary;
    }

    /// <summary>Jméno klávesy pro hráče — šipky jako šipky, ne jako „Up".</summary>
    private static string KeyName(Keys key) => key switch
    {
        Keys.Up => "↑",
        Keys.Down => "↓",
        Keys.Left => "←",
        Keys.Right => "→",
        Keys.OemComma => ",",
        Keys.OemPeriod => ".",
        Keys.Space => "Space",
        _ => key.ToString(),
    };

    private static string LabelKey(GameAction action) => $"controls.action.{char.ToLowerInvariant(action.ToString()[0])}{action.ToString()[1..]}";

    private void Note(string key, bool good)
    {
        _note = _screens.Loc[key];
        _noteColor = good ? UiPalette.Good : UiPalette.Text;
        BuildUi();
    }

    /// <summary>Co se přemapovat nedá, ale hráč to musí vědět.</summary>
    private static readonly string[] FixedControls =
    {
        "controls.fixed.escape",
        "controls.fixed.mouse",
        "controls.fixed.wheel",
        "controls.fixed.rightClick",
        "controls.fixed.drag",
        "controls.fixed.pad",
    };
}
