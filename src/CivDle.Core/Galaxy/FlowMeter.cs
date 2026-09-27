using CivDle.Core.Sim;

namespace CivDle.Core.Galaxy;

/// <summary>
/// Průběžné měření toků aktivního světa (svety-design.md 7.6) — kolik čeho
/// přibývá za sekundu a jak rychle rostou lidé.
///
/// <para><b>Proč průběžně:</b> odchod ze světa má být okamžitý. Kdyby se toky
/// měřily až při odchodu, musel by svět chvíli běžet „na zkoušku". Takhle se
/// každých deset sekund uloží kontrolní bod a tok je rozdíl dvou součtů
/// (<see cref="ResourceLedger.SteadyTotal"/>) dělený časem — přesný průměr,
/// ne vyhlazená hodnota, která kolísá podle toho, kdy doběhla poslední dávka.</para>
///
/// <para>Okno je minutu dlouhé: dost na zprůměrování pomalých receptů, dost
/// krátké na to, aby nový důl nečekal hodinu, než se projeví. Pole se alokují
/// jednou, v tiku nic.</para>
/// </summary>
public sealed class FlowMeter
{
    /// <summary>Kontrolní bod každých deset herních sekund.</summary>
    public const long CheckpointTicks = (long)(10 * Simulation.TicksPerSecond);

    /// <summary>Kolik bodů se drží — okno je (počet − 1) × 10 s.</summary>
    private const int Checkpoints = 7;

    private readonly long[] _ticks = new long[Checkpoints];
    private readonly double[] _population = new double[Checkpoints];
    private double[][] _steady = Array.Empty<double[]>();
    private int _count;
    private int _newest = -1;

    /// <summary>Zapomene měření (jiný svět, Vzestup).</summary>
    public void Reset()
    {
        _count = 0;
        _newest = -1;
    }

    /// <summary>
    /// Zapíše kontrolní bod, pokud od posledního uběhlo dost tiků. Levné —
    /// volá se klidně každý snímek.
    /// </summary>
    public void Sample(Simulation sim)
    {
        if (_count > 0 && sim.TickCount < _ticks[_newest])
        {
            Reset(); // hodiny šly pozpátku (načtení jiného savu) — staré body neplatí
        }

        if (_count > 0 && sim.TickCount - _ticks[_newest] < CheckpointTicks)
        {
            return;
        }

        var ledger = sim.Ledger;
        if (_steady.Length != Checkpoints || _steady[0].Length != ledger.Count)
        {
            _steady = new double[Checkpoints][];
            for (int i = 0; i < Checkpoints; i++)
            {
                _steady[i] = new double[ledger.Count];
            }

            _count = 0;
            _newest = -1;
        }

        _newest = (_newest + 1) % Checkpoints;
        _count = Math.Min(Checkpoints, _count + 1);
        _ticks[_newest] = sim.TickCount;
        _population[_newest] = sim.Population;
        var row = _steady[_newest];
        for (int r = 0; r < row.Length; r++)
        {
            row[r] = ledger.SteadyTotal(r);
        }
    }

    /// <summary>
    /// Toky za sekundu od nejstaršího bodu do teď. Bez aspoň pěti sekund dat
    /// (svět právě otevřený) se vezme vyhlazená evidence — lepší přibližný tok
    /// než žádný.
    /// </summary>
    /// <param name="sim">Měřený svět.</param>
    /// <param name="flows">Kam zapsat tok každé suroviny (délka = počet surovin).</param>
    /// <returns>Růst lidí za sekundu.</returns>
    public double Measure(Simulation sim, double[] flows)
    {
        var ledger = sim.Ledger;
        int oldest = _count == 0 ? -1 : (_newest - _count + 1 + Checkpoints) % Checkpoints;
        double seconds = oldest < 0 ? 0 : (sim.TickCount - _ticks[oldest]) / Simulation.TicksPerSecond;
        if (seconds < 5 || _steady.Length == 0 || _steady[0].Length != flows.Length)
        {
            // Obchod vede trasa sama (i za nepřítomnosti) — do toku světa nepatří.
            for (int r = 0; r < flows.Length; r++)
            {
                flows[r] = ledger.NetPerSecond(r) - ledger.ImportedPerSecond(r)
                    + ledger.ConsumedPerSecond(r, ConsumptionKind.Export);
            }

            return 0;
        }

        var start = _steady[oldest];
        for (int r = 0; r < flows.Length; r++)
        {
            flows[r] = (ledger.SteadyTotal(r) - start[r]) / seconds;
        }

        return (sim.Population - _population[oldest]) / seconds;
    }
}
