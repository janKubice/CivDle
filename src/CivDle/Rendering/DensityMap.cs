using CivDle.Core.Sim;
using CivDle.Core.World;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CivDle.Rendering;

/// <summary>
/// Zastavěnost upečená do textur: jeden draw call na kus mapy místo tisíců
/// obdélníčků.
///
/// <para>Proč to hra potřebuje: při maximálním oddálení se agregátní pohled
/// kreslil <b>buňku po buňce</b> — velkoměsto o desetitisících budov znamenalo
/// tisíce draw callů a přestavěný slovník každý snímek. A to všechno pro
/// obrázek, který se mezi snímky nemění, dokud někdo něco nepostaví.</para>
///
/// <para>Řešení je staré jako terénní renderer: nasekat mapu na kusy, každý
/// upéct do malé textury (jeden texel = jedna buňka) a překreslovat <b>líně</b>
/// — teprve když se změní zástavba. Kdo se ptá, jestli se změnila, je
/// <see cref="Simulation.BuildingRevision"/>; podle počtu budov to poznat
/// nejde, protože zbourat jednu a postavit jinou nechá počet stejný.</para>
///
/// <para>Textury jsou dvě na kus: denní barva a noční světlo. Jedna by
/// nestačila — v noci má jas růst <b>jen</b> s hustotou a nemít pevnou složku,
/// zatímco ve dne musí být vidět i buňka s jedním domem.</para>
///
/// <para>Vrstva: čistý render nad simulací. Nic nemění.</para>
/// </summary>
public sealed class DensityMap : IDisposable
{
    /// <summary>Kolik dlaždic pokryje jedna buňka hustoty.</summary>
    public const int CellTiles = 6;

    /// <summary>Kolik buněk má kus mapy na stranu (a tedy i jeho textura).</summary>
    public const int ChunkCells = 64;

    /// <summary>Kolik budov v buňce znamená plnou intenzitu.</summary>
    private const int DenseCount = 10;

    /// <summary>
    /// Kolik kusů se drží v paměti. Nekonečná mapa znamená nekonečně kusů —
    /// bez stropu by se paměť plnila prostě tím, že hráč jezdí kamerou.
    /// </summary>
    private const int MaxChunks = 96;

    private static readonly Color Dim = new(70, 90, 120);
    private static readonly Color Hot = new(255, 210, 120);
    private static readonly Color NightGlow = new(255, 196, 118);

    private sealed class Chunk
    {
        public Texture2D Day = null!;
        public Texture2D Night = null!;

        /// <summary>Pro kterou podobu zástavby je upečený.</summary>
        public long Revision = -1;

        /// <summary>Kdy se naposledy kreslil (pro vyhazování těch, na které se nikdo nedívá).</summary>
        public long LastDrawn;
    }

    private readonly GraphicsDevice _device;
    private readonly Dictionary<long, Chunk> _chunks = new();

    /// <summary>Pracovní buffery — jeden na celý život mapy, žádná alokace za snímek.</summary>
    private readonly Color[] _dayPixels = new Color[ChunkCells * ChunkCells];
    private readonly Color[] _nightPixels = new Color[ChunkCells * ChunkCells];
    private readonly int[] _counts = new int[ChunkCells * ChunkCells];
    private readonly List<int> _found = new();
    private readonly List<long> _evictable = new();

    private long _frame;

    public DensityMap(GraphicsDevice device) => _device = device;

    /// <summary>Kolik kusů mapy je zrovna upečených. Pro testy a diagnostiku.</summary>
    public int ChunkCount => _chunks.Count;

    /// <summary>Kolik textur se v posledním kreslení muselo přepéct. Pro testy.</summary>
    public int LastRebuilds { get; private set; }

    /// <summary>Kolik kusů se v posledním kreslení vykreslilo. Pro testy.</summary>
    public int LastChunksDrawn { get; private set; }

    /// <param name="nightFactor">Hloubka noci 0–1: ve dne plocha, v noci souhvězdí světel.</param>
    public void Draw(SpriteBatch spriteBatch, Camera2D camera, Simulation simulation, float nightFactor)
    {
        _frame++;
        LastRebuilds = 0;
        LastChunksDrawn = 0;

        var (min, max) = camera.VisibleWorldBounds();
        int chunkPixels = ChunkCells * CellTiles * TerrainRenderer.TileSize;
        int minChunkX = (int)Math.Floor(min.X / chunkPixels);
        int minChunkY = (int)Math.Floor(min.Y / chunkPixels);
        int maxChunkX = (int)Math.Floor(max.X / chunkPixels);
        int maxChunkY = (int)Math.Floor(max.Y / chunkPixels);

        float night = Math.Clamp(nightFactor, 0f, 1f);

        // Denní vrstva: normální míchání barev, plocha na krajině.
        spriteBatch.Begin(samplerState: SamplerState.PointClamp, transformMatrix: camera.Transform);
        for (int chunkY = minChunkY; chunkY <= maxChunkY; chunkY++)
        {
            for (int chunkX = minChunkX; chunkX <= maxChunkX; chunkX++)
            {
                var chunk = ChunkAt(chunkX, chunkY, simulation);
                if (chunk is null)
                {
                    continue;
                }

                LastChunksDrawn++;
                spriteBatch.Draw(
                    chunk.Day,
                    new Rectangle(chunkX * chunkPixels, chunkY * chunkPixels, chunkPixels, chunkPixels),
                    Color.White * (1f - night));
            }
        }

        spriteBatch.End();

        if (night > 0.02f)
        {
            DrawNight(spriteBatch, camera, minChunkX, minChunkY, maxChunkX, maxChunkY, chunkPixels, night, simulation);
        }

        Evict();
    }

