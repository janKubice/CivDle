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

    /// <summary>Hrana čtvercového tlačítka s ikonou. Pohodlné na myš i na dotyk.</summary>
    public const int IconButtonSize = 46;

    /// <summary>
    /// Čtvercové tlačítko s ikonou a bublinou místo popisku.
    ///
    /// <para>Lišta plná slov byla v akční hře nečitelná a roztahovala se přes
    /// celou šířku obrazovky. Ikona se pozná tvarem rychleji než text a zabere
    /// zlomek místa; co dělá, řekne bublina, jakmile se na ní kurzor zastaví.</para>
    ///
    /// <para>Rozměr je <b>pevný</b>, a to schválně: roztažitelné tlačítko si
    /// v panelu ukouslo víc plochy, než na kolik je vidět, a bralo pak kliknutí
    /// mapě pod sebou.</para>
    /// </summary>
    public static Button ToolButton(Texture2D? icon, string tooltip, Action onClick, string? fallbackText = null)
    {
        var button = new Button
        {
            Width = IconButtonSize,
            Height = IconButtonSize,
            Padding = new Thickness(0),
            Background = new SolidBrush(ButtonFill),
            Tooltip = tooltip,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
        };

        // Bez spritu (chybějící ikona v knihovně) se ukáže zkratka textem —
        // tlačítko nikdy nesmí být prázdný čtvereček.
        button.Content = icon is not null
            ? Icon(icon, IconButtonSize - 14)
            : CenteredLabel(fallbackText ?? "?");

        if (button.Content is Widget content)
        {
            content.HorizontalAlignment = HorizontalAlignment.Center;
            content.VerticalAlignment = VerticalAlignment.Center;
        }

        button.Click += (_, _) => onClick();
        return button;
    }

    /// <summary>
    /// Ikonové tlačítko s odznakem počtu v rohu.
    /// </summary>
    /// <param name="Root">Co se vkládá do rozvržení (tlačítko i odznak).</param>
    /// <param name="Button">Samotné tlačítko.</param>
    /// <param name="Badge">Popisek odznaku — volající mu mění text přes <see cref="SetBadge"/>.</param>
    public sealed record BadgedButton(Widget Root, Button Button, Label Badge);

    /// <summary>
    /// Ikonové tlačítko, které umí ukázat počet.
    ///
    /// <para>Bez čísla na ikoně musí hráč otevřít každé menu, aby zjistil, jestli
    /// v něm něco čeká — u úkolů, zakázek a výzkumu to znamená obcházet HUD
    /// dokola. Odznak to řekne rovnou.</para>
    /// </summary>
    public static BadgedButton ToolButtonWithBadge(
        Texture2D? icon, string tooltip, Action onClick, string? fallbackText = null)
    {
        var button = ToolButton(icon, tooltip, onClick, fallbackText);

        var badge = new Label
        {
            Text = string.Empty,
            TextColor = Color.White,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            Background = new SolidBrush(new Color(196, 72, 72, 245)),
            Padding = new Thickness(4, 1),
            Visible = false,
        };

        var root = new Panel
        {
            Width = IconButtonSize,
            Height = IconButtonSize,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
        };
        root.Widgets.Add(button);
        root.Widgets.Add(badge);
        return new BadgedButton(root, button, badge);
    }

    /// <summary>
    /// Nastaví odznak. Nula ho schová — prázdné kolečko na každé ikoně je šum,
    /// ne informace.
    /// </summary>
    public static void SetBadge(BadgedButton target, int count, Color? color = null)
    {
        target.Badge.Visible = count > 0;
        target.Badge.Text = count > 99 ? "99+" : count.ToString();
        if (color is { } fill)
        {
            target.Badge.Background = new SolidBrush(fill);
        }
    }

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
