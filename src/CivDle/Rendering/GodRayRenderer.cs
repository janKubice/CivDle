using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CivDle.Rendering;

/// <summary>
/// Sluneční paprsky za svítání a soumraku.
///
/// <para><b>Proč právě za rozbřesku:</b> nízké slunce prosvítá mezerami
/// v mracích a kreslí do vzduchu pruhy. Je to nejnápadnější věc, kterou umí
/// světlo udělat, a přitom se odehraje jen dvakrát za den — což je přesně to,
/// co scéna potřebovala: chvíli, kdy je nejhezčí, a kvůli které se vyplatí se
/// na město podívat i když se v něm nic neděje.</para>
///
/// <para><b>Jak bez shaderu:</b> paprsek je jeden natažený obdélník s měkkým
/// příčným přechodem. Přechod nese textura o šířce jednoho pixelu —
/// průhledná, uprostřed bílá, zase průhledná — natažená do délky. Kreslí se
/// aditivně, protože světlo se <b>přičítá</b>; průhledným překryvem by
/// vznikla mléčná fólie přes obraz.</para>
///
/// <para>Kreslí se až za složením scény: paprsek letí vzduchem mezi kamerou
/// a městem, takže ho nemá co zastínit.</para>
///
/// <para>Vrstva: čistý render. Čte jen denní dobu.</para>
/// </summary>
public sealed class GodRayRenderer : IDisposable
{
    /// <summary>Kolik paprsků. Pár jich má být vidět jednotlivě, ne jako mříž.</summary>
    private const int RayCount = 6;

    /// <summary>Nejvyšší jas jednoho paprsku. Nad tím se z rozbřesku stane přesvit.</summary>
    private const float MaxIntensity = 0.16f;

    /// <summary>Sklon paprsků v radiánech — nízké slunce svítí šikmo, ne kolmo.</summary>
    private const float Tilt = 0.42f;

    /// <summary>Šířka přechodové textury. Čím víc, tím měkčí okraj.</summary>
    private const int GradientHeight = 64;

    private readonly Texture2D _beam;
    private float _time;

    public GodRayRenderer(GraphicsDevice device) => _beam = BuildBeam(device);

    public void Update(float dt) => _time += dt;

    /// <summary>
    /// Přetáhne přes scénu paprsky, jestli je zrovna zlatá hodina.
    /// </summary>
    /// <param name="dusk">Síla zlatavého světla 0–1 (<see cref="DayNightCycle.DuskFactor"/>).</param>
    /// <param name="light">Barva denního světla — paprsky mají barvu toho, co je vyrobilo.</param>
    public void Draw(SpriteBatch spriteBatch, Viewport viewport, float dusk, Color light)
    {
        if (dusk <= 0.02f)
        {
            return;
        }

        // Paprsek musí být delší než úhlopříčka, jinak by při naklonění
        // nedosáhl do rohů a v obraze by končil ve vzduchu.
        int length = (int)(MathF.Sqrt(
            viewport.Width * (float)viewport.Width + viewport.Height * (float)viewport.Height) * 1.4f);
        int spacing = Math.Max(1, viewport.Width / RayCount);

        spriteBatch.Begin(blendState: BlendState.Additive, samplerState: SamplerState.LinearClamp);
        for (int i = 0; i < RayCount; i++)
        {
            // Každý paprsek jinak široký a jinak jasný, jinak z toho je mříž.
            float wobble = MathF.Sin(_time * 0.23f + i * 1.7f);
            int width = (int)(spacing * (0.28f + 0.16f * (i % 3)));

            // Pomalé plutí napříč: mraky nad sluncem se hýbou, takže se hýbou
            // i mezery mezi nimi.
            int x = (int)(i * spacing + wobble * spacing * 0.35f) - length / 3;
            float intensity = MaxIntensity * dusk * (0.55f + 0.45f * MathF.Cos(_time * 0.31f + i));

            spriteBatch.Draw(
                _beam,
                new Rectangle(x, -length / 4, length, Math.Max(1, width)),
                null,
                light * intensity,
                Tilt,
                Vector2.Zero,
                SpriteEffects.None,
                0f);
        }

        spriteBatch.End();
    }

    public void Dispose() => _beam.Dispose();

    /// <summary>
    /// Jeden sloupec pixelů: průhledný, uprostřed bílý, zase průhledný.
    /// Natažený do délky z něj je paprsek s měkkými okraji — totéž, co by
    /// jinak dělal shader, jen předpočítané.
    /// </summary>
    private static Texture2D BuildBeam(GraphicsDevice device)
    {
        var pixels = new Color[GradientHeight];
        for (int i = 0; i < GradientHeight; i++)
        {
            // Kosinové okno: na obou koncích přesně nula, uprostřed jednička,
            // bez zlomu. Lineární přechod by na okraji nechal viditelnou hranu.
            float t = i / (GradientHeight - 1f);
            float falloff = 0.5f - 0.5f * MathF.Cos(t * MathF.Tau);
            pixels[i] = new Color(1f, 1f, 1f, falloff * falloff);
        }

        var texture = new Texture2D(device, 1, GradientHeight);
        texture.SetData(pixels);
        return texture;
    }
}
