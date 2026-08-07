using CivDle.Core.Content.Mods;
using CivDle.Input;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Myra.Graphics2D;
using Myra.Graphics2D.Brushes;
using Myra.Graphics2D.UI;

namespace CivDle.Screens;

/// <summary>
/// Editor modů: hráč si přímo ve hře složí vlastní surovinu a budovu, nechá si
/// mod zkontrolovat a uloží ho do <c>mods/</c>.
///
/// <para>Klíčová je tady <b>kontrola</b>: tlačítko „Zkontrolovat" pustí mod
/// skutečným <see cref="ModValidator"/>, tedy tímtéž loaderem, kterým se
/// spouští hra. Autor tak vidí chybu ve chvíli, kdy ji udělal — ne až mu příště
/// nenaběhne hra, kdy už mod nemá kde vypnout.</para>
///
/// <para>Formulář je záměrně jen nad <see cref="ModDraft"/>: skládat JSON
/// v obrazovce by znamenalo, že se to, co editor ukládá, nedá otestovat bez
/// spuštění hry.</para>
///
/// <para>Vrstva: UI. Model i zápis jsou v jádře.</para>
/// </summary>
public sealed class ModEditorScreen : IScreen
{
    private const int PanelWidth = 780;
    private const int FieldWidth = 200;

    private readonly ScreenManager _screens;
    private readonly InputManager _input = new();
    private Desktop _desktop = null!;

    private readonly ModDraft _draft = new("muj_mod", "Můj mod");

    /// <summary>Poslední výsledek kontroly — prázdné, dokud hráč nezkontroluje.</summary>
    private string _status = string.Empty;
    private bool _statusOk;

    public ModEditorScreen(ScreenManager screens)
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
        spriteBatch.Draw(_screens.WhitePixel, new Rectangle(0, 0, viewport.Width, viewport.Height), Color.Black * 0.7f);
        spriteBatch.End();

