namespace CivDle.Capture;

/// <summary>
/// Nakreslí půdorys městečka, které vypadá jako městečko — ne jako mřížka.
///
/// <para>Kulisa do traileru má jediný úkol: aby si divák řekl „takhle by to
/// chtělo vypadat". <see cref="CityFixture"/> to neumí a ani nemá — ten pěstuje
/// <b>velkoměsto</b> pravidly hry, a jeho pravidelné bloky vypadají při pohledu
/// na čtyřicet dlaždic jako tabulka.</para>
///
/// <para>Čím se to liší od tabulky:</para>
/// <list type="bullet">
///   <item>ulice mají <b>nepravidelné rozteče</b> (4–6 dlaždic), takže bloky
///     nejsou stejné;</item>
///   <item>středem vedou <b>dvě širší třídy</b>, které se kříží na náměstí;</item>
///   <item>bloky se zastavují <b>po obvodu</b> a uvnitř zůstává dvorek — takhle
///     roste skutečné město a je to ten rozdíl, kvůli kterému to nevypadá jako
///     šachovnice;</item>
///   <item>každý blok má <b>svůj hlavní typ</b> domu a k němu pár výjimek, takže
///     ulice drží charakter a přitom se neopakuje;</item>
///   <item>část bloků se schválně <b>nezastaví vůbec</b> — parky pak ze shora
///     svítí zeleně proti zpevněné zemi.</item>
/// </list>
///
/// <para>Vrstva: čistá funkce (seed → plán). Žádná simulace, žádná grafika,
/// takže se dá otestovat headless.</para>
/// </summary>
internal static class TownPlanner
{
    /// <summary>
    /// Nejmenší a největší rozteč ulic. Rozptyl dělá nestejné bloky — a musí být
    /// pořádný: s rozmezím 4–6 vyšly všechny bloky prakticky stejné a záběr
    /// vypadal jako milimetrový papír.
    /// </summary>
    private const int MinStreetGap = 4;
    private const int MaxStreetGap = 9; // horní mez pro Random.Next (exkluzivní)

    /// <summary>Volný lem kolem městečka — pole a louky, ne domy až k okraji.</summary>
    private const int Margin = 2;

    /// <summary>Kolik bloků (mimo náměstí) zůstane zelených.</summary>
    private const double GreenBlockChance = 0.16;

    /// <summary>Jak často se v bloku objeví jiný dům než ten hlavní.</summary>
    private const double VariationChance = 0.42;

    /// <summary>Jak často dostane roh bloku velký dům 2×2.</summary>
    private const double CornerLandmarkChance = 0.55;

    /// <summary>Jak často se na dvorku uvnitř bloku objeví zeleň.</summary>
    private const double CourtyardGreenChance = 0.22;

    /// <summary>Kolik zástavby zbyde v posledním prstenci bloků (zbytek jsou pole a louky).</summary>
    private const double EdgeDensity = 0.45;

    /// <summary>Sestaví plán městečka daného semínka.</summary>
    public static TownPlan Plan(long seed, int size)
    {
        var rng = new Random((int)(seed & 0x7FFFFFFF));

        bool[] streetX = StreetLines(rng, size);
        bool[] streetY = StreetLines(rng, size);

        var roads = new List<(int X, int Y)>();
        for (int y = Margin; y < size - Margin; y++)
        {
            for (int x = Margin; x < size - Margin; x++)
            {
                if (streetX[x] || streetY[y])
                {
                    roads.Add((x, y));
                }
            }
        }

        var blocksX = Runs(streetX, size);
        var blocksY = Runs(streetY, size);
        var lots = new List<TownLot>();

        int centerBlockX = blocksX.Count / 2;
        int centerBlockY = blocksY.Count / 2;
        int outerRing = Math.Max(
            Math.Max(centerBlockX, blocksX.Count - 1 - centerBlockX),
            Math.Max(centerBlockY, blocksY.Count - 1 - centerBlockY));

        for (int by = 0; by < blocksY.Count; by++)
        {
            for (int bx = 0; bx < blocksX.Count; bx++)
            {
                var (x, width) = blocksX[bx];
                var (y, height) = blocksY[by];
                int ring = Math.Max(Math.Abs(bx - centerBlockX), Math.Abs(by - centerBlockY));

                // Poslední prstenec se staví řidčeji. Bez toho končí město ostrým
                // čtvercem, jako by ho někdo vyřízl — a nic tak nekřičí „kulisa"
                // jako zástavba, která přestane po pravítku.
                double density = ring >= outerRing ? EdgeDensity : 1.0;
                FillBlock(rng, lots, x, y, width, height, RoleFor(rng, ring), density);
            }
        }

        return new TownPlan(size, roads, lots);
    }

