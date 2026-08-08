using CivDle.Core.Content;
using CivDle.Core.World;

namespace CivDle.Core.Sim;

/// <summary>
/// Co hráč dostane, když se k němu město přidá: hotové budovy a hotové ulice.
/// </summary>
/// <param name="Buildings">Instance budov tak, jak ve světě stály.</param>
/// <param name="Roads">Dlaždice ulic toho města.</param>
public readonly record struct NpcTownHandover(
    IReadOnlyList<BuildingInstance> Buildings,
    IReadOnlyList<RoadTile> Roads);

/// <summary>
/// <b>Postavená</b> cizí města: skutečné budovy a skutečné silnice ve světě.
///
/// <para>Proč vlastní systém a ne pár polí v <see cref="Simulation"/>: cizí
/// město má tytéž entity jako hráčovo (<see cref="BuildingInstance"/>,
/// <see cref="RoadTile"/>), ale <b>nesmí</b> procházet hráčovými systémy —
/// nevyrábí mu suroviny, nezaměstnává jeho lidi, nepočítá se do jeho milníků.
/// Kdyby ležela ve stejném poli, musel by tenhle rozdíl znát každý z dvou set
/// průchodů budovami. Takhle ho zná jedno místo.</para>
///
/// <para>Dřív byla cizí města zvláštní struktura, kterou uměl nakreslit jediný
/// renderer — vypadala jako cedule „tady je město" a po pohlcení se nedalo
/// předat nic, protože nic nestálo. Teď se materializují do týchž instancí,
/// jaké staví hráč, takže je kreslí <c>BuildingRenderer</c> a <c>RoadRenderer</c>
/// stejným kódem a pohlcení je <b>přesun vlastnictví</b>, ne nová stavba.</para>
///
/// <para>Nic z toho se neukládá: plán je čistá funkce seedu a klíče města, takže
/// se po načtení postaví znovu stejně. Ukládá se jen to, co hráč změnil (vztah,
/// pohlcení, zkáza) — a to drží <see cref="NpcCityState"/>.</para>
///
/// <para>Vrstva: čistá simulace, nezná render.</para>
/// </summary>
public sealed class NpcTownSystem
{
    /// <summary>Klíč vyhrazený pro cesty mezi městy — ty nepatří žádnému z nich.</summary>
    private const long LinkOwner = 0;

    private readonly GameContent _content;

    private BuildingInstance[] _buildings = new BuildingInstance[64];
    private long[] _owners = new long[64]; // souběžné pole: čí je budova (SoA)
    private int _count;

    /// <summary>Dlaždice zastavěné cizím městem → index budovy + 1.</summary>
    private readonly Dictionary<long, int> _occupancy = new();

    private readonly List<RoadTile> _roadTiles = new();
    private readonly List<long> _roadOwners = new();   // souběžné pole k _roadTiles
    private readonly Dictionary<long, long> _roads = new(); // dlaždice → majitel

    private readonly HashSet<long> _built = new();  // města, která už stojí
    private readonly HashSet<long> _linked = new(); // cesty, které už vedou

    /// <summary>Trasování cest mezi městy — týž plánovač jako u hráčových cest.</summary>
    private readonly CityRoadPlanner _planner = new();

    /// <summary>
    /// Obdélník, který zástavba města opravdu zabírá. Počítá se jednou při
    /// stavbě — kreslit hranici z pevného poloměru znamenalo, že u malého města
    /// visela v poli a u velkého vedla jeho středem.
    /// </summary>
    private readonly Dictionary<long, TownBounds> _bounds = new();

    /// <summary>Rozsah zástavby jednoho města v dlaždicích (včetně krajních).</summary>
    public readonly record struct TownBounds(int MinX, int MinY, int MaxX, int MaxY);

    public NpcTownSystem(GameContent content) => _content = content;

