using CivDle.Core.Content;

namespace CivDle.Screens;

/// <summary>
/// Formátování cen (stavba, vylepšení, výzkum) do čitelného řetězce „40 dřevo  20 kámen".
/// Jména surovin jdou z lokalizace — žádné texty natvrdo. Sdílené mezi HUD a panely.
/// </summary>
internal static class CostFormat
{
    /// <summary>Ceník na řádek: „{množství} {surovina}" oddělené mezerami; prázdná cena = „zdarma".</summary>
    public static string Line(GameContent content, Localization loc, IReadOnlyList<ResourceAmount> cost)
    {
        if (cost.Count == 0)
        {
            return loc["common.free"];
        }

        return string.Join("  ", cost.Select(c => $"{c.Amount} {loc[content.Resources[c.ResourceIndex].NameKey]}"));
    }
}