    /// <summary>
    /// Kde vedou ulice v jedné ose. Rozteče jsou náhodné v daném rozmezí —
    /// pravidelný krok je to jediné, co z města udělá tabulku.
    ///
    /// <para>Prostřední ulice je <b>dvojitá</b>: hlavní třída. Bez ní má město
    /// všechny ulice stejně důležité a oko nemá kam jít.</para>
    /// </summary>
    private static bool[] StreetLines(Random rng, int size)
    {
        var street = new bool[size];
        var lines = new List<int>();

        for (int pos = Margin; pos < size - Margin; pos += rng.Next(MinStreetGap, MaxStreetGap))
        {
            street[pos] = true;
            lines.Add(pos);
        }

        if (lines.Count > 0)
        {
            int avenue = lines[lines.Count / 2];
            if (avenue + 1 < size - Margin)
            {
                street[avenue + 1] = true;
            }
        }

        return street;
    }

    /// <summary>Souvislé úseky mezi ulicemi = bloky v jedné ose (začátek, délka).</summary>
    private static List<(int Start, int Length)> Runs(bool[] street, int size)
    {
        var runs = new List<(int, int)>();
        int start = -1;

        for (int i = Margin; i < size - Margin; i++)
        {
            if (!street[i])
            {
                start = start < 0 ? i : start;
                continue;
            }

            if (start >= 0)
            {
                runs.Add((start, i - start));
                start = -1;
            }
        }

        if (start >= 0)
        {
            runs.Add((start, size - Margin - start));
        }

        return runs;
    }

    /// <summary>
    /// Čím se blok zastaví podle vzdálenosti od středu. Uprostřed náměstí, kolem
    /// něj obchody, dál se bydlí a na kraji pracuje — tenhle spád má každé město
    /// a bez něj je zástavba jenom rovnoměrná kaše.
    /// </summary>
    private static BlockRole RoleFor(Random rng, int ring)
    {
        if (ring == 0)
        {
            return BlockRole.Plaza;
        }

        if (rng.NextDouble() < GreenBlockChance)
        {
            return BlockRole.Green;
        }

        return ring switch
        {
            1 => BlockRole.Core,
            2 => rng.NextDouble() < 0.35 ? BlockRole.Core : BlockRole.Residential,
            3 => BlockRole.Residential,
            _ => BlockRole.Outskirts,
        };
    }

    /// <summary>Rozvrhne jeden blok podle jeho role.</summary>
    /// <param name="density">Podíl parcel, které se v bloku opravdu zastaví.</param>
    private static void FillBlock(
        Random rng, List<TownLot> lots, int x, int y, int width, int height, BlockRole role, double density)
    {
        if (width <= 0 || height <= 0)
        {
            return;
        }

        var block = new BlockCanvas(lots, x, y, width, height);
        switch (role)
        {
            case BlockRole.Plaza:
                FillPlaza(rng, block);
                return;
            case BlockRole.Green:
                FillGreen(rng, block);
                return;
            default:
                FillPerimeter(rng, block, role, density);
                return;
        }
    }

    /// <summary>
    /// Náměstí: uprostřed pomník, po krajích lavičky a zeleň, jinak dlažba.
    /// Prázdné místo je tady záměr — náměstí bez volné plochy není náměstí.
    /// </summary>
    private static void FillPlaza(Random rng, BlockCanvas block)
    {
        if (!block.TryPlace((block.Width - 2) / 2, (block.Height - 2) / 2, 2, 2, PlazaBig))
        {
            block.TryPlace(0, 0, 1, 1, PlazaSmall);
        }

        foreach (var (lx, ly) in block.Corners())
        {
            if (rng.NextDouble() < 0.7)
            {
                block.TryPlace(lx, ly, 1, 1, PlazaSmall);
            }
        }
    }

