using CivDle.Core.Content;
using CivDle.Core.Sim;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CivDle.Rendering;

/// <summary>
/// Stopa sítě na zemi (svety-design.md 4.1, 5.3): v dosahu vody poušť
/// zezelená — tráva a trsy keřů — a mimo ni zůstane písek. Hranice vodní
/// sítě je tak vidět na první pohled, bez překryvu.
///
/// <para><b>Plynule, ne po čtvercích:</b> síť se počítá po buňkách 8×8, ale
/// zelená buňka s ostrou hranou by vypadala jako záhon z pravítka. Vlhkost se
/// proto mezi středy buněk prolíná (bilineárně) a trsy řídnou k okraji.</para>
///
/// <para><b>Bez stavu a bez alokací za snímek:</b> kde trs stojí a jak vypadá,
/// je hash souřadnic dlaždice — stejné místo vypadá stejně v každém snímku.
/// Pole vlhkosti výřezu je jedno na celý život renderu. Z velké dálky se
/// kreslí jen měkký nádech po buňkách.</para>
///
/// <para>Vrstva: render. Ze simulace jen čte (dodávku a pokrytí sítě, biom).</para>
/// </summary>
public sealed class NetworkGroundRenderer
{
    private const int TileSize = TerrainRenderer.TileSize;
    private const int CellSize = NetworkSystem.CellSize;

    /// <summary>Pod tímhle přiblížením se trsy nekreslí — jen nádech po buňkách.</summary>
    private const float TuftZoom = 0.9f;

    private readonly Texture2D _pixel;
    private readonly GameContent _content;
    private float[] _wet = Array.Empty<float>();
    private int _cellsX;
    private int _cellsY;

    public NetworkGroundRenderer(Texture2D whitePixel, GameContent content)
    {
        _pixel = whitePixel;
        _content = content;
    }

    /// <summary>Nakreslí stopu každé sítě, která ji má (svět bez ní nestojí nic).</summary>
    public void Draw(SpriteBatch spriteBatch, Camera2D camera, Simulation simulation)
    {
        var networks = _content.Networks;
        bool begun = false;
        for (int n = 0; n < networks.Count; n++)
        {
            if (networks[n].Ground is not { } ground)
            {
                continue;
            }

            if (!begun)
            {
                spriteBatch.Begin(samplerState: SamplerState.PointClamp, transformMatrix: camera.Transform);
                begun = true;
            }

            DrawNetwork(spriteBatch, camera, simulation, n, ground);
        }

        if (begun)
        {
            spriteBatch.End();
        }
    }

