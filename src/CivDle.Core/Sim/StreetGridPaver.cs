using CivDle.Core.Content;

namespace CivDle.Core.Sim;

/// <summary>
/// Vydláždí ulice kolem zastavěného bloku.
///
/// <para>Proč to vzniklo: pruhy pro ulice se sice nechávaly volné, ale dláždily
/// se jen tam, kudy zrovna vedla nejkratší cesta k síti. Z toho vznikaly útržky
/// — pár dlaždic tady, pár tam, tvary do „H" — a mezi nimi zely prázdné mezery.
/// Vypadalo to jako chyba, protože to chybou v jistém smyslu bylo: mřížka byla
/// jen <b>zakázaná zástavba</b>, ne skutečná síť ulic.</para>
///
/// <para>Teď se dláždí <b>celý obvod bloku naráz</b>. Sousední bloky pruh sdílejí,
/// takže se ulice samy napojí na sebe a z města je klasická šachovnice bloků,
/// ne cesta prokousaná zástavbou.</para>
///
/// <para>Dláždí se až od <see cref="MinBuildingsForStreets"/> budov v bloku:
/// jedna chalupa v pustině nemá mít kolem sebe okruh ulic, ale rozestavěná
/// čtvrť ano — a tam se práh překročí hned prvním kolem hromadné stavby.</para>
///
/// <para>Vrstva: čistá simulace, volá se při stavbě (ne v tikové smyčce).</para>
/// </summary>
internal sealed class StreetGridPaver
{
    /// <summary>
    /// Od kolika budov v bloku se kolem něj dláždí ulice. Vnitřek bloku má
    /// 25 dlaždic, takže osm znamená „čtvrť se rozjela". Níž by tři chalupy
    /// v pustině dostaly kolem sebe okruh dvaceti čtyř dlaždic dlažby.
    /// </summary>
    private const int MinBuildingsForStreets = 8;

    private readonly GameContent _content;

    public StreetGridPaver(GameContent content) => _content = content;

    /// <summary>
    /// Vydláždí obvod bloku, do kterého dlaždice patří. Vrací <c>true</c>, když
    /// se opravdu něco položilo (volající pak nemusí hledat cestu k síti).
    /// </summary>
    public bool PaveBlockAround(Simulation sim, int x, int y)
    {
        int left = CityLayout.BlockOrigin(x);
        int top = CityLayout.BlockOrigin(y);
        if (CountBuildings(sim, left, top) < MinBuildingsForStreets)
        {
            return false;
        }

        int right = left + CityLayout.StreetPeriod;
        int bottom = top + CityLayout.StreetPeriod;

        bool paved = false;
        for (int tileX = left; tileX <= right; tileX++)
        {
            paved |= Pave(sim, tileX, top);
            paved |= Pave(sim, tileX, bottom);
        }

        for (int tileY = top; tileY <= bottom; tileY++)
        {
            paved |= Pave(sim, left, tileY);
            paved |= Pave(sim, right, tileY);
        }

        return paved;
    }

    /// <summary>Kolik budov stojí uvnitř bloku (jeho vnitřek bez pruhů ulic).</summary>
    private static int CountBuildings(Simulation sim, int left, int top)
    {
        int count = 0;
        for (int y = top + 1; y < top + CityLayout.StreetPeriod; y++)
        {
            for (int x = left + 1; x < left + CityLayout.StreetPeriod; x++)
            {
                if (sim.IsOccupied(x, y))
                {
                    count++;
                }
            }
        }

        return count;
    }

    /// <summary>
    /// Položí jednu dlaždici ulice, pokud to jde. Vodu ani cizí zástavbu
    /// nepřebíjí: přes řeku vede most, který si najde hledání cesty, a co si
    /// hráč postavil na pruhu, to je jeho volba.
    /// </summary>
    private bool Pave(Simulation sim, int x, int y)
    {
        if (sim.IsRoad(x, y) || sim.IsOccupied(x, y) || IsWater(sim, x, y) || WouldFormPatch(sim, x, y))
        {
            return false;
        }

        sim.AddRoadTile(x, y);
        return true;
    }

    /// <summary>
    /// Vznikla by z téhle dlaždice souvislá plocha dlažby 2×2? Z ulic samotných
    /// vzniknout nemůže (pruhy jsou po jedné a šest od sebe), ale starší cesta
    /// vedená těsně vedle pruhu ano — a z ulice by bylo parkoviště.
    /// </summary>
    private static bool WouldFormPatch(Simulation sim, int x, int y)
    {
        for (int cornerY = -1; cornerY <= 0; cornerY++)
        {
            for (int cornerX = -1; cornerX <= 0; cornerX++)
            {
                if (Road(sim, x + cornerX, y + cornerY, x, y)
                    && Road(sim, x + cornerX + 1, y + cornerY, x, y)
                    && Road(sim, x + cornerX, y + cornerY + 1, x, y)
                    && Road(sim, x + cornerX + 1, y + cornerY + 1, x, y))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>Je dlaždice cesta — nebo je to zrovna ta, kterou chceme položit?</summary>
    private static bool Road(Simulation sim, int x, int y, int pendingX, int pendingY) =>
        (x == pendingX && y == pendingY) || sim.IsRoad(x, y);

    private bool IsWater(Simulation sim, int x, int y) =>
        _content.Biomes[sim.BiomeAt(x, y)].IsWater;
}
