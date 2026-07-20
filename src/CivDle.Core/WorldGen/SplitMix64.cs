namespace CivDle.Core.WorldGen;

/// <summary>
/// Deterministický PRNG (SplitMix64). Vlastní implementace místo <see cref="Random"/>,
/// protože seed musí dávat stejný svět napříč verzemi .NET — dokumentace Random
/// stabilitu algoritmu mezi verzemi negarantuje.
/// </summary>
internal struct SplitMix64
{
    private ulong _state;

    public SplitMix64(ulong seed) => _state = seed;

    /// <summary>Další 64bitové pseudonáhodné číslo.</summary>
    public ulong Next()
    {
        ulong z = _state += 0x9E3779B97F4A7C15UL;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }
}
