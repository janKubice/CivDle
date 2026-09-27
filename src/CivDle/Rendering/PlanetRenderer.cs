using CivDle.Core.Content;
using CivDle.Core.World;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CivDle.Rendering;

/// <summary>
/// Povrch planety jako mapa v zeměpisných souřadnicích (svety-design.md 5.4):
/// <b>planeta je doopravdy tvoje mapa</b> — biomy z generátoru světa se
/// jednou upečou do malé mapy a ta se pak promítá na kouli. K tomu mapa
/// nočních světel (kde žije město).
///
/// <para>Čistá data (pole barev), žádná grafická karta — upéct se dá
/// v testu a bez okna. Peče se jednou na svět a otevření mapy galaxie.</para>
/// </summary>
public sealed class PlanetSurface
{
    /// <summary>Šířka mapy (délka); výška je poloviční (šířka).</summary>
    public const int Width = 128;

    /// <summary>Výška mapy.</summary>
    public const int Height = 64;

    /// <summary>Kolik dlaždic světa připadá na jeden pixel mapy.</summary>
    public const int TilesPerPixel = 10;

    private PlanetSurface(Color[] colors, float[] lights)
    {
        Colors = colors;
        Lights = lights;
    }

    /// <summary>Barvy povrchu, řádek po řádku (Width × Height).</summary>
    public Color[] Colors { get; }

    /// <summary>Jas nočních světel 0–1 (Width × Height).</summary>
    public float[] Lights { get; }

    /// <summary>
    /// Povrch z terénu světa: kolem místa, kde stojí město, se čtou biomy
    /// a jejich barvy z dat. Póly dostanou čepičky, když je planeta má.
    /// </summary>
    /// <param name="content">Obsah světa (barvy biomů).</param>
    /// <param name="terrain">Terén světa.</param>
    /// <param name="look">Vzhled planety z dat.</param>
    /// <param name="centerX">Kam se dívá střed mapy (město).</param>
    /// <param name="centerY">Kam se dívá střed mapy (město).</param>
    /// <param name="population">Kolik lidí žije ve městě (velikost záře na noční straně).</param>
    public static PlanetSurface FromTerrain(
        GameContent content, ITerrain terrain, PlanetLook look, int centerX, int centerY, double population)
    {
        var colors = new Color[Width * Height];
        for (int v = 0; v < Height; v++)
        {
            for (int u = 0; u < Width; u++)
            {
                int x = centerX + (u - Width / 2) * TilesPerPixel;
                int y = centerY + (v - Height / 2) * TilesPerPixel;
                var biome = content.Biomes[terrain.BiomeAt(x, y)];
                colors[v * Width + u] = Caps(new Color(biome.MapColor.R, biome.MapColor.G, biome.MapColor.B), look, v);
            }
        }

        return new PlanetSurface(colors, CityLights(population));
    }

    /// <summary>
    /// Povrch planety, kterou hráč ještě nezaložil: z barev v datech —
    /// pásy plynného obra, nebo pevniny a moře z jednoduchého šumu.
    /// </summary>
    public static PlanetSurface FromLook(PlanetLook look, long seed)
    {
        var surface = new Color(look.Surface.R, look.Surface.G, look.Surface.B);
        var accent = new Color(look.Accent.R, look.Accent.G, look.Accent.B);
        var colors = new Color[Width * Height];
        for (int v = 0; v < Height; v++)
        {
            for (int u = 0; u < Width; u++)
            {
                Color color;
                if (look.Bands)
                {
                    // Pásy se lehce vlní — rovné pruhy vypadají jako vlajka, ne jako obr.
                    double wave = Math.Sin(u * 0.15 + seed % 7) * 1.5;
                    color = ((int)((v + wave) / 5) % 2 == 0) ? surface : accent;
                    color = Color.Lerp(color, Color.White, (float)(Noise(u, v, seed) * 0.12));
                }
                else
                {
                    double n = Noise(u / 2, v, seed) * 0.6 + Noise(u / 5, v / 3, seed + 11) * 0.4;
                    color = n > 0.52 ? surface : accent;
                }

                colors[v * Width + u] = Caps(color, look, v);
            }
        }

        return new PlanetSurface(colors, new float[Width * Height]);
    }

    /// <summary>Záře města: kolem středu mapy, širší s každým řádem obyvatel.</summary>
    private static float[] CityLights(double population)
    {
        var lights = new float[Width * Height];
        if (population < 1)
        {
            return lights;
        }

        double radius = 1.5 + Math.Log10(population + 1) * 1.6; // vesnice pár pixelů, megapole půl kontinentu
        for (int v = 0; v < Height; v++)
        {
            for (int u = 0; u < Width; u++)
            {
                double du = u - Width / 2;
                double dv = (v - Height / 2) * 1.6;
                double d = Math.Sqrt(du * du + dv * dv);
                if (d > radius)
                {
                    continue;
                }

                // Zrnitá, ne hladká: město jsou tečky světel, ne skvrna.
                double grain = Noise(u, v, 77) > 0.35 ? 1 : 0.3;
                lights[v * Width + u] = (float)((1 - d / radius) * grain);
            }
        }

        return lights;
    }

    private static Color Caps(Color color, PlanetLook look, int v)
    {
        if (!look.IceCaps)
        {
            return color;
        }

        int edge = Math.Min(v, Height - 1 - v);
        return edge < 6 ? Color.Lerp(color, Color.White, 0.85f) : color;
    }

    /// <summary>Hodnotový šum 0–1 z hashe — deterministický, bez knihovny.</summary>
    private static double Noise(int u, int v, long seed)
    {
        ulong h = (ulong)(u * 73856093) ^ (ulong)(v * 19349663) ^ (ulong)seed * 83492791UL;
        h ^= h >> 13;
        h *= 0x5bd1e995UL;
        h ^= h >> 15;
        return (h % 1000) / 1000.0;
    }
}

