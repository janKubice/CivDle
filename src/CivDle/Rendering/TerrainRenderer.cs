using CivDle.Core.Content;
using CivDle.Core.World;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CivDle.Rendering;

/// <summary>
/// Vykreslení NEKONEČNÉHO terénu přes chunky (tech-stack.md: „nekonečná" mapa +
/// chunking + LOD). Svět se dělí na chunky <see cref="ChunkTiles"/>×<see cref="ChunkTiles"/>
/// dlaždic; každý se líně „upeče" do textury 1 texel = 1 dlaždice (barva biomu
/// z JSON + deterministická variace jasu proti repetici) a kreslí jedním draw
/// callem se škálováním. Oddálení = menší textury (LOD zadarmo), přiblížení =
/// ostré dlaždice (PointClamp). Chunky mimo pohled se uvolňují (strop paměti).
/// Render jen čte terén, nikdy do simulace nezapisuje.
/// </summary>
public sealed class TerrainRenderer : IDisposable
{
    /// <summary>Velikost dlaždice ve world pixelech (referenční měřítko celé hry).</summary>
    public const int TileSize = 16;

    private const int ChunkTiles = 32;
    private const int ChunkPixels = ChunkTiles * TileSize;
    private const int MaxCachedChunks = 400;

    private readonly GraphicsDevice _device;
    private readonly BiomeRegistry _biomes;
    private readonly long _seed;
    private readonly Dictionary<long, Chunk> _cache = new();
    private readonly List<long> _evictScratch = new();
    private int _frame;

    /// <summary>
    /// Verze přepisů terénu, se kterou jsou upečené chunky v cache.
    ///
    /// <para>Bez tohohle byla terraformace neviditelná: simulace dlaždici
    /// změnila, ale chunk už byl upečený a nikdo ho neshodil — hráč tedy viděl
    /// starý biom a mechanika vypadala jako rozbitá. Přepečení celé cache je
    /// při změně terénu v pořádku: terraformace je akce hráče, ne něco, co se
    /// děje každý snímek.</para>
    /// </summary>
    private int _bakedRevision = -1;

    private sealed record Chunk(Texture2D Texture)
    {
        public int LastFrame { get; set; }
    }

    public TerrainRenderer(GraphicsDevice device, BiomeRegistry biomes, long seed)
    {
        _device = device;
        _biomes = biomes;
        _seed = seed;
    }

    /// <param name="overrides">
    /// Dlaždice, které simulace přepsala (terraformace, kráter po meteoru,
    /// zaplavené pobřeží). <c>null</c> = kreslí se holý terén, což je případ
    /// menu a časosběru.
    /// </param>
    /// <param name="revision">
    /// Číslo, které simulace zvýší při každé změně terénu. Když se liší od
    /// upečené cache, chunky se zahodí a napečou znovu.
    /// </param>
    public void Draw(
        SpriteBatch spriteBatch,
        Camera2D camera,
        ITerrain terrain,
        IReadOnlyDictionary<long, byte>? overrides = null,
        int revision = 0)
    {
        if (revision != _bakedRevision)
        {
            InvalidateCache();
            _bakedRevision = revision;
        }

        _frame++;
        var (min, max) = camera.VisibleWorldBounds();

        int startChunkX = FloorDiv((int)MathF.Floor(min.X / TileSize), ChunkTiles);
        int startChunkY = FloorDiv((int)MathF.Floor(min.Y / TileSize), ChunkTiles);
        int endChunkX = FloorDiv((int)MathF.Ceiling(max.X / TileSize), ChunkTiles);
        int endChunkY = FloorDiv((int)MathF.Ceiling(max.Y / TileSize), ChunkTiles);

        spriteBatch.Begin(samplerState: SamplerState.PointClamp, transformMatrix: camera.Transform);
        for (int cy = startChunkY; cy <= endChunkY; cy++)
        {
            for (int cx = startChunkX; cx <= endChunkX; cx++)
            {
                var chunk = GetChunk(terrain, overrides, cx, cy);
                chunk.LastFrame = _frame;
                spriteBatch.Draw(
                    chunk.Texture,
                    new Vector2(cx * ChunkPixels, cy * ChunkPixels),
                    null,
                    Color.White,
                    0f,
                    Vector2.Zero,
                    scale: TileSize,
                    SpriteEffects.None,
                    0f);
            }
        }

        spriteBatch.End();
        EvictStaleChunks();
    }

