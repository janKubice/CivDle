using CivDle.Core.Content;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CivDle.Rendering.Effects;

/// <summary>
/// Drobnosti poletující vzduchem: okvětní plátky, pyl, listí, sníh.
///
/// <para><b>Proč to scéna potřebuje:</b> nádech přes obraz řekne „je podzim"
/// jen tomu, kdo si toho všimne. Listí padající přes obraz to řekne i tomu,
/// kdo se dívá na jeden dům. A hlavně: idle hra se dívá sama na sebe většinu
/// času, takže potřebuje pohyb i ve chvíli, kdy se ve městě neděje nic —
/// jinak přestane oko obraz vnímat jako místo a začne ho vnímat jako
/// obrázek.</para>
///
/// <para><b>Co</b> poletuje, je v datech (<see cref="SeasonDef"/>: barva,
/// hustota, rychlost pádu). <b>Jak</b> se to hýbe, je tady.</para>
///
/// <para>Pevný bazén struktur v plochém poli, žádné alokace za snímek. Částice
/// žijí ve <b>světových</b> souřadnicích a při odletu z výřezu se překlopí na
/// protější stranu: v souřadnicích obrazovky by se při posunu kamery vláčely
/// s ní jako smítka na monitoru.</para>
///
/// <para>Vrstva: čistý render. Ze simulace čte jen období a vítr.</para>
/// </summary>
public sealed class AmbientMotes
{
    /// <summary>
    /// Kolik jich smí být naráz. Sto dvacet vyjde na plné obrazovce zhruba na
    /// jednu na tři sta pixelů — vidět, ale ne sněhová bouře.
    /// </summary>
    public const int Capacity = 120;

    /// <summary>Jak rychle částice letí s větrem (světové pixely za sekundu).</summary>
    private const float DriftSpeed = 34f;

    /// <summary>Nejrychlejší pád při <c>moteFall</c> = 1 (světové pixely za sekundu).</summary>
    private const float FallSpeed = 46f;

    /// <summary>Jedna poletující drobnost. Struktura, ne třída: je jich hodně a jsou malé.</summary>
    private struct Mote
    {
        public float X;
        public float Y;
        public float Size;

        /// <summary>Fáze vlastního kolébání — bez ní by všechny padaly v zákrytu.</summary>
        public float Phase;

        /// <summary>Kolikrát rychleji než ostatní letí tahle. Dělá to dojem hloubky.</summary>
        public float Speed;
    }

    private readonly Mote[] _motes = new Mote[Capacity];
    private readonly Random _random;
    private bool _placed;
    private float _time;

    /// <param name="seed">Aby dva světy nevypadaly stejně, ale týž svět ano.</param>
    public AmbientMotes(long seed) => _random = new Random((int)(seed & 0x7FFFFFFF));

    /// <summary>Kolik částic je právě v provozu. Pro testy bazénu.</summary>
    public int ActiveCount { get; private set; }

    /// <summary>
    /// Posune částice a ty, které odletěly z výřezu, překlopí na protější
    /// stranu.
    /// </summary>
    /// <param name="density">Kolik z bazénu je vidět (0–1). Z dat období.</param>
    /// <param name="fall">Jak rychle padají (0 = visí ve vzduchu, 1 = sníh).</param>
    public void Update(
        float dt, Vector2 min, Vector2 max, float density, float fall, float windX, float windY)
    {
        ActiveCount = (int)(Capacity * Math.Clamp(density, 0f, 1f));
        if (ActiveCount == 0)
        {
            _placed = false; // při návratu období se rozsypou znovu, ne na starých místech
            return;
        }

        _time += dt;
        if (!_placed)
        {
            Scatter(min, max);
            _placed = true;
        }

        var span = max - min;
        for (int i = 0; i < ActiveCount; i++)
        {
            ref var mote = ref _motes[i];

            // Kolébání napříč letem. Bez něj padá všechno po přímce a vypadá
            // to jako déšť z pixelů, ne jako něco, co nese vzduch.
            float wobble = MathF.Sin(_time * 1.6f + mote.Phase) * 12f;

            mote.X += (windX * DriftSpeed + wobble) * mote.Speed * dt;
            mote.Y += (windY * DriftSpeed + fall * FallSpeed) * mote.Speed * dt;

            Wrap(ref mote.X, min.X, span.X);
            Wrap(ref mote.Y, min.Y, span.Y);
        }
    }

