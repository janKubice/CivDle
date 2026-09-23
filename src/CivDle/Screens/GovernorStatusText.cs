using CivDle.Core.Content;
using CivDle.Core.Sim;

namespace CivDle.Screens;

/// <summary>
/// Stav guvernéra slovy: co dělá, na co šetří, proč uvízl — a co s tím.
///
/// <para>Proč to existuje: guvernér dřív uvízl potichu a hráč měl jen dojem,
/// že se město „zaseklo". Hlášení (toast) přijde jednou a zmizí; tenhle řádek
/// visí, dokud stav trvá, a v bublině říká, jak z toho ven.</para>
/// </summary>
internal static class GovernorStatusText
{
    /// <summary>Jedna věta o stavu; prázdná, když guvernér nemá co dělat.</summary>
    public static string Line(GameContent content, Localization loc, GovernorStatus status)
    {
        string building = status.DefIndex >= 0 ? loc[content.Buildings[status.DefIndex].NameKey] : string.Empty;
        string resource = status.ResourceIndex >= 0 ? loc[content.Resources[status.ResourceIndex].NameKey] : string.Empty;
        return status.Activity switch
        {
            GovernorActivity.Building when building.Length > 0 => loc.Format("governor.status.building", building),
            GovernorActivity.Saving when building.Length > 0 && resource.Length > 0 =>
                loc.Format("governor.status.saving", building, resource),
            GovernorActivity.Saving when building.Length > 0 => loc.Format("governor.status.savingAny", building),
            GovernorActivity.Stuck => StuckLine(loc, status.Blocker, building, resource),
            _ => string.Empty,
        };
    }

    /// <summary>Rada do bubliny: co může hráč udělat (prázdná, když není co radit).</summary>
    public static string Hint(Localization loc, GovernorStatus status) => status.Activity switch
    {
        GovernorActivity.Building => loc["governor.hint.building"],
        GovernorActivity.Saving => loc["governor.hint.saving"],
        GovernorActivity.Stuck => status.Blocker switch
        {
            GovernorBlocker.NoSite => loc["governor.hint.noSite"],
            GovernorBlocker.NeedsPeople => loc["governor.hint.needsPeople"],
            GovernorBlocker.SlowSupply => loc["governor.hint.slowSupply"],
            GovernorBlocker.Bootstrap => loc["governor.hint.bootstrap"],
            _ => loc["governor.hint.noProducer"],
        },
        _ => string.Empty,
    };

    /// <summary>
    /// Potřebuje stav hráčův zásah? Čekání na lidi ne — ti přibudou sami,
    /// a varovná barva by jen strašila.
    /// </summary>
    public static bool NeedsPlayer(GovernorStatus status) =>
        status.Activity == GovernorActivity.Stuck && status.Blocker != GovernorBlocker.NeedsPeople;

    private static string StuckLine(Localization loc, GovernorBlocker blocker, string building, string resource) => blocker switch
    {
        GovernorBlocker.NoSite when building.Length > 0 => loc.Format("governor.status.noSite", building),
        GovernorBlocker.NeedsPeople => loc["governor.status.needsPeople"],
        GovernorBlocker.SlowSupply when resource.Length > 0 => loc.Format("governor.status.slowSupply", resource),
        GovernorBlocker.Bootstrap when resource.Length > 0 => loc.Format("governor.status.bootstrap", resource),
        GovernorBlocker.NoProducer when resource.Length > 0 => loc.Format("governor.status.noProducer", resource),
        _ => string.Empty,
    };
}
