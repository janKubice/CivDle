using CivDle.Core.Content;
using CivDle.Core.Sim;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CivDle.Rendering;

/// <summary>
/// Minimapa v rohu obrazovky: pravidelně (ne každý snímek) vzorkuje terén v okolí
/// kamery do malé textury (1 pixel = <see cref="TilesPerPixel"/> dlaždic), přidá
/// tečky budov, objevených cizích měst a anomálií a rámeček aktuálního výřezu.
/// Nekonečná mapa — okno se posouvá s kamerou. Čistě render (jen čte simulaci).
///
/// <para>Dosah minimapy je zhruba pětinásobek toho, co je vidět na obrazovce.
/// Právě proto sem patří anomálie: leží skoro vždycky za okrajem výřezu, takže
/// jinak než odsud se o nich hráč nedozví.</para>
/// </summary>
public sealed class MinimapRenderer : IDisposable
{
    /// <summary>
    /// Hrana minimapy v pixelech. Bývala 168 a byla malá na to, aby se z ní dalo
    /// číst rozložení města — na obrazovce je místa dost.
    /// </summary>
    private const int SizePixels = 232;

    /// <summary>
    /// Kolik místa dole vpravo si minimapa bere. HUD si podle toho posadí sloupec
    /// tlačítek nad ni, místo aby jí ležel přes roh.
    /// </summary>
    public const int ReservedHeight = SizePixels + 24;
    private const int TilesPerPixel = 5;
    private const float RefreshSeconds = 0.4f;

    /// <summary>
    /// Jak daleko od kamery minimapa dohlédne (v dlaždicích).
    ///
    /// <para>Veřejné schválně: je to slib, na kterém stojí objevování. Nejbližší
    /// anomálie bývá kolem stovky dlaždic daleko — musí padnout sem dovnitř,
    /// jinak se o ní hráč nedozví nikde.</para>
    /// </summary>
    public const int ReachTiles = SizePixels * TilesPerPixel / 2;

    /// <summary>Barva anomálie — tatáž fialová jako značka na mapě, ať se to spojí dohromady.</summary>
    private static readonly Color AnomalyDot = new(214, 178, 255);

    /// <summary>Cíl běžící výpravy — zlatá, aby šel od ostatních anomálií rozeznat.</summary>
    private static readonly Color ExpeditionDot = new(255, 226, 150);

    private readonly GraphicsDevice _device;
    private readonly BiomeRegistry _biomes;
    private readonly Texture2D _pixel;
    private readonly Texture2D _mapTexture;
    private readonly Color[] _buffer = new Color[SizePixels * SizePixels];

    private float _refreshTimer;
    private int _centerTileX;
    private int _centerTileY;

    /// <summary>Anomálie v dosahu minimapy. Jeden seznam na celý život — žádná alokace za snímek.</summary>
    private readonly List<PointOfInterest> _anomalies = new();

    /// <summary>Fáze pulzu značek anomálií (tentýž rytmus jako na mapě).</summary>
    private float _pulse;

    public MinimapRenderer(GraphicsDevice device, BiomeRegistry biomes, Texture2D whitePixel)
    {
        _device = device;
        _biomes = biomes;
        _pixel = whitePixel;
        _mapTexture = new Texture2D(device, SizePixels, SizePixels);
    }

    public void Update(float dt, Camera2D camera, Simulation simulation)
    {
        _refreshTimer -= dt;
        _pulse += dt;
        _centerTileX = (int)MathF.Floor(camera.Position.X / TerrainRenderer.TileSize);
        _centerTileY = (int)MathF.Floor(camera.Position.Y / TerrainRenderer.TileSize);
        if (_refreshTimer > 0f)
        {
            return;
        }

        _refreshTimer = RefreshSeconds;
        RebuildTerrain(simulation);
    }

