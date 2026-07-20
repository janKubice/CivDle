namespace CivDle.Core.Sim;

/// <summary>
/// Akumulátor pevného kroku: převádí proměnlivý čas snímků (60 FPS render)
/// na celé tiky simulace (10 Hz). Drží sim/render split z tech-stack.md —
/// render volá <see cref="Advance"/> a odtiká, co mu smyčka řekne.
/// </summary>
public sealed class FixedStepLoop
{
    private readonly double _stepSeconds;
    private readonly int _maxTicksPerAdvance;
    private double _accumulator;

    /// <param name="ticksPerSecond">Frekvence simulace.</param>
    /// <param name="maxTicksPerAdvance">
    /// Strop tiků na jedno volání — po lagu/pauze se přebytek zahodí, aby hra
    /// nedoháněla stovky tiků v jednom snímku (spirála smrti).
    /// </param>
    public FixedStepLoop(double ticksPerSecond, int maxTicksPerAdvance = 5)
    {
        if (ticksPerSecond <= 0) throw new ArgumentOutOfRangeException(nameof(ticksPerSecond));
        if (maxTicksPerAdvance < 1) throw new ArgumentOutOfRangeException(nameof(maxTicksPerAdvance));

        _stepSeconds = 1.0 / ticksPerSecond;
        _maxTicksPerAdvance = maxTicksPerAdvance;
    }

    /// <summary>Přičte uplynulý čas a vrátí počet tiků, které má volající provést.</summary>
    public int Advance(double elapsedSeconds)
    {
        if (elapsedSeconds < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(elapsedSeconds));
        }

        _accumulator += elapsedSeconds;
        int ticks = (int)(_accumulator / _stepSeconds);

        if (ticks > _maxTicksPerAdvance)
        {
            ticks = _maxTicksPerAdvance;
            _accumulator = 0;
        }
        else
        {
            _accumulator -= ticks * _stepSeconds;
        }

        return ticks;
    }
}
