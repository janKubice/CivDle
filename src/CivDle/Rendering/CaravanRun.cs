using CivDle.Core.Sim;
using Microsoft.Xna.Framework;

namespace CivDle.Rendering;

/// <summary>
/// Jedna jízda karavany: kde je, kudy jede po silnicích a kolik za cestu zaplatí.
///
/// <para>Oddělené od <see cref="CaravanSystem"/> schválně — systém řeší, kdy
/// karavana přijede a jak se kreslí (potřebuje grafiku), tohle je čistá logika
/// cesty a výplaty, kterou jde otestovat bez okna.</para>
/// </summary>
public sealed class CaravanRun
{
    /// <summary>Kolik suroviny karavana veze za každou projetou dlaždici silnice.</summary>
    public const int PayoutPerTile = 3;

    /// <summary>O kolik zvedne výplatu jeden doprovodný klik (0.15 = +15 %).</summary>
    public const double EscortBonusPerClick = 0.15;

    /// <summary>Strop doprovodných kliků — jinak by šlo klikat donekonečna.</summary>
    public const int MaxEscortClicks = 8;

    /// <summary>Na jakou vzdálenost od středu města se počítá „dorazila".</summary>
    private const int ArriveDistanceSquared = 2;

    public CaravanRun(int tileX, int tileY)
    {
        TileX = tileX;
        TileY = tileY;
    }

    /// <summary>Dlaždice, na které karavana stojí.</summary>
    public int TileX { get; private set; }

    /// <summary>Dlaždice, na které karavana stojí.</summary>
    public int TileY { get; private set; }

    /// <summary>Kolik dlaždic silnice už projela (základ výplaty).</summary>
    public int TilesTravelled { get; private set; }

    /// <summary>Kolikrát ji hráč doprovodil klikem.</summary>
    public int EscortClicks { get; private set; }

    /// <summary>Jede karavana doleva? (Pro překlopení spritu.)</summary>
    public bool FacingLeft { get; private set; }

    /// <summary>Střed dlaždice ve world pixelech.</summary>
    public Vector2 Position =>
        new((TileX + 0.5f) * TerrainRenderer.TileSize, (TileY + 0.5f) * TerrainRenderer.TileSize);

    /// <summary>Přidá doprovodný klik (nad strop se nepřičítá).</summary>
    public void Escort() => EscortClicks = Math.Min(MaxEscortClicks, EscortClicks + 1);

    /// <summary>Stojí karavana u města?</summary>
    public bool HasArrived(Simulation simulation) =>
        DistanceToCity(simulation, TileX, TileY) <= ArriveDistanceSquared;

    /// <summary>
    /// Krok po silnici směrem k městu: ze čtyř sousedů vybere silniční dlaždici
    /// nejblíž centru. Není to hledání cesty — karavana má jet po hráčově síti,
    /// ne najít optimální trasu přes pole. Vrací false ve slepé uličce.
    /// </summary>
    public bool TryStepTowardsCity(Simulation simulation)
    {
        int bestX = TileX, bestY = TileY;
        long bestDistance = DistanceToCity(simulation, TileX, TileY);
        bool found = false;

        Span<(int X, int Y)> neighbours = stackalloc (int, int)[4]
        {
            (TileX + 1, TileY), (TileX - 1, TileY), (TileX, TileY + 1), (TileX, TileY - 1),
        };

        foreach (var (x, y) in neighbours)
        {
            if (!simulation.IsRoad(x, y))
            {
                continue;
            }

            long distance = DistanceToCity(simulation, x, y);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestX = x;
                bestY = y;
                found = true;
            }
        }

        if (!found)
        {
            return false;
        }

        FacingLeft = bestX < TileX;
        TileX = bestX;
        TileY = bestY;
        TilesTravelled++;
        return true;
    }

    /// <summary>Kolik suroviny karavana přiveze (délka trasy × doprovod).</summary>
    public int Payout() =>
        TilesTravelled == 0
            ? 0
            : Math.Max(1, (int)Math.Round(TilesTravelled * PayoutPerTile * (1.0 + EscortClicks * EscortBonusPerClick)));

    /// <summary>Surovina, které má město poměrně nejmíň — karavana veze to, co chybí.</summary>
    public static int ScarcestKnownResource(Simulation simulation)
    {
        int best = -1;
        double bestRatio = double.MaxValue;
        for (int i = 0; i < simulation.ResourceCount; i++)
        {
            if (!simulation.IsResourceKnown(i))
            {
                continue;
            }

            double cap = simulation.GetStorageCap(i);
            double ratio = cap > 0 ? simulation.GetResource(i) / cap : 1.0;
            if (ratio < bestRatio)
            {
                bestRatio = ratio;
                best = i;
            }
        }

        return best;
    }

    private static long DistanceToCity(Simulation simulation, int x, int y)
    {
        long dx = x - simulation.CityCenterX;
        long dy = y - simulation.CityCenterY;
        return dx * dx + dy * dy;
    }
}
