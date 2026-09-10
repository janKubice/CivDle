using Myra;
using Myra.Graphics2D;
using Myra.Graphics2D.Brushes;
using Myra.Graphics2D.UI;

namespace CivDle.Screens;

/// <summary>
/// Bublina u kurzoru, ve které má každý druh informace svou barvu.
///
/// <para>Myra umí u widgetu jen jeden řetězec a vykreslí ho jednou barvou.
/// Popisek budovy má přitom osm druhů řádků — cena, výroba, spotřeba, lidé,
/// sklad, proud, omezení, rada — a všechny vypadaly stejně. Hráč, který
/// porovnává tři budovy vedle sebe, tak musel pokaždé přečíst celý blok, aby
/// v něm našel to jediné číslo, které ho zajímá.</para>
///
/// <para>Řešení je globální hák <c>MyraEnvironment.TooltipCreator</c>: text si
/// nese neviditelné značky (<see cref="TipLine"/>), tahle třída je odloupne
/// a poskládá z řádků panel obarvených štítků. Kdo značky nepoužívá, dostane
/// bublinu jako dřív — jen bílou.</para>
///
/// <para>Vrstva: UI. Nic nerozhoduje, jen kreslí to, co dostane.</para>
/// </summary>
internal static class RichTooltip
{
    /// <summary>Šířka, za kterou se dlouhý řádek zalomí.</summary>
    private const int MaxWidth = 420;

    /// <summary>
    /// Zapne barevné bubliny pro celou hru. Volá se jednou při startu —
    /// je to nastavení Myry, ne obrazovky.
    /// </summary>
    public static void Install() => MyraEnvironment.TooltipCreator = Create;

    private static Widget Create(Widget target)
    {
        string text = target.Tooltip ?? string.Empty;
        var rows = new VerticalStackPanel { Spacing = 2 };

        foreach (string raw in text.Split('\n'))
        {
            var kind = TipLine.Split(raw, out string line);

            // Prázdný řádek zůstává mezerou: v popiskách odděluje odstavce
            // a bez něj by se blok slil do jednoho odstavce.
            rows.Widgets.Add(new Label
            {
                Text = line,
                TextColor = TipLine.ColorFor(kind),
                Wrap = true,
                MaxWidth = MaxWidth,
            });
        }

        return new Panel
        {
            Padding = new Thickness(10, 8),
            Background = new SolidBrush(UiPalette.PanelDeep),
            Border = new SolidBrush(UiPalette.AccentDim),
            BorderThickness = new Thickness(1),
            Widgets = { rows },
        };
    }
}
