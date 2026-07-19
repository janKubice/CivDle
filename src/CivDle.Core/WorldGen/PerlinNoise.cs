namespace CivDle.Core.WorldGen;

/// <summary>
/// 2D Perlinův gradientní šum se seedovanou permutační tabulkou.
/// Vlastní malá implementace dle tech-stack.md („šum: vlastní / malá noise lib") —
/// deterministická pro daný seed, bez alokací při vzorkování.
/// </summary>
public sealed class PerlinNoise
{
    private readonly byte[] _permutation = new byte[512];

    public PerlinNoise(long seed)
    {
        Span<byte> table = stackalloc byte[256];
        for (int i = 0; i < 256; i++)
        {
            table[i] = (byte)i;
        }

        // Fisher–Yates se SplitMix64 → stabilní zamíchání napříč platformami.
        var rng = new SplitMix64(unchecked((ulong)seed));
        for (int i = 255; i > 0; i--)
        {
            int j = (int)(rng.Next() % (ulong)(i + 1));
            (table[i], table[j]) = (table[j], table[i]);
        }

        for (int i = 0; i < 512; i++)
        {
            _permutation[i] = table[i & 255];
        }
    }

    /// <summary>
    /// Vzorek šumu v bodě (x, y), výstup přibližně v rozsahu −1 až 1.
    /// Diagonální gradienty (±1, ±1) drží teoretický rozsah přesně v ±1.
    /// </summary>
    public float Sample(float x, float y)
    {
        int floorX = FloorToInt(x);
        int floorY = FloorToInt(y);
        float fracX = x - floorX;
        float fracY = y - floorY;
        int cellX = floorX & 255;
        int cellY = floorY & 255;

        float u = Fade(fracX);
        float v = Fade(fracY);

        int aa = _permutation[_permutation[cellX] + cellY];
        int ab = _permutation[_permutation[cellX] + cellY + 1];
        int ba = _permutation[_permutation[cellX + 1] + cellY];
        int bb = _permutation[_permutation[cellX + 1] + cellY + 1];

        float x1 = Lerp(Grad(aa, fracX, fracY), Grad(ba, fracX - 1f, fracY), u);
        float x2 = Lerp(Grad(ab, fracX, fracY - 1f), Grad(bb, fracX - 1f, fracY - 1f), u);
        return Lerp(x1, x2, v);
    }

    private static float Grad(int hash, float x, float y) => (hash & 3) switch
    {
        0 => x + y,
        1 => -x + y,
        2 => x - y,
        _ => -x - y,
    };

    private static float Fade(float t) => t * t * t * (t * (t * 6f - 15f) + 10f);

    private static float Lerp(float a, float b, float t) => a + (b - a) * t;

    private static int FloorToInt(float value)
    {
        int truncated = (int)value;
        return value < truncated ? truncated - 1 : truncated;
    }
}
