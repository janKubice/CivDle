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
/// </summary>
public sealed class NetworkSystem
{
    /// <summary>Hrana buňky v dlaždicích jako mocnina dvojky (8 = 1 &lt;&lt; 3).</summary>
    public const int CellShift = 3;

    /// <summary>Hrana buňky v dlaždicích (8).</summary>
    public const int CellSize = 1 << CellShift;

    private NetworkGrid[] _grids = Array.Empty<NetworkGrid>();

    /// <summary>Kolik buněk má v síti aspoň něco (pro testy a diagnostiku).</summary>
    public int CellCount(int network) => network < _grids.Length ? _grids[network].CellCount : 0;

    /// <summary>
    /// Přepočítá všechny sítě od základu.
    ///
    /// <para>Od základu, ne přírůstkově: zbourat zdroj znamená ubrat dosah
    /// a dva zdroje mohou pokrývat tutéž čtvrť — jednu záplavu od druhé odečíst
    /// nejde. Volá se jen po změně zástavby.</para>
    /// </summary>
    public void Rebuild(ReadOnlySpan<BuildingInstance> buildings, GameContent content)
    {
        var networks = content.Networks;
        if (_grids.Length != networks.Count)
        {
            _grids = new NetworkGrid[networks.Count];
            for (int i = 0; i < _grids.Length; i++)
            {
                _grids[i] = new NetworkGrid();
            }
        }

        for (int n = 0; n < _grids.Length; n++)
        {
            var type = networks[n];
            _grids[n].Rebuild(buildings, content, n, type.IsEnabled ? type.Range : 0);
        }
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
            BuildingStall.NetworkShortage => false,  // sama vypadla (zamrzlý kotel nehřeje)
            _ => true,
        };

    internal static long CellOf(int x, int y) => TileKey.Pack(x >> CellShift, y >> CellShift);

    /// <summary>Jedna síť: poptávka a dodávka po buňkách.</summary>
    private sealed class NetworkGrid
    {
        private readonly Dictionary<long, double> _supply = new();
        private readonly Dictionary<long, double> _demand = new();
        private readonly Dictionary<long, int> _relay = new();
        private readonly Dictionary<long, int> _best = new();
        private readonly Queue<(long Cell, int Remaining)> _frontier = new();

        public int CellCount => _supply.Count + _demand.Count;

        public void Rebuild(ReadOnlySpan<BuildingInstance> buildings, GameContent content, int network, int range)
        {
            Clear();
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
                    _demand[cell] = _demand.GetValueOrDefault(cell) + demand;
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
                    Spread(buildings[i].X, buildings[i].Y, supply, Math.Min(range, MaxRange));
                }
            }
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

        public void Clear()
        {
            _supply.Clear();
            _demand.Clear();
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
        private void Spread(int x, int y, double power, int range)
        {
            _best.Clear();
            _frontier.Clear();

            Offer(CellOf(x, y), range);
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
