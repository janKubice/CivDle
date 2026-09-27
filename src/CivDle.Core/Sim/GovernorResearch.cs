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
/// <para>Výjimka: když na řadě je technologie, která čeká na surovinu, jež
/// nikde neteče (Karavany chtějí sůl a solná pláň u města není), čekání nemá
/// konec. Pak se vyzkoumá nejlevnější, na kterou město má — levné základy
/// tím nepřijdou zkrátka, protože je blokuje surovina, ne věda. Guvernér mezitím
/// zkouší výrobnu té suroviny postavit (<see cref="GovernorNeeds.MissingResearchMaterial"/>).</para>
///
/// <para>Utrácí jen to, na co smí automatika sáhnout: rezervu hráče ani to,
/// na co guvernér šetří stavbu, nevezme (<see cref="Simulation.AutomationCanSpend(IReadOnlyList{CivDle.Core.Content.ResourceAmount})"/>).</para>
/// </summary>
internal static class GovernorResearch
{
    /// <summary>Pod tímhle přítokem za sekundu surovina „neteče" (stejné jako u vyschlých vstupů).</summary>
    public const double NoFlowBelow = 0.005;

    /// <summary>Zkusí vyzkoumat technologii, která je na řadě. Vrací, jestli se povedlo.</summary>
    public static bool TryResearchNext(Simulation sim)
    {
        if (!sim.Plan.ChoosesResearch)
        {
            return false;
        }

        int tech = sim.CheapestOpenTech();
        if (tech >= 0 && StalledMaterial(sim, tech) >= 0)
        {
            tech = sim.CheapestOpenTech(affordableNow: true);
        }

        return tech >= 0
            && sim.CanResearch(tech) == PlacementResult.Ok
            && sim.AutomationCanSpend(sim.ScaledResearchCost(tech))
            && sim.TryResearch(tech) == PlacementResult.Ok;
    }

    /// <summary>
    /// Surovina (mimo vědu), které má technologie málo a která nikde neteče
    /// (ani výrobou, ani dovozem); −1 = nic takového. Věda se nepočítá — na tu se čeká vždycky, knihovny ji dělají.
    /// </summary>
    public static int StalledMaterial(Simulation sim, int tech)
    {
        int knowledge = sim.Content.Gameplay.Governor.Knowledge.ResourceIndex;
        var cost = sim.ScaledResearchCost(tech);
        for (int i = 0; i < cost.Count; i++)
        {
            int resource = cost[i].ResourceIndex;
            if (resource != knowledge && sim.GetResource(resource) < cost[i].Amount
                && sim.Ledger.InflowPerSecond(resource) <= NoFlowBelow)
            {
                return resource;
            }
        }

        return -1;
    }
}
