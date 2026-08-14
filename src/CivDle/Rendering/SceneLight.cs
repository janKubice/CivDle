using Microsoft.Xna.Framework;

namespace CivDle.Rendering;

/// <summary>
/// Odkud na scénu svítí a jak velký stín z toho padá. Jedno místo pro celou hru.
///
/// <para>Světlo je zleva shora, stín tedy padá doprava dolů. Je to volba, ne
/// fyzika: v top-down pohledu s pixelovými sprity je čitelnost důležitější než
/// správnost, a stín doprava dolů nezakrývá vchody, které se kreslí u spodní
/// hrany.</para>
///
/// <para><b>Přepsáno po pohledu na výsledek.</b> První verze kreslila dva plné
/// obdélníky: kopii budovy posunutou stranou a rámeček kolem paty. Na papíře to
/// dávalo smysl, na obrazovce ne — sprity nejsou obdélníky, takže z toho byly
/// tvrdé tmavé krabice čouhající z budov a u shluků se slévaly do špinavých
/// ploch na trávě. Vypadalo to jako flek, ne jako stín.</para>
///
/// <para>Teď je stín <b>měkká plochá skvrna u paty</b> budovy, mírně posunutá
/// po směru světla — tak, jak se to v top-down pixel artu dělá. Nekopíruje tvar
/// objektu (ten stejně neznáme), jen říká „tady to stojí na zemi". Měkký okraj
/// je to podstatné: právě tvrdá hrana dělala z předchozí verze krabici.</para>
///
/// <para>Délka roste s <b>výškou</b> objektu, kterou v 2D odhadujeme z půdorysu:
/// katedrála o dvou dlaždicích má vrhat větší stín než chalupa.</para>
///
/// <para>Vrstva: čistý render, žádný stav — jen převod „jak velký objekt" na
/// „kam a jak velká skvrna".</para>
/// </summary>
public static class SceneLight
{
    /// <summary>Vodorovná složka směru stínu (kladná = doprava).</summary>
    public const float DirectionX = 0.62f;

    /// <summary>Svislá složka směru stínu (kladná = dolů).</summary>
    public const float DirectionY = 0.78f;

    /// <summary>Posun stínu u nejnižšího objektu v pixelech.</summary>
    public const float MinLength = 2f;

    /// <summary>
    /// Strop posunu. Bez něj by megastruktura o osmi dlaždicích odhodila stín
    /// přes půl ulice, na které stojí.
    /// </summary>
    public const float MaxLength = 9f;

    /// <summary>
    /// Krytí stínu. Vyšší než u staré verze schválně — měkký okraj unese víc
    /// než tvrdá hrana, která při stejné hodnotě bila do očí.
    /// </summary>
    public const float ShadowAlpha = 0.34f;

    /// <summary>
    /// Jak plochá je skvrna vůči šířce budovy. Půlka by byla kruh, což
    /// v top-down pohledu vypadá jako díra pod domem.
    /// </summary>
    private const float Flatness = 0.42f;

    /// <summary>Barva stínu. Nejde o černou — modravý stín působí jako denní světlo.</summary>
    public static readonly Color ShadowColor = new(28, 30, 46);

    /// <summary>
    /// Kreslí se stín vůbec? Hráč si ho může v nastavení vypnout.
    ///
    /// <para>Statické ze stejného důvodu jako <see cref="DetailLevel"/>: čte to
    /// renderer v každém snímku, je to čistě prezentační přepínač bez stavu
    /// a protahovat ho konstruktory vrstev by nic nezpřehlednilo.</para>
    /// </summary>
    public static bool Enabled { get; private set; } = true;

    /// <summary>Nastaví se při startu a při změně nastavení.</summary>
    public static void Apply(bool enabled) => Enabled = enabled;

    /// <summary>
    /// Jak daleko se stín odsune od objektu o daném půdorysu (v pixelech).
    ///
    /// <para>Roste odmocninou, ne lineárně: mezi chalupou a domem má být rozdíl
    /// vidět, mezi mrakodrapem a megastrukturou už nikoho nezajímá.</para>
    /// </summary>
    public static float LengthFor(int footprintTiles)
    {
        int tiles = Math.Max(1, footprintTiles);
        return Math.Clamp(MinLength * MathF.Sqrt(tiles), MinLength, MaxLength);
    }

    /// <summary>
    /// Obdélník, do kterého se vykreslí měkká skvrna stínu — plochá elipsa
    /// u spodní hrany objektu, posunutá po směru světla.
    ///
    /// <para>Skvrna schválně přesahuje šířku budovy jen málo a leží <b>hlavně
    /// pod ní</b>: stín, který je větší než objekt, čte oko jako druhý objekt.</para>
    /// </summary>
    public static Rectangle ShadowRect(Rectangle bounds, int footprintTiles)
    {
        float length = LengthFor(footprintTiles);

        int width = bounds.Width + (int)MathF.Round(length);
        int height = Math.Max(3, (int)MathF.Round(bounds.Width * Flatness));

        // Střed skvrny sedí na spodní hraně budovy a odtud se posune po směru
        // světla. Kdyby seděl na středu budovy, vypadala by budova, že se
        // vznáší nad vlastním stínem.
        float centerX = bounds.X + bounds.Width * 0.5f + length * DirectionX;
        float centerY = bounds.Bottom + length * DirectionY * 0.5f;

        return new Rectangle(
            (int)MathF.Round(centerX - width * 0.5f),
            (int)MathF.Round(centerY - height * 0.5f),
            width,
            height);
    }
}
