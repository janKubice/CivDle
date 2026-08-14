namespace CivDle.Core.Sim;

/// <summary>
/// Kdy a kolik automatický výzkum zkusí. Čistý počet bez stavu — proto jde
/// ověřit bez odtikávání celé simulace, kde se rozpočet dá nastavit jedině
/// nákupem vylepšení Odkazu.
/// </summary>
public static class AutoResearchSchedule
{
    /// <summary>
    /// Základní rozestup pokusů v ticích. Deset sekund je dost na to, aby bylo
    /// vidět, že se něco děje, a málo na to, aby se čekalo.
    /// </summary>
    public const int BaseIntervalTicks = 100;

    /// <summary>
    /// Nejkratší možný rozestup. Pod ním by výzkum probublal celý strom
    /// za pár vteřin a hráč by neviděl, co se vlastně odemklo.
    /// </summary>
    public const int MinIntervalTicks = 10;

    /// <summary>Strop pokusů za jeden interval — pojistka proti dlouhému tiku.</summary>
    public const int MaxPerInterval = 32;

    /// <summary>
    /// Jak často se automat pokusí. Zpomalit ho nejde: násobič pod 1 by
    /// interval prodloužil, a tempo se má dát jen zvyšovat.
    /// </summary>
    public static int IntervalFor(double researchSpeed) =>
        Math.Max(MinIntervalTicks, (int)Math.Round(BaseIntervalTicks / Math.Max(1.0, researchSpeed)));

    /// <summary>
    /// Kolik uzlů smí automat za jeden interval vyzkoumat. Zaokrouhluje se
    /// dolů: půl úrovně nesmí dát ani půl výzkumu, ani celý — jinak by se
    /// rozpočet choval jinak, než co je napsané na tlačítku.
    /// </summary>
    public static int BudgetFor(double autoResearchLevel) =>
        (int)Math.Clamp(Math.Floor(autoResearchLevel), 0, MaxPerInterval);
}

/// <summary>
/// Výzkum, který si hráč nemusí odklikat sám. Odemyká se až v Odkazu, tedy
/// v nejhlubší vrstvě progrese.
///
/// <para>Proč to je až tam: klikání na uzly stromu je v první hře součást hry —
/// hráč se rozhoduje, kudy se vydá. Po několika Vzestupech ale tentýž strom
/// prochází poněkolikáté a rozhodnutí už žádné není, zbylo jen klikání.
/// Automatizace se má zapnout přesně v okamžiku, kdy z volby zbyla rutina, ne
/// dřív.</para>
///
/// <para><b>Neplatí za hráče nic navíc:</b> automat utrácí tytéž suroviny za
/// tytéž ceny. Automatizuje se klikání, ne cena — jinak by z toho byla sleva
/// schovaná za jiným jménem.</para>
///
/// <para>Vybírá se <b>nejlevnější dostupná</b> technologie, ne první v pořadí:
/// tak se strom prochází zhruba tak, jak by šel hráč, a nezasekne se na drahém
/// uzlu, na který zrovna nemá.</para>
///
/// <para>Vrstva: systém simulace, běží na nízké frekvenci. Deterministický —
/// při shodné ceně rozhoduje pořadí v datech.</para>
/// </summary>
internal sealed class AutoResearchSystem
{
    /// <summary>
    /// Zkusí vyzkoumat, na co město dosáhne. Volá se každý tik; sama si hlídá,
    /// jestli je čas.
    /// </summary>
    public void Tick(Simulation sim)
    {
        int budget = AutoResearchSchedule.BudgetFor(sim.AutoResearchLevel);
        if (budget <= 0)
        {
            return;
        }

        if (sim.TickCount % AutoResearchSchedule.IntervalFor(sim.ResearchSpeed) != 0)
        {
            return;
        }

        for (int i = 0; i < budget; i++)
        {
            if (!TryResearchCheapest(sim))
            {
                break; // není na co dosáhnout — zbytek rozpočtu propadne
            }
        }
    }

    /// <summary>
    /// Vyzkoumá nejlevnější technologii, na kterou město dosáhne. Vrací
    /// <c>false</c>, když žádná taková není.
    /// </summary>
    private static bool TryResearchCheapest(Simulation sim)
    {
        int best = -1;
        double bestCost = double.MaxValue;

        for (int i = 0; i < sim.TechCount; i++)
        {
            if (sim.CanResearch(i) != PlacementResult.Ok)
            {
                continue;
            }

            double cost = sim.TotalResearchCost(i);
            if (cost < bestCost)
            {
                bestCost = cost;
                best = i;
            }
        }

        return best >= 0 && sim.TryResearch(best) == PlacementResult.Ok;
    }
}
