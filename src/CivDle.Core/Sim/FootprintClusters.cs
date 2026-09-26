using CivDle.Core.Content;

namespace CivDle.Core.Sim;

/// <summary>
/// Shluky budov podle mezery mezi půdorysy: dvě budovy patří k sobě, když je
/// mezi nimi nejvýš <c>maxGap</c> volných dlaždic (Čebyševova mezera, 0 =
/// dotýkají se). Union-find, reprezentant shluku je nejnižší pozice — tedy
/// nejstarší budova, protože se budovy do pole jen přidávají.
///
/// <para><b>Proč vlastní třída:</b> sídla i čtvrti dřív porovnávaly každou
/// budovu s každou („budov jsou stovky, ne miliony"). U velkého města to byla
/// většina ceny tiku: 24 000 budov = 290 milionů porovnání při každém
/// přepočtu, tik trval 42 ms a jeden přepočet skoro dvě sekundy — dohánění
/// offline času pak trvalo hodiny. Tady se porovnávají jen budovy
/// v sousedních buňkách mřížky, takže cena roste s počtem budov lineárně.</para>
///
/// <para><b>Mřížka:</b> buňka má hranu <c>maxGap + nejširší půdorys</c>. Dvě
/// budovy s mezerou do <c>maxGap</c> mají rohy nejvýš o tolik od sebe, takže
/// leží ve stejné nebo sousední buňce. Buňky se neukládají do slovníku, ale
/// seřadí se podle klíče (<see cref="Array.Sort{TKey, TValue}(TKey[], TValue[], int, int)"/>)
/// a sousedé se hledají půlením intervalu — bez alokací za běhu; pole rostou
/// jen s městem.</para>
///
/// <para>Výsledek je <b>přesně stejný</b> jako u porovnání každé s každou
/// (hlídá to test na náhodných městech): union-find nezávisí na pořadí
/// spojování a kořen je vždy nejnižší pozice ve shluku.</para>
///
/// <para>Vrstva: simulace, běží na nízké frekvenci (jen po změně zástavby).</para>
/// </summary>
internal sealed class FootprintClusters
{
    private readonly GameContent _content;
    private int[] _parent = Array.Empty<int>();
    private long[] _keys = Array.Empty<long>();
    private int[] _order = Array.Empty<int>();

    public FootprintClusters(GameContent content) => _content = content;

    /// <summary>
    /// Spojí kandidáty do shluků. <paramref name="candidates"/> jsou indexy budov;
    /// shluky se pak ptají po <b>pozicích</b> v tomto seznamu (<see cref="Root"/>).
    /// </summary>
    public void Build(ReadOnlySpan<BuildingInstance> buildings, ReadOnlySpan<int> candidates, int maxGap)
    {
        int count = candidates.Length;
        Ensure(count);
        for (int i = 0; i < count; i++)
        {
            _parent[i] = i;
        }

        if (count < 2)
        {
            return;
        }

        // Nejširší půdorys mezi kandidáty, ne v celém obsahu: kosmodrom 7×7,
        // který ve městě nestojí, by zbytečně nafoukl buňky a s nimi i počet
        // porovnání.
        int widest = 1;
        for (int i = 0; i < count; i++)
        {
            var def = _content.Buildings[buildings[candidates[i]].DefIndex];
            widest = Math.Max(widest, Math.Max(def.FootprintWidth, def.FootprintHeight));
        }

        int cell = Math.Max(1, maxGap + widest);
        for (int i = 0; i < count; i++)
        {
            ref readonly var b = ref buildings[candidates[i]];
            _keys[i] = Key(FloorDiv(b.X, cell), FloorDiv(b.Y, cell));
            _order[i] = i;
        }

        Array.Sort(_keys, _order, 0, count);

        for (int p = 0; p < count; p++)
        {
            long key = _keys[p];
            int cx = (int)(key >> 32);
            int cy = (int)(uint)key;

            // Stejná buňka: jen dvojice dál v pořadí, ať se nic neporovná dvakrát.
            for (int q = p + 1; q < count && _keys[q] == key; q++)
            {
                TryJoin(buildings, candidates, _order[p], _order[q], maxGap);
            }

            // Polovina sousedství — každá dvojice buněk se tak projde jednou.
            JoinWithCell(buildings, candidates, p, count, Key(cx, cy + 1), maxGap);
            JoinWithCell(buildings, candidates, p, count, Key(cx + 1, cy - 1), maxGap);
            JoinWithCell(buildings, candidates, p, count, Key(cx + 1, cy), maxGap);
            JoinWithCell(buildings, candidates, p, count, Key(cx + 1, cy + 1), maxGap);
        }
    }

