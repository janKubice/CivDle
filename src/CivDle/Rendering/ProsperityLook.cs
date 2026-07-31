using Microsoft.Xna.Framework;

namespace CivDle.Rendering;

/// <summary>
/// Jak vypadá zástavba podle toho, jak se místu daří — jediné místo, kde se
/// z čísla prosperity stává barva.
///
/// <para>Proč to ve hře je: hráč zvedá spokojenost a bourá smog, ale výsledek
/// dosud viděl jen jako číslo v HUD. Když se úspěch propíše do <b>obrazu</b>,
/// stane se z abstraktní statistiky něco, co je vidět z okna: kvetoucí čtvrť
/// je barevná a má na domech květiny, zakouřená je šedá a zašlá.</para>
///
/// <para>Proč vlastní třída, a ne pár řádků v <c>BuildingRenderer</c>: barevná
/// křivka je rozhodnutí, které se ladí, a takhle jde ověřit bez grafiky
/// (SRP z CLAUDE.md — renderer kreslí, tohle rozhoduje jak).</para>
/// </summary>
public static class ProsperityLook
{
    /// <summary>Nad touhle prosperitou už se domy zdobí.</summary>
    public const double OrnamentThreshold = 0.8;

    /// <summary>Pod touhle prosperitou je na domech vidět špína.</summary>
    public const double GrimeThreshold = 0.45;

    /// <summary>Jak moc smí bída stáhnout jas. Nula by udělala z města siluety.</summary>
    private const float MaxDim = 0.35f;

    /// <summary>
    /// Nádech budovy podle prosperity: od zašlé šedi po teplý, sytý odstín.
    ///
    /// <para>Nikdy nejde na černou ani na křiklavou barvu — hra netrestá
    /// tvrdě a ani nekřičí; jen posouvá odstín tak, aby byl rozdíl vidět
    /// koutkem oka (soft pressure i v obraze).</para>
    /// </summary>
    public static Color Tint(double prosperity)
    {
        float t = (float)Math.Clamp(prosperity, 0.0, 1.0);

        // Bída: ztmavit a odbarvit do šedomodra. Prosperita: mírně dozlatit.
        var poor = new Color(150, 148, 152);
        var plain = Color.White;
        var rich = new Color(255, 250, 232);

        var color = t < 0.5f
            ? Color.Lerp(poor, plain, t * 2f)
            : Color.Lerp(plain, rich, (t - 0.5f) * 2f);

        // Jen RGB, alfa zůstává plná: ztmavit budovu ano, prosvítat skrz ni ne.
        float brightness = 1f - MaxDim * (1f - t);
        return new Color(
            (int)(color.R * brightness),
            (int)(color.G * brightness),
            (int)(color.B * brightness));
    }

    /// <summary>
    /// Vlastní barva budovy pod nádechem prosperity — pro budovy bez spritu,
    /// které se kreslí obdélníkem z <c>mapColor</c>. Násobení po složkách, ať
    /// zůstane jejich identita a změní se jen jas a teplota.
    /// </summary>
    public static Color Modulate(Color baseColor, Color tint) => new(
        baseColor.R * tint.R / 255,
        baseColor.G * tint.G / 255,
        baseColor.B * tint.B / 255);

    /// <summary>Zdobí se tenhle dům? (Květiny, markýzy, prapory.)</summary>
    public static bool HasOrnament(double prosperity) => prosperity >= OrnamentThreshold;

    /// <summary>Je na domě vidět zašlost? (Šmouhy od kouře.)</summary>
    public static bool HasGrime(double prosperity) => prosperity < GrimeThreshold;

    /// <summary>
    /// Barva ozdoby. Střídá se podle budovy, ať nemá celá ulice stejný záhon —
    /// odvozuje se z indexu, ne z náhody, takže se ozdoby mezi snímky nemihotají.
    /// </summary>
    public static Color OrnamentColor(int buildingIndex) => (buildingIndex % 4) switch
    {
        0 => new Color(232, 96, 110),
        1 => new Color(240, 200, 92),
        2 => new Color(226, 240, 250),
        _ => new Color(196, 128, 226),
    };
}
