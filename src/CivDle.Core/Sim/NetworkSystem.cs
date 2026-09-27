using CivDle.Core.Content;
using CivDle.Core.World;

namespace CivDle.Core.Sim;

/// <summary>
/// Sítě bez drátů — elektřina, voda, teplo, vztlak (svety-design.md 7.2).
///
/// <para><b>Co to je:</b> zobecněný dřívější <c>PowerGridSystem</c>. Zdroj
/// zaplní svou buňku 8×8 a šíří se do sousedních, dokud stačí dosah; výkon
/// rozdělí poměrně podle toho, kolik kde chtějí. Elektřina je jen jeden
/// z druhů — voda na Duně a teplo na Mrazu se chovají stejně, liší se dosahem
/// a tím, co znamená nedostatek (<see cref="NetworkShortage"/>).</para>
///
/// <para><b>Relé</b> (tepelná věž, cisterna, kanát): když síť dosáhne na buňku
/// s relé, teče odtud dál, jako by tam stál zdroj s dosahem relé. Relé samo nic
/// nevyrábí — jen prodlouží dosah zdroje, který na něj dosáhl.</para>
///
/// <para><b>Hrubá mřížka a nízká frekvence.</b> Přepočítává se jen po změně
/// zástavby (CLAUDE.md: růstové systémy nejedou každý tik), bez alokací —
/// slovníky a fronty se jen čistí.</para>
///
/// <para><b>Časované zdroje</b> (zrcadla ve dne, lapač rosy v noci) dodávají
/// podle <see cref="NetworkLight"/>; simulace síť přepočítá, když se změní
/// schod dne (<see cref="SupplyCurve.Phase"/>). Vedle okamžitého stavu se
/// počítá i <b>průměr dne</b> — podle něj plánuje guvernér.</para>
///
/// <para><b>Přírodní zdroje</b> (oáza) jsou dlaždice terénu. Hledají se jen
/// v dosahu buněk, kde někdo síť chce, a součet za buňku se pamatuje, dokud
/// se terén nezmění.</para>
/// </summary>
public sealed class NetworkSystem
{
    /// <summary>Hrana buňky v dlaždicích jako mocnina dvojky (8 = 1 &lt;&lt; 3).</summary>
    public const int CellShift = 3;

    /// <summary>Hrana buňky v dlaždicích (8).</summary>
    public const int CellSize = 1 << CellShift;

    private NetworkGrid[] _grids = Array.Empty<NetworkGrid>();

    /// <summary>Sítě spočítané s průměrem dne; <c>null</c> = žádný časovaný zdroj, platí okamžité.</summary>
    private NetworkGrid[]? _steadyGrids;

    /// <summary>Kolik buněk má v síti aspoň něco (pro testy a diagnostiku).</summary>
    public int CellCount(int network) => network < _grids.Length ? _grids[network].CellCount : 0;

    /// <summary>
    /// Počítadlo přepočtů. Kdo si z pokrytí něco odvozuje (dopad na bydlení),
    /// pozná podle něj, že je jeho výsledek zastaralý.
    /// </summary>
    public int Revision { get; private set; }

    /// <summary>
    /// Přepočítá všechny sítě od základu.
    ///
    /// <para>Od základu, ne přírůstkově: zbourat zdroj znamená ubrat dosah
    /// a dva zdroje mohou pokrývat tutéž čtvrť — jednu záplavu od druhé odečíst
    /// nejde. Volá se jen po změně zástavby.</para>
    /// </summary>
    public void Rebuild(ReadOnlySpan<BuildingInstance> buildings, GameContent content) =>
        Rebuild(buildings, content, NetworkLight.Noon, null, 0, NetworkBoost.None);