    /// <summary>Park: jeden velký záhon a k němu pár stromů, zbytek tráva.</summary>
    private static void FillGreen(Random rng, BlockCanvas block)
    {
        block.TryPlace((block.Width - 2) / 2, (block.Height - 2) / 2, 2, 2, GreenBig);

        foreach (var (lx, ly) in block.Corners())
        {
            if (rng.NextDouble() < 0.5)
            {
                block.TryPlace(lx, ly, 1, 1, GreenSmall);
            }
        }
    }

    /// <summary>
    /// Obvodová zástavba: domy stojí do ulice, uvnitř zůstává dvorek.
    ///
    /// <para>Tohle je jádro celého dojmu. Když se blok zaplní celý, vznikne
    /// souvislý kus zástavby bez struktury; s dvorkem má každý blok obrys
    /// a mezi domy prosvítá zeleň.</para>
    /// </summary>
    private static void FillPerimeter(Random rng, BlockCanvas block, BlockRole role, double density)
    {
        var (small, big) = PaletteFor(role);
        string lead = small[rng.Next(small.Length)];

        // Rohy dostanou velký dům — v každém městě je nároží honosnější než
        // řadový domek uprostřed bloku. Roh se posune dovnitř, aby se 2×2
        // vešlo celé do bloku.
        bool hasCourtyard = block.Width >= 3 && block.Height >= 3;
        if (hasCourtyard)
        {
            foreach (var (cx, cy) in Shuffled(rng, block.Corners()))
            {
                if (rng.NextDouble() < CornerLandmarkChance * density)
                {
                    block.TryPlace(
                        Math.Min(cx, block.Width - 2), Math.Min(cy, block.Height - 2), 2, 2,
                        WithFallback(big, GreenBig));
                }
            }
        }

        for (int ly = 0; ly < block.Height; ly++)
        {
            for (int lx = 0; lx < block.Width; lx++)
            {
                bool onEdge = lx == 0 || ly == 0 || lx == block.Width - 1 || ly == block.Height - 1;
                if (hasCourtyard && !onEdge)
                {
                    // Dvorek: většinou nic, občas kus zeleně.
                    if (rng.NextDouble() < CourtyardGreenChance)
                    {
                        block.TryPlace(lx, ly, 1, 1, GreenSmall);
                    }

                    continue;
                }

                if (rng.NextDouble() >= density)
                {
                    continue; // řídký okraj města: tady zůstane louka
                }

                string pick = rng.NextDouble() < VariationChance ? small[rng.Next(small.Length)] : lead;
                block.TryPlace(lx, ly, 1, 1, WithFallback(Preferring(pick, small), GreenSmall));
            }
        }
    }

    /// <summary>
    /// Kandidáti s vybraným typem na prvním místě a zbytkem palety jako záloha.
    /// <paramref name="first"/> je vždycky z <paramref name="palette"/>.
    /// </summary>
    private static string[] Preferring(string first, string[] palette)
    {
        var ordered = new string[palette.Length];
        ordered[0] = first;

        int next = 1;
        foreach (string id in palette)
        {
            if (id != first)
            {
                ordered[next++] = id;
            }
        }

        return ordered;
    }

    /// <summary>
    /// Přilepí na konec zeleň jako poslední záchranu.
    ///
    /// <para>Paleta domů je vázaná na biomy (do lesa se bydlet nesmí), takže na
    /// dlaždici s jiným podložím propadnou úplně všichni kandidáti a v ulici
    /// zůstane díra. Park smí skoro všude — a je to i pravdivější obrázek:
    /// mezi domy zůstal kus stromů.</para>
    /// </summary>
    private static string[] WithFallback(string[] candidates, string[] fallback) =>
        candidates.Concat(fallback.Except(candidates)).ToArray();

    private static List<(int X, int Y)> Shuffled(Random rng, IReadOnlyList<(int X, int Y)> items)
    {
        var copy = new List<(int X, int Y)>(items);
        for (int i = copy.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (copy[i], copy[j]) = (copy[j], copy[i]);
        }

        return copy;
    }

