using CivDle.Core.Content;
using CivDle.Core.WorldGen;
using Xunit;

namespace CivDle.Core.Tests.WorldGen;

public class NoiseTests
{
    private static readonly NoiseSpec Spec = new(Frequency: 1.5f, Octaves: 4, Persistence: 0.5f, Lacunarity: 2f);

    [Fact]
    public void Perlin_SameSeed_SameOutput()
    {
        var a = new PerlinNoise(1234);
        var b = new PerlinNoise(1234);

        for (float y = -3f; y < 3f; y += 0.37f)
        {
            for (float x = -3f; x < 3f; x += 0.41f)
            {
                Assert.Equal(a.Sample(x, y), b.Sample(x, y));
            }
        }
    }

    [Fact]
    public void Perlin_DifferentSeed_DifferentOutput()
    {
        var a = new PerlinNoise(1);
        var b = new PerlinNoise(2);

        bool anyDifference = false;
        for (float x = 0.1f; x < 20f && !anyDifference; x += 0.7f)
        {
            anyDifference = a.Sample(x, x * 0.5f) != b.Sample(x, x * 0.5f);
        }

        Assert.True(anyDifference, "Různé seedy nesmí dávat stejný šum.");
    }

    [Fact]
    public void Perlin_OutputWithinExpectedRange()
    {
        var noise = new PerlinNoise(99);

        for (float y = -5f; y < 5f; y += 0.13f)
        {
            for (float x = -5f; x < 5f; x += 0.17f)
            {
                float value = noise.Sample(x, y);
                Assert.InRange(value, -1.001f, 1.001f);
            }
        }
    }

    [Fact]
    public void Fractal_Sample01_StaysInUnitInterval()
    {
        var noise = new FractalNoise(7, Spec);

        for (float y = 0f; y < 10f; y += 0.23f)
        {
            for (float x = 0f; x < 10f; x += 0.29f)
            {
                float value = noise.Sample01(x, y);
                Assert.InRange(value, 0f, 1f);
            }
        }
    }

    [Fact]
    public void Fractal_SameSeed_Deterministic()
    {
        var a = new FractalNoise(42, Spec);
        var b = new FractalNoise(42, Spec);

        Assert.Equal(a.Sample01(1.7f, 2.9f), b.Sample01(1.7f, 2.9f));
        Assert.Equal(a.Sample01(0.01f, 8.5f), b.Sample01(0.01f, 8.5f));
    }
}
