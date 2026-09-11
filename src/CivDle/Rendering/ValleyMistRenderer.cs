using CivDle.Core.World;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CivDle.Rendering;

/// <summary>
/// Mlha ležící v údolích a u vody.
///
/// <para><b>Proč to scéna potřebuje:</b> reliéf terénu ukáže, kde je svah,
/// ale nížina a náhorní plošina vypadají pořád stejně. Mlha leží tam, kde je
/// <b>nízko</b> — a tím dá krajině třetí rozměr, který jinak z pohledu shora
/// nejde ukázat. Navíc je nejhustší za rozbřesku, takže s denní dobou dýchá.</para>
///
/// <para><b>Jak se pozná údolí:</b> výška se vzorkuje na hrubé mřížce po
/// čtyřech dlaždicích a mlha se kreslí tam, kde je pod prahem. Čísla jsou
/// změřená: souš má výšku v pátém percentilu 0,47 a v mediánu 0,54, takže
/// práh 0,52 vezme zhruba spodní třetinu souše — nížiny a okolí vody, ne celý
/// kontinent.</para>
///
/// <para>Kreslí se <b>pod budovy</b>: město má z mlhy vystupovat, ne v ní
/// mizet. Hráč se musí pořád dívat na to, co postavil.</para>
///
/// <para>Vrstva: čistý render. Z terénu jen čte výšku.</para>
/// </summary>
public sealed class ValleyMistRenderer : IDisposable
{
    /// <summary>Rozteč vzorků výšky v dlaždicích. Jemnější mřížka je jen dražší, ne hezčí.</summary>
    private const int CellTiles = 4;

    /// <summary>Nad touhle výškou už mlha není. Změřeno: medián souše je 0,54.</summary>
    private const float HighWater = 0.52f;

    /// <summary>Pod touhle výškou je mlha nejhustší.</summary>
    private const float LowWater = 0.44f;

    /// <summary>Nejvyšší krytí. Přes patnáct procent přestane být vidět, co pod tím leží.</summary>
    private const float MaxOpacity = 0.34f;

    /// <summary>Hrana měkké skvrny v pixelech.</summary>
    private const int BlobSize = 32;

    /// <summary>Jak rychle se mlha převaluje (světové pixely za sekundu).</summary>
    private const float DriftSpeed = 6f;

    /// <summary>Barva mlhy: bílá s nádechem do modra. Čistě bílá vypadá jako díra v obraze.</summary>
    private static readonly Color Tint = new(226, 234, 242);

    /// <summary>
    /// Kolik dlaždic se ještě kreslí. Nad tím je mlha z výšky jen mléčný
    /// závoj přes celý kontinent a stojí tisíce kreseb.
    /// </summary>
    public const int MaxCells = 4096;

    private readonly Texture2D _blob;
    private float _time;

    public ValleyMistRenderer(GraphicsDevice device) => _blob = BuildBlob(device);

    public void Update(float dt) => _time += dt;

    /// <summary>
    /// Jak hustá je mlha v danou denní dobu.
    ///
    /// <para>Vrcholí těsně před rozbřeskem a do dopoledne se zvedne; večer
    /// se vrací slaběji. Je to týž rytmus jako u skutečné radiační mlhy —
    /// a hlavně to znamená, že ráno vypadá jinak než odpoledne, což je celý
    /// smysl denního cyklu.</para>
    /// </summary>
    public static float Density(double timeOfDay01)
    {
        float t = (float)(timeOfDay01 - Math.Floor(timeOfDay01));
        float dawn = Bump(t, center: 0.22f, width: 0.10f);
        float evening = Bump(t, center: 0.84f, width: 0.07f) * 0.55f;
        return Math.Clamp(MathF.Max(dawn, evening), 0f, 1f);
    }

    /// <summary>Jak silná je mlha v místě o dané výšce (0 = nic, 1 = plná).</summary>
    public static float StrengthAt(float elevation)
    {
        if (elevation >= HighWater)
        {
            return 0f;
        }

        float t = (HighWater - elevation) / (HighWater - LowWater);
        return Math.Clamp(t, 0f, 1f);
    }

