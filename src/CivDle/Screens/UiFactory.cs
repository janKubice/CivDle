using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Myra.Graphics2D;
using Myra.Graphics2D.Brushes;
using Myra.Graphics2D.TextureAtlases;
using Myra.Graphics2D.UI;

namespace CivDle.Screens;

/// <summary>
/// Malá továrna na opakující se Myra widgety, ať mají obrazovky jednotný vzhled
/// a nestaví tlačítka pořád dokola.
/// </summary>
internal static class UiFactory
{
    public const int MenuButtonWidth = 300;
    public const int MenuButtonHeight = 46;

    /// <summary>Akcentní barva UI (jednotný „cool" tón napříč menu i HUD).</summary>
    public static readonly Color Accent = new(96, 196, 220);

    private static readonly Color PanelFill = new(18, 22, 30, 205);
    /// <summary>Výplň tlačítek. Veřejná kvůli přepínačům, které si zvýrazňují aktivní volbu.</summary>
    public static readonly Color ButtonFill = new(38, 48, 64, 235);

    /// <summary>Standardní menu tlačítko s akcí a decentním pozadím.</summary>
    /// <param name="tooltip">Vysvětlení u kurzoru (Myra ho kreslí u myši); null = bez popisku.</param>
    public static Button MenuButton(string text, Action onClick, string? tooltip = null)
    {
        var button = new Button
        {
            Content = CenteredLabel(text),
            Width = MenuButtonWidth,
            Height = MenuButtonHeight,
            HorizontalAlignment = HorizontalAlignment.Center,
            Background = new SolidBrush(ButtonFill),
            Tooltip = tooltip,
        };
        button.Click += (_, _) => onClick();
        return button;
    }

    /// <summary>Malé tlačítko (šipky přepínačů, „Náhodný"…).</summary>
    /// <param name="tooltip">Vysvětlení u kurzoru (Myra ho kreslí u myši); null = bez popisku.</param>
    /// <summary>
    /// Tlačítko s ikonkou a textem. Ikonka nese význam rychleji než slovo —
    /// v panelu budovy je „šipka nahoru" a „čtyři čtverce v jeden" poznat na
    /// první pohled, kdežto dvě podobně dlouhá slova se musí číst.
    /// </summary>
    public static Button IconButton(Texture2D? icon, string text, Action onClick, string? tooltip = null)
    {
        var row = new HorizontalStackPanel { Spacing = 8, HorizontalAlignment = HorizontalAlignment.Center };
        if (icon is not null)
        {
            row.Widgets.Add(Icon(icon, 22));
        }

        row.Widgets.Add(new Label { Text = text, VerticalAlignment = VerticalAlignment.Center });

        var button = new Button
        {
            Content = row,
            Height = 38,
            Padding = new Thickness(14, 0),
            HorizontalAlignment = HorizontalAlignment.Center,
            Background = new SolidBrush(ButtonFill),
            Tooltip = tooltip,
        };
        button.Click += (_, _) => onClick();
        return button;
    }

    public static Button SmallButton(string text, Action onClick, string? tooltip = null)
    {
        var button = new Button
        {
            Content = CenteredLabel(text),
            Height = 36,
            Padding = new Thickness(12, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Background = new SolidBrush(ButtonFill),
            Tooltip = tooltip,
        };
        button.Click += (_, _) => onClick();
        return button;
    }

    /// <summary>
    /// Podklad pod celé menu. Nabídky se kreslí přes živou mapu a bez podkladu se
    /// text ztrácel v pixelech terénu — tenhle panel dá obsahu vlastní plochu,
    /// takže je čitelný nad čímkoli.
    /// </summary>
    public static Panel MenuBackdrop(Widget content)
    {
        var backdrop = new Panel
        {
            Background = new SolidBrush(new Color(12, 16, 24, 232)),
            Border = new SolidBrush(new Color(90, 120, 150, 150)),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(36, 28),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        backdrop.Widgets.Add(content);

        var root = new Panel();
        root.Widgets.Add(backdrop);
        return root;
    }

    /// <summary>Poloprůhledný tmavý panel (HUD, podklad menu), ať je text čitelný nad mapou.</summary>
    public static Panel DarkPanel(Widget content)
    {
        var panel = new Panel
        {
            Background = new SolidBrush(PanelFill),
            Border = new SolidBrush(new Color(90, 120, 150, 120)),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(12, 10),
        };
        panel.Widgets.Add(content);
        return panel;
    }

    /// <summary>Ikonka ze spritu (např. surovina v HUD) jako Myra widget.</summary>
    public static Image Icon(Texture2D texture, int size) => new()
    {
        Renderable = new TextureRegion(texture),
        Width = size,
        Height = size,
        VerticalAlignment = VerticalAlignment.Center,
    };

    /// <summary>Řádek formuláře: popisek s pevnou šířkou + widgety (selector, textbox…).</summary>
    public static HorizontalStackPanel Row(string labelText, params Widget[] widgets)
    {
        var row = new HorizontalStackPanel
        {
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        row.Widgets.Add(new Label
        {
            Text = labelText,
            Width = 160,
            VerticalAlignment = VerticalAlignment.Center,
        });
        foreach (var widget in widgets)
        {
            row.Widgets.Add(widget);
        }

        return row;
    }

    private static Label CenteredLabel(string text) => new()
    {
        Text = text,
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center,
    };
}
