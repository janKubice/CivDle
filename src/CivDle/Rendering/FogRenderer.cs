using CivDle.Core.Sim;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CivDle.Rendering;

/// <summary>
/// Závoj přes neprozkoumaný svět.
///
/// <para>Kreslí se <b>po čtvercích mlhy</b> (viz <see cref="FogOfWar.ChunkTiles"/>)
/// a jen přes to, co je právě na obrazovce — na nekonečné mapě je jakýkoli jiný
/// postup nepoužitelný.</para>
///
/// <para>Neodhalený čtverec není úplně černý: nechá prosvítat obrys terénu.
/// Úplná čerň by z okraje mapy udělala zeď a hráč by neměl kam mířit; takhle
/// je vidět, že tam něco je, ale ne co.</para>
///
/// <para>Vrstva: čte ze simulace, nikdy do ní nezapisuje.</para>
/// </summary>
public sealed class FogRenderer
{
    /// <summary>Jak hustý je závoj nad neprozkoumaným (0 = nic, 1 = čerň).</summary>
    private const float Density = 0.82f;

    /// <summary>Poloviční závoj na hranici — ostrá hrana vypadá jako chyba, ne jako mlha.</summary>
    private const float EdgeDensity = 0.4f;

    private readonly Texture2D _pixel;

    public FogRenderer(Texture2D whitePixel) => _pixel = whitePixel;

    /// <summary>
    /// Zatáhne neprozkoumané části obrazu. Volá se po terénu a před budovami —
    /// mlha má schovat i to, co v ní stojí.
    /// </summary>
    public void Draw(SpriteBatch spriteBatch, Camera2D camera, FogOfWar fog)
    {
        var (min, max) = camera.VisibleWorldBounds();
        int chunkPixels = FogOfWar.ChunkTiles * TerrainRenderer.TileSize;

        int startX = FloorDiv((int)MathF.Floor(min.X), chunkPixels);
        int startY = FloorDiv((int)MathF.Floor(min.Y), chunkPixels);
        int endX = FloorDiv((int)MathF.Ceiling(max.X), chunkPixels);
        int endY = FloorDiv((int)MathF.Ceiling(max.Y), chunkPixels);

        spriteBatch.Begin(transformMatrix: camera.Transform);
        for (int cy = startY; cy <= endY; cy++)
        {
            for (int cx = startX; cx <= endX; cx++)
            {
                int tileX = cx * FogOfWar.ChunkTiles;
                int tileY = cy * FogOfWar.ChunkTiles;
                if (fog.IsExplored(tileX, tileY))
                {
                    continue;
                }

                // Sousedí-li čtverec s odhaleným, ztmav ho jen napůl. Vznikne
                // z toho měkký lem místo schodovité zdi.
                float density = HasExploredNeighbour(fog, tileX, tileY) ? EdgeDensity : Density;
                spriteBatch.Draw(
                    _pixel,
                    new Rectangle(cx * chunkPixels, cy * chunkPixels, chunkPixels, chunkPixels),
                    new Color(6, 8, 14) * density);
            }
        }

        spriteBatch.End();
    }

    private static bool HasExploredNeighbour(FogOfWar fog, int tileX, int tileY) =>
        fog.IsExplored(tileX + FogOfWar.ChunkTiles, tileY)
        || fog.IsExplored(tileX - FogOfWar.ChunkTiles, tileY)
        || fog.IsExplored(tileX, tileY + FogOfWar.ChunkTiles)
        || fog.IsExplored(tileX, tileY - FogOfWar.ChunkTiles);

    private static int FloorDiv(int value, int divisor)
    {
        int quotient = value / divisor;
        return value % divisor != 0 && (value < 0) != (divisor < 0) ? quotient - 1 : quotient;
    }
}
