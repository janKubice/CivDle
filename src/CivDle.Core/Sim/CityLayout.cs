namespace CivDle.Core.Sim;

/// <summary>
/// Jak má město vypadat, když ho staví automat.
///
/// <para>Dřív bral každý automat první volné políčko, na které narazil —
/// auto-stavba od kotvy, plnění zóny od levého horního rohu. Výsledek byl
/// beztvará skvrna a zóna vylitá jako slitek: přesně ta „náhodná změť", kvůli
/// které automatika vypadala jako chyba, a ne jako civilizace.</para>
///
/// <para>Pravidla jsou úmyslně tři a hrubá — na hezké město stačí: <b>stavět
/// k cestám</b> (ulice vznikají samy), <b>lepit se k sousedům</b> (zástavba je
/// celistvá, ne roztroušená) a <b>nezazdít se</b> (dům bez jediné volné strany
/// vypadá špatně a nemá kudy ven).</para>
///
/// <para>Bydlí to v jednom místě proto, aby auto-stavba a zóny stavěly stejně.
/// Dva různé vkusy v jednom městě jsou vidět víc než jeden nedokonalý.</para>
///
/// <para>Vrstva: čistá simulace, jen čte. Volá se jednou za interval nad
/// hrstkou míst, ne v tikové smyčce.</para>
/// </summary>
internal static class CityLayout
{
    /// <summary>Cesta u domu váží nejvíc — kolem ní vznikají ulice.</summary>
    private const int RoadWeight = 9;

    /// <summary>Soused drží zástavbu pohromadě, ale slaběji než cesta.</summary>
    private const int NeighbourWeight = 3;

    /// <summary>Sráží místo bez jediné volné strany.</summary>
    private const int EnclosedPenalty = 14;

    /// <summary>Jak hezky místo zapadne do města. Vyšší je lepší.</summary>
    public static int Score(Simulation sim, int x, int y)
    {
        int roads = 0;
        int neighbours = 0;

        // Jen čtyři ortogonální sousedé: ulice a fronta domů se poznají podle
        // stran, ne podle rohů.
        Look(sim, x - 1, y, ref roads, ref neighbours);
        Look(sim, x + 1, y, ref roads, ref neighbours);
        Look(sim, x, y - 1, ref roads, ref neighbours);
        Look(sim, x, y + 1, ref roads, ref neighbours);

        int score = (roads * RoadWeight) + (neighbours * NeighbourWeight);
        if (roads + neighbours == 4)
        {
            score -= EnclosedPenalty;
        }

        return score;
    }

    private static void Look(Simulation sim, int x, int y, ref int roads, ref int neighbours)
    {
        if (sim.HasRoadAt(x, y))
        {
            roads++;
        }
        else if (sim.IsOccupied(x, y))
        {
            neighbours++;
        }
    }
}
