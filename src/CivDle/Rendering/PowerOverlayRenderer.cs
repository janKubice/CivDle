using CivDle.Core.Sim;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CivDle.Rendering;

/// <summary>
/// Kam dosáhne proud a kde ho je málo.
///
/// <para><b>Proč to musí být vidět:</b> dosah elektrárny je pravidlo, které
/// hráč nemá jak odhadnout — na mapě není nic, co by ho naznačovalo. Bez téhle
/// vrstvy by mu továrna jen tiše jela na třetinu a on by hledal důvod
/// v surovinách.</para>
///
/// <para>Barva nese <b>pokrytí</b>, ne výkon: zelená = plný proud, přes žlutou
/// do červené = tady se šetří. Buňky, kde proud nikdo nechce, se nekreslí
/// vůbec — jinak by přes celou mapu ležel zelený koberec bez informace.</para>
///
/// <para>Vrstva: čistý render. Ze simulace jen čte; mřížku si simulace
/// přepočítává sama a líně.</para>
/// </summary>
public sealed class PowerOverlayRenderer
{
    private const int TileSize = TerrainRenderer.TileSize;
    private const int CellSize = PowerGridSystem.CellSize;

    private const float FillAlpha = 0.30f;
    private const int Border = 2;

    private readonly Texture2D _pixel;

    public PowerOverlayRenderer(Texture2D whitePixel) => _pixel = whitePixel;

    /// <summary>
    /// Barva pro dané pokrytí. Veřejná a statická kvůli testům i legendě —
    /// „co je málo proudu" je rozhodnutí o hře, ne o kreslení.
    /// </summary>
    public static Color ColorFor(double coverage)
    {
        if (coverage >= 0.999)
        {
            return new Color(110, 210, 130); // plný proud
        }

        if (coverage >= 0.6)
        {
            return new Color(226, 202, 88);  // šetří se
        }

        return coverage > 0.001
            ? new Color(224, 132, 62)        // je toho málo
            : new Color(214, 78, 70);        // tma
    }

    /// <summary>Vykreslí pokrytí přes buňky ve výřezu.</summary>
    /// <param name="fade">Průhlednost 0–1; vrstva se rozsvěcí plynule.</param>
    public void Draw(SpriteBatch spriteBatch, Camera2D camera, Simulation simulation, float fade)
    {
        if (fade <= 0.01f)
        {
            return;
        }

        var (min, max) = camera.VisibleWorldBounds();
        int fromX = (int)Math.Floor(min.X / TileSize) >> PowerGridSystem.CellShift;
        int toX = (int)Math.Ceiling(max.X / TileSize) >> PowerGridSystem.CellShift;
        int fromY = (int)Math.Floor(min.Y / TileSize) >> PowerGridSystem.CellShift;
        int toY = (int)Math.Ceiling(max.Y / TileSize) >> PowerGridSystem.CellShift;

        spriteBatch.Begin(samplerState: SamplerState.PointClamp, transformMatrix: camera.Transform);

        for (int cellY = fromY; cellY <= toY; cellY++)
        {
            for (int cellX = fromX; cellX <= toX; cellX++)
            {
                int tileX = cellX * CellSize;
                int tileY = cellY * CellSize;

                // Kde proud nikdo nechce, se nekreslí nic. Zelený koberec přes
                // celou mapu by neřekl vůbec nic.
                if (simulation.PowerSupplyAt(tileX, tileY) <= 0 && simulation.PowerAt(tileX, tileY) >= 0.999)
                {
                    continue;
                }

                var color = ColorFor(simulation.PowerAt(tileX, tileY));
                var rect = new Rectangle(tileX * TileSize, tileY * TileSize, CellSize * TileSize, CellSize * TileSize);

                spriteBatch.Draw(_pixel, rect, color * (FillAlpha * fade));
                spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Y, rect.Width, Border), color * (0.7f * fade));
                spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Y, Border, rect.Height), color * (0.7f * fade));
            }
        }

        spriteBatch.End();
    }
}
