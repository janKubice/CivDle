namespace CivDle.Core.Content;

/// <summary>
/// Co hráči odemknou dohrané výzvy (scénáře).
///
/// <para>Odměna výzvy platí <b>pro hráče</b>, ne pro jeden svět: dohrané výzvy
/// se píšou do profilu a každá další hra z nich dostane klíče odemčení
/// (<c>challenge:&lt;id&gt;</c>), na které míří <c>unlockedBy</c> budov
/// a politik. Obsah tak říká, <b>čím</b> se odemyká, a výzva sama o odměně
/// nic vědět nemusí — jedno místo pravdy, žádné dvojí účetnictví.</para>
///
/// <para>Klíč <c>challenge:all</c> znamená „všechny výzvy z dat" — odměna
/// Mistra. Počítá se tady, ne v datech, protože „všechny" se s každou novou
/// výzvou mění a psát seznam ručně by znamenalo na něj jednou zapomenout.</para>
/// </summary>
public static class ChallengeRewards
{
    /// <summary>Předpona klíče odemčení výzvou.</summary>
    public const string Prefix = "challenge:";

    /// <summary>Vyhrazené ID „všechny výzvy" (<c>challenge:all</c>).</summary>
    public const string AllChallengesId = "all";

    /// <summary>Klíč odemčení pro výzvu.</summary>
    public static string KeyOf(string scenarioId) => Prefix + scenarioId;

    /// <summary>
    /// Klíče odemčení z dohraných výzev. Výzvy, které už v datech nejsou, se
    /// tiše přeskočí — profil přežije i výzvu, kterou pozdější verze vyřadila.
    /// </summary>
    /// <param name="catalog">Výzvy z dat.</param>
    /// <param name="won">ID dohraných výzev z profilu.</param>
    public static IReadOnlyList<string> UnlockKeys(ScenarioCatalog catalog, IEnumerable<string> won)
    {
        var wonSet = new HashSet<string>(won, StringComparer.Ordinal);
        var keys = new List<string>();
        int count = 0;
        for (int i = 0; i < catalog.Count; i++)
        {
            if (wonSet.Contains(catalog[i].Id))
            {
                keys.Add(KeyOf(catalog[i].Id));
                count++;
            }
        }

        if (catalog.Count > 0 && count == catalog.Count)
        {
            keys.Add(KeyOf(AllChallengesId));
        }

        return keys;
    }

    /// <summary>Kolik výzev z dat už hráč dohrál (pro „Síň výzev: 7/11").</summary>
    public static int WonCount(ScenarioCatalog catalog, IEnumerable<string> won)
    {
        var wonSet = new HashSet<string>(won, StringComparer.Ordinal);
        int count = 0;
        for (int i = 0; i < catalog.Count; i++)
        {
            if (wonSet.Contains(catalog[i].Id))
            {
                count++;
            }
        }

        return count;
    }
}
