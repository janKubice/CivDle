using CivDle.Core.World;

namespace CivDle.Core.Sim;

/// <summary>
/// Trasa dálnice k cizímu městu.
///
/// <para>Proč vlastní třída a ne <see cref="RoadBuilder"/>: ten hledá BFS
/// nejbližší napojení v okruhu pár desítek dlaždic a je stavěný na „dům se
/// napojí na ulici". Cesta k cizímu městu je něco jiného — vede přes půl mapy
/// a hráč u ní čeká rovnou silnici, ne nejkratší klikatinu. Proto se trasuje
/// dopředu: rovně po delší ose, pak po druhé, a jen kolem překážek se uhne.</para>
///
/// <para>Dřív se cesta vůbec nestavěla: zaplatilo se a městu se jen nastavil
/// příznak „spojeno". Na mapě po tom nezůstalo nic — hráč zaplatil a nic
/// neviděl, což vypadalo jako rozbité tlačítko.</para>
///
/// <para>Vrstva: čistá simulace. Nic nekreslí, jen vrací dlaždice.</para>
/// </summary>
internal sealed class CityRoadPlanner
{
    /// <summary>O kolik dlaždic do strany se smí uhnout, než to trasa vzdá.</summary>
    private const int MaxSidestep = 8;

    /// <summary>Jak daleko od středu města se hledá poslední dlaždice cesty.</summary>
    private const int TargetSearchRadius = 12;

    /// <summary>
    /// Najde trasu z bodu k cizímu městu po hráčových pravidlech pro silnice.
    /// Vrací <c>false</c>, když cesta nevede — třeba přes oceán, který se nedá
    /// přemostit.
    /// </summary>
    /// <param name="into">Dlaždice trasy; volající je teprve položí.</param>
    public bool TryTrace(Simulation sim, int fromX, int fromY, int cityX, int cityY, List<(int X, int Y)> into) =>
        TryTrace(
            passable: (x, y) => sim.IsRoad(x, y) || sim.CanBuildRoad(x, y) == PlacementResult.Ok,
            pavable: (x, y) => sim.CanBuildRoad(x, y) == PlacementResult.Ok,
            fromX, fromY, cityX, cityY, into);

    /// <summary>
    /// Najde trasu mezi dvěma body podle zadaných pravidel.
    ///
    /// <para>Pravidla jsou parametr, protože cesty staví dvě různé strany:
    /// hráč (kde platí mosty a vlastní síť) a cizí města mezi sebou (kde
    /// existující cizí ulice cestu jen pokračují). Trasování je ale stejné, a
    /// dvě skoro stejné kopie stejného hledání jsou přesně to, po čem zůstane
    /// v jedné z nich chyba.</para>
    /// </summary>
    /// <param name="passable">Dá se přes dlaždici projet? (Včetně už položené cesty.)</param>
    /// <param name="pavable">Dá se na dlaždici položit NOVÁ cesta?</param>
    public bool TryTrace(
        Func<int, int, bool> passable,
        Func<int, int, bool> pavable,
        int fromX,
        int fromY,
        int cityX,
        int cityY,
        List<(int X, int Y)> into)
    {
        into.Clear();
        if (!TryFindApproach(pavable, cityX, cityY, out int targetX, out int targetY))
        {
            return false;
        }

        int x = fromX;
        int y = fromY;

        // Strop kroků: trasa se smí klikatit, ale nesmí chodit dokola. Násobek
        // vzdálenosti nechá dost prostoru na objížďky a přitom vždycky skončí.
        int budget = (Math.Abs(targetX - x) + Math.Abs(targetY - y)) * 3 + 64;
        var visited = new HashSet<long> { TileKey.Pack(x, y) };

        while ((x != targetX || y != targetY) && budget-- > 0)
        {
            if (!TryStep(passable, visited, ref x, ref y, targetX, targetY))
            {
                return false;
            }

            if (pavable(x, y))
            {
                into.Add((x, y));
            }
        }

        return x == targetX && y == targetY;
    }

