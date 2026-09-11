using CivDle.Core.Sim;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CivDle.Rendering;

/// <summary>
/// Kreslení jednoho snímku časosběru: terén a nad ním zástavba, jak vypadala
/// v dané chvíli.
///
/// <para>Proč to nesedí uvnitř obrazovky přehrávače: totéž chce i načítací
/// obrazovka, která místo prázdného pozadí ukazuje, jak město rostlo. Kdyby si
/// každá kreslila svoje, rozešly by se — a přehrávka v načítání by po pár
/// změnách vypadala jinak než přehrávka v menu.</para>
///
/// <para>Buňka kroniky je čtverec 8×8 dlaždic a kreslí se jako <b>domek</b>, ne
/// jako čtvereček: podezdívka, tělo, tmavší střecha, světlý hřeben. Pár
/// obdélníků navíc, ale právě ony dělají rozdíl mezi mapou obsazených polí
/// a městem.</para>
///
/// <para>Vrstva: čistý render. Z kroniky jen čte.</para>
/// </summary>
public sealed class HistoryPlayback : IDisposable
{
    private readonly TerrainRenderer _terrain;
    private readonly Texture2D _pixel;
    private readonly Color _roadColor;

    /// <param name="roadColor">
    /// Barva silnice z dat. Kronika si na buňku nemůže dovolit víc než jeden
    /// bajt, takže se silnice od zástavby pozná právě a jen podle barvy.
    /// </param>
    public HistoryPlayback(GraphicsDevice device, Core.Content.BiomeRegistry biomes, long seed, Texture2D whitePixel, Color roadColor)
    {
        _terrain = new TerrainRenderer(device, biomes, seed);
        _pixel = whitePixel;
        _roadColor = roadColor;
    }

    /// <summary>Vykreslí terén a nad ním zástavbu ve snímku <paramref name="frame"/>.</summary>
    public void Draw(
        SpriteBatch spriteBatch,
        Camera2D camera,
        Core.World.ITerrain terrain,
        CityHistory history,
        int frame)
    {
        _terrain.Draw(spriteBatch, camera, terrain);
        DrawCells(spriteBatch, camera, history, frame);
    }

    /// <summary>
    /// Srovná kameru tak, aby bylo celé město v záběru. Bez toho by přehrávka
    /// začínala pohledem někam do prázdna.
    /// </summary>
    public static void FrameCity(Camera2D camera, CityHistory history, int viewportWidth, int viewportHeight)
    {
        camera.SetViewport(viewportWidth, viewportHeight);
        if (history.Count == 0)
        {
            return;
        }

        const int cellWorld = CityHistory.TilesPerCell * TerrainRenderer.TileSize;
        int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
        int last = history.Count - 1;
        for (int cy = 0; cy < CityHistory.GridSize; cy++)
        {
            for (int cx = 0; cx < CityHistory.GridSize; cx++)
            {
                if (!history.IsOccupied(last, cx, cy))
                {
                    continue;
                }

                minX = Math.Min(minX, cx);
                minY = Math.Min(minY, cy);
                maxX = Math.Max(maxX, cx);
                maxY = Math.Max(maxY, cy);
            }
        }

        if (minX > maxX)
        {
            return; // prázdná kronika — kamera zůstane, kde byla
        }

        int half = CityHistory.GridSize / 2;
        float centerX = ((minX + maxX + 1) * 0.5f - half) * cellWorld;
        float centerY = ((minY + maxY + 1) * 0.5f - half) * cellWorld;
        camera.Position = new Vector2(centerX, centerY);

        // Měřítko podle delší strany, s rezervou — město nemá lepit na okraj.
        float spanX = (maxX - minX + 1) * cellWorld * 1.25f;
        float spanY = (maxY - minY + 1) * cellWorld * 1.25f;
        camera.SetZoom(Math.Min(viewportWidth / Math.Max(1f, spanX), viewportHeight / Math.Max(1f, spanY)));
    }

