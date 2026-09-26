using CivDle.Core.Content;
using Myra.Graphics2D.Brushes;
using Myra.Graphics2D.UI;

namespace CivDle.Screens;

/// <summary>
/// Přepínač „kolik úrovní koupit jedním kliknutím“ (×1 / ×5 / ×25 / Max).
///
/// <para>Kupovat po jedné je u opakovatelných upgradů, kde hráč utrácí stovky
/// bodů, jen klikání. Obrazovka Vzestupu to uměla, Odkaz ne — přitom má
/// upgrady až do padesáté úrovně. Stav i vzhled jsou tady, aby obě vrstvy
/// prestiže vypadaly a chovaly se stejně.</para>
/// </summary>
internal sealed class PurchaseBatch
{
    /// <summary>Nabídka dávek. <see cref="int.MaxValue"/> = „Max“, tedy na co body stačí.</summary>
    public static readonly int[] Sizes = { 1, 5, 25, int.MaxValue };

    /// <summary>Kolik úrovní se kupuje jedním kliknutím.</summary>
    public int Size { get; set; } = 1;

    /// <summary>Řádek tlačítek; <paramref name="changed"/> se zavolá po přepnutí (obrazovka se přestaví).</summary>
    public Widget Picker(Localization loc, Action changed)
    {
        var row = new HorizontalStackPanel { Spacing = 6, HorizontalAlignment = HorizontalAlignment.Center };
        row.Widgets.Add(new Label
        {
            Text = loc["prestige.batch"],
            TextColor = UiPalette.Text,
            VerticalAlignment = VerticalAlignment.Center,
        });

        foreach (int size in Sizes)
        {
            int captured = size;
            var button = UiFactory.SmallButton(
                size == int.MaxValue ? loc["prestige.batchMax"] : "×" + size,
                () =>
                {
                    Size = captured;
                    changed();
                });

            if (size == Size)
            {
                button.Background = new PanelBrush(UiPalette.PanelAccent);
            }

            row.Widgets.Add(button);
        }

        return row;
    }

    /// <summary>
    /// Text tlačítka koupě: při dávce ukáže, kolik úrovní na body opravdu
    /// vyjde, a jejich součet. „Koupit ×5“ a pak koupit tři je horší než nic
    /// neslibovat.
    /// </summary>
    public static string BuyLabel(Localization loc, string singleKey, int levels, long nextCost, long batchCost) =>
        levels > 1
            ? loc.Format("prestige.buyMany", levels, CivDle.Core.Numbers.Format(batchCost))
            : loc.Format(singleKey, CivDle.Core.Numbers.Format(nextCost));
}
