using CivDle.Core.Content;
using CivDle.Core.World;

namespace CivDle.Core.Sim;

/// <summary>
/// Kolik toho v krajině zbývá: eviduje jen dlaždice, do kterých hráč zasáhl.
///
/// <para>Proč jen ty: mapa je nekonečná, takže si stav každého stromu pamatovat
/// nejde. Nedotčená dlaždice je implicitně plná — v evidenci nemá záznam a nic
/// nestojí. Paměť tak roste s tím, co hráč opravdu vytěžil, ne s velikostí
/// světa.</para>
///
/// <para>Dorůstání je čistá funkce času: uloží se tik, kdy uzel došel, a při
/// dalším dotazu se spočítá, jestli už je zpátky. Žádné procházení dlaždic
/// každý tik — stejný trik jako u počasí a ročních období.</para>
/// </summary>
public sealed class NodeLedger
{
    /// <summary>Stav jedné načaté dlaždice.</summary>
    private struct NodeState
    {
        /// <summary>Kolik sběrů ještě zbývá.</summary>
        public int ChargesLeft;

        /// <summary>Tik, ve kterém uzel došel (platné jen když <see cref="ChargesLeft"/> = 0).</summary>
        public long DepletedTick;
    }

    private readonly Dictionary<long, NodeState> _touched = new();

    /// <summary>Kolik dlaždic je zrovna načatých nebo vytěžených (do savu i statistik).</summary>
    public int TouchedCount => _touched.Count;

    /// <summary>Je na dlaždici zrovna co sbírat?</summary>
    public bool IsAvailable(int x, int y, ClickYield yield, long tick)
    {
        if (!yield.IsExhaustible)
        {
            return true; // nevyčerpatelný uzel (staré chování, ložiska bez dorůstání)
        }

        if (!_touched.TryGetValue(TileKey.Pack(x, y), out var state))
        {
            return true; // nedotčená dlaždice je plná
        }

        if (state.ChargesLeft > 0)
        {
            return true;
        }

        return yield.IsRenewable && tick - state.DepletedTick >= yield.RegrowTicks;
    }

    /// <summary>
    /// Kolik sběrů dlaždici zbývá (pro vykreslení „nakousnutého" uzlu). Vrací
    /// plný počet u nedotčené i dorostlé dlaždice.
    /// </summary>
    public int ChargesLeft(int x, int y, ClickYield yield, long tick)
    {
        if (!yield.IsExhaustible)
        {
            return int.MaxValue;
        }

        if (!_touched.TryGetValue(TileKey.Pack(x, y), out var state))
        {
            return yield.Charges;
        }

        if (state.ChargesLeft > 0)
        {
            return state.ChargesLeft;
        }

        return yield.IsRenewable && tick - state.DepletedTick >= yield.RegrowTicks ? yield.Charges : 0;
    }

    /// <summary>
    /// Odečte jeden sběr. Vrací false, když na dlaždici nic není — volající pak
    /// sběr vůbec neprovede.
    /// </summary>
    public bool TryConsume(int x, int y, ClickYield yield, long tick)
    {
        if (!yield.IsExhaustible)
        {
            return true;
        }

        long key = TileKey.Pack(x, y);
        int left = ChargesLeft(x, y, yield, tick);
        if (left <= 0)
        {
            return false;
        }

        left--;
        _touched[key] = new NodeState
        {
            ChargesLeft = left,
            DepletedTick = left == 0 ? tick : 0,
        };

        // Dlaždice, která dorostla do plného stavu, se z evidence smaže — jinak
        // by paměť rostla i tam, kde se les dávno vrátil.
        if (left == yield.Charges)
        {
            _touched.Remove(key);
        }

        return true;
    }

    /// <summary>Vrátí dlaždici do plného stavu (zasazení háje na vytěžené místo).</summary>
    public void Restore(int x, int y) => _touched.Remove(TileKey.Pack(x, y));

    /// <summary>
    /// Vypálí dlaždici naráz, bez sběru — les po dopadu meteoritu shoří, nikdo
    /// ho nevytěžil. Dorůstání běží dál od <paramref name="tick"/>, takže se
    /// krajina po čase vzpamatuje.
    /// </summary>
    public void Deplete(int x, int y, long tick) =>
        _touched[TileKey.Pack(x, y)] = new NodeState { ChargesLeft = 0, DepletedTick = tick };

    /// <summary>Záznamy k uložení do savu.</summary>
    public IEnumerable<(int X, int Y, int ChargesLeft, long DepletedTick)> Entries()
    {
        foreach (var (key, state) in _touched)
        {
            yield return (TileKey.X(key), TileKey.Y(key), state.ChargesLeft, state.DepletedTick);
        }
    }

    /// <summary>Obnoví záznam ze savu.</summary>
    public void RestoreEntry(int x, int y, int chargesLeft, long depletedTick) =>
        _touched[TileKey.Pack(x, y)] = new NodeState { ChargesLeft = chargesLeft, DepletedTick = depletedTick };

    /// <summary>Vyprázdní evidenci (Vzestup přestavuje svět od nuly).</summary>
    public void Clear() => _touched.Clear();
}
