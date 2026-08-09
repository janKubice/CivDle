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

    /// <summary>
    /// O kolik dlaždic za okraj chunku se sahá při pečení.
    ///
    /// <para>Vzhled dlaždice závisí na okolí (ditherovaná hranice biomu, pěna
    /// u břehu, hloubka vody). Bez přesahu by se u švu mezi chunky okolí
    /// „useklo" a hráč by viděl přesně mřížku chunků — tedy pravý opak toho,
    /// proč se to počítá. Musí pokrýt okno hloubky vody.</para>
    /// </summary>
    private const int BakePad = TerrainPainter.WaterWindowRadius;

    private const int PaddedTiles = ChunkTiles + 2 * BakePad;

    private readonly GraphicsDevice _device;
    private readonly BiomeRegistry _biomes;
    private readonly TerrainPainter _painter;
    private readonly Dictionary<long, Chunk> _cache = new();
    private readonly List<long> _evictScratch = new();

    /// <summary>
    /// Pracovní buffer biomů s přesahem. Znovupoužitelný: pečení chunku je
    /// sice vzácné, ale při odhalování mapy jich přijde naráz několik desítek
    /// a alokovat pokaždé kilobajty by dělalo v tu chvíli zbytečné škubnutí.
    /// </summary>
    private readonly byte[] _bakeScratch = new byte[PaddedTiles * PaddedTiles];

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
        _painter = new TerrainPainter(biomes, seed);
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

        // Nejdřív se načte celé okolí i s přesahem. Sáhnout na terén jednou za
        // dlaždici je podstatně levnější než pro každou dlaždici znovu na
        // devět sousedů — a generátor terénu je z celého pečení to nejdražší.
        SampleWithPadding(terrain, overrides, baseX, baseY);

        Span<byte> ring = stackalloc byte[9];
        for (int ty = 0; ty < ChunkTiles; ty++)
        {
            for (int tx = 0; tx < ChunkTiles; tx++)
            {
                int px = tx + BakePad;
                int py = ty + BakePad;
                for (int dy = -1; dy <= 1; dy++)
                {
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        ring[(dy + 1) * 3 + dx + 1] = _bakeScratch[(py + dy) * PaddedTiles + px + dx];
                    }
                }

                pixels[ty * ChunkTiles + tx] = _painter.Tile(
                    baseX + tx, baseY + ty, ring, CountWater(px, py));
            }
        }

        var texture = new Texture2D(_device, ChunkTiles, ChunkTiles);
        texture.SetData(pixels);
        var chunk = new Chunk(texture);
        _cache[key] = chunk;
        return chunk;
    }

    /// <summary>
    /// Načte biomy chunku i s přesahem do pracovního bufferu. Přepis simulace
    /// má přednost před vygenerovaným terénem — je to to, co hráč (nebo
    /// katastrofa) s dlaždicí opravdu udělal.
    /// </summary>
    private void SampleWithPadding(
        ITerrain terrain, IReadOnlyDictionary<long, byte>? overrides, int baseX, int baseY)
    {
        for (int y = 0; y < PaddedTiles; y++)
        {
            int worldY = baseY + y - BakePad;
            for (int x = 0; x < PaddedTiles; x++)
            {
                int worldX = baseX + x - BakePad;
                byte biomeIndex = terrain.BiomeAt(worldX, worldY);
                if (overrides is not null
                    && overrides.TryGetValue(TileKey.Pack(worldX, worldY), out byte overridden))
                {
                    biomeIndex = overridden;
                }

                _bakeScratch[y * PaddedTiles + x] = biomeIndex;
            }
        }
    }

    /// <summary>
    /// Kolik vody je v okně kolem dlaždice (souřadnice jsou v pracovním
    /// bufferu, ne ve světě). Z toho se odvozuje hloubka: zátoka mezi mysy má
    /// zůstat mělká a světlá, otevřené moře tmavé.
    /// </summary>
    private int CountWater(int px, int py)
    {
        int count = 0;
        for (int dy = -BakePad; dy <= BakePad; dy++)
        {
            int row = (py + dy) * PaddedTiles;
            for (int dx = -BakePad; dx <= BakePad; dx++)
            {
                if (_biomes[_bakeScratch[row + px + dx]].IsWater)
                {
                    count++;
                }
            }
        }

        return count;
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

    private static int FloorDiv(int a, int b) => (int)MathF.Floor((float)a / b);
}
