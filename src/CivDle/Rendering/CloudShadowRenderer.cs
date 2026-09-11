using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CivDle.Rendering;

/// <summary>
/// Stíny mraků plující přes krajinu.
///
/// <para><b>Proč právě tohle:</b> v pohledu shora je obloha mimo záběr, takže
/// se z počasí dá ukázat jedině to, co udělá se zemí. Pomalu plující tmavé
/// skvrny přitom dělají dvě věci naráz — rozbijí jednolitou plochu terénu
/// a dají obrazu <b>pohyb</b> i ve chvíli, kdy se ve městě nic neděje. Ze
/// všech vizuálních přídavků má tenhle nejlepší poměr dopadu k práci.</para>
///
/// <para>Mraky jsou jedna vygenerovaná textura šumu, která se dlaždicově
/// opakuje a posouvá s časem. Žádné částice, žádný stav na mrak: celý efekt je
/// jeden draw call přes obrazovku.</para>
///
/// <para><b>Jak se kreslí a proč ne násobením:</b> násobení počítá
/// <c>cíl × zdroj</c>, takže texel s hustým mrakem (černá) vynásobí scénu
/// nulou — z mraku je díra do černa a síla stínu se nedá řídit vůbec.
/// Přesně tak to taky vypadalo. Správně je <b>míchání přes alfu</b> s tmavou
/// barvou: výsledek je <c>cíl × (1 − α) + stín × α</c>, tedy per-pixel
/// násobek, kde α nese hustota mraku a sílu určuje volající.</para>
///
/// <para>Stín přitom není šedý závoj: barva je tmavě modrošedá, protože
/// zastíněné místo dostává světlo z oblohy, ne od slunce.</para>
///
/// <para>Vrstva: čistý render. Nic nečte ze simulace kromě větru a času.</para>
/// </summary>
public sealed class CloudShadowRenderer : IDisposable
{
    /// <summary>Hrana textury mraků. Nemusí být velká — v pohybu si oko opakování nevšimne.</summary>
    private const int TextureSize = 256;

    /// <summary>Kolik světových pixelů zabere jedno opakování textury.</summary>
    private const float WorldSpan = 2400f;

    /// <summary>Jak rychle mraky plují (světové pixely za sekundu).</summary>
    private const float DriftSpeed = 14f;

    /// <summary>Nejtmavší, co stín udělá. Přes 30 % už to vypadá jako zatmění.</summary>
    private const float MaxDarkening = 0.26f;

    /// <summary>
    /// Barva stínu. Ne černá: zastíněné místo pořád dostává rozptýlené světlo
    /// z oblohy, takže je chladnější, ne tmavší do ztracena.
    /// </summary>
    private static readonly Color ShadowColor = new(38, 46, 62);

    /// <summary>
    /// Práh a měkkost okraje pro stínovou vrstvu. Pod prahem je jasno; bez něj
    /// by z toho byla rovnoměrná šeď, ne mraky.
    /// </summary>
    private const float Threshold = 0.42f;

    private const float Softness = 0.34f;

    /// <summary>
    /// Seed tvaru. Vrstva nad městem má jiný, aby stín neležel přesně pod
    /// mrakem — mraky letí výš a jinou rychlostí, takže by zákryt byl chyba.
    /// </summary>
    private const int Seed = 1;

    private readonly Texture2D _clouds;
    private float _time;

    public CloudShadowRenderer(GraphicsDevice device)
    {
        // Hustota v ALFĚ, bílá v RGB. Verze s hustotou v barvě se dá kreslit
        // jedině násobením, a to znamená díru do černa (viz hlavička třídy).
        _clouds = CloudNoise.Build(device, TextureSize, Threshold, Softness, Seed, asShade: false);
    }

    public void Update(float dt) => _time += dt;

    /// <summary>
    /// Přetáhne přes scénu stíny mraků.
    /// </summary>
    /// <param name="coverage">
    /// Kolik oblohy mraky zabírají, 0–1. Bere se z počasí: za jasna skoro nic,
    /// v dešti skoro všechno. Při nule se nekreslí vůbec.
    /// </param>
    /// <param name="windX">Směr větru vodorovně (−1 až 1) — mraky letí s ním.</param>
    /// <param name="windY">Směr větru svisle.</param>
    public void Draw(
        SpriteBatch spriteBatch, Camera2D camera, Viewport viewport,
        float coverage, float windX, float windY)
    {
        if (coverage <= 0.01f)
        {
            return;
        }

        // Mraky se posouvají ve SVĚTĚ, ne po obrazovce: jinak by při posunu
        // kamery letěly s ní a vypadaly by jako šmouha na monitoru.
        var drift = new Vector2(windX, windY) * (_time * DriftSpeed);
        var (min, _) = camera.VisibleWorldBounds();
        var offset = (min + drift) / WorldSpan;

        var source = new Rectangle(
            (int)(offset.X * TextureSize),
            (int)(offset.Y * TextureSize),
            (int)(viewport.Width / camera.Zoom / WorldSpan * TextureSize),
            (int)(viewport.Height / camera.Zoom / WorldSpan * TextureSize));

        // Aspoň jeden texel, jinak MonoGame kreslí nic — a při velkém přiblížení
        // by výřez do textury vyšel na nulu.
        source.Width = Math.Max(1, source.Width);
        source.Height = Math.Max(1, source.Height);

        // Síla jde do alfy tintu, hustota mraku přijde z textury — obě se
        // vynásobí, takže řídký okraj mraku stíní slabě a jádro naplno.
        float darkening = MaxDarkening * Math.Clamp(coverage, 0f, 1f);
        var shade = ShadowColor * darkening;

        spriteBatch.Begin(
            blendState: BlendState.AlphaBlend,
            samplerState: SamplerState.LinearWrap);
        spriteBatch.Draw(
            _clouds,
            new Rectangle(0, 0, viewport.Width, viewport.Height),
            source,
            shade);
        spriteBatch.End();
    }

    public void Dispose() => _clouds.Dispose();
}
