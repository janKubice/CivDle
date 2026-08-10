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

    /// <param name="nightFactor">
    /// Hloubka noci 0–1. V noci se z hustoty stane <b>světelná mapa</b>: město
    /// není teplá skvrna na krajině, ale souhvězdí světel v tmavé zemi.
    /// Je to týž údaj nakreslený jinak — a je to ten obraz, kvůli kterému se
    /// v idle hře oddaluje.
    /// </param>
    public void Draw(
        SpriteBatch spriteBatch, Viewport viewport, Camera2D camera, Simulation simulation, float nightFactor = 0f)
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

        float night = Math.Clamp(nightFactor, 0f, 1f);

        // Ve dne obarvená plocha (kreslí se normálně), v noci aditivní světla
        // (svítí skrz tmu). Dva režimy, dvě různá míchání barev — a proto dva
        // Begin/End, ne jeden s podmínkou uvnitř.
        spriteBatch.Begin(samplerState: SamplerState.PointClamp, transformMatrix: camera.Transform);
        foreach (var (key, count) in _cells)
        {
            int cellX = TileKey.X(key);
            int cellY = TileKey.Y(key);
            float t = MathF.Min(1f, count / (float)DenseCount);
            var color = Color.Lerp(dim, hot, t) * ((0.55f + 0.35f * t) * (1f - night));
            spriteBatch.Draw(_pixel, new Rectangle(cellX * cellPixels, cellY * cellPixels, cellPixels - 1, cellPixels - 1), color);
        }

        spriteBatch.End();

        if (night > 0.02f)
        {
            DrawLightMap(spriteBatch, camera, cellPixels, night);
        }

        DrawPopulation(spriteBatch, viewport, simulation);
    }

    /// <summary>
    /// Noční světelná mapa: z každé obydlené buňky se stane světelný bod,
    /// jasný podle hustoty.
    ///
    /// <para>Dvě vrstvy na buňku — široká měkká zář a menší jasné jádro. Právě
    /// ten rozdíl dělá „světlo", zatímco jeden obdélník by byl jen světlejší
    /// čtverec. Kreslí se aditivně, takže se u hustého centra světla slévají
    /// do jedné záplavy přesně tak, jako to dělá skutečné město z letadla.</para>
    /// </summary>
    private void DrawLightMap(SpriteBatch spriteBatch, Camera2D camera, int cellPixels, float night)
    {
        var glow = new Color(255, 196, 118);
        var core = new Color(255, 232, 186);

        spriteBatch.Begin(
            blendState: BlendState.Additive,
            samplerState: SamplerState.PointClamp,
            transformMatrix: camera.Transform);

        foreach (var (key, count) in _cells)
        {
            int x = TileKey.X(key) * cellPixels;
            int y = TileKey.Y(key) * cellPixels;
            float t = MathF.Min(1f, count / (float)DenseCount);

            // Zář roste JEN s hustotou a nemá pevnou složku. Kdyby ji měla,
            // přispěla by stejně i buňka s jedním domem — a protože se aditivně
            // sčítají sousedi, slil by se z okraje města ostrý světlý obdélník
            // místo světel, která k okraji řídnou.
            spriteBatch.Draw(
                _pixel,
                new Rectangle(x - cellPixels / 4, y - cellPixels / 4, cellPixels + cellPixels / 2, cellPixels + cellPixels / 2),
                glow * (0.20f * t) * night);

            int inset = (int)(cellPixels * (0.36f - 0.24f * t));
            spriteBatch.Draw(
                _pixel,
                new Rectangle(x + inset, y + inset, cellPixels - 2 * inset, cellPixels - 2 * inset),
                core * (0.18f + 0.52f * t) * night);
        }

        spriteBatch.End();
    }

    /// <summary>Velké číslo populace nahoře uprostřed — „koukni, jak je to velké".</summary>
    private void DrawPopulation(SpriteBatch spriteBatch, Viewport viewport, Simulation simulation)
    {
        string text = CivDle.Core.Numbers.Format(simulation.Population);
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
