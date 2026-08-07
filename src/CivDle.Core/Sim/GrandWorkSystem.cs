using CivDle.Core.Content;

namespace CivDle.Core.Sim;

/// <summary>
/// Velké dílo: kam hráč sype přebytky.
///
/// <para>Drží jen dvě čísla — <see cref="Stage"/> (kolikátý stupeň se staví)
/// a kolik už je do něj vloženo z každé suroviny. Odvozený stav (bonusy) si
/// počítá simulace, systém sám nic nenásobí; jeho jediná zodpovědnost je
/// „přijmout suroviny a poznat, kdy je stupeň hotový".</para>
///
/// <para>Vkládá se <b>ručně</b>, ne automaticky. Automatický odběr by z díla
/// udělal neviditelnou daň z produkce; takhle je to rozhodnutí — a okamžik,
/// kdy stupeň dopadne, je vidět.</para>
///
/// <para>Vrstva: čistá simulace, nezná render.</para>
/// </summary>
public sealed class GrandWorkSystem
{
    private readonly GrandWorkConfig _config;
    private readonly double[] _invested;

    public GrandWorkSystem(GrandWorkConfig config, int resourceCount)
    {
        _config = config;
        _invested = new double[resourceCount];
    }

    /// <summary>Kolikátý stupeň se právě staví (0 = první).</summary>
    public int Stage { get; private set; }

    /// <summary>Kolik už je do rozestavěného stupně vloženo dané suroviny.</summary>
    public double Invested(int resourceIndex) => _invested[resourceIndex];

    /// <summary>Kolik daného stupně ještě chybí.</summary>
    public double Remaining(int resourceIndex) =>
        Math.Max(0, _config.CostOf(Stage, resourceIndex) - _invested[resourceIndex]);

    /// <summary>Postup rozestavěného stupně 0–1 (průměr přes potřebné suroviny).</summary>
    public double Progress01()
    {
        var pattern = _config.StageAt(Stage);
        if (pattern.Cost.Count == 0)
        {
            return 0;
        }

        double sum = 0;
        for (int i = 0; i < pattern.Cost.Count; i++)
        {
            int resource = pattern.Cost[i].ResourceIndex;
            double need = _config.CostOf(Stage, resource);
            sum += need <= 0 ? 1 : Math.Clamp(_invested[resource] / need, 0, 1);
        }

        return sum / pattern.Cost.Count;
    }

    /// <summary>
    /// Vloží suroviny do díla. Vrací <c>true</c>, když se tím stupeň dokončil —
    /// volající pak připíše bonus a posune se na další.
    /// </summary>
    public bool Invest(int resourceIndex, double amount)
    {
        if (amount <= 0)
        {
            return false;
        }

        _invested[resourceIndex] += amount;
        return IsStageComplete();
    }

    /// <summary>Posune se na další stupeň a vynuluje vklad.</summary>
    public GrandWorkStage AdvanceStage()
    {
        var finished = _config.StageAt(Stage);
        Array.Clear(_invested);
        Stage++;
        return finished;
    }

    /// <summary>Obnova ze savu.</summary>
    public void Restore(int stage, IReadOnlyList<double> invested)
    {
        Stage = Math.Max(0, stage);
        for (int i = 0; i < _invested.Length && i < invested.Count; i++)
        {
            _invested[i] = Math.Max(0, invested[i]);
        }
    }

    /// <summary>Vklady k uložení.</summary>
    public IReadOnlyList<double> InvestedAll() => _invested;

    /// <summary>Nový svět po Vzestupu dílo nemaže — je to stavba napříč věky.</summary>
    private bool IsStageComplete()
    {
        var pattern = _config.StageAt(Stage);
        for (int i = 0; i < pattern.Cost.Count; i++)
        {
            int resource = pattern.Cost[i].ResourceIndex;
            if (_invested[resource] < _config.CostOf(Stage, resource))
            {
                return false;
            }
        }

        return pattern.Cost.Count > 0;
    }
}
