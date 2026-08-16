using FontStashSharp;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Myra.Graphics2D.UI.Styles;

namespace CivDle.Capture;

/// <summary>
/// Plátno záběru: rozměry, písmo a pár tahů, které potřebuje každý titulek
/// (ztmavení, gradient, vystředěný text, prolnutí do černé).
///
/// <para>Skládá se v <b>návrhových pixelech</b> nad 1920×1080 a přepočítává se
/// na skutečný rozměr. Bez toho by náhled v půlce rozlišení měl poloviční text
/// vůči všemu ostatnímu a nedalo by se podle něj nic posoudit.</para>
///
/// <para>Písmo je herní (Myra), ne vlastní: trailer má vypadat jako hra. Kreslí
/// se přes <see cref="SamplerState.PointClamp"/> — zvětšený pixelový font má být
/// ostrý a hranatý, ne rozmazaný.</para>
/// </summary>
internal sealed class TrailerCanvas
{
    /// <summary>Rozlišení, ve kterém jsou psané všechny rozměry záběrů.</summary>
    public const float DesignWidth = 1920f;
    public const float DesignHeight = 1080f;

    private readonly SpriteBatch _spriteBatch;
    private readonly Texture2D _pixel;

    public TrailerCanvas(SpriteBatch spriteBatch, Texture2D pixel, int width, int height)
    {
        _spriteBatch = spriteBatch;
        _pixel = pixel;
        Width = width;
        Height = height;
        Scale = height / DesignHeight;
    }

    public int Width { get; }

    public int Height { get; }

    /// <summary>Násobič z návrhových pixelů na skutečné.</summary>
    public float Scale { get; }

    /// <summary>Herní písmo.</summary>
    public SpriteFontBase Font => Stylesheet.Current.LabelStyle.Font;

    /// <summary>Návrhový rozměr → skutečný.</summary>
    public float Px(float design) => design * Scale;

    /// <summary>Otevře kreslení s ostrým (bodovým) filtrem — pixel art se nemá rozmazávat.</summary>
    public void Begin() => _spriteBatch.Begin(samplerState: SamplerState.PointClamp);

    public void End() => _spriteBatch.End();

    /// <summary>Vyplní obdélník (v návrhových pixelech).</summary>
    public void Fill(float x, float y, float width, float height, Color color) =>
        _spriteBatch.Draw(
            _pixel,
            new Rectangle(
                (int)MathF.Round(Px(x)), (int)MathF.Round(Px(y)),
                Math.Max(1, (int)MathF.Round(Px(width))), Math.Max(1, (int)MathF.Round(Px(height)))),
            color);

    /// <summary>
    /// Svislý přechod přes celé plátno. Kreslí se po pruzích — jeden obdélník
    /// s alfa by dal plochu, ne přechod, a ostrá hrana je na plátně vidět víc
    /// než samotné ztmavení.
    /// </summary>
    public void VerticalGradient(Color top, Color bottom)
    {
        const int bands = 96;
        float band = DesignHeight / bands;
        for (int i = 0; i < bands; i++)
        {
            Fill(0, i * band, DesignWidth, band + 1, Color.Lerp(top, bottom, i / (float)(bands - 1)));
        }
    }

    /// <summary>Ztmavení k okrajům — drží oko uprostřed obrazu.</summary>
    public void Vignette(float strength)
    {
        const int steps = 48;
        float thickness = DesignHeight / 4f;
        for (int i = 0; i < steps; i++)
        {
            float t = 1f - (i / (float)steps);
            var shade = new Color(6, 8, 12) * (strength * t * t);
            float band = thickness / steps;

            Fill(0, i * band, DesignWidth, band + 1, shade);
            Fill(0, DesignHeight - (i + 1) * band, DesignWidth, band + 1, shade);
            Fill(i * band, 0, band + 1, DesignHeight, shade);
            Fill(DesignWidth - (i + 1) * band, 0, band + 1, DesignHeight, shade);
        }
    }

    /// <summary>Černá přes celý obraz — prolnutí na začátku a konci záběru.</summary>
    public void FadeToBlack(float amount)
    {
        if (amount > 0.001f)
        {
            Fill(0, 0, DesignWidth, DesignHeight, Color.Black * Math.Clamp(amount, 0f, 1f));
        }
    }

    /// <summary>Šířka textu v návrhových pixelech při daném zvětšení.</summary>
    public float MeasureWidth(string text, float fontScale) => Font.MeasureString(text).X * fontScale;

    /// <summary>
    /// Zvětšení písma tak, aby se text vešel do dané šířky. Titulek se nesmí
    /// rozjet přes okraj jen proto, že v datech přibyla stovka technologií.
    /// </summary>
    public float FitScale(string text, float maxWidth, float preferred)
    {
        float natural = Font.MeasureString(text).X;
        return natural <= 0 ? preferred : Math.Min(preferred, maxWidth / natural);
    }

    /// <summary>Text vystředěný na dané ose X (souřadnice v návrhových pixelech).</summary>
    public void DrawCentered(string text, float centerX, float top, float fontScale, Color color) =>
        DrawText(text, centerX - MeasureWidth(text, fontScale) / 2f, top, fontScale, color);

    /// <summary>
    /// Text v návrhových pixelech. <paramref name="rightAligned"/> zarovná konec
    /// textu na <paramref name="x"/> — to potřebuje počítadlo, kterému během
    /// natáčení přibývají číslice a jinak by celý řádek poskakoval.
    /// </summary>
    public void DrawText(string text, float x, float y, float fontScale, Color color, bool rightAligned = false)
    {
        float left = rightAligned ? x - MeasureWidth(text, fontScale) : x;
        float pixels = fontScale * Scale;
        _spriteBatch.DrawString(
            Font, text, new Vector2(Px(left), Px(y)), color, scale: new Vector2(pixels, pixels));
    }

    /// <summary>Sprite v návrhových pixelech, vystředěný na zadaném bodě.</summary>
    public void DrawSprite(Texture2D texture, Vector2 center, float size, Color tint)
    {
        float side = Px(size);
        _spriteBatch.Draw(
            texture,
            new Rectangle(
                (int)MathF.Round(Px(center.X) - side / 2f),
                (int)MathF.Round(Px(center.Y) - side / 2f),
                (int)MathF.Round(side),
                (int)MathF.Round(side)),
            tint);
    }
}
