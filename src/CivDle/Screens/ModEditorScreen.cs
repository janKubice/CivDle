using CivDle.Core.Content.Mods;
using CivDle.Input;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Myra.Graphics2D;
using Myra.Graphics2D.Brushes;
using Myra.Graphics2D.UI;

namespace CivDle.Screens;

/// <summary>
/// Tvůrce obsahu: hráč si přímo ve hře poskládá vlastní budovu, surovinu,
/// událost, výzkum, faunu, úkol nebo jméno města, nakreslí k tomu obrázek,
/// nechá si to zkontrolovat a uloží jako mod.
///
/// <para>Formulář je <b>obecný</b>: co se dá vyplnit, říká katalog typů
/// z <c>data/mod-types.json</c>, ne tenhle soubor. Přidat další typ obsahu
/// proto znamená přidat záznam do dat — ne psát novou obrazovku. Přesně proto
/// tu dřív byly jen suroviny a budovy: každý další typ znamenal další ručně
/// psaný formulář a u sedmi typů se to rozpadlo.</para>
///
/// <para>Klíčová zůstává <b>kontrola</b>: tlačítko „Zkontrolovat" pustí mod
/// skutečným <see cref="ModValidator"/>, tedy tímtéž loaderem, kterým se spouští
/// hra. Autor vidí chybu ve chvíli, kdy ji udělal — ne až mu příště nenaběhne
/// hra, kdy už mod nemá kde vypnout.</para>
///
/// <para>Vrstva: UI. Model, zápis i katalog jsou v jádře.</para>
/// </summary>
public sealed class ModEditorScreen : IScreen
{
    private const int PanelWidth = 860;
    private const int FieldWidth = 260;

    private readonly ScreenManager _screens;
    private readonly InputManager _input = new();
    private readonly ModDraft _draft;
    private readonly ModTypeCatalog _catalog;

    private Desktop _desktop = null!;

    /// <summary>Typ, který hráč zrovna přidává (index do katalogu).</summary>
    private int _typeIndex;

    /// <summary>Rozepsaný záznam — vzniká prázdný a přidá se tlačítkem.</summary>
    private ModEntry _entry;

    private string _status = string.Empty;
    private bool _statusOk;

    public ModEditorScreen(ScreenManager screens)
    {
        _screens = screens;
        _catalog = ModTypeCatalog.LoadFrom(Path.Combine(DataDirectory, "mod-types.json"));
        _draft = new ModDraft("muj_mod", "Můj mod") { Types = _catalog };
        _entry = new ModEntry(CurrentType?.Id ?? string.Empty);
        BuildUi();
        _screens.Loc.LanguageChanged += BuildUi;
    }

    public bool IsOverlay => true;

    public void OnActivated()
    {
        _input.Resync();

        // Návrat z kreslítka: obrázek mohl vzniknout, takže se seznam překreslí.
        BuildUi();
    }

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

