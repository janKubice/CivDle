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
/// <para>Kreslí se <b>násobením</b>, ne průhledným překryvem — stín je ubrané
/// světlo. Překryv by plochu jen zašedil a vypadal by jako špína na skle.</para>
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

    private readonly Texture2D _clouds;
    private float _time;

    public CloudShadowRenderer(GraphicsDevice device)
    {
        _clouds = BuildCloudTexture(device);
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

        float darkening = MaxDarkening * Math.Clamp(coverage, 0f, 1f);
        var shade = new Color(1f - darkening, 1f - darkening, 1f - darkening * 0.85f);

        spriteBatch.Begin(
            blendState: DayNightCycle.MultiplyBlend,
            samplerState: SamplerState.LinearWrap);
        spriteBatch.Draw(
            _clouds,
            new Rectangle(0, 0, viewport.Width, viewport.Height),
            source,
            shade);
        spriteBatch.End();
    }

    public void Dispose() => _clouds.Dispose();

    /// <summary>
    /// Vyrobí texturu mraků: pár vrstev hodnotového šumu přes sebe a na to
    /// práh, aby vznikly ostrůvky zataženo a mezi nimi jasno.
    ///
    /// <para>Bílá znamená „sem svítí slunce", černá „tady je stín". Textura se
    /// pak kreslí násobením, takže bílá nechá scénu být.</para>
    /// </summary>
    private static Texture2D BuildCloudTexture(GraphicsDevice device)
    {
        var pixels = new Color[TextureSize * TextureSize];
        for (int y = 0; y < TextureSize; y++)
        {
            for (int x = 0; x < TextureSize; x++)
            {
                // Tři oktávy. Dlaždicové opakování drží to, že se vzorkuje po
                // celých násobcích velikosti textury — okraj tak navazuje.
                float value =
                    Octave(x, y, 4) * 0.55f +
                    Octave(x, y, 8) * 0.30f +
                    Octave(x, y, 16) * 0.15f;

                // Práh: bez něj je to rovnoměrná šeď, ne mraky. Takhle vzniknou
                // souvislé kusy stínu s měkkým okrajem.
                float shade = Smooth(Math.Clamp((value - 0.42f) / 0.34f, 0f, 1f));
                pixels[y * TextureSize + x] = new Color(1f - shade, 1f - shade, 1f - shade);
            }
        }

        var texture = new Texture2D(device, TextureSize, TextureSize);
        texture.SetData(pixels);
        return texture;
    }

    /// <summary>Jedna vrstva hodnotového šumu, která se po hraně textury opakuje.</summary>
    private static float Octave(int x, int y, int cells)
    {
        int size = TextureSize / cells;
        int cx = x / size;
        int cy = y / size;
        float fx = Smooth((x % size) / (float)size);
        float fy = Smooth((y % size) / (float)size);

        float a = Corner(cx, cy, cells);
        float b = Corner(cx + 1, cy, cells);
        float c = Corner(cx, cy + 1, cells);
        float d = Corner(cx + 1, cy + 1, cells);

        return MathHelper.Lerp(MathHelper.Lerp(a, b, fx), MathHelper.Lerp(c, d, fx), fy);
    }

    /// <summary>
    /// Hodnota v rohu buňky. Souřadnice se zbytkem po dělení zabalí dokola,
    /// takže levý okraj textury sedí na pravý a nahoře je totéž co dole.
    /// </summary>
    private static float Corner(int cx, int cy, int cells)
    {
        int wrappedX = ((cx % cells) + cells) % cells;
        int wrappedY = ((cy % cells) + cells) % cells;

        unchecked
        {
            uint h = (uint)(wrappedX * 374761393 + wrappedY * 668265263 + cells * 1442695041);
            h = (h ^ (h >> 13)) * 1274126177;
            return ((h ^ (h >> 16)) & 0xFFFF) / 65535f;
        }
    }

    private static float Smooth(float t) => t * t * (3f - 2f * t);
}
