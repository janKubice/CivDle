namespace CivDle.Screens;

/// <summary>
/// Seřadí uzly uvnitř vrstev tak, aby se spojnice křížily co nejmíň
/// (Sugiyamův postup pro vrstvené grafy).
///
/// <para>Klíčový krok jsou <b>fiktivní uzly</b>: hrana, která přeskakuje víc než
/// jednu vrstvu, se rozseká na řetěz krátkých hran přes mezilehlé vrstvy. Bez
/// toho se dlouhá hrana při řazení nedá zohlednit — prochází vrstvou, ve které
/// nemá žádný uzel, a kříží tam všechno, co jí přijde do cesty. Právě tyhle
/// „přelety" dělaly z tech stromu pavouka.</para>
///
/// <para>Nad rozšířeným grafem pak běží dvě známé heuristiky: <b>medián</b>
/// (uzel se stěhuje k prostřední výšce svých sousedů, střídavě zleva a zprava)
/// a <b>prohazování</b> sousedních dvojic, které se nechá, jen když křížení ubude.
/// Optimální řešení je NP-těžké; tyhle dvě dají v praxi řádově lepší výsledek
/// než pořadí ze souboru a spočítají se okamžitě.</para>
/// </summary>
internal sealed class LayeredOrdering
{
    private const int MedianSweeps = 12;
    private const int TransposePasses = 8;

    private readonly List<List<int>> _layers = new();
    private readonly List<List<int>> _up = new();   // sousedé o vrstvu vlevo
    private readonly List<List<int>> _down = new(); // sousedé o vrstvu vpravo
    private readonly List<int> _layerOf = new();
    private readonly Dictionary<(int From, int To), List<int>> _waypoints = new();
    private int[] _row = Array.Empty<int>();

    /// <param name="depth">Vrstva každého skutečného uzlu.</param>
    /// <param name="edges">Hrany (od → do); směr musí jít do vyšší vrstvy.</param>
    public LayeredOrdering(IReadOnlyList<int> depth, IReadOnlyList<(int From, int To)> edges)
    {
        for (int i = 0; i < depth.Count; i++)
        {
            AddNode(depth[i]);
        }

        foreach (var (from, to) in edges)
        {
            var dummies = Connect(from, to, depth[from], depth[to]);
            if (dummies.Count > 0)
            {
                _waypoints[(from, to)] = dummies;
            }
        }

        _row = new int[_layerOf.Count];
        AssignRows();
        Order();
    }

    /// <summary>Řada, do které uzel vyšel (0 = nahoře).</summary>
    public int RowOf(int node) => _row[node];

    /// <summary>Kolik uzlů má nejširší vrstva (včetně fiktivních).</summary>
    public int WidestLayer
    {
        get
        {
            int widest = 1;
            foreach (var layer in _layers)
            {
                widest = Math.Max(widest, layer.Count);
            }

            return widest;
        }
    }

    /// <summary>Kolik uzlů (včetně fiktivních) je ve vrstvě daného uzlu.</summary>
    public int LayerSizeOf(int node) => _layers[_layerOf[node]].Count;

    /// <summary>Vrstva uzlu (u fiktivních uzlů sloupec, kterým hrana prochází).</summary>
    public int LayerOf(int node) => _layerOf[node];

    /// <summary>
    /// Body lomu dlouhé hrany (fiktivní uzly), nebo prázdné pro hrany mezi
    /// sousedními vrstvami. Kreslení podle nich hranu vede tam, kde ji řazení
    /// opravdu umístilo — jinak by optimalizace neměla na výsledný obrázek vliv.
    /// </summary>
    public IReadOnlyList<int> WaypointsOf(int from, int to) =>
        _waypoints.TryGetValue((from, to), out var points) ? points : Array.Empty<int>();

    private int AddNode(int layer)
    {
        while (_layers.Count <= layer)
        {
            _layers.Add(new List<int>());
        }

        int index = _layerOf.Count;
        _layerOf.Add(layer);
        _up.Add(new List<int>());
        _down.Add(new List<int>());
        _layers[layer].Add(index);
        return index;
    }

