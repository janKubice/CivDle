using CivDle.Core.Content;
using CivDle.Core.Sim;
using CivDle.Rendering.Sprites;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CivDle.Rendering;

/// <summary>
/// Vykreslení landmarků (living-map.md §4): vzácné výrazné body zájmu, které lámou
/// monotónnost nekonečné mapy. Sbíratelné (stádo, háj, žíla) mají navíc jemný
/// prstenec, aby hráč poznal, že se na ně vyplatí kliknout.
///
/// Čte jen ze simulace (výskyt je čistá funkce pozice) a kreslí jen viditelné
/// dlaždice — LOD/culling dle tech-stack.md.
/// </summary>
public sealed class LandmarkRenderer
{
    /// <summary>Pod tímhle zoomem se landmarky nekreslí (LOD — z výšky by byly stejně pod rozlišením).</summary>
    public const float MinZoom = 0.75f;


    private readonly Texture2D _pixel;
    private readonly GameContent _content;
    private readonly SpriteLibrary _sprites;
    private readonly Texture2D? _shadow;

    public LandmarkRenderer(Texture2D whitePixel, GameContent content, SpriteLibrary sprites)
    {
        _pixel = whitePixel;
        _content = content;
        _sprites = sprites;
        _shadow = sprites.Get("fx.shadow");
    }

    public void Draw(SpriteBatch spriteBatch, Camera2D camera, Simulation simulation)
    {
        if (_content.Landmarks.Count == 0)
        {
            return;
        }

        const int tileSize = TerrainRenderer.TileSize;
        var (min, max) = camera.VisibleWorldBounds();
        int minTileX = (int)MathF.Floor(min.X / tileSize);
        int maxTileX = (int)MathF.Ceiling(max.X / tileSize);
        int minTileY = (int)MathF.Floor(min.Y / tileSize);
        int maxTileY = (int)MathF.Ceiling(max.Y / tileSize);

        spriteBatch.Begin(samplerState: SamplerState.PointClamp, transformMatrix: camera.Transform);
        for (int tileY = minTileY; tileY <= maxTileY; tileY++)
        {
            for (int tileX = minTileX; tileX <= maxTileX; tileX++)
            {
                int index = simulation.LandmarkAt(tileX, tileY);
                if (index < 0)
                {
                    continue;
                }

                var def = _content.Landmarks[index];

                // Sprite, když ho data mají. Vrak lodi jako hnědý čtvereček
                // nevypadá jako vrak lodi — a přesně to hráč hlásil.
                if (def.SpriteKey is { } key && _sprites.Get(key) is { } sprite)
                {
                    DrawSprite(spriteBatch, sprite, def, tileX, tileY);
                    continue;
                }

                int size = Math.Min(def.Size, tileSize);
                int offset = (tileSize - size) / 2;
                int x = tileX * tileSize + offset;
                int y = tileY * tileSize + offset;

                // Sbíratelný landmark dostane světlý prstenec = „klikni na mě".
                if (def.IsHarvestable)
                {
                    spriteBatch.Draw(_pixel, new Rectangle(x - 2, y - 2, size + 4, size + 4), new Color(255, 245, 200) * 0.35f);
                }

                spriteBatch.Draw(_pixel, new Rectangle(x, y, size, size), def.MapColor.ToXna());
            }
        }

        spriteBatch.End();
    }

    /// <summary>
    /// Landmark spritem. Roste do svého půdorysu (ruiny a vrak jsou 2×2) a stojí
    /// na stínu, aby na terénu seděl a nevznášel se.
    /// </summary>
    private void DrawSprite(SpriteBatch spriteBatch, Texture2D sprite, LandmarkDef def, int tileX, int tileY)
    {
        const int tileSize = TerrainRenderer.TileSize;
        int span = def.Footprint * tileSize;

        // Vycentrováno NA dlaždici, ne od jejího rohu doprava dolů. Landmark je
        // v simulaci na jedné dlaždici; když se dvoupolní sprite kreslil od
        // rohu, seděl vedle místa, na které se dá kliknout — a přesně to hráč
        // hlásil jako „jsou posunuté".
        int offset = (span - tileSize) / 2;
        var bounds = new Rectangle(
            tileX * tileSize - offset, tileY * tileSize - offset, span, span);

        if (_shadow is not null)
        {
            var scale = new Vector2(span * 0.7f / _shadow.Width, span * 0.24f / _shadow.Height);
            spriteBatch.Draw(_shadow, new Vector2(bounds.X + span * 0.5f, bounds.Bottom - span * 0.12f),
                null, Color.White * 0.6f, 0f,
                new Vector2(_shadow.Width * 0.5f, _shadow.Height * 0.5f), scale, SpriteEffects.None, 0f);
        }

        // Sbíratelné místo dostane teplý nádech = „klikni na mě".
        var tint = def.IsHarvestable ? new Color(255, 245, 210) : Color.White;
        spriteBatch.Draw(sprite, bounds, tint);
    }
}
