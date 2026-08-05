using CivDle.Core.Content;
using CivDle.Core.Sim;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CivDle.Rendering;

/// <summary>
/// Vykreslení auto-silnic: každá silniční dlaždice má středový polštářek
/// a ramena k sousedním silnicím či budovám — síť tak vypadá jako spojité
/// pěšiny, ne šachovnice. Barva z gameplay dat, culling podle výřezu.
/// Čte jen ze simulace (nekonečná mapa — silnice jsou souřadnice).
/// </summary>
public sealed class RoadRenderer
{
    private const int Pad = 5;      // odsazení středového polštářku
    private const int Thickness = 6; // šířka pěšiny

    private readonly Texture2D _pixel;
    private readonly GameContent _content;

    public RoadRenderer(Texture2D whitePixel, GameContent content)
    {
        _pixel = whitePixel;
        _content = content;
    }

    public void Draw(SpriteBatch spriteBatch, Camera2D camera, Simulation simulation)
    {
        var (min, max) = camera.VisibleWorldBounds();

        spriteBatch.Begin(samplerState: SamplerState.PointClamp, transformMatrix: camera.Transform);
        DrawTiles(spriteBatch, simulation, simulation.RoadTiles, min, max);

        // Ulice cizích měst a cesty mezi nimi. Jsou to tytéž silniční dlaždice,
        // takže se kreslí týmž kódem — dřív to byly čáry přes mapu a bylo na
        // první pohled poznat, že to nejsou silnice, po kterých se dá jet.
        DrawTiles(spriteBatch, simulation, simulation.NpcRoadTiles, min, max);
        spriteBatch.End();
    }

    /// <summary>Vykreslí jeden seznam silničních dlaždic. Vlastník na vzhled nemá vliv.</summary>
    private void DrawTiles(
        SpriteBatch spriteBatch, Simulation simulation, IReadOnlyList<RoadTile> roadTiles,
        Vector2 min, Vector2 max)
    {
        const int tileSize = TerrainRenderer.TileSize;
        var roadColor = _content.Gameplay.Roads.MapColor.ToXna();
        // Most = silnice po vodě. Dřevěná deska pod cestou ho odliší od běžné pěšiny.
        var bridgeColor = new Color(122, 88, 56);

        for (int i = 0; i < roadTiles.Count; i++)
        {
            int tileX = roadTiles[i].X;
            int tileY = roadTiles[i].Y;
            int x = tileX * tileSize;
            int y = tileY * tileSize;
            if (x + tileSize < min.X || x > max.X || y + tileSize < min.Y || y > max.Y)
            {
                continue;
            }

            var color = roadColor;
            if (simulation.IsBridge(tileX, tileY))
            {
                // Podklad mostu přes celou dlaždici, ať je nad vodou čitelný.
                spriteBatch.Draw(_pixel, new Rectangle(x, y, tileSize, tileSize), bridgeColor);
                color = new Color(168, 132, 92);
            }

            spriteBatch.Draw(_pixel, new Rectangle(x + Pad, y + Pad, Thickness, Thickness), color);

            if (Connects(simulation, tileX + 1, tileY))
            {
                spriteBatch.Draw(_pixel, new Rectangle(x + Pad + Thickness, y + Pad, tileSize - Pad - Thickness, Thickness), color);
            }

            if (Connects(simulation, tileX - 1, tileY))
            {
                spriteBatch.Draw(_pixel, new Rectangle(x, y + Pad, Pad, Thickness), color);
            }

            if (Connects(simulation, tileX, tileY + 1))
            {
                spriteBatch.Draw(_pixel, new Rectangle(x + Pad, y + Pad + Thickness, Thickness, tileSize - Pad - Thickness), color);
            }

            if (Connects(simulation, tileX, tileY - 1))
            {
                spriteBatch.Draw(_pixel, new Rectangle(x + Pad, y, Thickness, Pad), color);
            }
        }
    }

    /// <summary>
    /// Rameno se kreslí k sousední silnici i k budově (vizuální napojení na vchod).
    /// Cizí ulice a domy se počítají taky — jinak by se hráčova silnice před
    /// cizím městem zastavila a mezi nimi zůstala mezera.
    /// </summary>
    private static bool Connects(Simulation simulation, int x, int y) =>
        simulation.HasRoadAt(x, y) || simulation.IsOccupied(x, y) || simulation.IsNpcOccupied(x, y);
}