    /// <summary>Budovy všech postavených cizích měst. Render je kreslí stejně jako hráčovy.</summary>
    public ReadOnlySpan<BuildingInstance> Buildings => _buildings.AsSpan(0, _count);

    /// <summary>Ulice cizích měst a cesty mezi nimi.</summary>
    public IReadOnlyList<RoadTile> RoadTiles => _roadTiles;

    /// <summary>Stojí už tohle město?</summary>
    public bool IsBuilt(long key) => _built.Contains(key);

    /// <summary>Vede už tahle cesta mezi městy?</summary>
    public bool IsLinked(long a, long b) => _linked.Contains(LinkKey(a, b));

    /// <summary>Je na dlaždici cizí silnice?</summary>
    public bool IsRoad(int x, int y) => _roads.ContainsKey(TileKey.Pack(x, y));

    /// <summary>Stojí na dlaždici cizí budova?</summary>
    public bool IsOccupied(int x, int y) => _occupancy.ContainsKey(TileKey.Pack(x, y));

    /// <summary>Zabírá dlaždici cokoli cizího? (Hráč tam pak nesmí stavět.)</summary>
    public bool Blocks(int x, int y)
    {
        long tile = TileKey.Pack(x, y);
        return _occupancy.ContainsKey(tile) || _roads.ContainsKey(tile);
    }

    /// <summary>
    /// Kterému městu patří dlaždice. Tudy se pozná, na co hráč klikl — na město
    /// se míří jeho zástavbou, ne jedním pixelem uprostřed.
    /// </summary>
    public bool TryOwnerAt(int x, int y, out long key)
    {
        long tile = TileKey.Pack(x, y);
        if (_occupancy.TryGetValue(tile, out int slot))
        {
            key = _owners[slot - 1];
            return key != LinkOwner;
        }

        if (_roads.TryGetValue(tile, out key))
        {
            return key != LinkOwner;
        }

        key = 0;
        return false;
    }

    /// <summary>
    /// Postaví město: z plánu udělá skutečné budovy a skutečné silnice.
    ///
    /// <para><paramref name="canBuild"/> rozhoduje o terénu i o kolizích
    /// s hráčovou zástavbou. Vrací počet postavených budov — nula znamená, že
    /// se na to místo nevešlo nic (samá voda, samá skála), a město tam pak
    /// zůstane jen značkou.</para>
    /// </summary>
    public int Build(
        long seed, in NpcCity city, Func<int, int, int, bool> canBuild, Func<int, int, bool> canPave)
    {
        if (!_built.Add(city.Key))
        {
            return 0;
        }

        var plan = NpcTownPlanner.Plan(_content, seed, city, canBuild, canPave);

        for (int i = 0; i < plan.Roads.Count; i++)
        {
            AddRoad(plan.Roads[i].X, plan.Roads[i].Y, city.Key);
        }

        for (int i = 0; i < plan.Buildings.Count; i++)
        {
            var planned = plan.Buildings[i];
            AddBuilding(planned.DefIndex, planned.X, planned.Y, city.Key);
        }

        RememberBounds(city.Key, plan);
        return plan.Buildings.Count;
    }

    /// <summary>Rozsah zástavby města; <c>false</c> = nic tam nestojí.</summary>
    public bool TryBounds(long key, out TownBounds bounds) => _bounds.TryGetValue(key, out bounds);

    /// <summary>
    /// Zapamatuje si, kam až město sahá. Bere ulice i domy — hranice města vede
    /// po okraji zástavby, ne po okraji domů uvnitř ulic.
    /// </summary>
    private void RememberBounds(long key, in NpcTownPlan plan)
    {
        if (plan.Buildings.Count == 0 && plan.Roads.Count == 0)
        {
            return;
        }

        int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;

        for (int i = 0; i < plan.Roads.Count; i++)
        {
            var road = plan.Roads[i];
            minX = Math.Min(minX, road.X);
            minY = Math.Min(minY, road.Y);
            maxX = Math.Max(maxX, road.X);
            maxY = Math.Max(maxY, road.Y);
        }

        for (int i = 0; i < plan.Buildings.Count; i++)
        {
            var b = plan.Buildings[i];
            var def = _content.Buildings[b.DefIndex];
            minX = Math.Min(minX, b.X);
            minY = Math.Min(minY, b.Y);
            maxX = Math.Max(maxX, b.X + def.FootprintWidth - 1);
            maxY = Math.Max(maxY, b.Y + def.FootprintHeight - 1);
        }

        _bounds[key] = new TownBounds(minX, minY, maxX, maxY);
    }

