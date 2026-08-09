using Microsoft.Xna.Framework;

namespace CivDle.Rendering.Sprites;

/// <summary>
/// Společná paleta hry — 32 barev, na které se srovná všechno, co se kreslí.
///
/// <para>Proč to vzniklo: sprity vznikaly postupně a každý si nesl vlastní
/// odstíny. Ve výsledku měla hra několik set různých hnědých a modrých, které
/// se od sebe lišily o pár jednotek. Oko to nepřečte jako bohatství, ale jako
/// <b>špínu</b>: obrázek nedrží pohromadě a vypadá, že ho kreslilo dvacet lidí,
/// kteří se spolu nebavili. Omezená paleta je to, co dělá pixel art pixel
/// artem — je jedno, kolik má barev, důležité je, že je jich <em>málo</em>
/// a že se opakují.</para>
///
/// <para>Nepřekresluje se nic. Sprity se kreslí dál po svém a až hotová kresba
/// se přemapuje na nejbližší barvu z palety (<see cref="Snap"/>). Za pár řádků
/// tak dostane celá hra jednotný nádech a nová budova se do něj trefí sama,
/// aniž by na to autor musel myslet.</para>
///
/// <para>Paleta je stavěná jako <b>rampy</b> (tmavá → světlá v jednom
/// odstínu), ne jako sbírka jednotlivostí. Právě rampy dělají stínování:
/// když má hnědá čtyři stupně, dá se z ní vystínovat střecha; když má jednu,
/// nedá se nic.</para>
/// </summary>
public static class GamePalette
{
    /// <summary>
    /// 32 barev v rampách: neutrály, hnědé, zelené, modré, teplé, červené,
    /// fialové a písek/pleť.
    /// </summary>
    private static readonly Color[] Colors =
    {
        // Neutrály — od noci po papír.
        new(27, 26, 33), new(51, 50, 61), new(78, 76, 89),
        new(110, 107, 121), new(156, 153, 166), new(207, 205, 214),

        // Dřevo a hlína.
        new(58, 42, 30), new(94, 67, 44), new(138, 100, 64), new(185, 141, 92),

        // Zeleň.
        new(31, 58, 36), new(47, 92, 51), new(71, 129, 74),
        new(111, 168, 92), new(156, 201, 111),

        // Voda a chlad.
        new(22, 40, 63), new(35, 74, 107), new(47, 113, 155),
        new(76, 156, 196), new(143, 203, 224),

        // Teplé světlo, oheň, kov v ohni.
        new(107, 62, 27), new(176, 106, 34), new(226, 160, 47), new(245, 210, 92),

        // Červené (odznaky, střechy, výstrahy).
        new(92, 30, 30), new(163, 50, 50), new(220, 91, 75),

        // Fialové (věda, energie, noc).
        new(51, 34, 74), new(92, 58, 128), new(145, 96, 184),

        // Písek a pleť.
        new(214, 185, 140), new(240, 221, 187),
    };

    /// <summary>Kolik barev paleta má.</summary>
    public static int Count => Colors.Length;

    /// <summary>Barva palety podle pořadí.</summary>
    public static Color At(int index) => Colors[((index % Count) + Count) % Count];

    /// <summary>
    /// Nejbližší barva z palety. Alfa se nemění — průhlednost je tvar, ne barva,
    /// a snap na ni sáhnout nesmí, jinak by se rozpadly měkké okraje spritů.
    ///
    /// <para>Vzdálenost je vážená (2 : 4 : 3), protože oko je nejcitlivější na
    /// zeleň a nejméně na modrou. Prostý eukleidovský rozdíl v RGB by modré
    /// odstíny házel do sebe a zelené naopak zbytečně trhal.</para>
    /// </summary>
    public static Color Snap(Color color)
    {
        if (color.A == 0)
        {
            return color; // úplně průhledný pixel nemá barvu, kterou by šlo srovnat
        }

        int best = 0;
        int bestDistance = int.MaxValue;
        for (int i = 0; i < Colors.Length; i++)
        {
            int dr = color.R - Colors[i].R;
            int dg = color.G - Colors[i].G;
            int db = color.B - Colors[i].B;
            int distance = 2 * dr * dr + 4 * dg * dg + 3 * db * db;
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = i;
            }
        }

        var snapped = Colors[best];
        return new Color(snapped.R, snapped.G, snapped.B, color.A);
    }
}
