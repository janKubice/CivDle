namespace CivDle.Capture;

/// <summary>
/// Jeden podklad do obchodu v rozměru, který Steam vyžaduje.
///
/// <para>Rozměry nejsou volba: Steam je má pevně dané a špatná velikost se
/// prostě nenahraje. Proto jsou tady vypsané jako data — přidat další formát
/// znamená přidat řádek, ne psát kód.</para>
///
/// <para><b>Hra kreslí jen scénu, ne logo.</b> Nápis přidává až
/// <c>tools/make_store_assets.py</c>: písmo z herního enginu je stavěné na HUD,
/// ne na obal, a „proužek s textem" přes město byl přesně ten důvod, proč
/// kapsle vypadaly jako screenshot s popiskem místo jako obálka hry.</para>
/// </summary>
/// <param name="FileName">Jméno souboru bez přípony.</param>
/// <param name="Width">Šířka v pixelech.</param>
/// <param name="Height">Výška v pixelech.</param>
public sealed record CapsuleSpec(string FileName, int Width, int Height)
{
    /// <summary>
    /// Podklady pro všechny kapsle, které Steamworks chce.
    ///
    /// <para>Rozměry odpovídají <b>současným</b> požadavkům Steamu (dvojnásobek
    /// těch původních — header býval 460×215, dnes je 920×430). Jména souborů
    /// nesou rozměr, aby se při nahrávání nedalo splést, co kam patří.</para>
    ///
    /// <para>Hero se kreslí rovnou v plných 3840×1240, ne dvojnásobným zvětšením
    /// z poloviny: je to největší obrázek, jaký Steam u hry ukazuje, a rozmazané
    /// domy jsou na něm vidět víc než kdekoliv jinde.</para>
    /// </summary>
    public static IReadOnlyList<CapsuleSpec> All { get; } = new[]
    {
        new CapsuleSpec("bg-header-920x430", 920, 430),
        new CapsuleSpec("bg-small-462x174", 462, 174),
        new CapsuleSpec("bg-main-1232x706", 1232, 706),
        new CapsuleSpec("bg-vertical-748x896", 748, 896),
        new CapsuleSpec("bg-library-capsule-600x900", 600, 900),
        new CapsuleSpec("bg-library-hero-3840x1240", 3840, 1240),
        new CapsuleSpec("bg-page-1438x810", 1438, 810),
    };
}
