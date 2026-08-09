using CivDle.Core.Content;

namespace CivDle.Core.Sim;

/// <summary>
/// Zapisovatel časosběru: co pár minut sejme hrubý půdorys města a čísla k němu.
///
/// <para>Vrstva simulace, ale <b>nic neovlivňuje</b> — jen čte stav a ukládá ho
/// stranou. Běží na nízké frekvenci (CLAUDE.md: pomalé systémy nepatří do tikové
/// smyčky), takže i u velkoměsta stojí zlomek jednoho tiku za minutu.</para>
///
/// <para>Mřížka buněk se drží mezi snímky a jen přepisuje, takže se za běhu
/// nealokuje.</para>
/// </summary>
internal sealed class HistorySystem
{
    private readonly GameContent _content;
    private readonly byte[] _cells = new byte[CityHistory.CellBytes];

    /// <summary>Hodnota buňky pro definici budovy (index do palety + 1); −1 = ještě nespočteno.</summary>
    private readonly int[] _cellValueOfDef;

    private readonly int _intervalTicks;

    public HistorySystem(GameContent content)
    {
        _content = content;
        _cellValueOfDef = new int[content.Buildings.Count];
        Array.Fill(_cellValueOfDef, -1);
        _intervalTicks = Math.Max(1, (int)Math.Round(
            content.Gameplay.History.IntervalSeconds * Simulation.TicksPerSecond));
    }

    /// <summary>Zapisuje se vůbec? Bez bloku v datech je časosběr vypnutý.</summary>
    public bool IsEnabled => _content.Gameplay.History.IsEnabled;

    public void Tick(Simulation sim)
    {
        if (!IsEnabled || sim.TickCount % _intervalTicks != 0)
        {
            return;
        }

        Capture(sim);
    }

    /// <summary>
    /// Sejme snímek teď hned. Veřejné kvůli okamžikům, které si snímek zaslouží
    /// bez ohledu na hodiny — hlavně těsně před Vzestupem, aby v časosběru
    /// zůstala i poslední podoba města.
    /// </summary>
    public void Capture(Simulation sim)
    {
        if (!IsEnabled)
        {
            return;
        }

        // Vzestup kroniku (i s paletou) vyprázdnil — cache mapování by ukazovala
        // do prázdné palety a celé nové město by se kreslilo neviditelně.
        if (sim.History.Palette.Count == 0)
        {
            Array.Fill(_cellValueOfDef, -1);
        }

        Array.Clear(_cells);

        // Silnice první, aby je zástavba mohla přepsat: na křižovatce budovy
        // a cesty patří buňka budově. Bez silnic vypadala přehrávka jako ostrůvky
        // baráků ve vzduchoprázdnu — město drží pohromadě právě síť mezi nimi.
        var roads = sim.RoadTiles;

        // Barva silnic se do palety přidá, teprve až nějaká silnice stojí —
        // jinak by paleta každého savu nesla barvu, kterou nic nepoužívá.
        byte roadValue = roads.Count > 0 ? RoadValue(sim.History) : (byte)0;
        for (int i = 0; i < roads.Count; i++)
        {
            if (CityHistory.TryCellOf(roads[i].X, roads[i].Y, out int roadCellX, out int roadCellY))
            {
                _cells[roadCellY * CityHistory.GridSize + roadCellX] = roadValue;
            }
        }

        var buildings = sim.Buildings;
        for (int i = 0; i < buildings.Length; i++)
        {
            if (!buildings[i].IsComplete)
            {
                continue; // staveniště ještě není město
            }

            // Celý půdorys, ne jen roh: na jemné mřížce je velká budova opravdu
            // velká a hráč na přehrávce pozná huť od chalupy.
            var def = _content.Buildings[buildings[i].DefIndex];
            byte value = CellValueOf(sim.History, buildings[i].DefIndex);
            for (int ty = 0; ty < def.FootprintHeight; ty++)
            {
                for (int tx = 0; tx < def.FootprintWidth; tx++)
                {
                    if (CityHistory.TryCellOf(buildings[i].X + tx, buildings[i].Y + ty, out int cellX, out int cellY))
                    {
                        _cells[cellY * CityHistory.GridSize + cellX] = value;
                    }
                }
            }
        }

        // Čísla ke snímku: z nich se pak kreslí grafy. Jsou to skaláry, takže
        // snímek zůstává drobný proti mřížce zástavby.
        sim.History.Add(
            new HistoryFrame(
                sim.TickCount,
                Numbers.ToLong(sim.Population),
                buildings.Length,
                sim.CurrentEraIndex,
                sim.Happiness,
                Numbers.ToLong(sim.HousingCapacity),
                sim.AirPollutionOverCity,
                sim.Settlements.Count),
            _cells);
    }

    /// <summary>
    /// Hodnota buňky pro silnici. Bere barvu z dat (stejnou, jakou má síť na
    /// mapě), takže přehrávka vypadá jako to město, ne jako schéma.
    /// </summary>
    private byte RoadValue(CityHistory history) =>
        (byte)(history.PaletteIndexOf(_content.Gameplay.Roads.MapColor) + 1);

    /// <summary>
    /// Hodnota buňky pro definici budovy. Barva se do palety kroniky přidává
    /// líně a jen jednou na definici — mapování se cachuje, ať se paleta
    /// neprohledává u každé budovy velkoměsta.
    /// </summary>
    private byte CellValueOf(CityHistory history, int defIndex)
    {
        int cached = _cellValueOfDef[defIndex];
        if (cached >= 0)
        {
            return (byte)cached;
        }

        int value = history.PaletteIndexOf(_content.Buildings[defIndex].MapColor) + 1;
        _cellValueOfDef[defIndex] = value;
        return (byte)value;
    }
}
