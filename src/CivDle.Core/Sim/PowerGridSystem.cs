using CivDle.Core.Content;
using CivDle.Core.World;

namespace CivDle.Core.Sim;

/// <summary>
/// Kam až dosáhne proud — a kolik ho tam zbývá.
///
/// <para><b>Co se tím mění:</b> energie byla jedno globální číslo. Elektrárna
/// postavená kdekoli zásobovala všechno, takže „kam s ní" nebyla otázka.
/// Teď má dosah, a tím se z ní stává rozhodnutí o místě — přesně to, na čem
/// stojí celý žánr.</para>
///
/// <para><b>Žádné dráty.</b> Elektrárna zaplní svou buňku a šíří se do
/// sousedních, dokud stačí výkon. Ušetří to hráči mikromanagement vedení
/// a nám pathfinding; dosah je číslo z dat, ne nakreslená síť.</para>
///
/// <para><b>Hrubá mřížka a nízká frekvence.</b> Počítá se po buňkách 8×8
/// dlaždic a jen když se zástavba změnila — CLAUDE.md: růstové systémy
/// nejedou každý tik.</para>
/// </summary>
public sealed class PowerGridSystem
{
    /// <summary>Hrana buňky v dlaždicích. Mocnina dvojky — posun místo dělení.</summary>
    public const int CellShift = 3;

    /// <summary>Hrana buňky v dlaždicích (8).</summary>
    public const int CellSize = 1 << CellShift;

    private readonly Dictionary<long, double> _supply = new();
    private readonly Dictionary<long, double> _demand = new();
    private readonly Queue<(long Cell, int Steps)> _frontier = new();
    private readonly HashSet<long> _seen = new();
    private readonly List<long> _sources = new();

    /// <summary>Kolik buněk má aspoň něco. Pro testy a diagnostiku.</summary>
    public int CellCount => _supply.Count + _demand.Count;

    /// <summary>
    /// Přepočítá pokrytí od základu.
    ///
    /// <para>Od základu, ne přírůstkově: zbourat elektrárnu znamená ubrat dosah,
    /// a dvě elektrárny mohou pokrývat tutéž čtvrť — jednu záplavu od druhé
    /// odečíst nejde. Volá se jen při změně zástavby.</para>
    /// </summary>
    public void Rebuild(ReadOnlySpan<BuildingInstance> buildings, GameContent content)
    {
        _supply.Clear();
        _demand.Clear();

        var config = content.Gameplay.Power;
        if (!config.IsEnabled)
        {
            return;
        }

        // Napřed spotřeba: každá budova, která proud potřebuje, si ho zapíše
        // do své buňky. Až pak se rozlévá výroba, aby bylo co pokrývat.
        for (int i = 0; i < buildings.Length; i++)
        {
            var def = content.Buildings[buildings[i].DefIndex];
            if (def.PowerDemand > 0 && buildings[i].IsComplete)
            {
                long cell = CellOf(buildings[i].X, buildings[i].Y);
                _demand[cell] = _demand.GetValueOrDefault(cell) + def.PowerDemand;
            }
        }

        for (int i = 0; i < buildings.Length; i++)
        {
            var def = content.Buildings[buildings[i].DefIndex];
            if (def.PowerSupply > 0 && IsDelivering(buildings[i]))
            {
                Spread(buildings[i].X, buildings[i].Y, def.PowerSupply, config.Range);
            }
        }
    }

    /// <summary>
    /// Sype tahle elektrárna opravdu do sítě?
    ///
    /// <para>Dřív stačilo, že stojí — a to byla díra, kterou šlo projet
    /// městem: jaderná elektrárna dodávala plných 260 i s prázdným zásobníkem
    /// uranu a prázdnou směnou. Hráč pak koukal na budovy hlásící „má proud",
    /// zatímco elektrárna, ze které ten proud měl téct, nevyráběla nic.</para>
    ///
    /// <para>Zhaslá elektrárna je zhaslá celá, ne z poloviny: proud se nedá
    /// vyrobit napůl a poloviční dodávka by z jasného „došlo palivo" udělala
    /// nevysvětlitelné zpomalení čtvrti.</para>
    /// </summary>
    private static bool IsDelivering(in BuildingInstance building) =>
        building.IsComplete && building.Stall switch
        {
            BuildingStall.MissingInput => false,  // došlo palivo
            BuildingStall.NoWorkers => false,     // nemá kdo obsluhovat
            BuildingStall.Damaged => false,       // dostala zásah
            _ => true,
        };