    /// <summary>
    /// Jeden blok při rozvrhování: ví, co je v něm už zabrané, a nepustí dvě
    /// budovy na totéž místo.
    ///
    /// <para>Vzniklo z chyby, kterou odhalil test: náměstí si nejdřív posadilo
    /// pomník doprostřed a pak lavičky do rohů — a v malém bloku to bylo totéž
    /// místo. Překryv přitom není vidět jako chyba, jen se druhá budova tiše
    /// nepostaví a v ulici zůstane díra.</para>
    /// </summary>
    private sealed class BlockCanvas
    {
        private readonly List<TownLot> _lots;
        private readonly bool[,] _taken;
        private readonly int _originX;
        private readonly int _originY;

        public BlockCanvas(List<TownLot> lots, int x, int y, int width, int height)
        {
            _lots = lots;
            _originX = x;
            _originY = y;
            _taken = new bool[width, height];
            Width = width;
            Height = height;
        }

        public int Width { get; }

        public int Height { get; }

        /// <summary>Rohy bloku v lokálních souřadnicích.</summary>
        public IReadOnlyList<(int X, int Y)> Corners() => new[]
        {
            (0, 0),
            (Width - 1, 0),
            (0, Height - 1),
            (Width - 1, Height - 1),
        };

        /// <summary>Zabere místo a zapíše parcelu. Vrací false, když se tam nevejde.</summary>
        public bool TryPlace(int x, int y, int width, int height, IReadOnlyList<string> candidates)
        {
            if (x < 0 || y < 0 || x + width > Width || y + height > Height)
            {
                return false;
            }

            for (int ty = y; ty < y + height; ty++)
            {
                for (int tx = x; tx < x + width; tx++)
                {
                    if (_taken[tx, ty])
                    {
                        return false;
                    }
                }
            }

            for (int ty = y; ty < y + height; ty++)
            {
                for (int tx = x; tx < x + width; tx++)
                {
                    _taken[tx, ty] = true;
                }
            }

            _lots.Add(new TownLot(_originX + x, _originY + y, width, height, candidates));
            return true;
        }
    }

    private static (string[] Small, string[] Big) PaletteFor(BlockRole role) => role switch
    {
        BlockRole.Core => (CoreSmall, CoreBig),
        BlockRole.Outskirts => (WorkSmall, WorkBig),
        _ => (HomeSmall, HomeBig),
    };

    // ----- palety -----
    //
    // Jsou to ID z dat, ne nové budovy: kulisa smí ukazovat jen to, co ve hře
    // opravdu je. Že sedí i půdorys, hlídá ShowcaseTown při stavbě (fail-fast) —
    // kdyby se v JSON změnila velikost budovy, spadne to hned a hlasitě.

    private static readonly string[] CoreSmall =
        { "brick_house", "tenement", "library", "shrine", "fountain_square", "cottage" };

    private static readonly string[] CoreBig =
        { "market", "townhouses", "manor", "temple", "school", "grand_library" };

    private static readonly string[] HomeSmall =
        { "house", "cottage", "brick_house", "apartment", "tenement" };

    private static readonly string[] HomeBig =
        { "terrace_row", "townhouses", "manor", "housing_block" };

    private static readonly string[] WorkSmall =
        { "workshop", "toolmaker", "sawmill", "lumber_camp", "granary", "depot", "cottage", "house" };

    private static readonly string[] WorkBig =
        { "farm", "windmill", "timber_yard", "warehouse", "tree_nursery" };

    private static readonly string[] GreenSmall = { "park" };

    private static readonly string[] GreenBig = { "city_park", "botanical_garden" };

    private static readonly string[] PlazaSmall = { "fountain_square", "obelisk", "clock_tower", "park" };

    private static readonly string[] PlazaBig = { "great_statue", "triumphal_arch", "observatory" };

    /// <summary>Všechna ID, která plán může chtít — <see cref="ShowcaseTown"/> je odemyká.</summary>
    public static IEnumerable<string> AllBuildingIds =>
        CoreSmall.Concat(CoreBig).Concat(HomeSmall).Concat(HomeBig)
            .Concat(WorkSmall).Concat(WorkBig).Concat(GreenSmall).Concat(GreenBig)
            .Concat(PlazaSmall).Concat(PlazaBig)
            .Distinct();
}
