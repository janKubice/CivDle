namespace CivDle.Core.Sim;

/// <summary>Souhrn toho, co civilizace stihla „za tvé nepřítomnosti".</summary>
/// <param name="ElapsedSeconds">Kolik reálného času uběhlo od uložení.</param>
/// <param name="CreditedSeconds">Kolik z toho se započítalo (po stropu).</param>
/// <param name="ResourceGains">Přírůstek každé suroviny (index = surovina).</param>
/// <param name="PopulationGain">Přírůstek populace.</param>
/// <param name="BuildingsGain">Kolik budov přibylo (auto-stavba).</param>
public readonly record struct OfflineSummary(
    long ElapsedSeconds,
    long CreditedSeconds,
    double[] ResourceGains,
    double PopulationGain,
    int BuildingsGain)
{
    /// <summary>Stálo to za zobrazení? (uběhla aspoň minuta a něco se stalo).</summary>
    public bool Worthwhile => CreditedSeconds >= 60
        && (PopulationGain > 0.5 || BuildingsGain > 0 || Array.Exists(ResourceGains, g => g > 0.5));
}

/// <summary>
/// Offline postup („co se dělo, cos byl pryč"): dožene deterministickou simulaci
/// o čas uplynulý od uložení (se stropem), a vrátí souhrn přírůstků pro uvítací
/// obrazovku. Idle retenční háček — deterministický tik je na to jako dělaný.
/// Čte hodiny přes parametr (testovatelné, žádný skrytý <c>DateTime.Now</c>).
/// </summary>
public static class OfflineProgress
{
    /// <summary>Strop dohonu — offline se počítá max tolik (ať to není nekonečno ani dlouhý výpočet).</summary>
    public const long MaxCreditedSeconds = 12 * 3600;

    /// <summary>
    /// Dožene simulaci o čas mezi uložením a teď (se stropem × offline bonus Vzestupu),
    /// vrátí souhrn. Oznámení vzniklá při dohonu se zahodí (souhrn je zpětná vazba).
    /// </summary>
    public static OfflineSummary Apply(Simulation simulation, DateTime savedAtUtc, DateTime nowUtc)
    {
        // Dohon celý naráz. Volá se odtud, kde na pár tiků nezáleží (testy,
        // krátká pauza); hra ho pouští po dávkách přes OfflineCatchUp, aby
        // okno mezitím překreslovalo a dalo se přeskočit.
        var catchUp = new OfflineCatchUp(simulation, savedAtUtc, nowUtc);
        catchUp.Advance(catchUp.TotalTicks);
        return catchUp.Finish();
    }
}
