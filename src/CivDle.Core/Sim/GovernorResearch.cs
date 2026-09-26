namespace CivDle.Core.Sim;

/// <summary>
/// Výzkum vybíraný guvernérem — když si ho hráč v plánu zapne
/// (<see cref="GovernorPlan.ChoosesResearch"/>).
///
/// <para><b>Proč to existuje:</b> guvernér stavěl knihovny, ale vědu nikdo
/// neutrácel; hráč, který nechal město běžet samo, se po návratu díval na
/// plný sklad vědy a strom bez jediného nového uzlu. Automatický výzkum
/// z Odkazu přichází až v nejhlubší vrstvě progrese — tohle je hráčova volba
/// kdykoli, výchozí vypnutá.</para>
///
/// <para>Bere technologii, která je „na řadě" (<see cref="Simulation.CheapestOpenTech"/>):
/// nejlevnější, na kterou má město předpoklady. Když na ni ještě nemá, čeká —
/// nepřeskakuje na dražší, na kterou zrovna náhodou má, protože tak by
/// levné základy zůstaly nevyzkoumané navždy.</para>
///
/// <para>Utrácí jen to, na co smí automatika sáhnout: rezervu hráče ani to,
/// na co guvernér šetří stavbu, nevezme (<see cref="Simulation.AutomationCanSpend(IReadOnlyList{CivDle.Core.Content.ResourceAmount})"/>).</para>
/// </summary>
internal static class GovernorResearch
{
    /// <summary>Zkusí vyzkoumat technologii, která je na řadě. Vrací, jestli se povedlo.</summary>
    public static bool TryResearchNext(Simulation sim)
    {
        if (!sim.Plan.ChoosesResearch)
        {
            return false;
        }

        int tech = sim.CheapestOpenTech();
        return tech >= 0
            && sim.CanResearch(tech) == PlacementResult.Ok
            && sim.AutomationCanSpend(sim.ScaledResearchCost(tech))
            && sim.TryResearch(tech) == PlacementResult.Ok;
    }
}
