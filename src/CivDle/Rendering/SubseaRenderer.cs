using CivDle.Core.Sim;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CivDle.Rendering;

/// <summary>
/// Ukáže, kam až od přístavů sahá moře, ve kterém se dá stavět.
///
/// <para><b>Proč to musí být vidět:</b> dosah sítě je pravidlo, které hráč
/// nemá jak odhadnout — voda vypadá všude stejně. Bez téhle vrstvy by mu hra
/// jen odmítala stavbu s hláškou a on by hádal, o kolik dlaždic vedle. Takhle
/// je odpověď na obrazovce dřív, než se stihne zeptat.</para>
///
/// <para>Kreslí se dvě věci: <b>světlejší nádech</b> na dosažitelné vodě
/// a <b>obrys</b> na jejím okraji. Samotný nádech na hluboké modré skoro není
/// vidět; hranu oko najde okamžitě.</para>
///
/// <para>Vrstva: čistý render. Ze simulace jen čte (síť si počítá sama a líně),
/// nic do ní nezapisuje. Kreslí se jen to, co je ve výřezu kamery — dosah může
/// být rozlitý přes stovky dlaždic.</para>
/// </summary>
public sealed class SubseaRenderer
{
    private const int TileSize = TerrainRenderer.TileSize;

    /// <summary>Nádech na dosažitelné vodě. Slabý schválně — má napovídat, ne přebít mapu.</summary>
    private static readonly Color Fill = new(120, 220, 235);

    private const float FillAlpha = 0.16f;
    private const float EdgeAlpha = 0.55f;
    private const int EdgeThickness = 2;

    private readonly Texture2D _pixel;

    public SubseaRenderer(Texture2D whitePixel) => _pixel = whitePixel;

    /// <summary>
    /// Vykreslí dosah sítě přes vodu ve výřezu.
    /// </summary>
    /// <param name="fade">
    /// Průhlednost 0–1. Vrstva se rozsvěcí a zhasíná plynule; skok by při
    /// každém výběru budovy trhnul celou obrazovkou.
    /// </param>
    public void Draw(SpriteBatch spriteBatch, Camera2D camera, Simulation simulation, float fade)
    {
        if (fade <= 0.01f || !simulation.Subsea.IsEnabled || simulation.Subsea.CoveredTiles == 0)
        {
            return;
        }

        var network = simulation.Subsea;
        var (min, max) = camera.VisibleWorldBounds();

        int fromX = (int)Math.Floor(min.X / TileSize);
        int toX = (int)Math.Ceiling(max.X / TileSize);
        int fromY = (int)Math.Floor(min.Y / TileSize);
        int toY = (int)Math.Ceiling(max.Y / TileSize);

        spriteBatch.Begin(samplerState: SamplerState.PointClamp, transformMatrix: camera.Transform);

        for (int y = fromY; y <= toY; y++)
        {
            for (int x = fromX; x <= toX; x++)
            {
                if (!network.Covers(x, y))
                {
                    continue;
                }

                int px = x * TileSize;
                int py = y * TileSize;
                spriteBatch.Draw(_pixel, new Rectangle(px, py, TileSize, TileSize), Fill * (FillAlpha * fade));

                // Hrana se kreslí jen tam, kde soused do sítě nepatří — obrys
                // kolem každé dlaždice by byl mřížka, ne hranice.
                float edge = EdgeAlpha * fade;
                if (!network.Covers(x, y - 1))
                {
                    spriteBatch.Draw(_pixel, new Rectangle(px, py, TileSize, EdgeThickness), Fill * edge);
                }

                if (!network.Covers(x, y + 1))
                {
                    spriteBatch.Draw(_pixel, new Rectangle(px, py + TileSize - EdgeThickness, TileSize, EdgeThickness), Fill * edge);
                }

                if (!network.Covers(x - 1, y))
                {
                    spriteBatch.Draw(_pixel, new Rectangle(px, py, EdgeThickness, TileSize), Fill * edge);
                }

                if (!network.Covers(x + 1, y))
                {
                    spriteBatch.Draw(_pixel, new Rectangle(px + TileSize - EdgeThickness, py, EdgeThickness, TileSize), Fill * edge);
                }
            }
        }

        spriteBatch.End();
    }
}
