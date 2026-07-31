using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CivDle.Screens;

/// <summary>
/// Jeden graf: řada čísel v čase, vykreslená jako lomená čára s výplní pod ní.
///
/// <para>Proč vlastní kreslení a ne widget: graf je čistá geometrie z pole
/// <c>double</c> a nepotřebuje layout ani vstup. Takhle je to pár desítek řádků,
/// bez závislosti navíc (CLAUDE.md „no balast") — a hlavně se to dá ověřit
/// testem, protože měřítko a mapování bodů jsou obyčejné funkce.</para>
///
/// <para>Vrstva: nic nečte ze simulace; dostane hotová čísla a nakreslí je.</para>
/// </summary>
public sealed class LineChart
{
    private readonly Texture2D _pixel;

    public LineChart(Texture2D whitePixel)
    {
        _pixel = whitePixel;
    }

    /// <summary>
    /// Rozsah osy Y pro danou řadu. Vždycky začíná na nule (jinak by malé kolísání
    /// vypadalo jako dramatický propad) a nikdy není nulově vysoký, aby se nedělilo
    /// nulou u konstantní řady.
    /// </summary>
    public static (double Min, double Max) RangeOf(IReadOnlyList<double> values)
    {
        double max = 0;
        for (int i = 0; i < values.Count; i++)
        {
            max = Math.Max(max, values[i]);
        }

        return (0, max <= 0 ? 1 : max);
    }

    /// <summary>Kde v rámečku leží bod řady.</summary>
    public static Vector2 PointAt(
        IReadOnlyList<double> values, int index, Rectangle bounds, double min, double max)
    {
        float x = values.Count <= 1
            ? bounds.Left
            : bounds.Left + bounds.Width * index / (float)(values.Count - 1);
        double span = max - min;
        float normalized = span <= 0 ? 0f : (float)((values[index] - min) / span);
        return new Vector2(x, bounds.Bottom - bounds.Height * Math.Clamp(normalized, 0f, 1f));
    }

    /// <summary>
    /// Nakreslí graf do rámečku. Volající si otevře dávku sám — na jedné
    /// obrazovce jich je víc a otevírat dávku u každého by bylo plýtvání.
    /// </summary>
    public void Draw(SpriteBatch spriteBatch, Rectangle bounds, IReadOnlyList<double> values, Color color)
    {
        // Podklad a základní čára, ať je graf čitelný i prázdný.
        spriteBatch.Draw(_pixel, bounds, new Color(18, 22, 28));
        spriteBatch.Draw(_pixel, new Rectangle(bounds.Left, bounds.Bottom - 1, bounds.Width, 1), new Color(70, 78, 88));

        // Vodicí linky po čtvrtinách — bez nich se z křivky nedá odhadnout výška.
        for (int i = 1; i < 4; i++)
        {
            int y = bounds.Bottom - bounds.Height * i / 4;
            spriteBatch.Draw(_pixel, new Rectangle(bounds.Left, y, bounds.Width, 1), new Color(44, 50, 58));
        }

        if (values.Count == 0)
        {
            return;
        }

        var (min, max) = RangeOf(values);

        // Jediný bod nemá co spojovat — nakresli aspoň tečku, ať graf nemlčí.
        if (values.Count == 1)
        {
            var only = PointAt(values, 0, bounds, min, max);
            spriteBatch.Draw(_pixel, new Rectangle((int)only.X, (int)only.Y - 1, 3, 3), color);
            return;
        }

        var previous = PointAt(values, 0, bounds, min, max);
        for (int i = 1; i < values.Count; i++)
        {
            var point = PointAt(values, i, bounds, min, max);

            // Výplň pod čarou: svislý sloupek k základně. Levnější než trojúhelníky
            // a na téhle velikosti k nerozeznání.
            int columnLeft = (int)previous.X;
            int columnWidth = Math.Max(1, (int)point.X - columnLeft);
            int top = (int)Math.Min(previous.Y, point.Y);
            spriteBatch.Draw(
                _pixel,
                new Rectangle(columnLeft, top, columnWidth, Math.Max(1, bounds.Bottom - top)),
                color * 0.18f);

            DrawSegment(spriteBatch, previous, point, color);
            previous = point;
        }
    }

    /// <summary>Úsečka po pixelech — na desítky bodů je to levnější než rotovaný sprite.</summary>
    private void DrawSegment(SpriteBatch spriteBatch, Vector2 from, Vector2 to, Color color)
    {
        int steps = Math.Max(1, (int)Math.Abs(to.X - from.X));
        for (int i = 0; i <= steps; i++)
        {
            float t = i / (float)steps;
            var point = Vector2.Lerp(from, to, t);
            spriteBatch.Draw(_pixel, new Rectangle((int)point.X, (int)point.Y - 1, 2, 2), color);
        }
    }
}