    /// <summary>
    /// Postaví cestu mezi dvěma městy — <b>skutečnou silnicí</b>, ne čárou přes
    /// mapu. Trasa se láme podle toho, které osy zbývá víc, s drobným
    /// rozkolísáním z klíčů měst: rovná linka přes sto dlaždic vypadá jako
    /// kreslený vektor, ne jako cesta, kterou někdo prošlapal.
    /// </summary>
    /// <param name="from">Skutečný začátek (střed zástavby, ne bod z mřížky).</param>
    /// <param name="to">Skutečný konec.</param>
    /// <param name="canPave">Smí na dlaždici ležet silnice? (Voda, hráčova zástavba.)</param>
    public void Link(
        in NpcCityLink link, (int X, int Y) from, (int X, int Y) to, Func<int, int, bool> canPave)
    {
        long pair = LinkKey(link.From.Key, link.To.Key);
        if (!_linked.Add(pair))
        {
            return;
        }

        // Trasu hledá stejný plánovač jako u hráčových cest k městům.
        //
        // Dřív se šlo naslepo k cíli a dlaždice, kam se dláždit nedalo, se prostě
        // PŘESKOČILY — cesta pak měla díry přesně tam, kde překážela řeka nebo
        // skála, a přes vodu z ní zbyla přerušovaná čára. Plánovač kolem překážky
        // uhne, a když cesta nevede vůbec, nepoloží se nic: žádná cesta je
        // uvěřitelnější než cesta do moře.
        var route = new List<(int X, int Y)>();
        bool passable(int x, int y) => canPave(x, y) || IsRoad(x, y);
        if (_planner.TryTrace(passable, canPave, from.X, from.Y, to.X, to.Y, route))
        {
            foreach (var (tileX, tileY) in route)
            {
                AddRoad(tileX, tileY, LinkOwner);
            }
        }
    }

    /// <summary>
    /// Předá město hráči: vyjme jeho budovy a ulice a vrátí je tak, jak stály.
    ///
    /// <para>Tohle je celé pohlcení. <b>Nic se nestaví znovu</b> — kdyby se
    /// stavělo, narazilo by to na to, že hráč tamní budovy ještě nemá vyzkoumané,
    /// a z města by nezbylo nic. Jsou to tytéž instance, jen jim vyměníte
    /// majitele.</para>
    /// </summary>
    public NpcTownHandover Take(long key)
    {
        var buildings = new List<BuildingInstance>();
        var roads = new List<RoadTile>();
        RemoveOwner(key, buildings, roads);
        _built.Remove(key);
        _bounds.Remove(key); // od téhle chvíle to není cizí město, ale hráčova čtvrť
        return new NpcTownHandover(buildings, roads);
    }

    /// <summary>
    /// Srovná město se zemí: zmizí beze stopy a nikomu se nepředává. (Meteorit,
    /// povodeň.) Cesty k němu zůstanou — ty jsou v krajině i po tom, co po městě
    /// zbylo.
    /// </summary>
    public void Forget(long key)
    {
        RemoveOwner(key, null, null);
        _built.Remove(key);
        _bounds.Remove(key);
    }

    /// <summary>Nový svět (Vzestup): cizí města začínají znovu neobjevená.</summary>
    public void Clear()
    {
        _count = 0;
        _occupancy.Clear();
        _roadTiles.Clear();
        _roadOwners.Clear();
        _roads.Clear();
        _built.Clear();
        _linked.Clear();
        _bounds.Clear();
    }