/// <summary>
/// Kreslí planetu jako otáčející se kouli: povrch se promítá na kotouč,
/// strana od slunce tmavne do terminátoru a na noční straně svítí město.
///
/// <para><b>Levně:</b> pro každý pixel kotouče se jednou spočítá zeměpisná
/// šířka, výchozí délka a osvětlení (tabulka pro danou velikost); rotace je
/// pak jen posun v délce. Textura se překresluje dvanáctkrát za sekundu, ne
/// každý snímek — pomalé otáčení to nepozná a CPU to ušetří.</para>
/// </summary>
public sealed class PlanetDisk : IDisposable
{
    /// <summary>Kolikrát za sekundu se kotouč překreslí.</summary>
    private const float RefreshPerSecond = 12f;

    private readonly PlanetSurface _surface;
    private readonly Texture2D _texture;
    private readonly Color[] _pixels;
    private readonly float[] _lon;
    private readonly int[] _row;
    private readonly float[] _light;
    private readonly bool[] _inside;
    private readonly float[] _rim;
    private readonly Color _atmosphere;
    private float _sinceRefresh = float.MaxValue;

    /// <param name="device">Grafické zařízení.</param>
    /// <param name="surface">Upečený povrch.</param>
    /// <param name="diameter">Průměr kotouče v pixelech.</param>
    /// <param name="atmosphere">Barva atmosféry na okraji.</param>
    public PlanetDisk(GraphicsDevice device, PlanetSurface surface, int diameter, Color atmosphere)
    {
        _surface = surface;
        _atmosphere = atmosphere;
        Diameter = Math.Max(8, diameter);
        _texture = new Texture2D(device, Diameter, Diameter);
        int count = Diameter * Diameter;
        _pixels = new Color[count];
        _lon = new float[count];
        _row = new int[count];
        _light = new float[count];
        _inside = new bool[count];
        _rim = new float[count];

        // Slunce zleva shora a trochu zepředu: terminátor přes pravou třetinu.
        var sun = Vector3.Normalize(new Vector3(-0.75f, -0.35f, 0.55f));
        float radius = Diameter / 2f;
        for (int y = 0; y < Diameter; y++)
        {
            for (int x = 0; x < Diameter; x++)
            {
                int i = y * Diameter + x;
                float nx = (x + 0.5f - radius) / radius;
                float ny = (y + 0.5f - radius) / radius;
                float d2 = nx * nx + ny * ny;
                if (d2 > 1f)
                {
                    continue;
                }

                float nz = MathF.Sqrt(1f - d2);
                _inside[i] = true;
                _lon[i] = MathF.Atan2(nx, nz) / MathF.Tau * PlanetSurface.Width;
                float lat = MathF.Asin(Math.Clamp(ny, -1f, 1f));
                _row[i] = Math.Clamp((int)((lat / MathF.PI + 0.5f) * PlanetSurface.Height), 0, PlanetSurface.Height - 1);
                _light[i] = Vector3.Dot(new Vector3(nx, ny, nz), sun);
                _rim[i] = MathF.Pow(1f - nz, 3f);
            }
        }
    }

    /// <summary>Průměr kotouče v pixelech.</summary>
    public int Diameter { get; }

    /// <summary>Otočení planety (podíl otáčky, 0–1).</summary>
    public float Rotation { get; set; }

    /// <summary>
    /// Posune otáčení a podle potřeby kotouč překreslí.
    /// </summary>
    public void Update(float dt, float turnsPerSecond)
    {
        Rotation = (Rotation + dt * turnsPerSecond) % 1f;
        _sinceRefresh += dt;
        if (_sinceRefresh < 1f / RefreshPerSecond)
        {
            return;
        }

        _sinceRefresh = 0;
        Paint();
    }

    /// <summary>Nakreslí kotouč na dané místo (střed).</summary>
    public void Draw(SpriteBatch batch, Vector2 center, float alpha = 1f)
    {
        if (_sinceRefresh == float.MaxValue)
        {
            Paint(); // první snímek: nečekat na tik
            _sinceRefresh = 0;
        }

        batch.Draw(
            _texture,
            new Rectangle((int)(center.X - Diameter / 2f), (int)(center.Y - Diameter / 2f), Diameter, Diameter),
            Color.White * alpha);
    }

    public void Dispose() => _texture.Dispose();

    private void Paint()
    {
        var colors = _surface.Colors;
        var lights = _surface.Lights;
        float shift = Rotation * PlanetSurface.Width;
        for (int i = 0; i < _pixels.Length; i++)
        {
            if (!_inside[i])
            {
                _pixels[i] = Color.Transparent;
                continue;
            }

            int u = (int)(_lon[i] + shift + PlanetSurface.Width * 2) % PlanetSurface.Width;
            int index = _row[i] * PlanetSurface.Width + u;
            var ground = colors[index];

            // Den plně, terminátor měkce, noc tmavě modře — ale ne černě,
            // ať je tvar planety čitelný i ze stínu.
            float day = Math.Clamp(_light[i] * 1.6f + 0.25f, 0f, 1f);
            var night = new Color(10, 14, 30);
            var color = Color.Lerp(night, ground, 0.15f + day * 0.85f);

            // Světla města jen na noční straně: na denní je přesvítí slunce.
            float glow = lights[index] * (1f - day);
            if (glow > 0.02f)
            {
                color = Color.Lerp(color, new Color(255, 214, 140), Math.Min(1f, glow * 1.4f));
            }

            color = Color.Lerp(color, _atmosphere, _rim[i] * 0.8f);
            _pixels[i] = color;
        }

        _texture.SetData(_pixels);
    }
}
