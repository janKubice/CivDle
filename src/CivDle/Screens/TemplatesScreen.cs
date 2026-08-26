using CivDle.Core.Config;
using CivDle.Core.Sim;
using CivDle.Input;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Myra.Graphics2D.UI;

namespace CivDle.Screens;

/// <summary>
/// Šablony zástavby (bod 44): seznam uložených kusů města, pokládání a mazání.
///
/// <para>Proč vlastní obrazovka a ne další podmenu v liště: u šablony je
/// potřeba vidět jméno a rozměr, přejmenovat ji a smazat — to se do řady ikon
/// nevejde. Do lišty vede jedna ikona, která tuhle obrazovku otevře.</para>
///
/// <para>Vrstva: čte a mění profil hráče, jinak jen volá zpět do herní
/// obrazovky (co se má vzít do ruky, co sejmout). Vlastní logiku nedrží.</para>
/// </summary>
public sealed class TemplatesScreen : IScreen
{
    private const int PanelWidth = 620;

    private readonly ScreenManager _screens;
    private readonly Action _startCapture;
    private readonly Action<BuildTemplate> _pickTemplate;
    private readonly InputManager _input = new();
    private Desktop _desktop = null!;

    /// <summary>Poslední hláška ke sdílení (zkopírováno / vloženo / nepovedlo se).</summary>
    private string _shareNote = string.Empty;
    private Color _shareColor = UiPalette.Text;

    /// <param name="screens">Správce obrazovek (profil, jazyk).</param>
    /// <param name="startCapture">Zapne na mapě snímání nové šablony.</param>
    /// <param name="pickTemplate">Vezme šablonu do ruky, aby ji šlo položit.</param>
    public TemplatesScreen(ScreenManager screens, Action startCapture, Action<BuildTemplate> pickTemplate)
    {
        _screens = screens;
        _startCapture = startCapture;
        _pickTemplate = pickTemplate;
        BuildUi();
        _screens.Loc.LanguageChanged += BuildUi;
    }

    public bool IsOverlay => true;

    public void OnActivated() => _input.Resync();

    private void BuildUi()
    {
        var loc = _screens.Loc;
        var layout = new VerticalStackPanel { Spacing = 10, Width = PanelWidth };

        layout.Widgets.Add(new Label
        {
            Text = loc["templates.title"],
            TextColor = UiPalette.TextBright,
            HorizontalAlignment = HorizontalAlignment.Center,
        });

        layout.Widgets.Add(new Label
        {
            Text = loc["templates.desc"],
            TextColor = Color.LightGray,
            Wrap = true,
            Width = PanelWidth - 20,
        });

        layout.Widgets.Add(UiFactory.SmallButton(loc["templates.capture"], () =>
        {
            _screens.Pop();
            _startCapture();
        }));

        // Vložení kódu od kamaráda. Je to jediná cesta, jak se do hry dostane
        // cizí rozvržení — a musí být vidět dřív než seznam vlastních šablon,
        // jinak ji nikdo nenajde.
        layout.Widgets.Add(UiFactory.SmallButton(loc["templates.paste"], PasteFromClipboard));

        if (_shareNote.Length > 0)
        {
            layout.Widgets.Add(new Label
            {
                Text = _shareNote,
                TextColor = _shareColor,
                Wrap = true,
                Width = PanelWidth - 20,
            });
        }

        var templates = _screens.Profile.Templates;
        if (templates.Count == 0)
        {
            layout.Widgets.Add(new Label
            {
                Text = loc["templates.empty"],
                TextColor = UiPalette.Text,
                Wrap = true,
                Width = PanelWidth - 20,
            });

            Finish(layout);
            return;
        }

        var list = new VerticalStackPanel { Spacing = 6, Width = PanelWidth - 20 };
        for (int i = 0; i < templates.Count; i++)
        {
            list.Widgets.Add(Row(templates[i], i));
        }

        // Seznam ve scrolleru: po dvaceti šablonách by okno přerostlo obrazovku.
        layout.Widgets.Add(new ScrollViewer
        {
            Content = list,
            Width = PanelWidth - 10,
            Height = 320,
        });

        Finish(layout);
    }

    private Widget Row(SavedTemplate saved, int index)
    {
        var loc = _screens.Loc;
        var template = saved.ToTemplate();
        var row = new HorizontalStackPanel { Spacing = 8, Width = PanelWidth - 40 };

        // Jméno jde přepsat rovnou v řádku — přejmenovat šablonu je běžnější
        // než ji smazat, a modální okno kvůli jednomu slovu je zbytečné.
        var name = new TextBox { Text = saved.Name, Width = 220 };
        name.TextChanged += (_, _) =>
        {
            saved.Name = name.Text ?? string.Empty;
            _screens.SaveProfile();
        };
        row.Widgets.Add(name);

        row.Widgets.Add(new Label
        {
            Text = loc.Format("templates.size", template.Width, template.Height, template.Buildings.Count),
            TextColor = UiPalette.Text,
            VerticalAlignment = VerticalAlignment.Center,
        });

        row.Widgets.Add(UiFactory.SmallButton(loc["templates.place"], () =>
        {
            _screens.Pop();
            _pickTemplate(template);
        }));

        // Kopírovat je vedle Položit schválně: sdílení má být stejně po ruce
        // jako použití, jinak se na něj přijde jen omylem.
        row.Widgets.Add(UiFactory.SmallButton(loc["templates.copy"], () =>
        {
            bool copied = Platform.Clipboard.TrySet(TemplateCode.Write(template));
            Note(copied ? "templates.copied" : "templates.clipboardFailed", copied);
        }));

        row.Widgets.Add(UiFactory.SmallButton(loc["templates.delete"], () =>
        {
            _screens.Profile.Templates.RemoveAt(index);
            _screens.SaveProfile();
            BuildUi();
        }));

        return row;
    }

    /// <summary>
    /// Vezme kód ze schránky a udělá z něj šablonu.
    ///
    /// <para>Nesmyslný obsah končí hláškou, ne pádem — do schránky se dostane
    /// leccos a hráč tam vloží, co má zrovna zkopírované.</para>
    /// </summary>
    private void PasteFromClipboard()
    {
        if (!TemplateCode.TryRead(Platform.Clipboard.Get(), out var template) || template.IsEmpty)
        {
            Note("templates.pasteFailed", good: false);
            return;
        }

        var saved = template.ToSaved();
        if (string.IsNullOrWhiteSpace(saved.Name))
        {
            saved.Name = _screens.Loc["templates.pastedName"];
        }

        _screens.Profile.Templates.Add(saved);
        _screens.SaveProfile();
        Note("templates.pasted", good: true);
    }

    private void Note(string key, bool good)
    {
        _shareNote = _screens.Loc[key];
        _shareColor = good ? UiPalette.Good : UiPalette.Warn;
        BuildUi();
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
