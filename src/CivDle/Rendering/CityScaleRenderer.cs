using CivDle.Core.Sim;
using FontStashSharp;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CivDle.Rendering;

/// <summary>
/// Agregátní pohled na měřítko města při velkém oddálení („wow" moment
/// z game-feel-wow.md): místo drobných jednotlivých budov hustota zástavby
/// a velké číslo populace. Populace je agregát (viz CLAUDE.md), ne miliony
/// jednotlivců.
///
/// <para>Samotnou hustotu kreslí <see cref="DensityMap"/> — upečená do textur,
/// jeden draw call na kus mapy. Tahle třída drží to ostatní: kdy se přepnout
/// a co k tomu napsat. Dřív dělala obojí a při velkoměstě to znamenalo tisíce
/// obdélníčků a přestavěný slovník každý snímek.</para>
/// </summary>
public sealed class CityScaleRenderer : IDisposable
{
    /// <summary>Pod tímto zoomem se přepne z jednotlivých budov na agregátní hustotu.</summary>
    public const float ThresholdZoom = 0.5f;

    private readonly Texture2D _pixel;
    private readonly SpriteFontBase _font;
    private readonly DensityMap _density;

    public CityScaleRenderer(Texture2D whitePixel, SpriteFontBase font, GraphicsDevice device)
    {
        _pixel = whitePixel;
        _font = font;
        _density = new DensityMap(device);
    }

    /// <summary>Upečená mapa hustoty — kvůli diagnostice a testům.</summary>
    public DensityMap Density => _density;

    /// <param name="nightFactor">
    /// Hloubka noci 0–1. V noci se z hustoty stane <b>světelná mapa</b>: město
    /// není teplá skvrna na krajině, ale souhvězdí světel v tmavé zemi.
    /// Je to týž údaj nakreslený jinak — a je to ten obraz, kvůli kterému se
    /// v idle hře oddaluje.
    /// </param>
    public void Draw(
        SpriteBatch spriteBatch, Viewport viewport, Camera2D camera, Simulation simulation, float nightFactor = 0f)
    {
        _density.Draw(spriteBatch, camera, simulation, nightFactor);
        DrawPopulation(spriteBatch, viewport, simulation);
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

    public void Dispose() => _density.Dispose();
}
