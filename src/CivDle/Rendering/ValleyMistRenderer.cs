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

    /// <summary>
    /// Nejvyšší krytí jedné skvrny. Přes patnáct procent přestane být vidět,
    /// co pod tím leží.
    ///
    /// <para>Bývalo 0,34 — víc než dvojnásobek toho, co si tenhle komentář sám
    /// předepisoval. Naměřeno na hotovém snímku: mlha přidala za svítání
    /// <b>63 bodů jasu</b> (medián 54 → 117) a poslala tím svítání nad poledne
    /// (106). Rozbřesk vycházel světlejší než pravé poledne.</para>
    /// </summary>
    private const float MaxOpacity = 0.15f;

    /// <summary>Krytí jedné skvrny — vystavené, aby si na mez mohl sáhnout test.</summary>
    public static float MaxPatchOpacity => MaxOpacity;

    /// <summary>
    /// Nad tímhle přiblížením je mlha v plné síle.
    ///
    /// <para>Mlha je detail zblízka — leží v údolích a dává terénu třetí rozměr.
    /// Při oddálení je ale nížina většina obrazu, takže z místního jevu je
    /// <b>závoj přes celou obrazovku</b> a hra vypadá vybledle. Tvrdý strop na
    /// počtu buněk to neuhlídá: při oddálení na 0,75 se jich vejde ~900,
    /// tedy pod limit, a mlha se pořád kreslí.</para>
    /// </summary>
    private const float FullZoom = 2.0f;

    /// <summary>Pod tímhle přiblížením mlha zmizí úplně.</summary>
    private const float GoneZoom = 1.1f;

    /// <summary>Hrana měkké skvrny v pixelech.</summary>
    private const int BlobSize = 32;

    /// <summary>Jak rychle se mlha převaluje (světové pixely za sekundu).</summary>
    private const float DriftSpeed = 6f;

    /// <summary>Barva mlhy: bílá s nádechem do modra. Čistě bílá vypadá jako díra v obraze.</summary>
    private static readonly Color Tint = new(226, 234, 242);

    /// <summary>
    /// Kolik ok mřížky se ještě kreslí. Nad tím se mlha vypne.
    ///
    /// <para>Číslo je spočítané, ne odhadnuté: na plné obrazovce v základním
    /// přiblížení vyjde kolem pěti set ok, a každé se kreslí jako skvrna
    /// dvakrát širší než oko, aby mezi nimi nevznikla šachovnice. Tisíc dvě
    /// stě tedy nechá mlhu i při mírném oddálení a přitom drží překreslování
    /// v rozumných mezích — při čtyřech tisících už by se týž pixel maloval
    /// třicetkrát.</para>
    ///
    /// <para>Z výšky je z mlhy stejně jen mléčný závoj přes celý kontinent,
    /// takže se tím nic neztrácí.</para>
    /// </summary>
    public const int MaxCells = 1200;

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
        // Ráno bývalo na plnou sílu a večer na 0,55. Radiační mlha je ráno
        // opravdu hustší, jenže rozdíl mezi „hustší" a „dvojnásobná" je přesně
        // ten rozdíl mezi atmosférou a vybělenou obrazovkou — a stěžovalo se
        // na obojí, na východ i na západ.
        float dawn = Bump(t, center: 0.22f, width: 0.10f) * 0.8f;
        float evening = Bump(t, center: 0.84f, width: 0.07f) * 0.55f;
        return Math.Clamp(MathF.Max(dawn, evening), 0f, 1f);
    }

    /// <summary>
    /// Kolik z mlhy zbude při daném přiblížení (1 = plná, 0 = žádná).
    ///
    /// <para>Zblízka je mlha v údolí; z výšky by to byl závoj přes celý
    /// kontinent. Přechod je plynulý, aby mlha při odjezdu kamerou
    /// <b>nezmizela skokem</b> — toho by si oko všimlo víc než mlhy samotné.</para>
    /// </summary>
    public static float ZoomFade(float zoom)
    {
        if (zoom >= FullZoom)
        {
            return 1f;
        }

        if (zoom <= GoneZoom)
        {
            return 0f;
        }

        float t = (zoom - GoneZoom) / (FullZoom - GoneZoom);
        return t * t * (3f - 2f * t); // hladký nájezd, ne lineární
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
        density *= ZoomFade(camera.Zoom);
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
