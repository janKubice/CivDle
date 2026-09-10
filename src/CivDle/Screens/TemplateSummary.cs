using System.Text;
using CivDle.Core.Content;
using CivDle.Core.Sim;

namespace CivDle.Screens;

/// <summary>
/// Co je v šabloně a kolik bude stát ji postavit.
///
/// <para>Proč to existuje: v seznamu šablon stálo jen „5×4, 7 budov". To je
/// rozměr, ne obsah — hráč po týdnu netuší, jestli je „Blok 2" obytná ulice
/// nebo řada pil, a jestli na ni vůbec má. Musel ji položit a dívat se, co
/// vznikne.</para>
///
/// <para>Skládá se z DAT (ceny budov), takže nová budova v JSON se do soupisu
/// i do ceny promítne sama.</para>
///
/// <para>Vrstva: UI. Simulaci ani obsah nemění — jen čte a formátuje.</para>
/// </summary>
internal static class TemplateSummary
{
    /// <summary>Kolik druhů budov se vypíše, než se zbytek shrne do „a další".</summary>
    private const int MaxListed = 4;

    /// <summary>
    /// Soupis budov: <c>3× Chalupa, 2× Pila</c>. Seřazeno od nejčetnějších —
    /// hráč pozná blok podle toho, čeho je v něm nejvíc.
    /// </summary>
    public static string Contents(GameContent content, Localization loc, BuildTemplate template)
    {
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var part in template.Buildings)
        {
            counts[part.BuildingId] = counts.GetValueOrDefault(part.BuildingId) + 1;
        }

        if (counts.Count == 0)
        {
            return string.Empty;
        }

        var ordered = counts.OrderByDescending(pair => pair.Value).ThenBy(pair => pair.Key).ToList();
        var text = new StringBuilder();
        for (int i = 0; i < ordered.Count && i < MaxListed; i++)
        {
            if (text.Length > 0)
            {
                text.Append(", ");
            }

            // Budova, kterou data mezitím ztratila (mod, přejmenování), se
            // vypíše aspoň svým ID — mlčet by znamenalo tvářit se, že v šabloně
            // není, a součet by pak neseděl s tím, co se postaví.
            string name = content.Buildings.TryIndexOf(ordered[i].Key, out int defIndex)
                ? loc[content.Buildings[defIndex].NameKey]
                : ordered[i].Key;

            text.Append(ordered[i].Value).Append("× ").Append(name);
        }

        if (ordered.Count > MaxListed)
        {
            text.Append(loc.Format("templates.andMore", ordered.Count - MaxListed));
        }

        return text.ToString();
    }

    /// <summary>
    /// Kolik dohromady stojí postavit celou šablonu. Prázdný řetězec = šablona
    /// je prázdná nebo v datech nic z ní nezbylo.
    /// </summary>
    public static string Cost(GameContent content, Localization loc, BuildTemplate template)
    {
        var total = new Dictionary<int, int>();
        foreach (var part in template.Buildings)
        {
            if (!content.Buildings.TryIndexOf(part.BuildingId, out int defIndex))
            {
                continue;
            }

            foreach (var amount in content.Buildings[defIndex].BuildCost)
            {
                total[amount.ResourceIndex] = total.GetValueOrDefault(amount.ResourceIndex) + amount.Amount;
            }
        }

        if (total.Count == 0)
        {
            return string.Empty;
        }

        var line = total
            .OrderBy(pair => pair.Key)
            .Select(pair => new ResourceAmount(pair.Key, pair.Value))
            .ToList();

        return CostFormat.Line(content, loc, line);
    }
}
