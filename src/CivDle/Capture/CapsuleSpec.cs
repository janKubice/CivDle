namespace CivDle.Capture;

/// <summary>
/// Jeden obrázek do obchodu v rozměru, který Steam vyžaduje.
///
/// <para>Rozměry nejsou volba: Steam je má pevně dané a špatná velikost se
/// prostě nenahraje. Proto jsou tady vypsané jako data — přidat další formát
/// znamená přidat řádek, ne psát kód.</para>
/// </summary>
/// <param name="FileName">Jméno souboru bez přípony.</param>
/// <param name="Width">Šířka v pixelech.</param>
/// <param name="Height">Výška v pixelech.</param>
/// <param name="TitleScale">Jak velký je na obrázku název hry (0 = bez názvu).</param>
/// <param name="ShowTagline">Vejde se pod název i podtitul?</param>
public sealed record CapsuleSpec(
    string FileName,
    int Width,
    int Height,
    float TitleScale,
    bool ShowTagline)
{
    /// <summary>
    /// Sada, kterou Steam chce ke store stránce a knihovně. Jména odpovídají
    /// tomu, jak se pole jmenují v Steamworks, aby se nedalo splést, co kam patří.
    /// </summary>
    public static IReadOnlyList<CapsuleSpec> All { get; } = new[]
    {
        new CapsuleSpec("header-capsule-460x215", 460, 215, 1.0f, true),
        new CapsuleSpec("small-capsule-231x87", 231, 87, 0.55f, false),
        new CapsuleSpec("main-capsule-616x353", 616, 353, 1.4f, true),
        new CapsuleSpec("vertical-capsule-374x448", 374, 448, 1.1f, true),
        new CapsuleSpec("library-capsule-600x900", 600, 900, 1.7f, true),
        new CapsuleSpec("library-header-920x430", 920, 430, 1.9f, true),
        new CapsuleSpec("library-hero-1920x620", 1920, 620, 0f, false),
        new CapsuleSpec("page-background-1438x810", 1438, 810, 0f, false),
        new CapsuleSpec("community-icon-184x184", 184, 184, 0.7f, false),
    };
}
