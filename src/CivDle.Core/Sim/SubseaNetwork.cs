namespace CivDle.Core.Sim;

/// <summary>
/// Kam až od přístavů sahá moře, ve kterém se dá stavět.
///
/// <para><b>Proč to existuje:</b> podmořská budova není samostatná osada — visí
/// na přístavu, který ji zásobuje. Bez dosahu by šel obytný dóm postavit
/// uprostřed oceánu na druhé polokouli a rozhodnutí „kam s přístavem" by
/// nic neznamenalo. Takhle je pořadí obrácené a hra tím získává: nejdřív
/// přístav, a moře kolem něj se tím teprve otevře.</para>
///
/// <para><b>Šíří se to jen po vodě</b>, ne vzdušnou čarou. Poloostrov je tedy
/// hráz a dvě zátoky vedle sebe nejsou totéž co jedna — což je přesně ta
/// vlastnost, kvůli které stojí za to počítat záplavu a ne vzdálenost.</para>
///
/// <para><b>Vrstva a výkon:</b> čistá simulace, nic o kreslení neví. Nepočítá se
/// v tikové smyčce — jen když se změní zástavba a někdo se pak zeptá. Záplava
/// je omezená dosahem, takže jedna kotva stojí nejvýš <c>(2·dosah+1)²</c>
/// navštívených dlaždic, ať je oceán jakkoli velký.</para>
/// </summary>
public sealed class SubseaNetwork
{
    /// <summary>Kotva sítě — půdorys budovy, od které se moře otevírá.</summary>
    /// <param name="X">Levý horní roh.</param>
    /// <param name="Y">Levý horní roh.</param>
    /// <param name="Width">Šířka půdorysu v dlaždicích.</param>
    /// <param name="Height">Výška půdorysu v dlaždicích.</param>
    public readonly record struct Anchor(int X, int Y, int Width, int Height);

    private readonly int _range;
    private readonly Func<int, int, bool> _isWater;
    private readonly HashSet<long> _reach = new();
    private readonly Queue<(int X, int Y, int Steps)> _frontier = new();
    private readonly HashSet<long> _visited = new();

    /// <param name="range">Kolik vodních dlaždic od kotvy síť dosáhne; 0 = vrstva vypnutá.</param>
    /// <param name="isWater">Je na téhle dlaždici voda? Terén zná simulace, síť ne.</param>
    public SubseaNetwork(int range, Func<int, int, bool> isWater)
    {
        _range = Math.Max(0, range);
        _isWater = isWater;
    }

    /// <summary>Dá se pod hladinou vůbec stavět? (Vypnuto = obsah o vrstvě nemluví.)</summary>
    public bool IsEnabled => _range > 0;

    /// <summary>Kolik vodních dlaždic je v dosahu. Pro testy a pro cedulku v UI.</summary>
    public int CoveredTiles => _reach.Count;

    /// <summary>
    /// Přepočítá síť od základu podle zadaných kotev.
    ///
    /// <para>Od základu, ne přírůstkově: zbourat přístav znamená ubrat dosah,
    /// a to se přírůstkově udělat nedá — dvě kotvy mohou pokrývat tutéž
    /// zátoku a odečíst jednu záplavu od druhé nejde. Přepočítává se jen při
    /// změně zástavby, takže na tom nesejde.</para>
    /// </summary>
    public void Rebuild(IReadOnlyList<Anchor> anchors)
    {
        _reach.Clear();
        if (!IsEnabled)
        {
            return;
        }

        for (int i = 0; i < anchors.Count; i++)
        {
            Spread(anchors[i]);
        }
    }

    /// <summary>Je tahle dlaždice v dosahu sítě?</summary>
    public bool Covers(int x, int y) => _reach.Contains(World.TileKey.Pack(x, y));

    /// <summary>Je celý půdorys v dosahu? Půl domu v síti a půl mimo neplatí.</summary>
    public bool CoversFootprint(int x, int y, int width, int height)
    {
        for (int tileY = y; tileY < y + height; tileY++)
        {
            for (int tileX = x; tileX < x + width; tileX++)
            {
                if (!Covers(tileX, tileY))
                {
                    return false;
                }
            }
        }

        return true;
    }

    /// <summary>Zapomene všechno (nový svět, Vzestup).</summary>
    public void Clear() => _reach.Clear();

    /// <summary>
    /// Záplava od jedné kotvy. Semínka jsou vodní dlaždice půdorysu — a když
    /// budova stojí na břehu (přístav), tak voda hned vedle něj.
    /// </summary>
    private void Spread(Anchor anchor)
    {
        _frontier.Clear();
        _visited.Clear();

        for (int tileY = anchor.Y; tileY < anchor.Y + anchor.Height; tileY++)
        {
            for (int tileX = anchor.X; tileX < anchor.X + anchor.Width; tileX++)
            {
                if (_isWater(tileX, tileY))
                {
                    Seed(tileX, tileY);
                }
                else
                {
                    // Přístav stojí na souši; síť začíná u mola, tedy na sousední vodě.
                    Seed(tileX + 1, tileY);
                    Seed(tileX - 1, tileY);
                    Seed(tileX, tileY + 1);
                    Seed(tileX, tileY - 1);
                }
            }
        }

        while (_frontier.Count > 0)
        {
            var (x, y, steps) = _frontier.Dequeue();
            _reach.Add(World.TileKey.Pack(x, y));

            if (steps >= _range)
            {
                continue;
            }

            Step(x + 1, y, steps + 1);
            Step(x - 1, y, steps + 1);
            Step(x, y + 1, steps + 1);
            Step(x, y - 1, steps + 1);
        }
    }

    private void Seed(int x, int y)
    {
        if (_isWater(x, y) && _visited.Add(World.TileKey.Pack(x, y)))
        {
            _frontier.Enqueue((x, y, 0));
        }
    }

    private void Step(int x, int y, int steps)
    {
        if (_isWater(x, y) && _visited.Add(World.TileKey.Pack(x, y)))
        {
            _frontier.Enqueue((x, y, steps));
        }
    }
}
