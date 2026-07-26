using CivDle.Core.Content;
using CivDle.Core.Sim;
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

    public LandmarkRenderer(Texture2D whitePixel, GameContent content)
    {
        _pixel = whitePixel;
        _content = content;
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
}