    /// <summary>
    /// Jak dobře je místo zásobené: 1 = plný proud, 0 = tma.
    ///
    /// <para>Při nedostatku klesnou <b>všichni poměrně</b>, ne že první tři
    /// dostanou a zbytek nic. Nedostatek proudu má být zpomalení celé čtvrti,
    /// ne loterie podle pořadí v poli.</para>
    /// </summary>
    public double CoverageAt(int x, int y)
    {
        long cell = CellOf(x, y);
        double demand = _demand.GetValueOrDefault(cell);
        if (demand <= 0)
        {
            return 1.0; // nikdo tu proud nechce, tedy nikomu nechybí
        }

        double supply = _supply.GetValueOrDefault(cell);
        return Math.Clamp(supply / demand, 0.0, 1.0);
    }

    /// <summary>Kolik výkonu do buňky doteče (pro UI a testy).</summary>
    public double SupplyAt(int x, int y) => _supply.GetValueOrDefault(CellOf(x, y));

    /// <summary>Zapomene všechno (nový svět, Vzestup).</summary>
    public void Clear()
    {
        _supply.Clear();
        _demand.Clear();
    }

    /// <summary>
    /// Rozlije výkon jedné elektrárny do okolí.
    ///
    /// <para><b>Poměrně, ne kdo dřív přijde.</b> Elektrárna si napřed spočítá,
    /// kolik proudu po ní v dosahu celkem chtějí, a pak každé buňce dá její
    /// podíl. Když výkon nestačí, klesnou všichni stejně — což je zpomalení
    /// čtvrti, ne loterie podle pořadí v poli. Kdyby se rozdávalo od nejbližší
    /// buňky, dvě továrny vedle sebe by jely naplno a třetí o dlaždici dál
    /// vůbec, bez viditelného důvodu.</para>
    ///
    /// <para>Tím je výsledek taky nezávislý na pořadí elektráren — dvě
    /// elektrárny nad toutéž čtvrtí dají totéž, ať se počítají v jakémkoli
    /// pořadí.</para>
    /// </summary>
    private void Spread(int x, int y, double power, int range)
    {
        _frontier.Clear();
        _seen.Clear();
        _sources.Clear();

        long start = CellOf(x, y);
        _frontier.Enqueue((start, 0));
        _seen.Add(start);

        double wanted = 0;
        while (_frontier.Count > 0)
        {
            var (cell, steps) = _frontier.Dequeue();
            _sources.Add(cell);
            wanted += _demand.GetValueOrDefault(cell);

            if (steps >= range)
            {
                continue;
            }

            int cellX = TileKey.X(cell);
            int cellY = TileKey.Y(cell);
            Step(cellX + 1, cellY, steps + 1);
            Step(cellX - 1, cellY, steps + 1);
            Step(cellX, cellY + 1, steps + 1);
            Step(cellX, cellY - 1, steps + 1);
        }

        if (wanted <= 0)
        {
            return; // v dosahu nikdo proud nechce — není co rozdávat
        }

        for (int i = 0; i < _sources.Count; i++)
        {
            double demand = _demand.GetValueOrDefault(_sources[i]);
            if (demand <= 0)
            {
                continue;
            }

            _supply[_sources[i]] = _supply.GetValueOrDefault(_sources[i]) + (power * demand / wanted);
        }
    }

    private void Step(int cellX, int cellY, int steps)
    {
        long cell = TileKey.Pack(cellX, cellY);
        if (_seen.Add(cell))
        {
            _frontier.Enqueue((cell, steps));
        }
    }

    private static long CellOf(int x, int y) => TileKey.Pack(x >> CellShift, y >> CellShift);
}