    private ModTypeDef? CurrentType =>
        _catalog.Types.Count == 0 ? null : _catalog.Types[Math.Clamp(_typeIndex, 0, _catalog.Types.Count - 1)];

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
            TextColor = UiPalette.TextBright,
            HorizontalAlignment = HorizontalAlignment.Center,
        });
        layout.Widgets.Add(Note(loc["modedit.intro"], Color.LightGray));

        var body = new VerticalStackPanel { Spacing = 10 };
        body.Widgets.Add(ModSection());

        if (_catalog.IsEnabled)
        {
            body.Widgets.Add(TypePicker());
            body.Widgets.Add(EntryForm());
            body.Widgets.Add(ContentList());
        }
        else
        {
            body.Widgets.Add(Note(loc["modedit.noTypes"], UiPalette.Warn));
        }

        layout.Widgets.Add(new ScrollViewer { Content = body, Height = 460, Width = PanelWidth });

        if (_status.Length > 0)
        {
            layout.Widgets.Add(Note(_status, _statusOk ? UiPalette.Good : UiPalette.Warn));
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

    /// <summary>Volba typu obsahu — jedno tlačítko na typ, jak si hráč přál „naklikat".</summary>
    private Widget TypePicker()
    {
        var loc = _screens.Loc;
        var box = Section(loc["modedit.typeSection"]);
        var row = new HorizontalStackPanel { Spacing = 6 };

        for (int i = 0; i < _catalog.Types.Count; i++)
        {
            var type = _catalog.Types[i];
            int captured = i;
            var button = UiFactory.SmallButton(loc[type.NameKey], () =>
            {
                _typeIndex = captured;
                _entry = new ModEntry(type.Id); // jiný typ = jiná pole, začíná se načisto
                BuildUi();
            });

            if (i == _typeIndex)
            {
                button.Background = new SolidBrush(UiPalette.PanelAccent);
            }

            row.Widgets.Add(button);
        }

        box.Widgets.Add(row);
        return box;
    }

    /// <summary>Formulář vykreslený podle katalogu — jedno pole na řádek.</summary>
    private Widget EntryForm()
    {
        var loc = _screens.Loc;
        var type = CurrentType!;
        var box = Section(loc[type.NameKey]);

        foreach (var field in type.Fields)
        {
            box.Widgets.Add(FieldRow(type, field));
        }

        box.Widgets.Add(UiFactory.SmallButton(loc["modedit.add"], () =>
        {
            if (!type.PlainList && _entry.IdOf(type).Length == 0)
            {
                Fail(loc["modedit.needIdAndName"]);
                return;
            }

            _draft.Entries.Add(_entry);
            _entry = new ModEntry(type.Id);
            _status = string.Empty;
            BuildUi();
        }));

        return box;
    }

    /// <summary>Jeden řádek formuláře. Jak se vyplňuje, určuje druh pole z katalogu.</summary>
    private Widget FieldRow(ModTypeDef type, ModFieldDef field)
    {
        var loc = _screens.Loc;
        var row = new HorizontalStackPanel { Spacing = 8 };
        row.Widgets.Add(new Label
        {
            Text = loc[field.LabelKey],
            Width = 160,
            VerticalAlignment = VerticalAlignment.Center,
        });

        switch (field.Kind)
        {
            case ModFieldKind.Toggle:
            {
                bool on = _entry.Value(field).Equals("true", StringComparison.OrdinalIgnoreCase);
                row.Widgets.Add(UiFactory.SmallButton(on ? loc["modedit.yes"] : loc["modedit.no"], () =>
                {
                    _entry.With(field.Key, on ? "false" : "true");
                    BuildUi();
                }));
                break;
            }

            case ModFieldKind.Choice:
            {
                // Cyklující tlačítko místo rozbalovacího seznamu: nabídky jsou
                // krátké a klikat se má dát bez míření do vyskakovacího okna.
                string current = _entry.Value(field);
                row.Widgets.Add(UiFactory.SmallButton(
                    current.Length == 0 ? loc["modedit.none"] : current,
                    () =>
                    {
                        var options = field.Options ?? Array.Empty<string>();
                        int index = 0;
                        for (int i = 0; i < options.Count; i++)
                        {
                            if (options[i] == current)
                            {
                                index = i;
                                break;
                            }
                        }

                        _entry.With(field.Key, options[(index + 1) % options.Count]);
                        BuildUi();
                    }));
                break;
            }

            case ModFieldKind.Sprite:
            {
                row.Widgets.Add(UiFactory.SmallButton(loc["modedit.draw"], () =>
                {
                    string id = _entry.IdOf(type);
                    if (id.Length == 0)
                    {
                        Fail(loc["modedit.needIdAndName"]);
                        return;
                    }

                    // Kreslí se rovnou do složky modu pod ID budovy: hra pak
                    // obrázek najde sama, bez dalšího políčka k vyplnění.
                    _entry.With(field.Key, id);
                    _screens.Push(new SpriteEditorScreen(
                        _screens, Path.Combine(ModsDirectory, _draft.Id, "sprites", id + ".png")));
                }));
                break;
            }

            default:
            {
                var textBox = new TextBox { Text = _entry.Value(field), Width = FieldWidth };
                textBox.TextChanged += (_, _) => _entry.With(
                    field.Key,
                    field.Kind == ModFieldKind.Id ? Slug(textBox.Text ?? string.Empty) : textBox.Text ?? string.Empty);
                row.Widgets.Add(textBox);
                break;
            }
        }

        // Nápověda: u odkazů vypíše, z čeho se vybírá — hráč nemusí hádat ID.
        string hint = Hint(field);
        if (hint.Length > 0)
        {
            row.Widgets.Add(new Label
            {
                Text = hint,
                TextColor = UiPalette.TextDim,
                Width = PanelWidth - 480,
                VerticalAlignment = VerticalAlignment.Center,
            });
        }

        return row;
    }

    /// <summary>Nápověda k poli: u odkazů skutečná ID z obsahu, u čísel meze.</summary>
    private string Hint(ModFieldDef field)
    {
        var loc = _screens.Loc;
        if (field.Kind is ModFieldKind.Reference or ModFieldKind.References or ModFieldKind.Amounts)
        {
            var ids = ReferenceIds(field.Reference);
            if (ids.Count == 0)
            {
                return string.Empty;
            }

            string sample = string.Join(", ", ids.Take(6));
            string prefix = field.Kind == ModFieldKind.Amounts ? loc["modedit.amountsHint"] : loc["modedit.refHint"];
            return $"{prefix} {sample}{(ids.Count > 6 ? "…" : string.Empty)}";
        }

        if (field.Kind is ModFieldKind.Number or ModFieldKind.Decimal
            && field.Min > double.MinValue && field.Max < double.MaxValue)
        {
            return $"{field.Min:0.##}–{field.Max:0.##}";
        }

        return string.Empty;
    }

    /// <summary>ID, ze kterých se u daného odkazu vybírá (skutečný obsah hry).</summary>
    private IReadOnlyList<string> ReferenceIds(string reference)
    {
        var content = _screens.Content;
        return reference switch
        {
            "resource" => content.Resources.All.Select(r => r.Id).ToList(),
            "building" => content.Buildings.All.Select(b => b.Id).ToList(),
            "biome" => content.Biomes.All.Select(b => b.Id).ToList(),
            "tech" => content.Techs.All.Select(t => t.Id).ToList(),
            _ => Array.Empty<string>(),
        };
    }

    /// <summary>Co už mod obsahuje. Mazat jde po jednom — mod pack se skládá postupně.</summary>
    private Widget ContentList()
    {
        var loc = _screens.Loc;
        var box = Section(loc.Format("modedit.contentSection", _draft.Entries.Count));

        if (_draft.Entries.Count == 0)
        {
            box.Widgets.Add(Note(loc["modedit.contentEmpty"], UiPalette.TextDim));
            return box;
        }

        foreach (var entry in _draft.Entries.ToList())
        {
            var type = _catalog.Find(entry.TypeId);
            string label = type is null
                ? entry.TypeId
                : $"{loc[type.NameKey]}: {Describe(entry, type)}";

            box.Widgets.Add(ListRow(label, () =>
            {
                _draft.Entries.Remove(entry);
                BuildUi();
            }));
        }

        return box;
    }

    private static string Describe(ModEntry entry, ModTypeDef type)
    {
        if (type.PlainList)
        {
            return entry.Values.Values.FirstOrDefault() ?? string.Empty;
        }

        string id = entry.IdOf(type);
        foreach (var field in type.Fields)
        {
            if (field.Kind == ModFieldKind.Lang)
            {
                string name = entry.Value(field);
                return name.Length > 0 ? $"{name} ({id})" : id;
            }
        }

        return id;
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
            Background = new SolidBrush(UiPalette.Panel),
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
        row.Widgets.Add(new Label { Text = label, Width = 160, VerticalAlignment = VerticalAlignment.Center });

        var box = new TextBox { Text = value, Width = FieldWidth };
        box.TextChanged += (_, _) => onChange(box.Text ?? string.Empty);
        row.Widgets.Add(box);
        return row;
    }

    private static Label Note(string text, Color color) => new()
    {
        Text = text,
        TextColor = color,
        Wrap = true,
        Width = PanelWidth - 24,
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
}
