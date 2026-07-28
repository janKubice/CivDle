using CivDle.Core.Sim;
using CivDle.Rendering.Sprites;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CivDle.Rendering;

/// <summary>
/// Značky skrýší k objevení na nekonučné mapě: kde je (deterministicky) skrýš a
/// ještě nepadla, bliká na terénu maják. Dává důvod projíždět svět a klikat.
/// Čistě render (čte simulaci); vyzvednutí řeší klik v herní obrazovce.
/// </summary>
public sealed class DiscoveryRenderer
{
    private static readonly Color Tint = new(120, 220, 255);

    private readonly SpriteLibrary _sprites;
    private float _pulse;

    public DiscoveryRenderer(SpriteLibrary sprites)
    {
        _sprites = sprites;
    }

    public void Update(float dt) => _pulse += dt;

    public void Draw(SpriteBatch spriteBatch, Camera2D camera, Simulation simulation)
    {
        if (camera.Zoom < DetailLevel.Creatures)
        {
            return;
        }

        var sprite = _sprites.Get("fx.golden");
        if (sprite is null)
        {
            return;
        }

        const int tileSize = TerrainRenderer.TileSize;
        var (min, max) = camera.VisibleWorldBounds();
        int startX = (int)MathF.Floor(min.X / tileSize), endX = (int)MathF.Ceiling(max.X / tileSize);
        int startY = (int)MathF.Floor(min.Y / tileSize), endY = (int)MathF.Ceiling(max.Y / tileSize);
        float bob = MathF.Sin(_pulse * 3f) * 3f;
        float size = tileSize * 1.15f;

        spriteBatch.Begin(samplerState: SamplerState.PointClamp, transformMatrix: camera.Transform);
        for (int y = startY; y <= endY; y++)
        {
            for (int x = startX; x <= endX; x++)
            {
                if (!simulation.IsDiscoveryTile(x, y) || simulation.IsDiscoveryClaimed(x, y))
                {
                    continue;
                }

                var center = new Vector2((x + 0.5f) * tileSize, (y + 0.5f) * tileSize + bob);
                spriteBatch.Draw(sprite, center, null, Tint, 0f,
                    new Vector2(sprite.Width * 0.5f, sprite.Height * 0.5f), size / sprite.Width, SpriteEffects.None, 0f);
            }
        }

        spriteBatch.End();
    }
}