    private void DrawNetwork(SpriteBatch spriteBatch, Camera2D camera, Simulation simulation, int network, NetworkGround ground)
    {
        var (min, max) = camera.VisibleWorldBounds();
        int fromTileX = (int)Math.Floor(min.X / TileSize);
        int fromTileY = (int)Math.Floor(min.Y / TileSize);
        int toTileX = (int)Math.Ceiling(max.X / TileSize);
        int toTileY = (int)Math.Ceiling(max.Y / TileSize);

        // Vlhkost buněk výřezu i s okrajem (kvůli prolínání na hranách).
        int fromCellX = (fromTileX >> NetworkSystem.CellShift) - 1;
        int fromCellY = (fromTileY >> NetworkSystem.CellShift) - 1;
        int cellsX = (toTileX >> NetworkSystem.CellShift) - fromCellX + 2;
        int cellsY = (toTileY >> NetworkSystem.CellShift) - fromCellY + 2;
        if (cellsX <= 0 || cellsY <= 0 || cellsX * cellsY > 40_000)
        {
            return; // absurdní výřez (extrémní oddálení) — stopa by stejně nebyla vidět
        }

        if (_wet.Length < cellsX * cellsY)
        {
            _wet = new float[cellsX * cellsY];
        }

        _cellsX = cellsX;
        _cellsY = cellsY;

        bool any = false;
        for (int cy = 0; cy < cellsY; cy++)
        {
            for (int cx = 0; cx < cellsX; cx++)
            {
                int tileX = (fromCellX + cx) * CellSize;
                int tileY = (fromCellY + cy) * CellSize;
                // Zelená tam, kam voda opravdu teče k odběratelům (domy, háje) —
                // ne celý dosah studny: ten je na „žilnatinu" moc široký a zelenala
                // by celá poušť kolem města. Buňka, které voda nestačí, zelená méně.
                float wet = 0f;
                if (simulation.NetworkSupplyAt(network, tileX, tileY) > 0)
                {
                    wet = 0.35f + 0.65f * (float)Math.Clamp(simulation.NetworkCoverageAt(network, tileX, tileY), 0, 1);
                    any = true;
                }

                _wet[cy * cellsX + cx] = wet;
            }
        }

        if (!any)
        {
            return;
        }

        var color = new Color(ground.Color.R, ground.Color.G, ground.Color.B);
        if (camera.Zoom < TuftZoom)
        {
            DrawHaze(spriteBatch, cellsX, cellsY, fromCellX, fromCellY, color);
            return;
        }

        var dark = new Color((int)(color.R * 0.72f), (int)(color.G * 0.78f), (int)(color.B * 0.72f));
        var light = Color.Lerp(color, Color.White, 0.25f);
        var mask = ground.BiomeMask;
        for (int tileY = fromTileY; tileY <= toTileY; tileY++)
        {
            for (int tileX = fromTileX; tileX <= toTileX; tileX++)
            {
                float wet = WetAt(tileX, tileY, fromCellX, fromCellY);
                if (wet <= 0.02f)
                {
                    continue;
                }

                byte biome = simulation.BiomeAt(tileX, tileY);
                if (biome >= mask.Count || !mask[biome])
                {
                    continue;
                }

                int px = tileX * TileSize;
                int py = tileY * TileSize;

                // Nádech: vlhká zem pod trsy, řídne s vlhkostí. Slabý, aby
                // zpevněná zem města pod ním zůstala čitelná.
                spriteBatch.Draw(_pixel, new Rectangle(px, py, TileSize, TileSize), color * (0.16f * wet));

                uint h = Hash(tileX, tileY);
                if ((h & 0xFFFF) / 65535f > wet * (float)ground.Density)
                {
                    continue;
                }

                // Trs trávy: tři stébla různé výšky nad tmavší patkou; poloha
                // a výška z hashe, takže stejné místo vypadá pořád stejně.
                int ox = 1 + (int)((h >> 16) % (uint)Math.Max(1, TileSize - 6));
                int oy = 3 + (int)((h >> 22) % (uint)Math.Max(1, TileSize - 8));
                int tall = 2 + (int)((h >> 28) & 3);
                spriteBatch.Draw(_pixel, new Rectangle(px + ox, py + oy + tall, 5, 1), dark);
                spriteBatch.Draw(_pixel, new Rectangle(px + ox, py + oy + 1, 1, tall), color);
                spriteBatch.Draw(_pixel, new Rectangle(px + ox + 2, py + oy, 1, tall + 1), light);
                spriteBatch.Draw(_pixel, new Rectangle(px + ox + 4, py + oy + 2, 1, tall - 1), color);
            }
        }
    }

    /// <summary>Z dálky: měkký nádech po buňkách, síla podle vlhkosti.</summary>
    private void DrawHaze(SpriteBatch spriteBatch, int cellsX, int cellsY, int fromCellX, int fromCellY, Color color)
    {
        int cellPixels = CellSize * TileSize;
        for (int cy = 0; cy < cellsY; cy++)
        {
            for (int cx = 0; cx < cellsX; cx++)
            {
                float wet = _wet[cy * cellsX + cx];
                if (wet <= 0f)
                {
                    continue;
                }

                spriteBatch.Draw(_pixel,
                    new Rectangle((fromCellX + cx) * cellPixels, (fromCellY + cy) * cellPixels, cellPixels, cellPixels),
                    color * (0.28f * wet));
            }
        }
    }

    /// <summary>Vlhkost dlaždice: bilineárně mezi středy čtyř nejbližších buněk.</summary>
    private float WetAt(int tileX, int tileY, int fromCellX, int fromCellY)
    {
        float gx = (tileX + 0.5f) / CellSize - 0.5f - fromCellX;
        float gy = (tileY + 0.5f) / CellSize - 0.5f - fromCellY;
        int x0 = (int)Math.Floor(gx);
        int y0 = (int)Math.Floor(gy);
        float fx = gx - x0;
        float fy = gy - y0;
        float a = Cell(x0, y0);
        float b = Cell(x0 + 1, y0);
        float c = Cell(x0, y0 + 1);
        float d = Cell(x0 + 1, y0 + 1);
        return (a * (1 - fx) + b * fx) * (1 - fy) + (c * (1 - fx) + d * fx) * fy;
    }

    private float Cell(int x, int y) =>
        x < 0 || y < 0 || x >= _cellsX || y >= _cellsY ? 0f : _wet[y * _cellsX + x];

    private static uint Hash(int x, int y)
    {
        uint h = (uint)(x * 374761393 + y * 668265263);
        h = (h ^ (h >> 13)) * 1274126177;
        return h ^ (h >> 16);
    }
}
