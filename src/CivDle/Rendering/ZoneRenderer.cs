using CivDle.Core.Content;
using CivDle.Core.Sim;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CivDle.Rendering;

/// <summary>
/// Vykreslení zón (automatizace, stupeň 3): jemný barevný tint přes dlaždice
/// zóny plus ohraničení, ať je zóna čitelná i tam, kde už stojí budovy. Barva
/// se bere z typu zóny. Čte jen ze simulace (zóny jsou obdélníky souřadnic),
/// culling podle výřezu kamery.
/// </summary>
public sealed class ZoneRenderer
{
    private const int TileSize = TerrainRenderer.TileSize;
    private const float FillAlpha = 0.18f;
    private const int Border = 2;

    private readonly Texture2D _pixel;
    private readonly GameContent _content;

    public ZoneRenderer(Texture2D whitePixel, GameContent content)
    {
        _pixel = whitePixel;
        _content = content;
    }

    /// <summary>Vykreslí všechny namalované zóny (tint na zemi, pod budovami).</summary>
    public void Draw(SpriteBatch spriteBatch, Camera2D camera, Simulation simulation)
    {
        var zones = simulation.Zones;
        if (zones.Count == 0)
        {
            return;
        }

        var (min, max) = camera.VisibleWorldBounds();
        spriteBatch.Begin(samplerState: SamplerState.PointClamp, transformMatrix: camera.Transform);
        for (int i = 0; i < zones.Count; i++)
        {
            var zone = zones[i];
            var color = _content.ZoneTypes[zone.TypeIndex].MapColor.ToXna();
            DrawRect(spriteBatch, zone.X, zone.Y, zone.Width, zone.Height, color, min, max, preview: false);
        }

        spriteBatch.End();
    }

    /// <summary>Náhled právě malované zóny během tažení (výraznější než hotové zóny).</summary>
    public void DrawPreview(SpriteBatch spriteBatch, Camera2D camera, int typeIndex, int x, int y, int width, int height)
    {
        if (typeIndex < 0 || typeIndex >= _content.ZoneTypes.Count || width <= 0 || height <= 0)
        {
            return;
        }

        var (min, max) = camera.VisibleWorldBounds();
        var color = _content.ZoneTypes[typeIndex].MapColor.ToXna();
        spriteBatch.Begin(samplerState: SamplerState.PointClamp, transformMatrix: camera.Transform);
        DrawRect(spriteBatch, x, y, width, height, color, min, max, preview: true);
        spriteBatch.End();
    }

    private void DrawRect(SpriteBatch spriteBatch, int tileX, int tileY, int width, int height, Color color, Vector2 min, Vector2 max, bool preview)
    {
        int px = tileX * TileSize;
        int py = tileY * TileSize;
        int pw = width * TileSize;
        int ph = height * TileSize;
        if (px + pw < min.X || px > max.X || py + ph < min.Y || py > max.Y)
        {
            return; // celý obdélník mimo výřez
        }

        spriteBatch.Draw(_pixel, new Rectangle(px, py, pw, ph), color * (preview ? FillAlpha * 1.6f : FillAlpha));

        // Ohraničení (4 pruhy) v plnější barvě, ať hrany zóny jasně čtou.
        var edge = color * (preview ? 1.0f : 0.75f);
        spriteBatch.Draw(_pixel, new Rectangle(px, py, pw, Border), edge);
        spriteBatch.Draw(_pixel, new Rectangle(px, py + ph - Border, pw, Border), edge);
        spriteBatch.Draw(_pixel, new Rectangle(px, py, Border, ph), edge);
        spriteBatch.Draw(_pixel, new Rectangle(px + pw - Border, py, Border, ph), edge);
    }
}