    /// <summary>
    /// Noční světla: tatáž mapa aditivně, dvakrát — jednou rozmazaně a přes
    /// okraj (zář), podruhé přesně (jádro).
    ///
    /// <para>Ten rozdíl dělá „světlo". Jedna vrstva by byla jen světlejší
    /// čtverec.</para>
    /// </summary>
    private void DrawNight(
        SpriteBatch spriteBatch, Camera2D camera,
        int minChunkX, int minChunkY, int maxChunkX, int maxChunkY,
        int chunkPixels, float night, Simulation simulation)
    {
        int bleed = chunkPixels / ChunkCells / 2;

        spriteBatch.Begin(
            blendState: BlendState.Additive,
            samplerState: SamplerState.LinearClamp,
            transformMatrix: camera.Transform);

        for (int chunkY = minChunkY; chunkY <= maxChunkY; chunkY++)
        {
            for (int chunkX = minChunkX; chunkX <= maxChunkX; chunkX++)
            {
                var chunk = ChunkAt(chunkX, chunkY, simulation);
                if (chunk is null)
                {
                    continue;
                }

                var glow = new Rectangle(
                    chunkX * chunkPixels - bleed, chunkY * chunkPixels - bleed,
                    chunkPixels + 2 * bleed, chunkPixels + 2 * bleed);
                spriteBatch.Draw(chunk.Night, glow, NightGlow * (0.45f * night));
            }
        }

        spriteBatch.End();

        // Jádro ostře — proto vlastní dávka s bodovým vzorkováním.
        spriteBatch.Begin(
            blendState: BlendState.Additive,
            samplerState: SamplerState.PointClamp,
            transformMatrix: camera.Transform);

        for (int chunkY = minChunkY; chunkY <= maxChunkY; chunkY++)
        {
            for (int chunkX = minChunkX; chunkX <= maxChunkX; chunkX++)
            {
                var chunk = ChunkAt(chunkX, chunkY, simulation);
                if (chunk is null)
                {
                    continue;
                }

                spriteBatch.Draw(
                    chunk.Night,
                    new Rectangle(chunkX * chunkPixels, chunkY * chunkPixels, chunkPixels, chunkPixels),
                    Color.White * (0.7f * night));
            }
        }

        spriteBatch.End();
    }

    /// <summary>
    /// Kus mapy připravený ke kreslení, nebo <c>null</c>, když v něm nic
    /// nestojí — prázdné kusy nemá smysl péct ani držet.
    /// </summary>
    private Chunk? ChunkAt(int chunkX, int chunkY, Simulation simulation)
    {
        long key = TileKey.Pack(chunkX, chunkY);
        _chunks.TryGetValue(key, out var chunk);

        if (chunk is not null && chunk.Revision == simulation.BuildingRevision)
        {
            chunk.LastDrawn = _frame;
            return chunk;
        }

        if (!Bake(chunkX, chunkY, simulation))
        {
            // Prázdný kus: co se dřív upeklo, se zahodí — město mohlo zmizet
            // (Vzestup) a stará textura by pak svítila nad prázdnou krajinou.
            if (chunk is not null)
            {
                Release(key, chunk);
            }

            return null;
        }

        chunk ??= NewChunk(key);
        chunk.Day.SetData(_dayPixels);
        chunk.Night.SetData(_nightPixels);
        chunk.Revision = simulation.BuildingRevision;
        chunk.LastDrawn = _frame;
        LastRebuilds++;
        return chunk;
    }

    private Chunk NewChunk(long key)
    {
        var chunk = new Chunk
        {
            Day = new Texture2D(_device, ChunkCells, ChunkCells),
            Night = new Texture2D(_device, ChunkCells, ChunkCells),
        };

        _chunks[key] = chunk;
        return chunk;
    }

    /// <summary>Spočte hustotu v kusu mapy do pracovních bufferů této instance.</summary>
    private bool Bake(int chunkX, int chunkY, Simulation simulation)
        => BakeInto(simulation, chunkX, chunkY, _counts, _dayPixels, _nightPixels, _found);

