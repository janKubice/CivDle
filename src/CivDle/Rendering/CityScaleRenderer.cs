using CivDle.Core.Sim;
using CivDle.Core.World;
using FontStashSharp;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CivDle.Rendering;

/// <summary>
/// Agregátní pohled na měřítko města při velkém oddálení („wow" moment z
/// game-feel-wow.md): místo drobných jednotlivých budov nakreslí hustotu zástavby
/// v hrubé mřížce (teplá barva podle počtu) a velké číslo populace. Populace je
/// agregát (viz CLAUDE.md), ne miliony jednotlivců. Čistě render.
/// </summary>
public sealed class CityScaleRenderer
{
    /// <summary>Pod tímto zoomem se přepne z jednotlivých budov na agregátní hustotu.</summary>
    public const float ThresholdZoom = 0.5f;

    private const int CellTiles = 6; // hrubá mřížka hustoty
    private const int DenseCount = 10; // kolik budov v buňce = plná intenzita

    private readonly Texture2D _pixel;
    private readonly SpriteFontBase _font;
    private readonly Dictionary<long, int> _cells = new();

    public CityScaleRenderer(Texture2D whitePixel, SpriteFontBase font)
    {
        _pixel = whitePixel;
        _font = font;
    }

    public void Draw(SpriteBatch spriteBatch, Viewport viewport, Camera2D camera, Simulation simulation)
    {
        const int tileSize = TerrainRenderer.TileSize;
        int cellPixels = CellTiles * tileSize;

        // Nasčítej budovy do hrubých buněk (řídce — jen obsazené buňky).
        _cells.Clear();
        var buildings = simulation.Buildings;
        for (int i = 0; i < buildings.Length; i++)
        {
            int cellX = (int)MathF.Floor(buildings[i].X / (float)CellTiles);
            int cellY = (int)MathF.Floor(buildings[i].Y / (float)CellTiles);
            long key = TileKey.Pack(cellX, cellY);
            _cells[key] = _cells.TryGetValue(key, out int c) ? c + 1 : 1;
        }

        var dim = new Color(70, 90, 120);
        var hot = new Color(255, 210, 120);

        spriteBatch.Begin(samplerState: SamplerState.PointClamp, transformMatrix: camera.Transform);
        foreach (var (key, count) in _cells)
        {
            int cellX = TileKey.X(key);
            int cellY = TileKey.Y(key);
            float t = MathF.Min(1f, count / (float)DenseCount);
            var color = Color.Lerp(dim, hot, t) * (0.55f + 0.35f * t);
            spriteBatch.Draw(_pixel, new Rectangle(cellX * cellPixels, cellY * cellPixels, cellPixels - 1, cellPixels - 1), color);
        }

        spriteBatch.End();

        DrawPopulation(spriteBatch, viewport, simulation);
    }

    /// <summary>Velké číslo populace nahoře uprostřed — „koukni, jak je to velké".</summary>
    private void DrawPopulation(SpriteBatch spriteBatch, Viewport viewport, Simulation simulation)
    {
        string text = ((long)simulation.Population).ToString("N0");
        var size = _font.MeasureString(text);
        var position = new Vector2((viewport.Width - size.X) * 0.5f, viewport.Height * 0.16f);

        spriteBatch.Begin();
        spriteBatch.Draw(_pixel,
            new Rectangle((int)(position.X - 18), (int)(position.Y - 10), (int)size.X + 36, (int)size.Y + 20),
            new Color(12, 16, 22) * 0.7f);
        spriteBatch.DrawString(_font, text, position + new Vector2(2f, 2f), Color.Black * 0.6f);
        spriteBatch.DrawString(_font, text, position, new Color(255, 224, 168));
        spriteBatch.End();
    }
}