    /// <summary>Zahodí upečené chunky — po změně terénu se musí napéct znovu.</summary>
    private void InvalidateCache()
    {
        foreach (var chunk in _cache.Values)
        {
            chunk.Texture.Dispose();
        }

        _cache.Clear();
    }

    public void Dispose()
    {
        foreach (var chunk in _cache.Values)
        {
            chunk.Texture.Dispose();
        }

        _cache.Clear();
    }

    private Chunk GetChunk(
        ITerrain terrain, IReadOnlyDictionary<long, byte>? overrides, int chunkX, int chunkY)
    {
        long key = TileKey.Pack(chunkX, chunkY);
        if (_cache.TryGetValue(key, out var existing))
        {
            return existing;
        }

        var pixels = new Color[ChunkTiles * ChunkTiles];
        int baseX = chunkX * ChunkTiles;
        int baseY = chunkY * ChunkTiles;
        for (int ty = 0; ty < ChunkTiles; ty++)
        {
            for (int tx = 0; tx < ChunkTiles; tx++)
            {
                int worldX = baseX + tx;
                int worldY = baseY + ty;
                // Přepis má přednost před vygenerovaným terénem — je to to,
                // co hráč (nebo katastrofa) s dlaždicí opravdu udělal.
                byte biomeIndex = terrain.BiomeAt(worldX, worldY);
                if (overrides is not null
                    && overrides.TryGetValue(TileKey.Pack(worldX, worldY), out byte overridden))
                {
                    biomeIndex = overridden;
                }

                var biome = _biomes[biomeIndex];
                float brightness = 1f + (HashToUnit(worldX, worldY) * 2f - 1f) * biome.ColorVariation;
                pixels[ty * ChunkTiles + tx] = new Color(
                    ClampByte(biome.MapColor.R * brightness),
                    ClampByte(biome.MapColor.G * brightness),
                    ClampByte(biome.MapColor.B * brightness));
            }
        }

        var texture = new Texture2D(_device, ChunkTiles, ChunkTiles);
        texture.SetData(pixels);
        var chunk = new Chunk(texture);
        _cache[key] = chunk;
        return chunk;
    }

    private void EvictStaleChunks()
    {
        if (_cache.Count <= MaxCachedChunks)
        {
            return;
        }

        _evictScratch.Clear();
        foreach (var (key, chunk) in _cache)
        {
            if (chunk.LastFrame != _frame)
            {
                _evictScratch.Add(key);
            }
        }

        foreach (long key in _evictScratch)
        {
            _cache[key].Texture.Dispose();
            _cache.Remove(key);
        }
    }

    private static byte ClampByte(float value) => (byte)Math.Clamp(value, 0f, 255f);

    private static int FloorDiv(int a, int b) => (int)MathF.Floor((float)a / b);

    /// <summary>Deterministický hash dlaždice → 0–1 (mix konstantami ze SplitMix64).</summary>
    private float HashToUnit(int x, int y)
    {
        ulong h = unchecked((ulong)_seed);
        h ^= (uint)x * 0x9E3779B97F4A7C15UL;
        h ^= (uint)y * 0xBF58476D1CE4E5B9UL;
        h = (h ^ (h >> 30)) * 0xBF58476D1CE4E5B9UL;
        h = (h ^ (h >> 27)) * 0x94D049BB133111EBUL;
        h ^= h >> 31;
        return (h >> 40) / (float)(1 << 24);
    }
}
