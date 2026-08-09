using Microsoft.Xna.Framework;

namespace CivDle.Rendering;

/// <summary>
/// Odkud na scénu svítí. Jedno místo pro celou hru.
///
/// <para>Proč to vzniklo: každý renderer si stín kreslil po svém — budova měla
/// třípixelový proužek u spodní hrany, strom nic, landmark zase něco jiného.
/// Když každý objekt vrhá stín jinam (nebo žádný), scéna nevypadá jako místo
/// se sluncem, ale jako nálepky na papíře. Stačí, aby všechno vrhalo stín
/// <b>stejným směrem</b>, a plochý obraz najednou má hloubku.</para>
///
/// <para>Světlo je zleva shora, stín tedy padá doprava dolů. Je to volba, ne
/// fyzika: v top-down pohledu s pixelovými sprity je čitelnost důležitější než
/// správnost, a stín doprava dolů nezakrývá vchody, které se kreslí u spodní
/// hrany.</para>
///
/// <para>Délka stínu roste s <b>výškou</b> objektu, kterou v 2D odhadujeme
/// z půdorysu: katedrála o dvou dlaždicích má vrhat delší stín než chalupa.
/// Bez toho by celé město vypadalo jako jedna nízká vrstva.</para>
///
/// <para>Vrstva: čistý render, žádný stav — jen převod „jak velký objekt" na
/// „kam a jak dlouhý stín".</para>
/// </summary>
public static class SceneLight
{
    /// <summary>Vodorovná složka směru stínu (kladná = doprava).</summary>
    public const float DirectionX = 0.62f;

    /// <summary>Svislá složka směru stínu (kladná = dolů).</summary>
    public const float DirectionY = 0.78f;

    /// <summary>Stín nejnižšího objektu v pixelech. Míň by nebylo vidět.</summary>
    public const float MinLength = 3f;

    /// <summary>
    /// Strop délky stínu. Bez něj by megastruktura o osmi dlaždicích vrhala
    /// stín přes půl obrazovky a zakryla by ulici, na které stojí.
    /// </summary>
    public const float MaxLength = 14f;

    /// <summary>Krytí vrženého stínu. Záměrně slabé — je to nádech, ne díra.</summary>
    public const float ShadowAlpha = 0.28f;

    /// <summary>
    /// Krytí ztmavení kolem paty budovy (ambient occlusion). Ještě slabší než
    /// stín: kdyby bylo vidět jako obrys, vypadaly by budovy orámované.
    /// </summary>
    public const float ContactAlpha = 0.14f;

    /// <summary>Barva stínu. Nejde o černou — modravý stín působí jako denní světlo.</summary>
    public static readonly Color ShadowColor = new(28, 30, 46);

    /// <summary>
    /// Jak daleko sahá stín objektu o daném půdorysu (v pixelech).
    ///
    /// <para>Roste odmocninou, ne lineárně: mezi chalupou a domem má být rozdíl
    /// vidět, mezi mrakodrapem a megastrukturou už nikoho nezajímá.</para>
    /// </summary>
    public static float LengthFor(int footprintTiles)
    {
        int tiles = Math.Max(1, footprintTiles);
        return Math.Clamp(MinLength * MathF.Sqrt(tiles), MinLength, MaxLength);
    }

    /// <summary>Posun stínu objektu o daném půdorysu (zaokrouhlený na pixely).</summary>
    public static Point OffsetFor(int footprintTiles)
    {
        float length = LengthFor(footprintTiles);
        return new Point(
            (int)MathF.Round(length * DirectionX),
            (int)MathF.Round(length * DirectionY));
    }

    /// <summary>
    /// Obdélník vrženého stínu pod objektem. Stín je o něco nižší než objekt —
    /// jinak by vypadal jako druhá kopie budovy posunutá stranou.
    /// </summary>
    public static Rectangle ShadowRect(Rectangle bounds, int footprintTiles)
    {
        var offset = OffsetFor(footprintTiles);
        int shrink = Math.Min(bounds.Height / 4, 4);
        return new Rectangle(
            bounds.X + offset.X,
            bounds.Y + offset.Y + shrink,
            bounds.Width,
            Math.Max(1, bounds.Height - shrink));
    }

    /// <summary>
    /// Ztmavení těsně kolem paty objektu — to, čemu se v 3D říká ambient
    /// occlusion. Obdélník o pár pixelů větší než budova, kreslený pod ni.
    /// Díky němu budova „sedí" v terénu, místo aby na něm ležela.
    /// </summary>
    public static Rectangle ContactRect(Rectangle bounds)
    {
        const int spread = 2;
        return new Rectangle(
            bounds.X - spread, bounds.Y - spread,
            bounds.Width + 2 * spread, bounds.Height + 2 * spread);
    }
}
