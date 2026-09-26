using CivDle.Core.Content;

namespace CivDle.Core.Sim;

/// <summary>Jeden běžící efekt volby z události.</summary>
/// <param name="Kind">Na co působí.</param>
/// <param name="ResourceIndex">Surovina (−1 = všechny / netýká se).</param>
/// <param name="Multiplier">Násobič.</param>
/// <param name="EndsAtTick">V jakém tiku skončí.</param>
public readonly record struct ActiveEventEffect(EventEffectKind Kind, int ResourceIndex, double Multiplier, long EndsAtTick);

/// <summary>
/// Běžící dočasné efekty z voleb v událostech (viz <see cref="EventEffectDef"/>).
///
/// <para>Drží seznam (bývá prázdný, nanejvýš pár položek) a z něj předpočítané
/// násobiče po surovinách. Výrobní tik se ptá u každé budovy, takže dotaz musí
/// být jen sáhnutí do pole — seznam se prochází jen při přidání a vypršení.</para>
///
/// <para>Vrstva: stav simulace, ukládá se (efekt z povodně nesmí zmizet uložením).</para>
/// </summary>
public sealed class EventEffects
{
    private readonly List<ActiveEventEffect> _active = new();
    private readonly double[] _productionMult;
    private double _growthMult = 1.0;

    /// <param name="resourceCount">Kolik surovin hra má.</param>
    public EventEffects(int resourceCount)
    {
        _productionMult = new double[resourceCount];
        Array.Fill(_productionMult, 1.0);
    }

    /// <summary>Co zrovna běží (pro UI a save).</summary>
    public IReadOnlyList<ActiveEventEffect> Active => _active;

    /// <summary>Násobič výroby suroviny ze všech běžících efektů.</summary>
    public double ProductionMult(int resourceIndex) => _productionMult[resourceIndex];

    /// <summary>Násobič růstu populace ze všech běžících efektů.</summary>
    public double GrowthMult => _growthMult;

    /// <summary>Spustí efekt volby.</summary>
    internal void Start(EventEffectDef effect, long tick)
    {
        long ticks = Math.Max(1, (long)Math.Round(effect.Seconds * Simulation.TicksPerSecond));
        _active.Add(new ActiveEventEffect(effect.Kind, effect.ResourceIndex, effect.Multiplier, tick + ticks));
        Recompute();
    }

    /// <summary>Odebere, co vypršelo. Volá se jednou za tik.</summary>
    internal void Expire(long tick)
    {
        if (_active.Count == 0)
        {
            return;
        }

        // Ručně odzadu, ne RemoveAll s lambdou: ta by v tiku alokovala closure.
        bool removed = false;
        for (int i = _active.Count - 1; i >= 0; i--)
        {
            if (_active[i].EndsAtTick <= tick)
            {
                _active.RemoveAt(i);
                removed = true;
            }
        }

        if (removed)
        {
            Recompute();
        }
    }

    /// <summary>Zruší všechno (nový běh po Vzestupu).</summary>
    internal void Clear()
    {
        _active.Clear();
        Recompute();
    }

    /// <summary>Kolik tiků efektu zbývá (pro UI a save).</summary>
    public static long TicksLeft(ActiveEventEffect effect, long tick) => Math.Max(0, effect.EndsAtTick - tick);

    /// <summary>Obnoví efekt ze savu.</summary>
    internal void Restore(ActiveEventEffect effect)
    {
        _active.Add(effect);
        Recompute();
    }

    private void Recompute()
    {
        Array.Fill(_productionMult, 1.0);
        _growthMult = 1.0;
        foreach (var effect in _active)
        {
            if (effect.Kind == EventEffectKind.Growth)
            {
                _growthMult *= effect.Multiplier;
            }
            else if (effect.ResourceIndex < 0)
            {
                for (int i = 0; i < _productionMult.Length; i++)
                {
                    _productionMult[i] *= effect.Multiplier;
                }
            }
            else if (effect.ResourceIndex < _productionMult.Length)
            {
                _productionMult[effect.ResourceIndex] *= effect.Multiplier;
            }
        }
    }
}