        _screens.RenderDesktop(this, _desktop);
    }

    public void Dispose() => _screens.Loc.LanguageChanged -= BuildUi;

    private static string ModsDirectory => Path.Combine(AppContext.BaseDirectory, "mods");

    private static string DataDirectory => Path.Combine(AppContext.BaseDirectory, "data");

    private void BuildUi()
    {
        var loc = _screens.Loc;
        var layout = new VerticalStackPanel
        {
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };

        layout.Widgets.Add(new Label
        {
            Text = loc["modedit.title"],
            TextColor = new Color(240, 205, 110),
            HorizontalAlignment = HorizontalAlignment.Center,
        });
        layout.Widgets.Add(Note(loc["modedit.intro"], Color.LightGray));

        var body = new VerticalStackPanel { Spacing = 10 };
        body.Widgets.Add(ModSection());
        body.Widgets.Add(ResourceSection());
        body.Widgets.Add(BuildingSection());

        layout.Widgets.Add(new ScrollViewer { Content = body, Height = 400, Width = PanelWidth });

        if (_status.Length > 0)
        {
            layout.Widgets.Add(Note(_status, _statusOk ? new Color(150, 220, 150) : new Color(235, 150, 130)));
        }

        var buttons = new HorizontalStackPanel { Spacing = 8, HorizontalAlignment = HorizontalAlignment.Center };
        buttons.Widgets.Add(UiFactory.SmallButton(loc["modedit.check"], CheckMod));
        buttons.Widgets.Add(UiFactory.SmallButton(loc["modedit.save"], SaveMod));
        buttons.Widgets.Add(UiFactory.SmallButton(loc["panel.close"], _screens.Pop));
        layout.Widgets.Add(buttons);

        var panel = UiFactory.DarkPanel(layout);
        panel.HorizontalAlignment = HorizontalAlignment.Center;
        panel.VerticalAlignment = VerticalAlignment.Center;

        var root = new Panel();
        root.Widgets.Add(panel);
        _desktop = _screens.NewDesktop(root);
    }

    // ----- sekce -----

    private Widget ModSection()
    {
        var loc = _screens.Loc;
        var box = Section(loc["modedit.modSection"]);
        box.Widgets.Add(TextField(loc["modedit.id"], _draft.Id, v => _draft.Id = Slug(v)));
        box.Widgets.Add(TextField(loc["modedit.name"], _draft.Name, v => _draft.Name = v));
        box.Widgets.Add(TextField(loc["modedit.version"], _draft.Version, v => _draft.Version = v));
        return box;
    }

    private Widget ResourceSection()
    {
        var loc = _screens.Loc;
        var box = Section(loc.Format("modedit.resourceSection", _draft.Resources.Count));

        foreach (var resource in _draft.Resources)
        {
            box.Widgets.Add(ListRow($"{resource.Name}  ({resource.Id})", () =>
            {
                _draft.Resources.Remove(resource);
                BuildUi();
            }));
        }

        string id = string.Empty;
        string name = string.Empty;
        var row = new HorizontalStackPanel { Spacing = 6 };
        row.Widgets.Add(Box(loc["modedit.id"], v => id = Slug(v)));
        row.Widgets.Add(Box(loc["modedit.name"], v => name = v));
        row.Widgets.Add(UiFactory.SmallButton(loc["modedit.add"], () =>
        {
            if (id.Length == 0 || name.Length == 0)
            {
                Fail(loc["modedit.needIdAndName"]);
                return;
            }

            _draft.Resources.Add(new ResourceDraft(id, name));
            BuildUi();
        }));
        box.Widgets.Add(row);
        return box;
    }

    private Widget BuildingSection()
    {
        var loc = _screens.Loc;
        var box = Section(loc.Format("modedit.buildingSection", _draft.Buildings.Count));

        foreach (var building in _draft.Buildings)
        {
            box.Widgets.Add(ListRow($"{building.Name}  ({building.Id})", () =>
            {
                _draft.Buildings.Remove(building);
                BuildUi();
            }));
        }

        string id = string.Empty;
        string name = string.Empty;
        string costResource = "wood";
        int costAmount = 20;
        int workers = 1;

        var first = new HorizontalStackPanel { Spacing = 6 };
        first.Widgets.Add(Box(loc["modedit.id"], v => id = Slug(v)));
        first.Widgets.Add(Box(loc["modedit.name"], v => name = v));
        box.Widgets.Add(first);

        var second = new HorizontalStackPanel { Spacing = 6 };
        second.Widgets.Add(Box(loc["modedit.costResource"], v => costResource = Slug(v), "wood"));
        second.Widgets.Add(Box(loc["modedit.costAmount"], v => costAmount = ParseInt(v, 20), "20"));
        second.Widgets.Add(Box(loc["modedit.workers"], v => workers = ParseInt(v, 1), "1"));
        second.Widgets.Add(UiFactory.SmallButton(loc["modedit.add"], () =>
        {
            if (id.Length == 0 || name.Length == 0)
            {
                Fail(loc["modedit.needIdAndName"]);
                return;
            }

            _draft.Buildings.Add(new BuildingDraft(
                id,
                name,
                Description: name,
                WorkerSlots: workers,
                BuildCost: new[] { new AmountDraft(costResource, costAmount) }));
            BuildUi();
        }));
        box.Widgets.Add(second);

        box.Widgets.Add(Note(loc["modedit.buildingHint"], new Color(150, 160, 175)));
        return box;
    }

    // ----- akce -----

    private void CheckMod()
    {
        var loc = _screens.Loc;
        if (_draft.Id.Length == 0)
        {
            Fail(loc["modedit.needIdAndName"]);
            return;
        }

        // Kontroluje se do dočasné složky, ne do mods/: hráč si má moct zkusit
        // i mod, který se nenačte, aniž by tím rozbil příští start hry.
        string temporary = Path.Combine(Path.GetTempPath(), "civdle-modcheck-" + Guid.NewGuid().ToString("N"));
        try
        {
            string directory = _draft.WriteTo(temporary);
            var check = ModValidator.Check(DataDirectory, directory);
            _statusOk = check.Ok;
            _status = check.Message;
        }
        finally
        {
            TryDelete(temporary);
        }

        BuildUi();
    }

    private void SaveMod()
    {
        var loc = _screens.Loc;

        // Uložit se smí až to, co projde kontrolou — vadný mod v mods/ znamená,
        // že hra příště nenaběhne.
        CheckMod();
        if (!_statusOk)
        {
            return;
        }

        try
        {
            string directory = _draft.WriteTo(ModsDirectory);
            _status = loc.Format("modedit.saved", directory);
            _statusOk = true;
        }
        catch (IOException ex)
        {
            Fail(ex.Message);
        }
        catch (UnauthorizedAccessException ex)
        {
            Fail(ex.Message);
        }

        BuildUi();
    }

    private void Fail(string message)
    {
        _status = message;
        _statusOk = false;
        BuildUi();
    }

    private static void TryDelete(string directory)
    {
        try
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
        catch (IOException)
        {
            // Dočasná složka v temp nikomu nevadí; smaže ji systém.
        }
    }

    // ----- drobné stavební kameny -----

    private static VerticalStackPanel Section(string title)
    {
        var box = new VerticalStackPanel
        {
            Spacing = 5,
            Width = PanelWidth - 24,
            Padding = new Thickness(12, 8),
            Background = new SolidBrush(new Color(30, 34, 46, 235)),
        };
        box.Widgets.Add(new Label { Text = title, TextColor = UiFactory.Accent });
        return box;
    }

    private Widget ListRow(string text, Action onRemove)
    {
        var row = new HorizontalStackPanel { Spacing = 8 };
        row.Widgets.Add(new Label
        {
            Text = text,
            TextColor = Color.White,
            Width = PanelWidth - 180,
            VerticalAlignment = VerticalAlignment.Center,
        });
        row.Widgets.Add(UiFactory.SmallButton(_screens.Loc["modedit.remove"], onRemove));
        return row;
    }

    private static Widget TextField(string label, string value, Action<string> onChange)
    {
        var row = new HorizontalStackPanel { Spacing = 8 };
        row.Widgets.Add(new Label { Text = label, Width = 120, VerticalAlignment = VerticalAlignment.Center });

        var box = new TextBox { Text = value, Width = FieldWidth };
        box.TextChanged += (_, _) => onChange(box.Text ?? string.Empty);
        row.Widgets.Add(box);
        return row;
    }

    private static Widget Box(string placeholder, Action<string> onChange, string initial = "")
    {
        var stack = new VerticalStackPanel { Spacing = 2 };
        stack.Widgets.Add(new Label { Text = placeholder, TextColor = new Color(150, 160, 175) });

        var box = new TextBox { Text = initial, Width = 130 };
        box.TextChanged += (_, _) => onChange(box.Text ?? string.Empty);
        stack.Widgets.Add(box);
        return stack;
    }

    private static Label Note(string text, Color color) => new()
    {
        Text = text,
        TextColor = color,
        Wrap = true,
        Width = PanelWidth,
    };

    /// <summary>
    /// Udělá z uživatelského textu použitelné ID. Mezery a velká písmena v ID
    /// jsou nejčastější důvod, proč se mod nenačte — tohle je ušetří.
    /// </summary>
    private static string Slug(string text)
    {
        var chars = new List<char>(text.Length);
        foreach (char c in text.Trim().ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(c) || c == '_')
            {
                chars.Add(c);
            }
            else if (c is ' ' or '-')
            {
                chars.Add('_');
            }
        }

        return new string(chars.ToArray());
    }

    private static int ParseInt(string text, int fallback) =>
        int.TryParse(text, out int value) && value >= 0 ? value : fallback;
}
