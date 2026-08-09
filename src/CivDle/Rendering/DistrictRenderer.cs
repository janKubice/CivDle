using CivDle.Core.Content;
using CivDle.Core.Sim;
using FontStashSharp;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CivDle.Rendering;

/// <summary>
/// Čtvrti na mapě: jemné zabarvení země pod shlukem, orámování a cedule se
/// jménem uprostřed.
///
/// <para>Proč to stojí za vlastní vrstvu: bez ní je oddálené město anonymní kaše
/// budov. S cedulemi se hráč dívá na <b>místa</b> — „támhle je průmyslová čtvrť,
/// tady rezidenční" — a to je přesně ten obraz civilizace rozprostřené po mapě,
/// kvůli kterému se v idle hře oddaluje.</para>
///
/// <para>Kreslí se pod budovy (je to barva země, ne přes ně) a jen ve výřezu
/// kamery. Čte jen ze simulace — render do ní nikdy nepíše (CLAUDE.md).</para>
/// </summary>
public sealed class DistrictRenderer
{
    private const int TileSize = TerrainRenderer.TileSize;

    /// <summary>Síla zabarvení země zblízka. Záměrně sotva znatelná — je to kulisa, ne UI.</summary>
    private const float FillAlpha = 0.13f;

    /// <summary>
    /// Síla zabarvení z výšky.
    ///
    /// <para>Zblízka se hráč dívá na domy a barevná plocha pod nimi má jen
    /// šeptat. Z výšky ale domy zmizí a zůstane hnědozelená kaše — a přesně
    /// tam se z barevných ploch čtvrtí stává <b>hlavní kresba</b>: mapa, na
    /// které je vidět, že tady je průmysl a tamhle se bydlí. Proto síla
    /// s oddálením roste, místo aby jako všechno ostatní klesala.</para>
    /// </summary>
    private const float FarFillAlpha = 0.42f;

    private const int Border = 2;

    /// <summary>Pod tímhle přiblížením se cedule nekreslí — text by byl stejně nečitelný.</summary>
    private const float MinLabelZoom = 0.5f;

    private readonly Texture2D _pixel;
    private readonly GameContent _content;
    private readonly Localization _loc;
    private readonly SpriteFontBase _font;

    public DistrictRenderer(Texture2D whitePixel, GameContent content, Localization loc, SpriteFontBase font)
    {
        _pixel = whitePixel;
        _content = content;
        _loc = loc;
        _font = font;
    }

    /// <summary>Vykreslí zabarvení a rámy čtvrtí (pod budovami).</summary>
    public void Draw(SpriteBatch spriteBatch, Camera2D camera, Simulation simulation)
    {
        var districts = simulation.Districts;
        if (districts.Count == 0)
        {
            return;
        }

        var (min, max) = camera.VisibleWorldBounds();
        float fill = FillFor(camera.Zoom);
        spriteBatch.Begin(samplerState: SamplerState.PointClamp, transformMatrix: camera.Transform);
        for (int i = 0; i < districts.Count; i++)
        {
            var district = districts[i];
            var (px, py, pw, ph) = PixelBounds(district);
            if (px + pw < min.X || px > max.X || py + ph < min.Y || py > max.Y)
            {
                continue;
            }

            var color = _content.Districts.Types[district.TypeIndex].MapColor.ToXna();
            spriteBatch.Draw(_pixel, new Rectangle(px, py, pw, ph), color * fill);
            spriteBatch.Draw(_pixel, new Rectangle(px, py, pw, Border), color * 0.5f);
            spriteBatch.Draw(_pixel, new Rectangle(px, py + ph - Border, pw, Border), color * 0.5f);
            spriteBatch.Draw(_pixel, new Rectangle(px, py, Border, ph), color * 0.5f);
            spriteBatch.Draw(_pixel, new Rectangle(px + pw - Border, py, Border, ph), color * 0.5f);
        }

        spriteBatch.End();
    }

    /// <summary>
    /// Cedule se jmény. Kreslí se zvlášť a nad budovami — jméno místa má být
    /// čitelné, i když je pod ním hustá zástavba.
    /// </summary>
    public void DrawLabels(SpriteBatch spriteBatch, Camera2D camera, Simulation simulation)
    {
        var districts = simulation.Districts;
        if (districts.Count == 0 || camera.Zoom < DetailLevel.Scale(MinLabelZoom))
        {
            return;
        }

        var (min, max) = camera.VisibleWorldBounds();

        spriteBatch.Begin();
        for (int i = 0; i < districts.Count; i++)
        {
            var district = districts[i];
            var (px, py, pw, _) = PixelBounds(district);
            if (px + pw < min.X || px > max.X || py < min.Y || py > max.Y)
            {
                continue;
            }

            string text = _loc.Format(
                "building.inDistrict",
                _loc[_content.Districts.Types[district.TypeIndex].NameKey],
                district.BuildingCount);

            var screen = camera.WorldToScreen(new Vector2(district.CenterX * TileSize, py));
            var size = _font.MeasureString(text);
            var at = new Vector2(screen.X - size.X * 0.5f, screen.Y - size.Y - 4);

            // Stín pod textem: jméno musí být čitelné nad světlým i tmavým terénem.
            spriteBatch.DrawString(_font, text, at + new Vector2(1f, 1f), Color.Black * 0.6f);
            spriteBatch.DrawString(_font, text, at, new Color(236, 228, 206));
        }

        spriteBatch.End();
    }

    /// <summary>
    /// Jak silně se čtvrť obarví při daném přiblížení. Plynule, ne skokem —
    /// zlom uprostřed otáčení kolečkem by byl vidět jako bliknutí.
    /// </summary>
    public static float FillFor(float zoom)
    {
        const float near = 1.0f;
        float t = Math.Clamp((near - zoom) / (near - CityScaleRenderer.ThresholdZoom), 0f, 1f);
        return FillAlpha + (FarFillAlpha - FillAlpha) * t;
    }

    private static (int X, int Y, int Width, int Height) PixelBounds(in District district) => (
        district.MinX * TileSize,
        district.MinY * TileSize,
        (district.MaxX - district.MinX + 1) * TileSize,
        (district.MaxY - district.MinY + 1) * TileSize);
}
