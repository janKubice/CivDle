using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CivDle.Rendering;

/// <summary>
/// Textura mraků: pár vrstev hodnotového šumu přes sebe a na to práh, aby
/// vznikly ostrůvky zataženo a mezi nimi jasno.
///
/// <para><b>Proč to má vlastní třídu:</b> mraky jsou ve hře dvakrát — jednou
/// jako stíny plující po zemi, podruhé jako vrstva nad městem. Kdyby si každá
/// generovala svou, rozešly by se po první změně a hráč by viděl stín, který
/// nepatří k žádnému mraku nad ním.</para>
///
/// <para>Textura se dlaždicově opakuje: vzorkuje se po celých násobcích
/// velikosti, takže levý okraj navazuje na pravý a horní na dolní. V pohybu si
/// oko opakování nevšimne, takže nemusí být velká.</para>
///
/// <para>Vrstva: čistý render, bez zařízení kromě posledního kroku.</para>
/// </summary>
public static class CloudNoise
{
    /// <summary>
    /// Vyrobí texturu mraků.
    /// </summary>
    /// <param name="size">Hrana textury v pixelech. Musí být dělitelná 16.</param>
    /// <param name="threshold">
    /// Od jaké hustoty šumu začíná mrak (0–1). Vyšší práh = řidší, oddělenější
    /// ostrůvky; bez prahu je to rovnoměrná šeď, ne mraky.
    /// </param>
    /// <param name="softness">Jak široký je přechod z jasna do zataženo.</param>
    /// <param name="seed">Rozhoduje o tvaru. Dvě vrstvy se stejným seedem by ležely v zákrytu.</param>
    /// <param name="asShade">
    /// <c>true</c> = pro stíny: bílá znamená „sem svítí slunce", černá „tady je
    /// stín", a kreslí se násobením, takže bílá scénu nechá být.
    /// <c>false</c> = pro vrstvu nad městem: bílá v RGB, hustota v alfě, takže
    /// se dá kreslit běžným mícháním.
    /// </param>
    public static Texture2D Build(
        GraphicsDevice device, int size, float threshold, float softness, int seed, bool asShade)
    {
        var pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                pixels[y * size + x] = Texel(DensityAt(x, y, size, threshold, softness, seed), asShade);
            }
        }

        var texture = new Texture2D(device, size, size);
        texture.SetData(pixels);
        return texture;
    }

    /// <summary>
    /// Jeden texel textury mraků.
    ///
    /// <para><b>Na tomhle rozdílu záleží víc, než vypadá.</b> Verze pro stíny
    /// nese hustotu v <i>barvě</i>, takže se dá kreslit jedině násobením — a
    /// násobení počítá <c>cíl × zdroj</c>, takže hustý mrak (černá) vynásobí
    /// scénu nulou a udělá díru do černa. Přesně tak to taky vypadalo. Verze
    /// pro míchání přes alfu nese hustotu v <i>alfě</i> a bílou v RGB, takže
    /// výsledek je <c>cíl × (1 − α) + stín × α</c> — řízené ztmavení, jehož
    /// sílu určuje volající.</para>
    ///
    /// <para>Veřejné schválně: je to celý ten rozdíl a dá se ověřit bez
    /// grafického zařízení.</para>
    /// </summary>
    public static Color Texel(float density, bool asShade) => asShade
        ? new Color(1f - density, 1f - density, 1f - density)
        : new Color(1f, 1f, 1f, density);

    /// <summary>
    /// Hustota mraku na daném místě (0 = jasno, 1 = zataženo).
    ///
    /// <para>Veřejná schválně: je to celý tvar mraků a jde ověřit bez
    /// grafického zařízení — hlavně to, že textura navazuje po hranách.
    /// Bez toho by se přes oblohu táhl pravidelný šev a v pohybu by putoval
    /// přes obraz.</para>
    /// </summary>
    public static float DensityAt(int x, int y, int size, float threshold, float softness, int seed)
    {
        // Tři oktávy: hrubý tvar, členitost, okraje.
        float value =
            Octave(x, y, 4, size, seed) * 0.55f +
            Octave(x, y, 8, size, seed) * 0.30f +
            Octave(x, y, 16, size, seed) * 0.15f;

        return Smooth(Math.Clamp((value - threshold) / softness, 0f, 1f));
    }

    /// <summary>Jedna vrstva hodnotového šumu, která se po hraně textury opakuje.</summary>
    private static float Octave(int x, int y, int cells, int size, int seed)
    {
        int cell = size / cells;
        int cx = x / cell;
        int cy = y / cell;
        float fx = Smooth((x % cell) / (float)cell);
        float fy = Smooth((y % cell) / (float)cell);

        float a = Corner(cx, cy, cells, seed);
        float b = Corner(cx + 1, cy, cells, seed);
        float c = Corner(cx, cy + 1, cells, seed);
        float d = Corner(cx + 1, cy + 1, cells, seed);

        return MathHelper.Lerp(MathHelper.Lerp(a, b, fx), MathHelper.Lerp(c, d, fx), fy);
    }

    /// <summary>
    /// Hodnota v rohu buňky. Souřadnice se zbytkem po dělení zabalí dokola,
    /// takže levý okraj textury sedí na pravý a nahoře je totéž co dole.
    /// </summary>
    private static float Corner(int cx, int cy, int cells, int seed)
    {
        int wrappedX = ((cx % cells) + cells) % cells;
        int wrappedY = ((cy % cells) + cells) % cells;

        unchecked
        {
            uint h = (uint)(wrappedX * 374761393 + wrappedY * 668265263 + cells * 1442695041 + seed * 2654435761);
            h = (h ^ (h >> 13)) * 1274126177;
            return ((h ^ (h >> 16)) & 0xFFFF) / 65535f;
        }
    }

    private static float Smooth(float t) => t * t * (3f - 2f * t);
}
