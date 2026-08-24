using CivDle.Core.Content;
using CivDle.Core.Sim;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CivDle.Rendering;

/// <summary>
/// Útočníci na mapě a značka nad poškozenou budovou.
///
/// <para>Vrstva: čistý render. Ze simulace jen čte pole útočníků; nikam do ní
/// nezapisuje a o pravidlech bitvy neví nic.</para>
///
/// <para>Kreslí se jen to, co je ve výřezu — vlna může přijít z druhé strany
/// mapy a hráč se zrovna dívá jinam. A jen zblízka: z výšky, kde je vidět celá
/// aglomerace, jsou útočníci pod rozlišením a stejně by z nich byly tečky.</para>
/// </summary>
public sealed class FrontierRenderer
{
    private const int TileSize = TerrainRenderer.TileSize;

    /// <summary>Pod tímhle přiblížením se útočníci nekreslí (LOD).</summary>
    public const float MinZoom = 0.5f;

    /// <summary>Barva proužku zdraví, když je útočník celý.</summary>
    private static readonly Color HealthFull = new(196, 90, 78);

    private readonly Texture2D _pixel;
    private readonly GameContent _content;
    private readonly Sprites.SpriteLibrary _sprites;

    public FrontierRenderer(Texture2D whitePixel, GameContent content, Sprites.SpriteLibrary sprites)
    {
        _pixel = whitePixel;
        _content = content;
        _sprites = sprites;
    }

    /// <summary>Vykreslí útočníky a poškození. Mimo režim obrany nedělá nic.</summary>
    public void Draw(SpriteBatch spriteBatch, Camera2D camera, Simulation simulation)
    {
        if (!simulation.FrontierDefense || camera.Zoom < DetailLevel.Scale(MinZoom))
        {
            return;
        }

        var (min, max) = camera.VisibleWorldBounds();
        spriteBatch.Begin(samplerState: SamplerState.PointClamp, transformMatrix: camera.Transform);

        DrawDamage(spriteBatch, simulation, min, max);
        DrawAttackers(spriteBatch, simulation, min, max);

        spriteBatch.End();
    }

    private void DrawAttackers(SpriteBatch spriteBatch, Simulation simulation, Vector2 min, Vector2 max)
    {
        var attackers = simulation.Frontier.Attackers;
        var defs = _content.Frontier.Attackers;

        for (int i = 0; i < attackers.Length; i++)
        {
            float px = attackers[i].X * TileSize;
            float py = attackers[i].Y * TileSize;
            if (px < min.X - TileSize || px > max.X + TileSize || py < min.Y - TileSize || py > max.Y + TileSize)
            {
                continue;
            }

            var def = defs[attackers[i].TypeIndex];
            var sprite = _sprites.Get(def.Sprite);
            if (sprite is null)
            {
                continue;
            }

            int size = TileSize;
            spriteBatch.Draw(
                sprite,
                new Rectangle((int)(px - size / 2f), (int)(py - size / 2f), size, size),
                Color.White);

            DrawHealthBar(spriteBatch, px, py, attackers[i].Health / (float)Math.Max(1, def.Health));
        }
    }

    /// <summary>
    /// Proužek zdraví. Kreslí se jen zraněným: nad plnou vlnou by to byla
    /// mřížka červených čárek a informace v tom žádná.
    /// </summary>
    private void DrawHealthBar(SpriteBatch spriteBatch, float px, float py, float fraction)
    {
        if (fraction >= 0.999f)
        {
            return;
        }

        const int Width = 16;
        int left = (int)(px - Width / 2f);
        int top = (int)(py - TileSize * 0.6f);

        spriteBatch.Draw(_pixel, new Rectangle(left, top, Width, 3), new Color(20, 20, 24) * 0.7f);
        spriteBatch.Draw(
            _pixel,
            new Rectangle(left, top, Math.Max(1, (int)(Width * Math.Clamp(fraction, 0f, 1f))), 3),
            HealthFull);
    }

    /// <summary>Poškozená budova dostane výstražné šrafování — je to dočasný stav.</summary>
    private void DrawDamage(SpriteBatch spriteBatch, Simulation simulation, Vector2 min, Vector2 max)
    {
        var buildings = simulation.Buildings;
        for (int i = 0; i < buildings.Length; i++)
        {
            if (buildings[i].DisabledTicks <= 0)
            {
                continue;
            }

            var def = _content.Buildings[buildings[i].DefIndex];
            int px = buildings[i].X * TileSize;
            int py = buildings[i].Y * TileSize;
            int width = def.FootprintWidth * TileSize;
            int height = def.FootprintHeight * TileSize;

            if (px + width < min.X || px > max.X || py + height < min.Y || py > max.Y)
            {
                continue;
            }

            spriteBatch.Draw(_pixel, new Rectangle(px, py, width, height), new Color(220, 90, 70) * 0.28f);
            spriteBatch.Draw(_pixel, new Rectangle(px, py, width, 2), new Color(230, 120, 90) * 0.8f);
            spriteBatch.Draw(_pixel, new Rectangle(px, py + height - 2, width, 2), new Color(230, 120, 90) * 0.8f);
        }
    }
}