    /// <summary>
    /// Jeden krok k cíli: napřed rovně po delší ose, pak po druhé, a teprve když
    /// ani jedno nejde, do strany. Uhýbá se na tu stranu, která cíl neztrácí.
    /// </summary>
    private static bool TryStep(
        Func<int, int, bool> passable, HashSet<long> visited, ref int x, ref int y, int targetX, int targetY)
    {
        int dx = Math.Sign(targetX - x);
        int dy = Math.Sign(targetY - y);
        bool horizontalFirst = Math.Abs(targetX - x) >= Math.Abs(targetY - y);

        // Pořadí pokusů: hlavní osa, vedlejší osa, pak úhyby do stran.
        if (horizontalFirst)
        {
            if (Advance(passable, visited, ref x, ref y, dx, 0)) return true;
            if (Advance(passable, visited, ref x, ref y, 0, dy)) return true;
            return Sidestep(passable, visited, ref x, ref y, 0, 1, targetY);
        }

        if (Advance(passable, visited, ref x, ref y, 0, dy)) return true;
        if (Advance(passable, visited, ref x, ref y, dx, 0)) return true;
        return Sidestep(passable, visited, ref x, ref y, 1, 0, targetX);
    }

    /// <summary>Posune se o krok, jde-li tam vést cesta a nebyli jsme tam.</summary>
    private static bool Advance(
        Func<int, int, bool> passable, HashSet<long> visited, ref int x, ref int y, int dx, int dy)
    {
        if (dx == 0 && dy == 0)
        {
            return false;
        }

        int nextX = x + dx;
        int nextY = y + dy;
        if (!passable(nextX, nextY) || !visited.Add(TileKey.Pack(nextX, nextY)))
        {
            return false;
        }

        x = nextX;
        y = nextY;
        return true;
    }

    /// <summary>
    /// Obchvat kolem překážky: zkusí obě strany kolmé osy a vezme tu, která
    /// míří blíž k cíli. Delší úhyb než <see cref="MaxSidestep"/> už není
    /// objížďka, ale bloudění.
    /// </summary>
    private static bool Sidestep(
        Func<int, int, bool> passable, HashSet<long> visited,
        ref int x, ref int y, int dx, int dy, int targetOnAxis)
    {
        int current = dx != 0 ? x : y;
        int first = targetOnAxis >= current ? 1 : -1;

        foreach (int sign in new[] { first, -first })
        {
            for (int step = 1; step <= MaxSidestep; step++)
            {
                int nextX = x + dx * sign * step;
                int nextY = y + dy * sign * step;
                if (!passable(nextX, nextY))
                {
                    break; // za neprůchodnou dlaždicí se na téhle straně dál nedostaneme
                }

                if (visited.Add(TileKey.Pack(nextX, nextY)))
                {
                    x = nextX;
                    y = nextY;
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Poslední dlaždice cesty: nejbližší místo u města, kam se ještě dá
    /// dláždit. Střed města zabírá samotné město, takže cíl je vždycky kus
    /// vedle — a hledá se v prstencích, ať cesta končí co nejblíž bráně.
    /// </summary>
    private static bool TryFindApproach(
        Func<int, int, bool> pavable, int cityX, int cityY, out int x, out int y)
    {
        for (int radius = 1; radius <= TargetSearchRadius; radius++)
        {
            for (int offsetY = -radius; offsetY <= radius; offsetY++)
            {
                for (int offsetX = -radius; offsetX <= radius; offsetX++)
                {
                    if (Math.Max(Math.Abs(offsetX), Math.Abs(offsetY)) != radius)
                    {
                        continue; // jen okraj prstence
                    }

                    if (pavable(cityX + offsetX, cityY + offsetY))
                    {
                        x = cityX + offsetX;
                        y = cityY + offsetY;
                        return true;
                    }
                }
            }
        }

        x = 0;
        y = 0;
        return false;
    }
}
