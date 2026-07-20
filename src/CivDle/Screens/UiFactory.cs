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
    private static readonly Color ButtonFill = new(38, 48, 64, 235);

    /// <summary>Standardní menu tlačítko s akcí a decentním pozadím.</summary>
    public static Button MenuButton(string text, Action onClick)
    {
        var button = new Button
        {
            Content = CenteredLabel(text),
            Width = MenuButtonWidth,
            Height = MenuButtonHeight,
            HorizontalAlignment = HorizontalAlignment.Center,
            Background = new SolidBrush(ButtonFill),
        };
        button.Click += (_, _) => onClick();
        return button;
    }

    /// <summary>Malé tlačítko (šipky přepínačů, „Náhodný"…).</summary>
    public static Button SmallButton(string text, Action onClick)
    {
        var button = new Button
        {
            Content = CenteredLabel(text),
            Height = 36,
            Padding = new Thickness(12, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Background = new SolidBrush(ButtonFill),
        };
        button.Click += (_, _) => onClick();
        return button;
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
