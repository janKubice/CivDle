namespace CivDle.Core.WorldGen;

using CivDle.Core.Content;

/// <summary>
/// Fraktální šum (fBm): několik oktáv <see cref="PerlinNoise"/> nad sebou podle
/// <see cref="NoiseSpec"/> z dat. Výstup je normalizovaný do 0–1.
/// </summary>
public sealed class FractalNoise
{
    private readonly PerlinNoise _perlin;
    private readonly NoiseSpec _spec;
    private readonly float _amplitudeSum;

    public FractalNoise(long seed, NoiseSpec spec)
    {
        _perlin = new PerlinNoise(seed);
        _spec = spec;

        float amplitude = 1f;
        float sum = 0f;
        for (int octave = 0; octave < spec.Octaves; octave++)
        {
            sum += amplitude;
            amplitude *= spec.Persistence;
        }

        _amplitudeSum = sum;
    }

    /// <summary>
    /// Vzorek v bodě (x, y) normalizovaný do 0–1. Souřadnice jsou v „jednotkách šumu" —
    /// volající je škáluje (generátor mapy používá dlaždice / 100, takže
    /// <c>Frequency = počet vln na 100 dlaždic</c>).
    /// </summary>
    public float Sample01(float x, float y)
    {
        float frequency = _spec.Frequency;
        float amplitude = 1f;
        float sum = 0f;

        for (int octave = 0; octave < _spec.Octaves; octave++)
        {
            sum += _perlin.Sample(x * frequency, y * frequency) * amplitude;
            frequency *= _spec.Lacunarity;
            amplitude *= _spec.Persistence;
        }

        float normalized = sum / _amplitudeSum * 0.5f + 0.5f;
        return Math.Clamp(normalized, 0f, 1f);
    }
}
