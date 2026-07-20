using CivDle.Core.Content;
using CivDle.Core.Sim;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CivDle.Rendering;

/// <summary>
/// Minimapa v rohu obrazovky: pravidelně (ne každý snímek) vzorkuje terén v okolí
/// kamery do malé textury (1 pixel = <see cref="TilesPerPixel"/> dlaždic), přidá
/// tečky budov a rámeček aktuálního výřezu. Nekonečná mapa — okno se posouvá
/// s kamerou. Čistě render (jen čte simulaci).
/// </summary>
public sealed class MinimapRenderer : IDisposable
{
    private const int SizePixels = 168;
    private const int TilesPerPixel = 5;
    private const float RefreshSeconds = 0.4f;

    private readonly GraphicsDevice _device;
    private readonly BiomeRegistry _biomes;
    private readonly Texture2D _pixel;
    private readonly Texture2D _mapTexture;
    private readonly Color[] _buffer = new Color[SizePixels * SizePixels];

    private float _refreshTimer;
    private int _centerTileX;
    private int _centerTileY;

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