    public void Dispose() => _terrain.Dispose();

    private void DrawCells(SpriteBatch spriteBatch, Camera2D camera, CityHistory history, int frame)
    {
        if (history.Count == 0)
        {
            return;
        }

        int index = Math.Clamp(frame, 0, history.Count - 1);
        int previous = Math.Max(0, index - 1);
        const int cellWorld = CityHistory.TilesPerCell * TerrainRenderer.TileSize;

        spriteBatch.Begin(samplerState: SamplerState.PointClamp, transformMatrix: camera.Transform);
        for (int cy = 0; cy < CityHistory.GridSize; cy++)
        {
            for (int cx = 0; cx < CityHistory.GridSize; cx++)
            {
                if (history.ColorAt(index, cx, cy) is not { } color)
                {
                    continue;
                }

                int worldX = (cx - CityHistory.GridSize / 2) * cellWorld;
                int worldY = (cy - CityHistory.GridSize / 2) * cellWorld;
                var tint = color.ToXna();

                // Silnice zůstává plochá — je to povrch, ne stavba.
                if (tint == _roadColor)
                {
                    spriteBatch.Draw(_pixel, new Rectangle(worldX, worldY, cellWorld, cellWorld), tint * 0.95f);
                }
                else
                {
                    DrawHouse(spriteBatch, worldX, worldY, cellWorld, tint);
                }

                DrawEdges(spriteBatch, history, index, cx, cy, worldX, worldY, cellWorld);

                if (!history.IsOccupied(previous, cx, cy))
                {
                    spriteBatch.Draw(
                        _pixel, new Rectangle(worldX, worldY, cellWorld, cellWorld),
                        Color.White * 0.35f); // novostavba zazáří
                }
            }
        }

        spriteBatch.End();
    }

    private void DrawHouse(SpriteBatch spriteBatch, int x, int y, int size, Color tint)
    {
        int inset = Math.Max(1, size / 8);
        int roof = Math.Max(1, size / 3);

        // Stín pod stavbou — bez něj domky splývají s terénem.
        spriteBatch.Draw(_pixel, new Rectangle(x + inset, y + size - inset, size - inset, inset), Color.Black * 0.3f);
        spriteBatch.Draw(_pixel, new Rectangle(x, y + roof, size, size - roof), tint);

        // Střecha: tmavší odstín téže barvy, ať je poznat, čí ten dům je.
        spriteBatch.Draw(_pixel, new Rectangle(x, y, size, roof), Color.Lerp(tint, Color.Black, 0.35f));

        // Hřeben — jedna světlá linka, ze které vznikne dojem sklonu.
        spriteBatch.Draw(_pixel, new Rectangle(x, y, size, Math.Max(1, roof / 3)), Color.Lerp(tint, Color.White, 0.25f));
    }

    private void DrawEdges(
        SpriteBatch spriteBatch, CityHistory history, int frame, int cx, int cy,
        int worldX, int worldY, int cellWorld)
    {
        var edge = Color.Black * 0.45f;
        const int thickness = 2;

        if (!history.IsOccupied(frame, cx, cy - 1))
        {
            spriteBatch.Draw(_pixel, new Rectangle(worldX, worldY, cellWorld, thickness), edge);
        }

        if (!history.IsOccupied(frame, cx, cy + 1))
        {
            spriteBatch.Draw(_pixel, new Rectangle(worldX, worldY + cellWorld - thickness, cellWorld, thickness), edge);
        }

        if (!history.IsOccupied(frame, cx - 1, cy))
        {
            spriteBatch.Draw(_pixel, new Rectangle(worldX, worldY, thickness, cellWorld), edge);
        }

        if (!history.IsOccupied(frame, cx + 1, cy))
        {
            spriteBatch.Draw(_pixel, new Rectangle(worldX + cellWorld - thickness, worldY, thickness, cellWorld), edge);
        }
    }
}