    /// <summary>
    /// Přepočet se světlem (časované zdroje) a terénem (přírodní zdroje).
    /// </summary>
    /// <param name="buildings">Budovy.</param>
    /// <param name="content">Obsah (druhy sítí, budovy).</param>
    /// <param name="light">Denní čas a ztlumení slunce.</param>
    /// <param name="biomeAt">Biom na dlaždici (včetně přepisů); <c>null</c> = bez přírodních zdrojů.</param>
    /// <param name="terrainRevision">Verze terénu — po změně se zapomenou součty přírodních zdrojů.</param>
    /// <param name="boost">Vylepšení sítí světa ze Vzestupu (na proud se nevztahují).</param>
    /// <param name="season">Období — mění poptávku (zima na Mrazu chce víc tepla); <c>null</c> = beze změny.</param>
    public void Rebuild(
        ReadOnlySpan<BuildingInstance> buildings, GameContent content, in NetworkLight light,
        Func<int, int, byte>? biomeAt, int terrainRevision, in NetworkBoost boost, SeasonDef? season = null)
    {
        var networks = content.Networks;
        if (_grids.Length != networks.Count)
        {
            _grids = NewGrids(networks.Count);
            _steadyGrids = null;
        }

        bool timed = HasTimedSupply(content);
        if (timed && _steadyGrids is null)
        {
            _steadyGrids = NewGrids(networks.Count);
        }
        else if (!timed)
        {
            _steadyGrids = null;
        }

        var steady = light with { Steady = true };
        for (int n = 0; n < _grids.Length; n++)
        {
            var type = networks[n];
            // Poptávka se dělí DemandMult — víc poptávky v zimě = menší dělitel.
            var own = n == NetworkCatalog.PowerIndex
                ? NetworkBoost.None
                : boost with { DemandMult = boost.DemandMult / (season?.NetworkDemandMult(n) ?? 1.0) };
            int range = type.IsEnabled ? type.Range + own.RangeBonus : 0;

            // Volný výkon chce jen guvernér (a jen u sítí, bez kterých některá
            // budova neběží) — počítá se tedy jen v mřížce, kterou čte on.
            bool spare = HasCutoffConsumer(content, n);
            _grids[n].Rebuild(buildings, content, n, range, light, biomeAt, terrainRevision, own,
                spare && _steadyGrids is null);
            _steadyGrids?[n].Rebuild(buildings, content, n, range, steady, biomeAt, terrainRevision, own, spare);
        }

        Revision++;
    }