    /// <summary>
    /// Spočte hustotu v kusu mapy a upeče ji do dodaných bufferů. Vrací false,
    /// když v kusu nic nestojí.
    ///
    /// <para>Statická a veřejná kvůli testům: co se má v kusu mapy objevit, je
    /// rozhodnutí o obsahu obrázku, ne o kreslení, a má se dát ověřit bez
    /// grafického zařízení. Ostatně právě tady se dá splést dělení záporných
    /// souřadnic — a mapa je nekonečná oběma směry.</para>
    /// </summary>
    /// <param name="simulation">Odkud se čte zástavba.</param>
    /// <param name="chunkX">Kus mapy vodorovně.</param>
    /// <param name="chunkY">Kus mapy svisle.</param>
    /// <param name="counts">Pracovní pole počtů (<see cref="ChunkCells"/>²).</param>
    /// <param name="dayPixels">Výstup: denní barvy.</param>
    /// <param name="nightPixels">Výstup: noční světla.</param>
    /// <param name="scratch">Pracovní seznam pro dotaz do indexu zástavby.</param>
    public static bool BakeInto(
        Simulation simulation, int chunkX, int chunkY,
        int[] counts, Color[] dayPixels, Color[] nightPixels, List<int> scratch)
    {
        Array.Clear(counts);

        int originCellX = chunkX * ChunkCells;
        int originCellY = chunkY * ChunkCells;
        int minTileX = originCellX * CellTiles;
        int minTileY = originCellY * CellTiles;
        int maxTileX = minTileX + (ChunkCells * CellTiles) - 1;
        int maxTileY = minTileY + (ChunkCells * CellTiles) - 1;

        // Přes index zástavby (bod 1.1), ne přes celé město: bez něj by každý
        // kus mapy stál průchod všemi budovami a peklo by se to hůř, než se
        // dřív kreslilo.
        simulation.BuildingsIn(minTileX, minTileY, maxTileX, maxTileY, scratch);

        var buildings = simulation.Buildings;
        bool any = false;
        for (int i = 0; i < scratch.Count; i++)
        {
            int index = scratch[i];
            if (index >= buildings.Length)
            {
                continue;
            }

            int cellX = FloorDiv(buildings[index].X, CellTiles) - originCellX;
            int cellY = FloorDiv(buildings[index].Y, CellTiles) - originCellY;
            if (cellX < 0 || cellY < 0 || cellX >= ChunkCells || cellY >= ChunkCells)
            {
                continue; // budova přesahující do sousedního kusu — ten si ji spočte sám
            }

            counts[(cellY * ChunkCells) + cellX]++;
            any = true;
        }

        if (!any)
        {
            return false;
        }

        for (int i = 0; i < counts.Length; i++)
        {
            dayPixels[i] = DayColorFor(counts[i]);
            nightPixels[i] = NightColorFor(counts[i]);
        }

        return true;
    }

    /// <summary>Barva buňky ve dne. Vidět má být i buňka s jedním domem.</summary>
    public static Color DayColorFor(int count)
    {
        if (count <= 0)
        {
            return Color.Transparent;
        }

        float t = MathF.Min(1f, count / (float)DenseCount);
        return Color.Lerp(Dim, Hot, t) * (0.55f + (0.35f * t));
    }

    /// <summary>
    /// Jas buňky v noci.
    ///
    /// <para>Roste <b>jen</b> s hustotou a nemá pevnou složku. Kdyby ji měl,
    /// přispěla by stejně i buňka s jedním domem — a protože se aditivně
    /// sčítají sousedi, slil by se okraj města do ostrého světlého obdélníku
    /// místo světel, která k okraji řídnou.</para>
    /// </summary>
    public static Color NightColorFor(int count)
    {
        if (count <= 0)
        {
            return Color.Transparent;
        }

        float t = MathF.Min(1f, count / (float)DenseCount);
        return Color.White * (0.18f + (0.52f * t));
    }

    /// <summary>Vyhodí kusy, na které se nikdo nedívá, jakmile se jich nakupí moc.</summary>
    private void Evict()
    {
        if (_chunks.Count <= MaxChunks)
        {
            return;
        }

        _evictable.Clear();
        foreach (var (key, chunk) in _chunks)
        {
            if (chunk.LastDrawn != _frame)
            {
                _evictable.Add(key);
            }
        }

        _evictable.Sort((a, b) => _chunks[a].LastDrawn.CompareTo(_chunks[b].LastDrawn));
        for (int i = 0; i < _evictable.Count && _chunks.Count > MaxChunks; i++)
        {
            Release(_evictable[i], _chunks[_evictable[i]]);
        }
    }

    private void Release(long key, Chunk chunk)
    {
        chunk.Day.Dispose();
        chunk.Night.Dispose();
        _chunks.Remove(key);
    }

    /// <summary>Dělení dolů — mapa je nekonečná oběma směry a <c>/</c> u záporných čísel zaokrouhluje k nule.</summary>
    public static int FloorDiv(int value, int divisor)
    {
        int quotient = value / divisor;
        return value % divisor != 0 && (value < 0) != (divisor < 0) ? quotient - 1 : quotient;
    }

    public void Dispose()
    {
        foreach (var chunk in _chunks.Values)
        {
            chunk.Day.Dispose();
            chunk.Night.Dispose();
        }

        _chunks.Clear();
    }
}
