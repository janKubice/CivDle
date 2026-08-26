using CivDle.Core.Sim;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CivDle.Rendering;

/// <summary>
/// Slavnost, kterou je vidět: girlandy lampionů nad ulicemi a papírové
/// lampiony stoupající nad město.
///
/// <para>Proč to hra potřebuje: slavnost byla dosud číslo v tooltipu a zhasnuté
/// tlačítko. Hráč ji spustil, výroba mu vyskočila — a na mapě se nezměnilo
/// vůbec nic. Odměna, kterou není vidět, se neužije.</para>
///
/// <para>Girlandy se <b>nepamatují</b>: jejich poloha je funkce dlaždice
/// (jednoduchý hash souřadnic), takže se kreslí až ve výřezu kamery, drží se
/// mezi snímky samy od sebe a nestojí ani bajt stavu. Stoupající lampiony stav
/// mít musí — mají pohyb — a proto jsou v poolu o pevné velikosti, který se
/// alokuje jednou.</para>
///
/// <para>Vrstva: čistý render nad simulací. Nic nemění, jen se ptá, jestli
/// slavnost běží.</para>
/// </summary>
public sealed class FestivalRenderer
{
    private const int TileSize = TerrainRenderer.TileSize;

    /// <summary>Kolik lampionů může být naráz ve vzduchu. Pool, ne seznam — nulová alokace za běhu.</summary>
    private const int MaxLanterns = 48;

    /// <summary>Jak často se pouští další lampion (sekundy).</summary>
    private const float ReleaseIntervalSeconds = 0.35f;

    private const float LanternLifeSeconds = 9f;
    private const float LanternRiseTilesPerSecond = 0.9f;

    /// <summary>Jak daleko od okraje výřezu se ještě pouští — ať nevznikají „ze vzduchu" před očima.</summary>
    private const float SpawnMarginTiles = 3f;

    private static readonly Color[] LanternColors =
    {
        new(255, 176, 96),
        new(255, 122, 108),
        new(255, 220, 132),
        new(190, 150, 255),
    };

    private struct Lantern
    {
        public float X;
        public float Y;
        public float Age;
        public float Drift;
        public byte Color;
        public bool Alive;
    }

    private readonly Lantern[] _lanterns = new Lantern[MaxLanterns];
    private readonly Random _rng = new();
    private readonly Texture2D _pixel;

    private float _releaseTimer;

    /// <summary>Jak moc je slavnost vidět (0–1). Náběh a doběh, ne cvaknutí.</summary>
    private float _fade;

    public FestivalRenderer(Texture2D whitePixel) => _pixel = whitePixel;

    /// <summary>Je z výzdoby zrovna něco vidět? (Aby se dal přeskočit celý draw.)</summary>
    public bool IsVisible => _fade > 0.01f;

    /// <summary>
    /// Kolik lampionů je právě ve vzduchu. Veřejné kvůli testu: pool se pozná
    /// jedině tak, že se hlídá, jestli počet neroste donekonečna.
    /// </summary>
    public int ActiveLanterns
    {
        get
        {
            int count = 0;
            for (int i = 0; i < _lanterns.Length; i++)
            {
                if (_lanterns[i].Alive)
                {
                    count++;
                }
            }

            return count;
        }
    }

    /// <summary>Kolik lampionů se vejde naráz do vzduchu (strop poolu).</summary>
    public static int Capacity => MaxLanterns;

    public void Update(float dt, Camera2D camera, Simulation simulation)
    {
        // Náběh a doběh: girlandy, které se objeví jedním snímkem, vypadají
        // jako chyba vykreslování, ne jako slavnost.
        float target = simulation.IsBoostActive ? 1f : 0f;
        _fade += Math.Clamp(target - _fade, -dt * 1.2f, dt * 1.2f);

        for (int i = 0; i < _lanterns.Length; i++)
        {
            if (!_lanterns[i].Alive)
            {
                continue;
            }

            _lanterns[i].Age += dt;
            _lanterns[i].Y -= LanternRiseTilesPerSecond * TileSize * dt;
            _lanterns[i].X += _lanterns[i].Drift * dt;
            if (_lanterns[i].Age >= LanternLifeSeconds)
            {
                _lanterns[i].Alive = false;
            }
        }

        if (!simulation.IsBoostActive)
        {
            return;
        }

        _releaseTimer -= dt;
        if (_releaseTimer <= 0f)
        {
            _releaseTimer = ReleaseIntervalSeconds;
            Release(camera);
        }
    }

