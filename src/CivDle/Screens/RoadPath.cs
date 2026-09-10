using CivDle.Core.Sim;

namespace CivDle.Screens;

/// <summary>
/// Jedna dlaždice tažené silnice a jestli na ní stavba projde.
///
/// <para>Příznak nese trasa, ne až pokládání: hráč musí červenou vidět pod
/// kurzorem, ne se ji dozvědět z toho, že se nic nepostavilo.</para>
/// </summary>
/// <param name="X">Dlaždice vodorovně.</param>
/// <param name="Y">Dlaždice svisle.</param>
/// <param name="Ok">Vznikne tu silnice (nebo v režimu bourání: je co strhnout)?</param>
public readonly record struct RoadGhostTile(int X, int Y, bool Ok);

/// <summary>
/// Trasa tažené silnice z bodu A do bodu B — a co z ní opravdu vznikne.
///
/// <para>Čistá funkce nad simulací: nic nemění, jen se ptá. Díky tomu se dá
/// otestovat bez myši a bez okna, což je celý důvod, proč nesedí uvnitř
/// <see cref="MapTools"/> — tam by se k ní dalo dostat jedině přes stisk
/// tlačítka.</para>
///
/// <para>Vrstva: UI. Simulaci jen čte.</para>
/// </summary>
internal static class RoadPath
{
    /// <summary>
    /// Vytrasuje cestu do „L": nejdřív po delší ose, pak po druhé.
    ///
    /// <para>Lomená cesta je to, co hráč od tažení čeká — úhlopříčka po
    /// dlaždicích vypadá jako schody a v mřížkovém městě se nehodí.</para>
    /// </summary>
    /// <param name="simulation">Simulace, které se trasa ptá na proveditelnost.</param>
    /// <param name="erasing">Bourá se místo staví?</param>
    /// <param name="into">Seznam, do kterého se trasa zapíše (vyprázdní se).</param>
    /// <returns>Kolik dlaždic z trasy opravdu půjde postavit (nebo strhnout).</returns>
    public static int Trace(
        Simulation simulation,
        bool erasing,
        int fromX,
        int fromY,
        int toX,
        int toY,
        List<RoadGhostTile> into)
    {
        into.Clear();
        int usable = 0;

        bool horizontalFirst = Math.Abs(toX - fromX) >= Math.Abs(toY - fromY);
        int x = fromX;
        int y = fromY;
        Add(x, y);

        if (horizontalFirst)
        {
            while (x != toX) { x += Math.Sign(toX - x); Add(x, y); }
            while (y != toY) { y += Math.Sign(toY - y); Add(x, y); }
        }
        else
        {
            while (y != toY) { y += Math.Sign(toY - y); Add(x, y); }
            while (x != toX) { x += Math.Sign(toX - x); Add(x, y); }
        }

        return usable;

        void Add(int tileX, int tileY)
        {
            bool ok = erasing
                ? simulation.IsRoad(tileX, tileY)
                : simulation.CanBuildRoad(tileX, tileY) == PlacementResult.Ok;

            into.Add(new RoadGhostTile(tileX, tileY, ok));
            if (ok)
            {
                usable++;
            }
        }
    }
}