    /// <summary>
    /// Vyjme všechno, co patří jednomu městu. Budovy se z pole odstraňují
    /// prohozením s poslední — pořadí cizích budov nikoho nezajímá a přesouvat
    /// celý zbytek pole při každém pohlcení by bylo zbytečně drahé.
    /// </summary>
    private void RemoveOwner(long key, List<BuildingInstance>? buildings, List<RoadTile>? roads)
    {
        for (int i = _count - 1; i >= 0; i--)
        {
            if (_owners[i] != key)
            {
                continue;
            }

            buildings?.Add(_buildings[i]);
            UnregisterTiles(i);

            int last = _count - 1;
            if (i != last)
            {
                _buildings[i] = _buildings[last];
                _owners[i] = _owners[last];
                RegisterTiles(i); // přesunutá budova musí ukazovat na nový index
            }

            _count = last;
        }

        for (int i = _roadTiles.Count - 1; i >= 0; i--)
        {
            if (_roadOwners[i] != key)
            {
                continue;
            }

            roads?.Add(_roadTiles[i]);
            _roads.Remove(TileKey.Pack(_roadTiles[i].X, _roadTiles[i].Y));
            _roadTiles.RemoveAt(i);
            _roadOwners.RemoveAt(i);
        }
    }

    private void AddRoad(int x, int y, long owner)
    {
        long tile = TileKey.Pack(x, y);
        if (_roads.TryAdd(tile, owner))
        {
            _roadTiles.Add(new RoadTile(x, y));
            _roadOwners.Add(owner);
        }
    }

    private void AddBuilding(int defIndex, int x, int y, long owner)
    {
        if (_count == _buildings.Length)
        {
            Array.Resize(ref _buildings, _buildings.Length * 2);
            Array.Resize(ref _owners, _owners.Length * 2);
        }

        // Hotová budova bez rozpracovaného cyklu: cizí město tam stálo dávno
        // před hráčem. Násobiče zůstávají neutrální — cizí město nevyrábí do
        // hráčova skladu, takže by nebyly k ničemu. Dopočítají se, teprve až
        // budova přejde hráči (tam se stejně přepočítává celý odvozený stav).
        _buildings[_count] = new BuildingInstance
        {
            DefIndex = defIndex,
            X = x,
            Y = y,
            BiomeMult = 1f,
            AdjacencyMult = 1f,
            HaulMult = 1f,
            MilestoneMult = 1f,
            DistrictMult = 1f,
            PollutionMult = 1f,
            DistrictIndex = -1,
            BuildTicksRemaining = 0,
        };
        _owners[_count] = owner;
        _count++;
        RegisterTiles(_count - 1);
    }

    private void RegisterTiles(int index)
    {
        var def = _content.Buildings[_buildings[index].DefIndex];
        int x = _buildings[index].X;
        int y = _buildings[index].Y;
        for (int tileY = y; tileY < y + def.FootprintHeight; tileY++)
        {
            for (int tileX = x; tileX < x + def.FootprintWidth; tileX++)
            {
                _occupancy[TileKey.Pack(tileX, tileY)] = index + 1;
            }
        }
    }

    private void UnregisterTiles(int index)
    {
        var def = _content.Buildings[_buildings[index].DefIndex];
        int x = _buildings[index].X;
        int y = _buildings[index].Y;
        for (int tileY = y; tileY < y + def.FootprintHeight; tileY++)
        {
            for (int tileX = x; tileX < x + def.FootprintWidth; tileX++)
            {
                _occupancy.Remove(TileKey.Pack(tileX, tileY));
            }
        }
    }

    /// <summary>Klíč dvojice měst nezávislý na pořadí — cesta je jen jedna.</summary>
    private static long LinkKey(long a, long b) =>
        unchecked(a < b ? a * 31 + b : b * 31 + a);
}