    /// <summary>Přetáhne mlhu přes nížiny ve výřezu.</summary>
    /// <param name="density">Hustota podle denní doby (<see cref="Density"/>).</param>
    public void Draw(SpriteBatch spriteBatch, Camera2D camera, ITerrain terrain, float density)
    {
        if (density <= 0.02f)
        {
            return;
        }

        const int tileSize = TerrainRenderer.TileSize;
        const int cellWorld = CellTiles * tileSize;
        var (min, max) = camera.VisibleWorldBounds();

        int startX = (int)MathF.Floor(min.X / cellWorld);
        int startY = (int)MathF.Floor(min.Y / cellWorld);
        int endX = (int)MathF.Ceiling(max.X / cellWorld);
        int endY = (int)MathF.Ceiling(max.Y / cellWorld);

        // Z velké výšky by z mlhy byl závoj přes celý kontinent a stálo by to
        // tisíce kreseb. Radši nic — mlha je detail zblízka.
        if ((long)(endX - startX + 1) * (endY - startY + 1) > MaxCells)
        {
            return;
        }

        int drift = (int)(_time * DriftSpeed);

        spriteBatch.Begin(samplerState: SamplerState.LinearClamp, transformMatrix: camera.Transform);
        for (int cy = startY; cy <= endY; cy++)
        {
            for (int cx = startX; cx <= endX; cx++)
            {
                float strength = StrengthAt(terrain.ElevationAt(cx * CellTiles, cy * CellTiles));
                if (strength <= 0.01f)
                {
                    continue;
                }

                // Nestejnoměrnost: bez ní je z mlhy rovnoměrná deka a je vidět
                // mřížka, po které se vzorkovalo.
                float patch = CloudNoise.DensityAt(
                    Wrap(cx * 9 + drift / 7), Wrap(cy * 9), 256, threshold: 0.30f, softness: 0.45f, seed: 3);

                float alpha = MaxOpacity * density * strength * (0.45f + 0.55f * patch);
                if (alpha <= 0.01f)
                {
                    continue;
                }

                // Skvrny se překrývají (kreslí se větší, než je oko mřížky),
                // takže mezi nimi nevznikne šachovnice.
                spriteBatch.Draw(
                    _blob,
                    new Rectangle(
                        cx * cellWorld - cellWorld / 2,
                        cy * cellWorld - cellWorld / 2,
                        cellWorld * 2,
                        cellWorld * 2),
                    Tint * alpha);
            }
        }

        spriteBatch.End();
    }

    public void Dispose() => _blob.Dispose();

    /// <summary>Souřadnice do textury šumu — zabalená, aby nikdy nevyšla záporná.</summary>
    private static int Wrap(int value) => ((value % 256) + 256) % 256;

    /// <summary>Hladký hrbol se středem <paramref name="center"/> — týž tvar jako u soumraku.</summary>
    private static float Bump(float t, float center, float width)
    {
        float distance = MathF.Abs(t - center);
        distance = MathF.Min(distance, 1f - distance); // přes půlnoc
        if (distance >= width)
        {
            return 0f;
        }

        float x = 1f - distance / width;
        return x * x * (3f - 2f * x);
    }

    /// <summary>
    /// Měkká kulatá skvrna. Kraj musí být přesně nula, jinak by se ze skvrn
    /// složil viditelný čtvercový rastr.
    /// </summary>
    private static Texture2D BuildBlob(GraphicsDevice device)
    {
        var pixels = new Color[BlobSize * BlobSize];
        float radius = BlobSize * 0.5f;

        for (int y = 0; y < BlobSize; y++)
        {
            for (int x = 0; x < BlobSize; x++)
            {
                float dx = x + 0.5f - radius;
                float dy = y + 0.5f - radius;
                float distance = MathF.Sqrt(dx * dx + dy * dy) / radius;
                float falloff = Math.Clamp(1f - distance, 0f, 1f);
                pixels[y * BlobSize + x] = new Color(1f, 1f, 1f, falloff * falloff);
            }
        }

        var texture = new Texture2D(device, BlobSize, BlobSize);
        texture.SetData(pixels);
        return texture;
    }
}