    /// <summary>Má síť odběratele s vlastním tvrdým prahem (háj bez vody neurodí)?</summary>
    private static bool HasCutoffConsumer(GameContent content, int network)
    {
        for (int d = 0; d < content.Buildings.Count; d++)
        {
            var uses = content.Buildings[d].Networks;
            for (int i = 0; i < uses.Count; i++)
            {
                if (uses[i].NetworkIndex == network && uses[i].CutoffBelow > 0 && uses[i].Demand > 0)
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>Má obsah zdroj, který dodává jen někdy? Pak se počítá i průměr dne.</summary>
    public static bool HasTimedSupply(GameContent content)
    {
        for (int d = 0; d < content.Buildings.Count; d++)
        {
            if (content.Buildings[d].SupplyTime != SupplyTime.Always && content.Buildings[d].SuppliesNetwork)
            {
                return true;
            }
        }

        return false;
    }

    private static NetworkGrid[] NewGrids(int count)
    {
        var grids = new NetworkGrid[count];
        for (int i = 0; i < grids.Length; i++)
        {
            grids[i] = new NetworkGrid();
        }

        return grids;
    }

    /// <summary>
    /// Pokrytí podle průměru dne (lapač rosy dodává půl dne, zrcadla asi
    /// třetinu). Podle toho plánuje guvernér; bez časovaných zdrojů je to
    /// totéž co <see cref="CoverageAt"/>.
    /// </summary>
    public double SteadyCoverageAt(int network, int x, int y)
    {
        var grids = _steadyGrids ?? _grids;
        return network < grids.Length ? grids[network].CoverageAt(x, y) : 1.0;
    }

    /// <summary>
    /// Kolik volného výkonu (podle průměru dne) na místo dosáhne: součet toho,
    /// co zdroje v dosahu nerozdají svým dosavadním odběratelům. Guvernér se
    /// podle toho ptá „utáhne síť tady ještě háj?".
    ///
    /// <para><b>Proč ne dodávka ani pokrytí:</b> dodávka se zapisuje jen do
    /// buněk s poptávkou — první háj u nové studny by vodu „neviděl" a guvernér
    /// by místo něj stavěl studnu za studnou. A pokrytí je 1 všude, kde zatím
    /// nikdo nic nechce, takže by kupil háje kolem jedné studny, dokud by
    /// nevyschly všechny.</para>
    /// </summary>
    public double SteadySpareAt(int network, int x, int y)
    {
        var grids = _steadyGrids ?? _grids;
        return network < grids.Length ? grids[network].SpareAt(x, y) : 0.0;
    }

    /// <summary>
    /// Jak dobře je místo zásobené: 1 = plně, 0 = nic.
    ///
    /// <para>Při nedostatku klesnou <b>všichni poměrně</b>, ne že první tři
    /// dostanou a zbytek nic — nedostatek je zpomalení čtvrti, ne loterie podle
    /// pořadí v poli.</para>
    /// </summary>
    public double CoverageAt(int network, int x, int y) =>
        network < _grids.Length ? _grids[network].CoverageAt(x, y) : 1.0;

    /// <summary>Kolik výkonu sítě do místa doteče (pro UI a testy).</summary>
    public double SupplyAt(int network, int x, int y) =>
        network < _grids.Length ? _grids[network].SupplyAt(x, y) : 0.0;

    /// <summary>Kolik ze sítě místo chce (pro UI a testy).</summary>
    public double DemandAt(int network, int x, int y) =>
        network < _grids.Length ? _grids[network].DemandAt(x, y) : 0.0;

    /// <summary>Zapomene všechno (nový svět, Vzestup).</summary>
    public void Clear()
    {
        for (int i = 0; i < _grids.Length; i++)
        {
            _grids[i].Clear();
        }

        _steadyGrids = null;
        Revision++;
    }

    /// <summary>
    /// Dodává tahle budova opravdu do sítě?
    ///
    /// <para>Nestačí, že stojí: elektrárna bez uranu a bez směny nevyrábí nic
    /// a nesmí svítit jen proto, že je hotová. Zhaslý zdroj je zhaslý celý —
    /// poloviční dodávka by z jasného „došlo palivo" udělala nevysvětlitelné
    /// zpomalení čtvrti.</para>
    /// </summary>
    internal static bool IsDelivering(in BuildingInstance building) =>
        building.IsComplete && building.Stall switch
        {
            BuildingStall.MissingInput => false,     // došlo palivo
            BuildingStall.NoWorkers => false,        // nemá kdo obsluhovat
            BuildingStall.Damaged => false,          // dostala zásah
            BuildingStall.Buried => false,           // zasypaná studna nedává vodu
            BuildingStall.Flooded => false,          // zaplavená pec nehřeje
            BuildingStall.NetworkShortage => false,  // sama vypadla (zamrzlý kotel nehřeje)
            _ => true,
        };

    internal static long CellOf(int x, int y) => TileKey.Pack(x >> CellShift, y >> CellShift);

    /// <summary>Jedna síť: poptávka a dodávka po buňkách.</summary>
    private sealed class NetworkGrid
    {
        private readonly Dictionary<long, double> _supply = new();
        private readonly Dictionary<long, double> _demand = new();
        private readonly Dictionary<long, double> _spare = new();
        private readonly Dictionary<long, int> _relay = new();
        private readonly Dictionary<long, int> _best = new();
        private readonly Queue<(long Cell, int Remaining)> _frontier = new();

        // Zdroje posledního přepočtu — pro druhý průchod (volný výkon).
        private readonly List<(long Cell, double Power, int Range)> _sources = new();
        private bool _trackSpare;

        // Přírodní zdroje: kolik dodá buňka (součet dlaždic), dokud se nezmění terén.
        private readonly Dictionary<long, double> _terrainSupply = new();
        private readonly HashSet<long> _terrainCandidates = new();
        private int _terrainRevision = -1;

        public int CellCount => _supply.Count + _demand.Count;

        public void Rebuild(
            ReadOnlySpan<BuildingInstance> buildings, GameContent content, int network, int range,
            in NetworkLight light, Func<int, int, byte>? biomeAt, int terrainRevision, in NetworkBoost boost,
            bool trackSpare)
        {
            Clear();
            _trackSpare = trackSpare;
            if (range <= 0)
            {
                return;
            }

            // Napřed spotřeba a relé: každá budova, která síť chce nebo přenáší,
            // se zapíše do své buňky. Až pak se rozlévá výroba, aby bylo co pokrývat.
            for (int i = 0; i < buildings.Length; i++)
            {
                if (!buildings[i].IsComplete)
                {
                    continue;
                }

                var def = content.Buildings[buildings[i].DefIndex];
                if (!def.TouchesNetworks)
                {
                    continue;
                }

                long cell = CellOf(buildings[i].X, buildings[i].Y);
                int demand = def.DemandOf(network);
                if (demand > 0)
                {
                    _demand[cell] = _demand.GetValueOrDefault(cell) + demand / boost.DemandMult;
                }

                int relay = RelayOf(def, network);
                if (relay > 0 && NetworkSystem.IsDelivering(buildings[i]))
                {
                    _relay[cell] = Math.Max(_relay.GetValueOrDefault(cell), Math.Min(relay, MaxRange));
                }
            }

            for (int i = 0; i < buildings.Length; i++)
            {
                var def = content.Buildings[buildings[i].DefIndex];
                if (!def.TouchesNetworks)
                {
                    continue;
                }

                int supply = def.SupplyOf(network);
                if (supply > 0 && NetworkSystem.IsDelivering(buildings[i]))
                {
                    double power = supply * light.FactorFor(def.SupplyTime) * boost.SupplyMult;
                    if (power > 0)
                    {
                        Spread(CellOf(buildings[i].X, buildings[i].Y), power, Math.Min(range, MaxRange));
                    }
                }
            }

            var sources = content.Networks[network].TerrainSources;
            if (sources.Count > 0 && biomeAt is not null)
            {
                SpreadTerrain(sources, biomeAt, terrainRevision, Math.Min(range, MaxRange), boost.SupplyMult);
            }

            if (_trackSpare)
            {
                ComputeSpare();
            }
        }

        /// <summary>
        /// Druhý průchod: kolik z výkonu každého zdroje odběratelé opravdu
        /// spotřebují. Zdroj dá buňce podíl podle poptávky; když buňka dostane
        /// víc, než chce (dvě studny u jednoho háje), přebytek každého zdroje
        /// se vrátí do volného. Nevyužitý výkon zdroje pak patří každé buňce
        /// v jeho dosahu.
        ///
        /// <para>Jednoduchý rozdíl „výkon − poptávka v dosahu" nestačí: dvě
        /// studny u jednoho háje by každá viděla celou jeho poptávku a obě by
        /// hlásily nulu — guvernér by pak stavěl studnu za studnou.</para>
        /// </summary>
        private void ComputeSpare()
        {
            for (int s = 0; s < _sources.Count; s++)
            {
                var (origin, power, range) = _sources[s];
                Reach(origin, range);

                double wanted = 0;
                foreach (long cell in _best.Keys)
                {
                    wanted += _demand.GetValueOrDefault(cell);
                }

                double used = 0;
                if (wanted > 0)
                {
                    foreach (long cell in _best.Keys)
                    {
                        double demand = _demand.GetValueOrDefault(cell);
                        if (demand > 0)
                        {
                            double given = power * demand / wanted;
                            used += given * Math.Min(1.0, demand / _supply[cell]);
                        }
                    }
                }

                double unused = power - used;
                if (unused <= 1e-9)
                {
                    continue;
                }

                foreach (long cell in _best.Keys)
                {
                    _spare[cell] = _spare.GetValueOrDefault(cell) + unused;
                }
            }
        }

        /// <summary>
        /// Přírodní zdroje: buňky v dosahu někoho, kdo síť chce, a v nich
        /// dlaždice zdrojového biomu. Oáza daleko od města se nepočítá — nemá
        /// komu dodávat.
        /// </summary>
        private void SpreadTerrain(
            IReadOnlyList<TerrainSource> sources, Func<int, int, byte> biomeAt, int terrainRevision, int range,
            double supplyMult)
        {
            if (terrainRevision != _terrainRevision)
            {
                _terrainSupply.Clear();
                _terrainRevision = terrainRevision;
            }

            _terrainCandidates.Clear();
            foreach (long cell in _demand.Keys)
            {
                int cellX = TileKey.X(cell);
                int cellY = TileKey.Y(cell);
                for (int dy = -range; dy <= range; dy++)
                {
                    int span = range - Math.Abs(dy);
                    for (int dx = -span; dx <= span; dx++)
                    {
                        _terrainCandidates.Add(TileKey.Pack(cellX + dx, cellY + dy));
                    }
                }
            }

            foreach (long cell in _terrainCandidates)
            {
                if (!_terrainSupply.TryGetValue(cell, out double supply))
                {
                    supply = TerrainSupplyOf(cell, sources, biomeAt);
                    _terrainSupply[cell] = supply;
                }

                if (supply > 0)
                {
                    Spread(cell, supply * supplyMult, range);
                }
            }
        }

        private static double TerrainSupplyOf(long cell, IReadOnlyList<TerrainSource> sources, Func<int, int, byte> biomeAt)
        {
            int originX = TileKey.X(cell) << CellShift;
            int originY = TileKey.Y(cell) << CellShift;
            double supply = 0;
            for (int y = 0; y < CellSize; y++)
            {
                for (int x = 0; x < CellSize; x++)
                {
                    byte biome = biomeAt(originX + x, originY + y);
                    for (int s = 0; s < sources.Count; s++)
                    {
                        if (sources[s].BiomeIndex == biome)
                        {
                            supply += sources[s].SupplyPerTile;
                        }
                    }
                }
            }

            return supply;
        }

        public double CoverageAt(int x, int y)
        {
            long cell = CellOf(x, y);
            double demand = _demand.GetValueOrDefault(cell);
            if (demand <= 0)
            {
                return 1.0; // nikdo tu nic nechce, tedy nikomu nic nechybí
            }

            return Math.Clamp(_supply.GetValueOrDefault(cell) / demand, 0.0, 1.0);
        }

        public double SupplyAt(int x, int y) => _supply.GetValueOrDefault(CellOf(x, y));

        public double DemandAt(int x, int y) => _demand.GetValueOrDefault(CellOf(x, y));

        public double SpareAt(int x, int y) => _spare.GetValueOrDefault(CellOf(x, y));

        public void Clear()
        {
            _supply.Clear();
            _demand.Clear();
            _spare.Clear();
            _sources.Clear();
            _relay.Clear();
        }

        /// <summary>
        /// Rozlije výkon jednoho zdroje do okolí.
        ///
        /// <para><b>Poměrně, ne kdo dřív přijde.</b> Zdroj si napřed spočítá,
        /// kolik po něm v dosahu celkem chtějí, a pak každé buňce dá její podíl.
        /// Výsledek je tím nezávislý na pořadí zdrojů.</para>
        ///
        /// <para><b>Dosah s relé</b> se počítá jako „kolik kroků ještě zbývá":
        /// buňka s relé zbytek doplní na dosah relé. Buňka, na kterou se později
        /// dostane lepší zbytek (přes relé z jiné strany), se rozšíří znovu —
        /// zbytek je shora omezený, takže se to zastaví. Bez relé je to přesně
        /// původní šíření do vzdálenosti <c>range</c> po čtyřech směrech a každá
        /// buňka se projde jednou.</para>
        /// </summary>
        private void Spread(long origin, double power, int range)
        {
            if (_trackSpare)
            {
                _sources.Add((origin, power, range));
            }

            Reach(origin, range);

            double wanted = 0;
            foreach (long cell in _best.Keys)
            {
                wanted += _demand.GetValueOrDefault(cell);
            }

            if (wanted <= 0)
            {
                return; // v dosahu nikdo nic nechce — není co rozdávat
            }

            foreach (long cell in _best.Keys)
            {
                double demand = _demand.GetValueOrDefault(cell);
                if (demand > 0)
                {
                    _supply[cell] = _supply.GetValueOrDefault(cell) + (power * demand / wanted);
                }
            }
        }

        /// <summary>Buňky v dosahu zdroje (do <see cref="_best"/>), včetně prodloužení přes relé.</summary>
        private void Reach(long origin, int range)
        {
            _best.Clear();
            _frontier.Clear();

            Offer(origin, range);
            while (_frontier.Count > 0)
            {
                var (cell, remaining) = _frontier.Dequeue();
                if (_best[cell] != remaining || remaining <= 0)
                {
                    continue; // mezitím dosažena lépe, nebo už nemá kam
                }

                int cellX = TileKey.X(cell);
                int cellY = TileKey.Y(cell);
                Offer(TileKey.Pack(cellX + 1, cellY), remaining - 1);
                Offer(TileKey.Pack(cellX - 1, cellY), remaining - 1);
                Offer(TileKey.Pack(cellX, cellY + 1), remaining - 1);
                Offer(TileKey.Pack(cellX, cellY - 1), remaining - 1);
            }
        }

        /// <summary>
        /// Nabídne buňce zbytek dosahu (u relé doplněný); zapíše a zařadí ji jen,
        /// když je lepší než dosavadní.
        /// </summary>
        private void Offer(long cell, int remaining)
        {
            int effective = Math.Max(remaining, _relay.GetValueOrDefault(cell));
            if (_best.TryGetValue(cell, out int known) && known >= effective)
            {
                return;
            }

            _best[cell] = effective;
            _frontier.Enqueue((cell, effective));
        }

        private static int RelayOf(BuildingDef def, int network)
        {
            var networks = def.Networks;
            for (int i = 0; i < networks.Count; i++)
            {
                if (networks[i].NetworkIndex == network)
                {
                    return networks[i].RelayRange;
                }
            }

            return 0;
        }
    }

    /// <summary>Nejvyšší dosah, který data připustí (loader hlídá 1–64).</summary>
    private const int MaxRange = 64;
}
