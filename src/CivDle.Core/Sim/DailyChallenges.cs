using CivDle.Core.Content;

namespace CivDle.Core.Sim;

/// <summary>
/// Výběr a vyhodnocení denních výzev. Čistá statická logika (hodiny i stav jdou
/// parametrem), aby šla testovat bez simulace — perzistenci řeší volající.
///
/// <para>Výběr je odvozený z data, ne z náhody: všichni hráči mají stejný den
/// stejnou trojici a po restartu hry se sada nezmění. Žádný server k tomu není
/// potřeba.</para>
/// </summary>
public static class DailyChallenges
{
    /// <summary>
    /// Které výzvy dnes platí (indexy do fondu, vzestupně, bez opakování).
    /// Deterministické podle <paramref name="dateKey"/>.
    /// </summary>
    public static int[] Select(int poolSize, int dailyCount, string dateKey)
    {
        int take = Math.Clamp(dailyCount, 0, poolSize);
        if (take == 0)
        {
            return Array.Empty<int>();
        }

        // Fisher–Yates s deterministickým generátorem ze dne: rovnoměrný výběr
        // bez opakování a bez závislosti na pořadí ve fondu.
        var order = new int[poolSize];
        for (int i = 0; i < poolSize; i++)
        {
            order[i] = i;
        }

        var rng = new Random(DateSeed(dateKey));
        for (int i = poolSize - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (order[i], order[j]) = (order[j], order[i]);
        }

        var chosen = order.AsSpan(0, take).ToArray();
        Array.Sort(chosen);
        return chosen;
    }

    /// <summary>
    /// Kolik z výzvy je splněno. U kumulativních metrik (nasbíráno, zasazeno…)
    /// se počítá jen dnešní přírůstek nad <paramref name="baseline"/> — jinak by
    /// rozehraná hra splnila „nasbírej 150 dřeva" v okamžiku vydání výzvy.
    /// U stavových metrik (populace, zásoba) platí prostě aktuální hodnota.
    /// </summary>
    public static long Progress(MetricKind kind, long current, long baseline) =>
        IsCumulative(kind) ? Math.Max(0, current - baseline) : current;

    /// <summary>
    /// Roste metrika jen nahoru a dává smysl u ní měřit „co jsem stihl dnes"?
    /// Rozhodnutí patří do kódu, ne do dat — data říkají jen metriku a práh.
    /// </summary>
    public static bool IsCumulative(MetricKind kind) => kind switch
    {
        MetricKind.Harvested => true,
        MetricKind.PlantedNodes => true,
        MetricKind.TerraformedTiles => true,
        MetricKind.TotalBuildings => true,
        MetricKind.AscensionLevel => true,
        MetricKind.DayNumber => true,
        _ => false,
    };

    /// <summary>Stabilní seed ze dne — stejné datum dá vždy stejnou sadu výzev.</summary>
    private static int DateSeed(string dateKey)
    {
        // Vlastní hash: string.GetHashCode() je mezi běhy náhodný, takže by se
        // sada výzev změnila při každém spuštění hry.
        int hash = 17;
        foreach (char c in dateKey)
        {
            hash = unchecked(hash * 31 + c);
        }

        return hash;
    }
}
