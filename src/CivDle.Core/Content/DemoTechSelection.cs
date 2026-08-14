namespace CivDle.Core.Content;

/// <summary>
/// Které technologie demoverze nabízí.
///
/// <para>Naivní řez „prvních N v pořadí dat" nefunguje: strom <b>není</b> psaný
/// striktně od kořene. V reálných datech je třeba <c>cartography</c> nízko
/// v souboru, ale její předpoklad leží až za hranicí — hráč by pak koukal na
/// uzel, ke kterému v ukázce nevede cesta, a vypadalo by to jako chyba.</para>
///
/// <para>Vybírá se proto <b>uzávěr přes předpoklady</b>: opakovaně se přidá
/// technologie s nejnižším indexem, jejíž předpoklady už ve výběru jsou. Tím je
/// výřez zaručeně souvislý — na všechno, co je vidět, se dá dojít — a zároveň
/// deterministický, takže dvě spuštění dají tentýž strom.</para>
///
/// <para>Vrstva: čistá funkce nad obsahem. Žádný stav, takže jde ověřit bez
/// simulace.</para>
/// </summary>
public static class DemoTechSelection
{
    /// <summary>
    /// Vrátí masku technologií dostupných v ukázce.
    /// </summary>
    /// <param name="techs">Celý strom.</param>
    /// <param name="count">Kolik uzlů má ukázka nabídnout.</param>
    public static bool[] Build(IReadOnlyList<TechDef> techs, int count)
    {
        var allowed = new bool[techs.Count];
        int target = Math.Clamp(count, 0, techs.Count);

        for (int added = 0; added < target; added++)
        {
            int next = NextReachable(techs, allowed);
            if (next < 0)
            {
                // Zbytek stromu visí na něčem, co se do výřezu nevešlo. Radši
                // menší, ale souvislý strom než větší s dírami.
                break;
            }

            allowed[next] = true;
        }

        return allowed;
    }

    /// <summary>
    /// Nejnižší index technologie, kterou už výběr unese (předpoklady splněné),
    /// nebo −1, když žádná taková není.
    /// </summary>
    private static int NextReachable(IReadOnlyList<TechDef> techs, bool[] allowed)
    {
        for (int i = 0; i < techs.Count; i++)
        {
            if (allowed[i] || !PrerequisitesAllowed(techs[i], allowed))
            {
                continue;
            }

            return i;
        }

        return -1;
    }

    private static bool PrerequisitesAllowed(TechDef tech, bool[] allowed)
    {
        foreach (int prereq in tech.PrerequisiteIndices)
        {
            if (!allowed[prereq])
            {
                return false;
            }
        }

        return true;
    }
}
