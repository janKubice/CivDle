using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CivDle.Rendering.Sprites;

/// <summary>
/// Maličké kreslicí plátno pro procedurální sprity — dokud nejsou skutečné assety,
/// generují se ikonky a objekty přímo v kódu (kód = jak). Alfa se míchá, takže
/// tvary jdou vrstvit. <see cref="ToTexture"/> vyrobí hotovou <see cref="Texture2D"/>.
/// </summary>
public sealed class PixelCanvas
{
    private readonly Color[] _pixels;

    public PixelCanvas(int width, int height)
    {
        Width = width;
        Height = height;
        _pixels = new Color[width * height];
    }

    public int Width { get; }

    public int Height { get; }

    /// <summary>Nakreslí pixel s alfa blendingem přes stávající obsah.</summary>
    public void Blend(int x, int y, Color color)
    {
        if (x < 0 || x >= Width || y < 0 || y >= Height || color.A == 0)
        {
            return;
        }

        int i = y * Width + x;
        if (color.A == 255)
        {
            _pixels[i] = color;
            return;
        }

        float a = color.A / 255f;
        var dst = _pixels[i];
        float inv = 1f - a;
        _pixels[i] = new Color(
            (int)(color.R * a + dst.R * inv),
            (int)(color.G * a + dst.G * inv),
            (int)(color.B * a + dst.B * inv),
            (int)(color.A + dst.A * inv));
    }

    public void FillRect(int x, int y, int w, int h, Color color)
    {
        for (int yy = y; yy < y + h; yy++)
        {
            for (int xx = x; xx < x + w; xx++)
            {
                Blend(xx, yy, color);
            }
        }
    }

    /// <summary>Vyplněný kruh se středem (cx, cy) a poloměrem r.</summary>
    public void FillCircle(float cx, float cy, float r, Color color)
    {
        int minX = (int)MathF.Floor(cx - r), maxX = (int)MathF.Ceiling(cx + r);
        int minY = (int)MathF.Floor(cy - r), maxY = (int)MathF.Ceiling(cy + r);
        float r2 = r * r;
        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                float dx = x + 0.5f - cx, dy = y + 0.5f - cy;
                if (dx * dx + dy * dy <= r2)
                {
                    Blend(x, y, color);
                }
            }
        }
    }

    /// <summary>Rovnoramenný trojúhelník (např. střecha) mezi vrcholem a základnou.</summary>
    public void FillTriangle(float ax, float ay, float bx, float by, float cx, float cy, Color color)
    {
        int minX = (int)MathF.Floor(Math.Min(ax, Math.Min(bx, cx)));
        int maxX = (int)MathF.Ceiling(Math.Max(ax, Math.Max(bx, cx)));
        int minY = (int)MathF.Floor(Math.Min(ay, Math.Min(by, cy)));
        int maxY = (int)MathF.Ceiling(Math.Max(ay, Math.Max(by, cy)));
        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                float px = x + 0.5f, py = y + 0.5f;
                if (SameSide(px, py, ax, ay, bx, by, cx, cy)
                    && SameSide(px, py, bx, by, cx, cy, ax, ay)
                    && SameSide(px, py, cx, cy, ax, ay, bx, by))
                {
                    Blend(x, y, color);
                }
            }
        }
    }

    public Texture2D ToTexture(GraphicsDevice device)
    {
        var texture = new Texture2D(device, Width, Height);
        texture.SetData(_pixels);
        return texture;
    }

    /// <summary>
    /// Srovná hotovou kresbu na společnou paletu hry (<see cref="GamePalette"/>).
    ///
    /// <para>Dělá se to až <b>po</b> kreslení, ne během něj: sprity si můžou
    /// dál míchat barvy a stínovat průhledností, jak potřebují, a teprve
    /// výsledek se srovná. Kdyby se snapovalo při každém <see cref="Blend"/>,
    /// každá poloprůhledná vrstva by kresbu posunula o kus jinam a měkké
    /// přechody by se rozpadly.</para>
    /// </summary>
    public void SnapToPalette()
    {
        for (int i = 0; i < _pixels.Length; i++)
        {
            _pixels[i] = GamePalette.Snap(_pixels[i]);
        }
    }

    private static bool SameSide(float px, float py, float ax, float ay, float bx, float by, float refx, float refy)
    {
        float cross1 = (bx - ax) * (py - ay) - (by - ay) * (px - ax);
        float cross2 = (bx - ax) * (refy - ay) - (by - ay) * (refx - ax);
        return cross1 * cross2 >= 0;
    }
}
