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

        var buildings = sim.Buildings;
        for (int i = 0; i < buildings.Length; i++)
        {
            if (!buildings[i].IsComplete)
            {
                continue; // staveniště ještě není město
            }

            if (CityHistory.TryCellOf(buildings[i].X, buildings[i].Y, out int cellX, out int cellY))
            {
                // Poslední budova v buňce vyhrává — na hrubé mřížce je jedno
                // která, hlavně že buňka nese barvu skutečné zástavby.
                _cells[cellY * CityHistory.GridSize + cellX] = CellValueOf(sim.History, buildings[i].DefIndex);
            }
        }

        // Čísla ke snímku: z nich se pak kreslí grafy. Jsou to skaláry, takže
        // snímek zůstává drobný proti mřížce zástavby.
        sim.History.Add(
            new HistoryFrame(
                sim.TickCount,
                (long)sim.Population,
                buildings.Length,
                sim.CurrentEraIndex,
                sim.Happiness,
                sim.HousingCapacity,
                sim.AirPollutionOverCity,
                sim.Settlements.Count),
            _cells);
    }

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
