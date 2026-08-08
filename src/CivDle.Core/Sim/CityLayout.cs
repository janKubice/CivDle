namespace CivDle.Core.Sim;

/// <summary>
/// Jak má město vypadat, když ho staví automat.
///
/// <para>Dřív bral každý automat první volné políčko, na které narazil —
/// auto-stavba od kotvy, plnění zóny od levého horního rohu. Výsledek byl
/// beztvará skvrna a zóna vylitá jako slitek: přesně ta „náhodná změť", kvůli
/// které automatika vypadala jako chyba, a ne jako civilizace.</para>
///
/// <para>Pravidla jsou úmyslně čtyři a hrubá — na hezké město stačí: <b>stavět
/// k cestám</b> (ulice vznikají samy), <b>lepit se k sousedům</b> (zástavba je
/// celistvá, ne roztroušená), <b>nezazdít se</b> (dům bez jediné volné strany
/// vypadá špatně a nemá kudy ven) a <b>nedělat bloky bez konce</b> (řada domů
/// se po pár kusech přeruší, aby se mezi ně vešla ulice).</para>
///
/// <para>První tři jsou <see cref="Score"/> — vážená preference. Poslední je
/// <see cref="WouldWallOffTheBlock"/> — tvrdý zákaz, protože preferenci by
/// zaplňující se zóna nakonec stejně převálcovala.</para>
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

    /// <summary>
    /// Rozteč uliční mřížky v dlaždicích. Bloky vycházejí 5×5 s jednodlaždicovou
    /// mezerou mezi nimi.
    /// </summary>
    private const int StreetPeriod = 6;

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

    /// <summary>
    /// Je dlaždice vyhrazená pro ulici? Automat na ni nestaví.
    ///
    /// <para>Tohle je ten rozdíl mezi čtvrtí a slitkem. Samotné skóre stačit
    /// nemůže: preferenci zaplňující se zóna nakonec vždycky převálcuje (na
    /// konci prostě není kam jinam) a všechny domy skončí v jedné souvislé
    /// ploše, kterou obsluhuje jediná cesta zvenčí. Vyhrazený pruh zůstane
    /// volný, sousední bloky se tím pádem nedotýkají — a auto-silnice do těch
    /// mezer ulice položí, protože každý blok se musí napojit sám.</para>
    ///
    /// <para>Mřížka je ukotvená v počátku světa, ne ve městě: musí být stejná
    /// po celé (nekonečné) mapě, aby na sebe čtvrti navazovaly i po přesunu
    /// centra a napříč sídly.</para>
    ///
    /// <para>Platí jen pro automatiku. Hráč si na ulici postavit smí — je to
    /// jeho město.</para>
    /// </summary>
    public static bool IsReservedForStreet(int x, int y) =>
        Mod(x, StreetPeriod) == 0 || Mod(y, StreetPeriod) == 0;

    /// <summary>Zbytek po dělení, který funguje i pro záporné souřadnice (mapa je nekonečná oběma směry).</summary>
    private static int Mod(int value, int period)
    {
        int rest = value % period;
        return rest < 0 ? rest + period : rest;
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
