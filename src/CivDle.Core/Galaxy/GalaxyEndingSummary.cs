using CivDle.Core.Sim;

namespace CivDle.Core.Galaxy;

/// <summary>Jeden svět v epilogu galaxie: jeho souhrn a hvězdy.</summary>
/// <param name="WorldId">ID světa (Domovina první, pak kolonie v pořadí galaxie).</param>
/// <param name="Summary">Souhrn města toho světa (stejný jako na konci první kapitoly).</param>
/// <param name="Stars">Kolik hvězd svět získal (včetně mistrovské).</param>
public sealed record WorldEnding(string WorldId, EndingSummary Summary, int Stars);

/// <summary>
/// Souhrn celé galaxie pro epilog druhé kapitoly (svety-design.md 2.7):
/// každý založený svět a součty přes galaxii.
///
/// <para><b>Jen čte.</b> Stejně jako <see cref="EndingSummary"/>: epilog se dá
/// pustit znovu z menu a nesmí na galaxii sáhnout. Neaktivní světy se načtou
/// ze snímku jen ke čtení (<see cref="GalaxySession.PeekWorld"/>), jejich
/// souhrn se vezme a simulace se zahodí — v paměti tak nikdy nejsou všechny
/// světy naráz.</para>
/// </summary>
/// <param name="GalacticSeconds">Galaktický čas (hodiny celé galaxie).</param>
/// <param name="Worlds">Založené světy v pořadí galaxie.</param>
/// <param name="TradeShipped">Kolik zboží proteklo obchodními trasami celkem.</param>
public sealed record GalaxyEndingSummary(
    double GalacticSeconds,
    IReadOnlyList<WorldEnding> Worlds,
    double TradeShipped)
{
    /// <summary>Součet nejvyšších populací všech světů.</summary>
    public long PeakPopulation => Worlds.Sum(w => w.Summary.PeakPopulation);

    /// <summary>Kolik budov stojí v celé galaxii.</summary>
    public int Buildings => Worlds.Sum(w => w.Summary.Buildings);

    /// <summary>Hvězdy všech kolonií.</summary>
    public int Stars => Worlds.Sum(w => w.Stars);

    /// <summary>Divy a megastruktury ve všech světech.</summary>
    public long WondersCompleted => Worlds.Sum(w => w.Summary.WondersCompleted);

    /// <summary>Vzestupy ve všech světech.</summary>
    public int Ascensions => Worlds.Sum(w => w.Summary.Ascensions);

    /// <summary>Sestaví souhrn z běžící galaxie. Nic nemění.</summary>
    public static GalaxyEndingSummary Of(GalaxySession session)
    {
        var worlds = new List<WorldEnding>();
        foreach (var world in session.Catalog.Worlds)
        {
            if (!session.State.Records.TryGetValue(world.Id, out var record)
                || (world.Id != session.State.ActiveWorldId && record.Snapshot is null))
            {
                continue; // svět ještě nikdo nezaložil
            }

            var simulation = session.PeekWorld(world.Id);
            worlds.Add(new WorldEnding(world.Id, EndingSummary.Of(simulation), record.Stars.Count));
        }

        double shipped = session.State.Trade.Routes.Sum(r => r.TotalShipped);
        return new GalaxyEndingSummary(session.NowSeconds, worlds, shipped);
    }
}