    /// <summary>Vykreslí částice v barvě z dat období.</summary>
    public void Draw(SpriteBatch spriteBatch, Camera2D camera, Texture2D pixel, Color color)
    {
        if (ActiveCount == 0)
        {
            return;
        }

        // Velikost se dělí přiblížením, takže na obrazovce zůstane stejná.
        //
        // Bez toho rostly částice se zoomem: dvoupixelové smítko bylo při
        // trojnásobném přiblížení šestipixelový blok a z listí na moři byly
        // plovoucí bedny. Poletující drobnost je blízko u kamery, ne na zemi —
        // takže má mít pevnou velikost v pixelech obrazovky, ne ve světě.
        // Poloha zůstává světová, aby se částice při posunu kamery nevláčely.
        float worldPerPixel = WorldPerPixel(camera.Zoom);

        spriteBatch.Begin(samplerState: SamplerState.PointClamp, transformMatrix: camera.Transform);
        for (int i = 0; i < ActiveCount; i++)
        {
            ref readonly var mote = ref _motes[i];

            // Rychlejší částice jsou „blíž", takže jsou i výraznější — týž trik
            // jako u paralaxy mraků, jen o řád menší.
            float alpha = 0.35f + 0.4f * (mote.Speed - 0.6f);
            float size = Math.Max(1f, mote.Size) * worldPerPixel;

            spriteBatch.Draw(
                pixel,
                new Vector2(mote.X, mote.Y),
                null,
                color * alpha,
                0f,
                Vector2.Zero,
                size,
                SpriteEffects.None,
                0f);
        }

        spriteBatch.End();
    }

    /// <summary>
    /// Kolik světových jednotek je jeden pixel obrazovky.
    ///
    /// <para>Tím se dělí velikost částice, aby na obrazovce zůstala stejná.
    /// Bez toho rostly se zoomem: dvoupixelové smítko bylo při trojnásobném
    /// přiblížení šestipixelový blok a z listí na moři byly plovoucí bedny.
    /// Poletující drobnost je blízko u kamery, ne na zemi.</para>
    ///
    /// <para>Veřejné, protože je to celé to pravidlo a jde ověřit bez
    /// grafického zařízení.</para>
    /// </summary>
    public static float WorldPerPixel(float zoom) => 1f / Math.Max(0.0001f, zoom);

    /// <summary>Rozsype částice náhodně po výřezu. Volá se při prvním snímku období.</summary>
    private void Scatter(Vector2 min, Vector2 max)
    {
        for (int i = 0; i < _motes.Length; i++)
        {
            _motes[i] = new Mote
            {
                X = min.X + (float)_random.NextDouble() * (max.X - min.X),
                Y = min.Y + (float)_random.NextDouble() * (max.Y - min.Y),
                Size = 1f + (float)_random.NextDouble() * 2f,
                Phase = (float)_random.NextDouble() * MathF.Tau,
                Speed = 0.6f + (float)_random.NextDouble() * 0.8f,
            };
        }
    }

    /// <summary>
    /// Překlopí souřadnici, která vyjela z výřezu, na protější stranu.
    ///
    /// <para>Modulo, ne skok na okraj: při rychlém posunu kamery může být
    /// částice o několik obrazovek vedle a jeden skok by ji nedohnal — obraz
    /// by se vyprázdnil přesně ve chvíli, kdy si hráč efektu začne všímat.</para>
    ///
    /// <para>Veřejné schválně: je to jediné netriviální pravidlo celého efektu
    /// a jde ověřit bez grafického zařízení i bez sahání do bazénu.</para>
    /// </summary>
    /// <param name="value">Souřadnice, která se případně překlopí.</param>
    /// <param name="min">Levý (horní) okraj výřezu.</param>
    /// <param name="span">Šířka (výška) výřezu. Nula = výřez nemá plochu, nic se neděje.</param>
    public static void Wrap(ref float value, float min, float span)
    {
        if (span <= 0f)
        {
            return;
        }

        float offset = (value - min) % span;
        value = min + (offset < 0f ? offset + span : offset);
    }
}
