using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CivDle.Rendering;

/// <summary>
/// Mraky plující nad městem.
///
/// <para><b>Proč to doplňuje stíny:</b> stíny mraků (<see cref="CloudShadowRenderer"/>)
/// ukazují počasí na zemi, ale samotné mraky nebyly nikde — obloha je
/// v pohledu shora mimo záběr. Vrstva nad městem tu chybějící polovinu doplní
/// a udělá přitom něco, co ze země nejde: <b>zakryje</b> kus obrazu. Teprve
/// když něco město na chvíli schová, uvěří oko, že je mezi kamerou a zemí
/// vzduch.</para>
///
/// <para><b>Paralaxa:</b> mraky se při posunu kamery hýbou <i>víc</i> než zem,
/// protože jsou blíž. Je to jediná věc, která ve 2D pohledu shora vyrobí
/// hloubku — bez ní by vypadaly jako skvrna namalovaná na terénu.</para>
///
/// <para>Jsou průsvitné a bez ostrých hran: souvislá bílá plocha by vypadala
/// jako vada obrazu a hlavně by hráči zakryla město, se kterým si hraje.</para>
///
/// <para>Vrstva: čistý render. Nečte ze simulace nic než počasí a čas.</para>
/// </summary>
public sealed class CloudLayerRenderer : IDisposable
{
    /// <summary>Hrana textury. Většího mraku se dosáhne větším rozpětím, ne větší texturou.</summary>
    private const int TextureSize = 256;

    /// <summary>Kolik světových pixelů zabere jedno opakování. Víc než u stínů: mraky jsou blíž.</summary>
    private const float WorldSpan = 3200f;

    /// <summary>Jak rychle mraky plují (světové pixely za sekundu).</summary>
    private const float DriftSpeed = 22f;

    /// <summary>
    /// O kolik se mraky posunou proti zemi. Přes 1,0, protože jsou blíž
    /// ke kameře — tohle číslo je celá iluze hloubky.
    /// </summary>
    private const float Parallax = 1.22f;

    /// <summary>
    /// Nejvyšší krytí. Přes třetinu už mrak schová město natolik, že hráč
    /// přestane vidět, co staví — a to je hra, ne obrázek.
    /// </summary>
    private const float MaxOpacity = 0.30f;

    /// <summary>Barva mraku: bílá s nádechem do modra, aby nesvítila jako papír.</summary>
    private static readonly Color Tint = new(232, 238, 248);

    private readonly Texture2D _clouds;
    private float _time;

    public CloudLayerRenderer(GraphicsDevice device)
    {
        // Vyšší práh než u stínů: shora má být vidět pár oddělených mraků,
        // ne souvislá peřina. A jiný seed, aby mrak neležel přesně na svém
        // stínu — letí výš a jinou rychlostí, takže by zákryt byl chyba.
        _clouds = CloudNoise.Build(device, TextureSize, threshold: 0.56f, softness: 0.30f, seed: 7, asShade: false);
    }

    public void Update(float dt) => _time += dt;

    /// <summary>
    /// Přetáhne přes scénu mraky.
    /// </summary>
    /// <param name="coverage">Kolik oblohy mraky zabírají, 0–1. Při nule se nekreslí.</param>
    /// <param name="windX">Směr větru vodorovně (−1 až 1).</param>
    /// <param name="windY">Směr větru svisle.</param>
    /// <param name="light">
    /// Barva denního světla. Mraky ji dostanou taky — bílý mrak v noci by
    /// svítil jako neon.
    /// </param>
    public void Draw(
        SpriteBatch spriteBatch, Camera2D camera, Viewport viewport,
        float coverage, float windX, float windY, Color light)
    {
        if (coverage <= 0.01f)
        {
            return;
        }

        var drift = new Vector2(windX, windY) * (_time * DriftSpeed);
        var (min, _) = camera.VisibleWorldBounds();
        var offset = (min * Parallax + drift) / WorldSpan;

        var source = new Rectangle(
            (int)(offset.X * TextureSize),
            (int)(offset.Y * TextureSize),
            Math.Max(1, (int)(viewport.Width / camera.Zoom / WorldSpan * TextureSize)),
            Math.Max(1, (int)(viewport.Height / camera.Zoom / WorldSpan * TextureSize)));

        float opacity = MaxOpacity * Math.Clamp(coverage, 0f, 1f);
        var color = new Color(
            (byte)(Tint.R * light.R / 255),
            (byte)(Tint.G * light.G / 255),
            (byte)(Tint.B * light.B / 255)) * opacity;

        spriteBatch.Begin(blendState: BlendState.AlphaBlend, samplerState: SamplerState.LinearWrap);
        spriteBatch.Draw(_clouds, new Rectangle(0, 0, viewport.Width, viewport.Height), source, color);
        spriteBatch.End();
    }

    public void Dispose() => _clouds.Dispose();
}
