using CivDle.Core.Content;

namespace CivDle.Core.Sim;

/// <summary>
/// Zapisovatel časosběru: co pár minut sejme hrubý půdorys města a čísla k němu.
///
/// <para>Vrstva simulace, ale <b>nic neovlivňuje</b> — jen čte stav a ukládá ho
/// stranou. Běží na nízké frekvenci (CLAUDE.md: pomalé systémy nepatří do tikové
/// smyčky), takže i u velkoměsta stojí zlomek jednoho tiku za minutu.</para>
///
/// <para>Maska se drží mezi snímky a jen přepisuje, takže se za běhu nealokuje.</para>
/// </summary>
internal sealed class HistorySystem
{
    private readonly GameContent _content;
    private readonly byte[] _mask = new byte[CityHistory.MaskBytes];
    private readonly int _intervalTicks;

    public HistorySystem(GameContent content)
    {
        _content = content;
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

        Array.Clear(_mask);

        var buildings = sim.Buildings;
        for (int i = 0; i < buildings.Length; i++)
        {
            if (!buildings[i].IsComplete)
            {
                continue; // staveniště ještě není město
            }

            if (CityHistory.TryCellOf(buildings[i].X, buildings[i].Y, out int cellX, out int cellY))
            {
                int bit = cellY * CityHistory.GridSize + cellX;
                _mask[bit >> 3] |= (byte)(1 << (bit & 7));
            }
        }

        sim.History.Add(
            new HistoryFrame(sim.TickCount, (long)sim.Population, buildings.Length, sim.CurrentEraIndex),
            _mask);
    }
}
