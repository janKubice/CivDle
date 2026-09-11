namespace CivDle.Core.Content;

/// <summary>
/// Jak se povrch silnice kreslí. Řetězec z JSON se mapuje na tuhle hodnotu
/// a renderer si k ní drží vlastní kresbu — je to behavior-ID hook, ne data
/// s logikou: <b>co</b> je v datech, <b>jak</b> v kódu.
/// </summary>
public enum RoadSurfaceKind
{
    /// <summary>Hliněná cesta: dvě vyjeté koleje a rozdrbaný okraj.</summary>
    Dirt,

    /// <summary>Dlážděná cesta: pravidelný, ale nestejnoměrný kámen.</summary>
    Cobble,

    /// <summary>Asfalt: hladký povrch, vodorovné značení.</summary>
    Paved,
}

/// <summary>
/// Povrch silnice platný od dané éry.
///
/// <para><b>Proč to vzniklo:</b> silnice vypadala ve všech érách stejně —
/// jedna šedá barva od pravěku po orbitální civilizaci. Přitom je to nejdelší
/// souvislá čára na obrazovce a hráč po ní vodí oči, protože mu drží tvar
/// města. Když s postupem věků nezmění vzhled, nezmění se dojem z města ani
/// když se všechno ostatní posune o tisíc let.</para>
/// </summary>
/// <param name="FromEra">Od které éry (index) tenhle povrch platí.</param>
/// <param name="Kind">Která kresba se použije.</param>
/// <param name="Color">Barva vozovky; lem a vyjetý střed se z ní odvodí.</param>
public sealed record RoadSurface(int FromEra, RoadSurfaceKind Kind, RgbColor Color);
