using CivDle.Core.Content;
using CivDle.Core.Sim;

namespace CivDle.Screens;

/// <summary>
/// Text jednoho bodu plánu guvernéra („Dochází jídlo → pole, rybárny").
///
/// <para>Oddělené od obrazovky, aby se dalo otestovat bez grafiky, že každá
/// potřeba má svůj text a že se do něj dosadí správná surovina — chybějící
/// klíč by se ukázal jako holé „governor.need.power".</para>
/// </summary>
public static class GovernorPlanText
{
    /// <summary>Klíč textu potřeby.</summary>
    public static string KeyFor(CityNeed need) => $"governor.need.{need.ToString().ToLowerInvariant()}";

    /// <summary>Hotová věta pro jednu položku plánu.</summary>
    public static string Line(GameContent content, Localization loc, GovernorAgendaItem item)
    {
        string resource = item.ResourceIndex >= 0 && item.ResourceIndex < content.Resources.Count
            ? loc[content.Resources[item.ResourceIndex].NameKey]
            : string.Empty;
        return loc.Format(KeyFor(item.Need), resource);
    }
}
