using Microsoft.Xna.Framework;
using Myra.Graphics2D;
using Myra.Graphics2D.Brushes;
using Myra.Graphics2D.UI;

namespace CivDle.Screens;

/// <summary>
/// Malá továrna na opakující se Myra widgety, ať mají obrazovky jednotný vzhled
/// a nestaví tlačítka pořád dokola.
/// </summary>
internal static class UiFactory
{
    public const int MenuButtonWidth = 280;
    public const int MenuButtonHeight = 44;

    /// <summary>Standardní menu tlačítko s akcí.</summary>
    public static Button MenuButton(string text, Action onClick)
    {
        var button = new Button
        {
            Content = CenteredLabel(text),
            Width = MenuButtonWidth,
            Height = MenuButtonHeight,
            HorizontalAlignment = HorizontalAlignment.Center,
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
        };
        button.Click += (_, _) => onClick();
        return button;
    }

    /// <summary>Poloprůhledný tmavý panel (HUD, podklad menu), ať je text čitelný nad mapou.</summary>
    public static Panel DarkPanel(Widget content)
    {
        var panel = new Panel
        {
            Background = new SolidBrush(new Color(0, 0, 0, 170)),
            Padding = new Thickness(12),
        };
        panel.Widgets.Add(content);
        return panel;
    }

    private static Label CenteredLabel(string text) => new()
    {
        Text = text,
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center,
    };
}
