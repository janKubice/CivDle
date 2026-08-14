namespace CivDle.Core.Sim;

/// <summary>
/// Co si civilizace odnese přes Vzestup.
///
/// <para>Tohle je důvod, proč druhá prestižní vrstva vůbec stojí za to. Odkaz
/// uměl jen „ještě víc výroby" — tedy tytéž násobiče, jaké dává Vzestup, jen
/// dražší. Hráč z toho neměl <b>nový pocit</b>, jen jiné číslo. Přitom
/// nejsilnější věc, kterou může hlubší vrstva nabídnout, je zrušit část
/// resetu: ne zrychlit cestu zpátky, ale nemuset ji jít celou.</para>
///
/// <para>Dědí se tři věci, každá bere jednu z bolestí Vzestupu:</para>
/// <list type="bullet">
/// <item><b>Znalosti</b> — strom výzkumu nezačíná na nule.</item>
/// <item><b>Základy</b> — město po Vzestupu nezmizí celé, jádro zůstane stát.</item>
/// <item><b>Mapa</b> — svět se nemusí objevovat znovu.</item>
/// </list>
///
/// <para>Pořadí je podstatné: <b>technologie napřed, budovy až po nich.</b>
/// Zděděná budova, jejíž technologie se nevrátila, by sice stála, ale hráč by
/// ji neuměl postavit znovu — a to je matoucí. S technologiemi napřed sedí
/// zděděné město na znalostech, které k němu patří.</para>
///
/// <para>Vrstva: pomocník simulace, volaný jen při Vzestupu. Alokace tady
/// nevadí — Vzestup je zlomový okamžik, ne tiková smyčka.</para>
/// </summary>
internal static class LegacyInheritance
{
    /// <summary>Zapamatovaná budova: co a kde stálo.</summary>
    internal readonly record struct KeptBuilding(int DefIndex, int X, int Y);

    /// <summary>
    /// Vybere budovy, které Vzestup přežijí — <b>nejbližší středu města</b>.
    ///
    /// <para>Ne nejnovější a ne nejdražší: hráč si pod „zůstane mi jádro města"
    /// představí střed, ne rozházené kusy předměstí. Zděděné město tak drží
    /// tvar a dá se na něj rovnou stavět.</para>
    ///
    /// <para>Volá se <b>před</b> resetem, dokud budovy ještě existují.</para>
    /// </summary>
    internal static void Capture(Simulation sim, int keepCount, List<KeptBuilding> into)
    {
        into.Clear();
        if (keepCount <= 0)
        {
            return;
        }

        var buildings = sim.Buildings;
        var ranked = new List<(long Distance, int Index)>(buildings.Length);

        for (int i = 0; i < buildings.Length; i++)
        {
            ref readonly var building = ref buildings[i];
            long dx = building.X - sim.CityCenterX;
            long dy = building.Y - sim.CityCenterY;
            ranked.Add((dx * dx + dy * dy, i));
        }

        ranked.Sort(static (a, b) => a.Distance != b.Distance
            ? a.Distance.CompareTo(b.Distance)
            : a.Index.CompareTo(b.Index)); // shodná vzdálenost → stabilně podle pořadí

        int take = Math.Min(keepCount, ranked.Count);
        for (int i = 0; i < take; i++)
        {
            ref readonly var building = ref buildings[ranked[i].Index];
            into.Add(new KeptBuilding(building.DefIndex, building.X, building.Y));
        }
    }

    /// <summary>
    /// Vrátí zděděné budovy na jejich místa. Volá se <b>po</b> resetu.
    ///
    /// <para>Co se nevejde (terén se změnil, dlaždice je obsazená), se tiše
    /// přeskočí. Zděděná budova, která nejde postavit, je smůla — ne důvod
    /// shodit hráči Vzestup.</para>
    /// </summary>
    /// <returns>Kolik budov se doopravdy vrátilo.</returns>
    internal static int Restore(Simulation sim, List<KeptBuilding> kept)
    {
        int placed = 0;
        for (int i = 0; i < kept.Count; i++)
        {
            var building = kept[i];
            if (sim.TryPlaceBuildingFree(building.DefIndex, building.X, building.Y) == PlacementResult.Ok)
            {
                placed++;
            }
        }

        return placed;
    }

    /// <summary>
    /// Vrátí zadaný počet technologií — <b>nejlevnější dostupné</b>, opakovaně.
    ///
    /// <para>Prochází se stejně, jako by šel hráč: od kořene stromu dál. Kdyby
    /// se dědily nejdražší uzly, dostal by hráč konce větví bez jejich základů
    /// a strom by vypadal rozbitě.</para>
    ///
    /// <para>Suroviny se neodečítají — je to dědictví, ne nákup.</para>
    /// </summary>
    /// <returns>Kolik technologií se vrátilo.</returns>
    internal static int GrantTechs(Simulation sim, int count)
    {
        int granted = 0;
        for (int i = 0; i < count; i++)
        {
            int next = CheapestAvailable(sim);
            if (next < 0)
            {
                break; // strom došel
            }

            sim.GrantTechFree(next);
            granted++;
        }

        return granted;
    }

    /// <summary>
    /// Nejlevnější technologie, na kterou jsou splněné <b>předpoklady</b>.
    /// Cena se bere jen jako pořadí, ne jako podmínka: dědictví se neplatí.
    /// </summary>
    private static int CheapestAvailable(Simulation sim)
    {
        int best = -1;
        double bestCost = double.MaxValue;

        for (int i = 0; i < sim.TechCount; i++)
        {
            if (!sim.PrerequisitesMet(i) || sim.IsTechResearched(i))
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

        return best;
    }
}