    /// <summary>
    /// Napojí dva uzly; delší hranu rozseká fiktivními uzly po vrstvách a vrátí je
    /// jako body lomu — hrana se pak i kreslí tudy, kudy byla optimalizovaná.
    /// </summary>
    private List<int> Connect(int from, int to, int fromLayer, int toLayer)
    {
        if (toLayer <= fromLayer)
        {
            return new List<int>(); // hrana zpět (jen při rozbitých datech)
        }

        var dummies = new List<int>();
        int previous = from;
        for (int layer = fromLayer + 1; layer < toLayer; layer++)
        {
            int dummy = AddNode(layer);
            dummies.Add(dummy);
            Link(previous, dummy);
            previous = dummy;
        }

        Link(previous, to);
        return dummies;
    }

    private void Link(int upper, int lower)
    {
        _down[upper].Add(lower);
        _up[lower].Add(upper);
    }

    private void AssignRows()
    {
        if (_row.Length < _layerOf.Count)
        {
            Array.Resize(ref _row, _layerOf.Count);
        }

        foreach (var layer in _layers)
        {
            for (int i = 0; i < layer.Count; i++)
            {
                _row[layer[i]] = i;
            }
        }
    }

    private void Order()
    {
        for (int sweep = 0; sweep < MedianSweeps; sweep++)
        {
            bool forward = sweep % 2 == 0;
            for (int i = 0; i < _layers.Count; i++)
            {
                int index = forward ? i : _layers.Count - 1 - i;
                SortByMedian(_layers[index], forward);
            }

            AssignRows();
        }

        Transpose();
    }

    /// <summary>
    /// Seřadí vrstvu podle mediánu řad sousedů. Medián, ne průměr: jeden vzdálený
    /// soused by uzel přetáhl přes půl vrstvy a nadělal víc křížení, než ušetří.
    /// </summary>
    private void SortByMedian(List<int> layer, bool useUpper)
    {
        if (layer.Count < 2)
        {
            return;
        }

        var keys = new double[layer.Count];
        for (int i = 0; i < layer.Count; i++)
        {
            var neighbours = useUpper ? _up[layer[i]] : _down[layer[i]];
            keys[i] = neighbours.Count == 0 ? _row[layer[i]] : Median(neighbours);
        }

        var order = layer.ToArray();
        Array.Sort(keys, order);
        layer.Clear();
        layer.AddRange(order);
    }

    private double Median(List<int> neighbours)
    {
        Span<int> rows = neighbours.Count <= 32 ? stackalloc int[neighbours.Count] : new int[neighbours.Count];
        for (int i = 0; i < neighbours.Count; i++)
        {
            rows[i] = _row[neighbours[i]];
        }

        rows.Sort();
        int middle = rows.Length / 2;
        return rows.Length % 2 == 1 ? rows[middle] : (rows[middle - 1] + rows[middle]) / 2.0;
    }

    /// <summary>
    /// Prohodí sousední dvojici, kdykoli tím ubude křížení mezi jejich vrstvou
    /// a oběma sousedními. Opakuje se, dokud se něco lepší.
    /// </summary>
    private void Transpose()
    {
        for (int pass = 0; pass < TransposePasses; pass++)
        {
            bool improved = false;
            foreach (var layer in _layers)
            {
                for (int i = 0; i + 1 < layer.Count; i++)
                {
                    int a = layer[i], b = layer[i + 1];
                    int before = PairCrossings(a, b);
                    int after = PairCrossings(b, a);
                    if (after >= before)
                    {
                        continue;
                    }

                    (layer[i], layer[i + 1]) = (b, a);
                    _row[a] = i + 1;
                    _row[b] = i;
                    improved = true;
                }
            }

            if (!improved)
            {
                return;
            }
        }
    }

    /// <summary>Kolik křížení vznikne, když uzel <paramref name="upperNode"/> leží nad druhým.</summary>
    private int PairCrossings(int upperNode, int lowerNode) =>
        Inversions(_up[upperNode], _up[lowerNode]) + Inversions(_down[upperNode], _down[lowerNode]);

    private int Inversions(List<int> first, List<int> second)
    {
        int crossings = 0;
        for (int i = 0; i < first.Count; i++)
        {
            for (int j = 0; j < second.Count; j++)
            {
                if (_row[first[i]] > _row[second[j]])
                {
                    crossings++;
                }
            }
        }

        return crossings;
    }
}
