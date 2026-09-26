using CivDle.Core.Tests.Support;
using CivDle.Core.World;
using Xunit;

namespace CivDle.Core.Tests.World;

/// <summary>
/// Paměť biomů a mřížka obsazenosti: obě nahrazují něco pomalého (šum,
/// velký slovník) a obě musí vracet <b>přesně</b> totéž — i pro záporné
/// souřadnice a na hranách bloků, kde se nejsnáz splete dělení.
/// </summary>
public class TileCacheTests
{
    [Fact]
    public void CachedTerrain_ReturnsTheSameBiomesAsTheTerrain()
    {
        var content = TestData.LoadRealContent();
        var terrain = new ProceduralTerrain(content.Biomes, content.WorldGen.Presets[0], 777);
        var cached = new CachedTerrain(terrain);

        // Dvakrát: poprvé se počítá, podruhé se čte z paměti.
        for (int pass = 0; pass < 2; pass++)
        {
            for (int y = -70; y <= 70; y += 3)
            {
                for (int x = -70; x <= 70; x += 3)
                {
                    Assert.Equal(terrain.BiomeAt(x, y), cached.BiomeAt(x, y));
                }
            }
        }
    }

    [Fact]
    public void CachedTerrain_ForgetsWhenFull_AndStillAnswersTheSame()
    {
        // Nekonečná mapa nesmí nafouknout paměť: po zaplnění se zapomene vše.
        var cached = new CachedTerrain(new StripedTerrain());
        for (int i = 0; i <= CachedTerrain.MaxChunks; i++)
        {
            cached.BiomeAt(i * 32, 0);
        }

        Assert.True(cached.ChunkCount <= CachedTerrain.MaxChunks);
        Assert.Equal(new StripedTerrain().BiomeAt(-5, 3), cached.BiomeAt(-5, 3));
        Assert.Equal(new StripedTerrain().BiomeAt(0, 0), cached.BiomeAt(0, 0));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(31, 31)]
    [InlineData(32, -1)]
    [InlineData(-1, -32)]
    [InlineData(-33, 64)]
    [InlineData(1_000_000, -1_000_000)]
    public void TileMap_BehavesLikeADictionary_AroundChunkEdges(int x, int y)
    {
        var map = new TileMap();
        long key = TileKey.Pack(x, y);

        Assert.False(map.ContainsKey(key));
        map[key] = 7;
        Assert.True(map.TryGetValue(key, out int value));
        Assert.Equal(7, value);

        // Sousedé přes hranu bloku zůstávají prázdní.
        Assert.False(map.ContainsKey(TileKey.Pack(x - 1, y)));
        Assert.False(map.ContainsKey(TileKey.Pack(x, y + 1)));

        Assert.True(map.Remove(key));
        Assert.False(map.Remove(key));
        Assert.False(map.ContainsKey(key));
        Assert.Equal(0, map.Count);
    }

    [Fact]
    public void TileMap_MatchesADictionary_UnderRandomChanges()
    {
        var map = new TileMap();
        var reference = new Dictionary<long, int>();
        var rng = new Random(42);
        for (int step = 0; step < 20_000; step++)
        {
            long key = TileKey.Pack(rng.Next(-100, 100), rng.Next(-100, 100));
            switch (rng.Next(3))
            {
                case 0:
                    int value = rng.Next(1, 1000);
                    map[key] = value;
                    reference[key] = value;
                    break;
                case 1:
                    Assert.Equal(reference.Remove(key), map.Remove(key));
                    break;
                default:
                    Assert.Equal(reference.TryGetValue(key, out int expected), map.TryGetValue(key, out int actual));
                    Assert.Equal(expected, actual);
                    break;
            }
        }

        Assert.Equal(reference.Count, map.Count);
        map.Clear();
        Assert.Equal(0, map.Count);
        Assert.False(map.ContainsKey(reference.Keys.First()));
    }

    /// <summary>Terén, jehož biom se dá spočítat z hlavy: pruhy podle X.</summary>
    private sealed class StripedTerrain : ITerrain
    {
        public byte BiomeAt(int x, int y) => (byte)(((x % 5) + 5) % 5);
    }
}
