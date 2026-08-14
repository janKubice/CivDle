using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CivDle.Rendering;

/// <summary>
/// Měkká skvrna, kterou se kreslí stíny. Jedna textura, vyrobená při startu.
///
/// <para>Proč vlastní textura a ne obdélník z bílého pixelu: právě tvrdá hrana
/// dělala z původních stínů krabice. Barevný přechod k okraji je to jediné, co
/// z tmavé plochy udělá stín — a s jednou texturou to stojí stejně jako
/// obdélník, tedy jedno kreslení na budovu.</para>
///
/// <para>Textura je bílá s klesající průhledností; barvu i sílu určuje až tint
/// při kreslení, takže tatáž skvrna slouží stínu i čemukoli dalšímu, co bude
/// potřebovat měkký flek.</para>
///
/// <para>Vrstva: čistý render. Vlastní GPU zdroj, proto <see cref="IDisposable"/>.</para>
/// </summary>
public sealed class SoftShadow : IDisposable
{
    /// <summary>
    /// Rozlišení textury. Kreslí se roztažená na pár desítek pixelů, takže
    /// větší by nic nepřidalo — jen by zabrala paměť.
    /// </summary>
    private const int Size = 64;

    private readonly Texture2D _texture;

    public SoftShadow(GraphicsDevice device)
    {
        _texture = new Texture2D(device, Size, Size);
        _texture.SetData(BuildFalloff());
    }

    /// <summary>
    /// Vykreslí skvrnu do daného obdélníku.
    /// </summary>
    public void Draw(SpriteBatch spriteBatch, Rectangle destination, Color color) =>
        spriteBatch.Draw(_texture, destination, color);

    /// <summary>
    /// Kruhový spád průhlednosti od středu k okraji.
    ///
    /// <para>Spád je kvadratický, ne lineární: lineární má ještě u okraje vidět
    /// hranu kruhu, kvadratický se ztratí do pozadí. Data jsou předsobená
    /// (premultiplied), protože v tom režimu kreslí <see cref="SpriteBatch"/>
    /// ve výchozím nastavení — jinak by okraj svítil.</para>
    /// </summary>
    private static Color[] BuildFalloff()
    {
        var data = new Color[Size * Size];
        const float radius = Size * 0.5f;

        for (int y = 0; y < Size; y++)
        {
            for (int x = 0; x < Size; x++)
            {
                float dx = (x + 0.5f - radius) / radius;
                float dy = (y + 0.5f - radius) / radius;
                float distance = MathF.Sqrt(dx * dx + dy * dy);

                float alpha = Math.Clamp(1f - distance, 0f, 1f);
                alpha *= alpha;

                data[y * Size + x] = Color.White * alpha;
            }
        }

        return data;
    }

    public void Dispose() => _texture.Dispose();
}