    /// <summary>
    /// Pustí jeden lampion ze spodního okraje výřezu.
    ///
    /// <para>Z výřezu, ne z budov: lampion pouští lidi, ne domy, a hledat kvůli
    /// každému nejbližší obydlí by znamenalo dotaz do indexu zástavby
    /// třikrát za sekundu za nic.</para>
    /// </summary>
    private void Release(Camera2D camera)
    {
        int slot = FreeSlot();
        if (slot < 0)
        {
            return;
        }

        var (min, max) = camera.VisibleWorldBounds();
        _lanterns[slot] = new Lantern
        {
            X = min.X + (float)_rng.NextDouble() * (max.X - min.X),
            Y = max.Y + SpawnMarginTiles * TileSize,
            Age = 0f,
            Drift = ((float)_rng.NextDouble() - 0.5f) * 14f,
            Color = (byte)_rng.Next(LanternColors.Length),
            Alive = true,
        };
    }

    private int FreeSlot()
    {
        for (int i = 0; i < _lanterns.Length; i++)
        {
            if (!_lanterns[i].Alive)
            {
                return i;
            }
        }

        return -1;
    }

    public void Draw(SpriteBatch spriteBatch, Camera2D camera, Simulation simulation)
    {
        if (!IsVisible)
        {
            return;
        }

        spriteBatch.Begin(transformMatrix: camera.Transform, blendState: BlendState.Additive);
        DrawGarlands(spriteBatch, camera, simulation);
        DrawLanterns(spriteBatch);
        spriteBatch.End();
    }

    /// <summary>
    /// Řetězy světel nad ulicemi. Kreslí se jen tam, kde je silnice — girlanda
    /// natažená přes pole by neměla za co viset.
    /// </summary>
    private void DrawGarlands(SpriteBatch spriteBatch, Camera2D camera, Simulation simulation)
    {
        var (min, max) = camera.VisibleWorldBounds();
        int minX = (int)Math.Floor(min.X / TileSize);
        int minY = (int)Math.Floor(min.Y / TileSize);
        int maxX = (int)Math.Ceiling(max.X / TileSize);
        int maxY = (int)Math.Ceiling(max.Y / TileSize);

        // Strop na počet dlaždic: při maximálním oddálení je jich ve výřezu
        // statisíce a girlanda by v té velikosti stejně byla pod pixel.
        if ((long)(maxX - minX) * (maxY - minY) > 40_000)
        {
            return;
        }

        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                if (!simulation.IsRoad(x, y))
                {
                    continue;
                }

                // Ne na každou dlaždici: hustá řada světel splyne v čáru.
                uint hash = Hash(x, y);
                if ((hash & 3) != 0)
                {
                    continue;
                }

                var color = LanternColors[(int)((hash >> 8) % LanternColors.Length)];
                int lightSize = Math.Max(2, TileSize / 6);
                int offset = (int)((hash >> 16) % (uint)Math.Max(1, TileSize - lightSize));

                spriteBatch.Draw(
                    _pixel,
                    new Rectangle(x * TileSize + offset, y * TileSize + TileSize / 3, lightSize, lightSize),
                    color * (0.85f * _fade));
            }
        }
    }

    private void DrawLanterns(SpriteBatch spriteBatch)
    {
        for (int i = 0; i < _lanterns.Length; i++)
        {
            if (!_lanterns[i].Alive)
            {
                continue;
            }

            // Doháší ke konci života — lampion, který zmizí naráz, vypadá jako
            // vypnutá žárovka, ne jako odplouvající světlo.
            float life = 1f - (_lanterns[i].Age / LanternLifeSeconds);
            var color = LanternColors[_lanterns[i].Color] * (life * _fade * 0.9f);
            int size = Math.Max(3, TileSize / 4);

            spriteBatch.Draw(
                _pixel,
                new Rectangle((int)_lanterns[i].X, (int)_lanterns[i].Y, size, size),
                color);
        }
    }

    /// <summary>
    /// Rozhoď souřadnice na pseudonáhodné číslo. Deterministicky — girlanda
    /// musí mezi snímky zůstat na svém místě, jinak by světla blikala náhodně
    /// po celé ulici.
    /// </summary>
    private static uint Hash(int x, int y)
    {
        unchecked
        {
            uint h = (uint)(x * 374761393) ^ (uint)(y * 668265263);
            h = (h ^ (h >> 13)) * 1274126177;
            return h ^ (h >> 16);
        }
    }
}