    public void Draw(SpriteBatch spriteBatch, Viewport viewport, Camera2D camera, Simulation simulation)
    {
        int margin = 12;
        int x = viewport.Width - SizePixels - margin;
        int y = viewport.Height - SizePixels - margin;

        spriteBatch.Begin(samplerState: SamplerState.PointClamp);
        // Rámeček + podklad.
        spriteBatch.Draw(_pixel, new Rectangle(x - 3, y - 3, SizePixels + 6, SizePixels + 6), new Color(90, 120, 150, 160));
        spriteBatch.Draw(_pixel, new Rectangle(x - 1, y - 1, SizePixels + 2, SizePixels + 2), new Color(12, 16, 22, 235));
        spriteBatch.Draw(_mapTexture, new Rectangle(x, y, SizePixels, SizePixels), Color.White);

        // Tečky budov.
        var buildings = simulation.Buildings;
        for (int i = 0; i < buildings.Length; i++)
        {
            if (TileToMinimap(buildings[i].X, buildings[i].Y, out int mx, out int my))
            {
                spriteBatch.Draw(_pixel, new Rectangle(x + mx, y + my, 2, 2), new Color(255, 240, 200));
            }
        }

        // Objevená cizí města. Neobjevená se nekreslí — jinak by minimapa
        // prozradila, co má hráč teprve najít, a mlha by ztratila smysl.
        // Barva je jen „cizí / už moje": na dvou pixelech by odstín druhu města
        // stejně nikdo nerozeznal.
        foreach (var city in simulation.CitiesNear(_centerTileX, _centerTileY, ReachTiles))
        {
            if (!simulation.IsCityDiscovered(city) || !TileToMinimap(city.X, city.Y, out int cmx, out int cmy))
            {
                continue;
            }

            var dot = simulation.NpcStateOf(city.Key).Absorbed
                ? new Color(250, 210, 110)
                : new Color(150, 200, 255);
            spriteBatch.Draw(_pixel, new Rectangle(x + cmx - 1, y + cmy - 1, 4, 4), dot);
        }

        // Anomálie. Tohle je jediné místo, kde se o nich hráč dozví dřív, než
        // na ně omylem najede: značka na mapě pulzuje, ale nejbližší anomálie
        // bývá osmdesát i sto dlaždic daleko, tedy dávno za okrajem obrazovky —
        // a bez důvodu se tam nikdo nerozjede. Mlha se tím neporušuje: anomálie
        // není cizí město, které se má najít, ale pozvánka, aby se hráč vydal
        // ven z města.
        simulation.PointsOfInterest.InRange(
            _centerTileX - ReachTiles, _centerTileY - ReachTiles,
            _centerTileX + ReachTiles, _centerTileY + ReachTiles,
            _anomalies);

        float glow = 0.6f + (0.4f * MathF.Sin(_pulse * 2.4f));
        for (int i = 0; i < _anomalies.Count; i++)
        {
            if (TileToMinimap(_anomalies[i].X, _anomalies[i].Y, out int amx, out int amy))
            {
                spriteBatch.Draw(_pixel, new Rectangle(x + amx - 1, y + amy - 1, 4, 4), AnomalyDot * glow);
            }
        }

        // Cíl běžící výpravy zůstane vidět, i když je „vybraný" — jinak by
        // hráč po vypravení ztratil jediné místo, kde se zrovna něco děje.
        if (simulation.ExpeditionRunning
            && TileToMinimap(simulation.ExpeditionTarget.X, simulation.ExpeditionTarget.Y, out int tmx, out int tmy))
        {
            spriteBatch.Draw(_pixel, new Rectangle(x + tmx - 1, y + tmy - 1, 4, 4), ExpeditionDot);
        }

        // Rámeček viditelného výřezu.
        var (min, max) = camera.VisibleWorldBounds();
        if (TileToMinimap((int)(min.X / TerrainRenderer.TileSize), (int)(min.Y / TerrainRenderer.TileSize), out int vx0, out int vy0)
            && TileToMinimap((int)(max.X / TerrainRenderer.TileSize), (int)(max.Y / TerrainRenderer.TileSize), out int vx1, out int vy1))
        {
            int rw = Math.Max(2, vx1 - vx0);
            int rh = Math.Max(2, vy1 - vy0);
            var frame = new Color(255, 255, 255, 190);
            spriteBatch.Draw(_pixel, new Rectangle(x + vx0, y + vy0, rw, 1), frame);
            spriteBatch.Draw(_pixel, new Rectangle(x + vx0, y + vy0 + rh, rw, 1), frame);
            spriteBatch.Draw(_pixel, new Rectangle(x + vx0, y + vy0, 1, rh), frame);
            spriteBatch.Draw(_pixel, new Rectangle(x + vx0 + rw, y + vy0, 1, rh), frame);
        }

        spriteBatch.End();
    }

    public void Dispose() => _mapTexture.Dispose();

    private void RebuildTerrain(Simulation simulation)
    {
        int half = SizePixels / 2;
        for (int py = 0; py < SizePixels; py++)
        {
            for (int px = 0; px < SizePixels; px++)
            {
                int tileX = _centerTileX + (px - half) * TilesPerPixel;
                int tileY = _centerTileY + (py - half) * TilesPerPixel;
                _buffer[py * SizePixels + px] = _biomes[simulation.BiomeAt(tileX, tileY)].MapColor.ToXna();
            }
        }

        _mapTexture.SetData(_buffer);
    }

    private bool TileToMinimap(int tileX, int tileY, out int mx, out int my)
    {
        int half = SizePixels / 2;
        mx = half + (tileX - _centerTileX) / TilesPerPixel;
        my = half + (tileY - _centerTileY) / TilesPerPixel;
        return mx >= 0 && mx < SizePixels && my >= 0 && my < SizePixels;
    }
}