    /// <summary>Reprezentant shluku pozice — nejnižší pozice ve shluku.</summary>
    public int Root(int position)
    {
        int i = position;
        while (_parent[i] != i)
        {
            _parent[i] = _parent[_parent[i]];
            i = _parent[i];
        }

        return i;
    }

    /// <summary>Čebyševova mezera mezi půdorysy (0 = dotýkají se nebo překrývají).</summary>
    public int FootprintGap(in BuildingInstance a, in BuildingInstance b)
    {
        var defA = _content.Buildings[a.DefIndex];
        var defB = _content.Buildings[b.DefIndex];

        int gapX = Math.Max(0, Math.Max(a.X - (b.X + defB.FootprintWidth - 1), b.X - (a.X + defA.FootprintWidth - 1)) - 1);
        int gapY = Math.Max(0, Math.Max(a.Y - (b.Y + defB.FootprintHeight - 1), b.Y - (a.Y + defA.FootprintHeight - 1)) - 1);
        return Math.Max(gapX, gapY);
    }

    private void JoinWithCell(
        ReadOnlySpan<BuildingInstance> buildings, ReadOnlySpan<int> candidates, int p, int count, long cellKey, int maxGap)
    {
        int q = LowerBound(cellKey, count);
        for (; q < count && _keys[q] == cellKey; q++)
        {
            TryJoin(buildings, candidates, _order[p], _order[q], maxGap);
        }
    }

    private void TryJoin(ReadOnlySpan<BuildingInstance> buildings, ReadOnlySpan<int> candidates, int a, int b, int maxGap)
    {
        int rootA = Root(a);
        int rootB = Root(b);

        // Už jsou spolu: mezeru není třeba počítat. V hustém městě je tohle
        // většina dvojic, takže se ušetří víc než polovina práce.
        if (rootA == rootB || FootprintGap(buildings[candidates[a]], buildings[candidates[b]]) > maxGap)
        {
            return;
        }

        // Nižší kořen vyhrává → stabilní reprezentant (nejstarší budova shluku).
        if (rootA < rootB)
        {
            _parent[rootB] = rootA;
        }
        else
        {
            _parent[rootA] = rootB;
        }
    }

    private int LowerBound(long key, int count)
    {
        int lo = 0, hi = count;
        while (lo < hi)
        {
            int mid = (lo + hi) >>> 1;
            if (_keys[mid] < key)
            {
                lo = mid + 1;
            }
            else
            {
                hi = mid;
            }
        }

        return lo;
    }

    private void Ensure(int count)
    {
        if (_parent.Length >= count)
        {
            return;
        }

        int size = Math.Max(count, Math.Max(64, _parent.Length * 2));
        _parent = new int[size];
        _keys = new long[size];
        _order = new int[size];
    }

    /// <summary>Klíč buňky; řadí se podle X, pak podle Y (stačí, aby byl stejný pro stejnou buňku).</summary>
    private static long Key(int cellX, int cellY) => ((long)cellX << 32) | (uint)cellY;

    private static int FloorDiv(int value, int divisor)
    {
        int q = value / divisor;
        return (value % divisor != 0 && (value < 0) != (divisor < 0)) ? q - 1 : q;
    }
}
