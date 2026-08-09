using Microsoft.Xna.Framework;

namespace CivDle.Rendering;

/// <summary>
/// Čím se od sebe liší dva domy stejného druhu.
///
/// <para>Proč to vzniklo: každá budova se kreslí jedním spritem podle své
/// definice, takže sto chalup byla stokrát tatáž chalupa — z ulice vznikl
/// vzorek tapety. Skutečné město je nepravidelné: střechy mají jiný odstín,
/// dům je o kousek jinak posazený, tu a tam někdo přistavěl komín.</para>
///
/// <para>Klíč je <b>determinismus</b>: vzhled se odvozuje z polohy dlaždice, ne
/// z náhody ani z indexu v poli. Náhoda by mezi snímky blikala, index se mění
/// při bourání (budovy se v poli přerovnají) a celá ulice by po jednom zbourání
/// změnila barvu. Poloha se nemění nikdy.</para>
///
/// <para>Vrstva: čistý render. Nic z toho nesmí ovlivnit simulaci — je to
/// jenom o tom, jak se táž budova nakreslí.</para>
/// </summary>
public static class BuildingVariation
{
    /// <summary>Kolik odstínů střech se střídá.</summary>
    public const int PaletteCount = 5;

    /// <summary>Zhruba každá pátá budova dostane přístavek.</summary>
    private const int DetailEveryNth = 5;

    /// <summary>
    /// Odstíny střech. Drží se blízko sebe schválně — cílem je nepravidelnost
    /// ulice, ne barevný cirkus. Násobí se přes původní barvu spritu, takže si
    /// budova podrží svou identitu (cihlová zůstane cihlová, jen jinak vypálená).
    /// </summary>
    private static readonly Color[] Roofs =
    {
        new(255, 255, 255), // beze změny — ať je i pár „obyčejných"
        new(238, 226, 214), // vybledlá
        new(255, 240, 214), // dožluta
        new(226, 232, 240), // došeda
        new(246, 222, 220), // dorůžova
    };

    /// <summary>Vzhled jedné konkrétní budovy.</summary>
    public readonly record struct Look(
        int PaletteIndex,
        bool Mirrored,
        int OffsetX,
        int OffsetY,
        BuildingExtra Extra);

    /// <summary>
    /// Spočte vzhled budovy stojící na dané dlaždici.
    ///
    /// <para><paramref name="defIndex"/> je součástí klíče, aby se dvě různé
    /// budovy na témž místě (po přestavbě) nechovaly jako jedna — a hlavně aby
    /// se odstíny nezarovnaly podle typu, kdyby se jednou stavělo do řad.</para>
    /// </summary>
    public static Look For(int tileX, int tileY, int defIndex)
    {
        ulong h = Hash(tileX, tileY, defIndex);

        // Posun jen o pixel a jen doprava/dolů: víc už by budovy vylézaly ze
        // svého půdorysu a hráč by je viděl přesahovat přes silnici.
        int offsetX = (int)(h >> 8 & 1);
        int offsetY = (int)(h >> 9 & 1);

        // Zrcadlí se jen menšina — zrcadlená polovina města vypadá jako chyba,
        // občasné převrácení jako nepravidelnost.
        bool mirrored = (h >> 12 & 3) == 0;

        var extra = (h >> 16) % DetailEveryNth == 0
            ? (BuildingExtra)(1 + (int)((h >> 20) % 3))
            : BuildingExtra.None;

        return new Look((int)(h % PaletteCount), mirrored, offsetX, offsetY, extra);
    }

    /// <summary>Nádech střechy pro danou paletu (násobí se přes barvu spritu).</summary>
    public static Color RoofTint(int paletteIndex) => Roofs[Math.Abs(paletteIndex) % PaletteCount];

    /// <summary>
    /// Složí variaci s nádechem prosperity. Obojí je násobič, takže se dá
    /// poskládat — chudá čtvrť zůstane chudá, jen v ní nejsou všechny střechy
    /// stejné.
    /// </summary>
    public static Color Combine(Color prosperityTint, int paletteIndex) =>
        ProsperityLook.Modulate(prosperityTint, RoofTint(paletteIndex));

    /// <summary>Deterministický hash dlaždice a typu (mix konstantami ze SplitMix64).</summary>
    private static ulong Hash(int x, int y, int defIndex)
    {
        ulong h = (ulong)(uint)x * 0x9E3779B97F4A7C15UL
            ^ (ulong)(uint)y * 0xBF58476D1CE4E5B9UL
            ^ (ulong)(uint)defIndex * 0x94D049BB133111EBUL;
        h = (h ^ (h >> 30)) * 0xBF58476D1CE4E5B9UL;
        h = (h ^ (h >> 27)) * 0x94D049BB133111EBUL;
        return h ^ (h >> 31);
    }
}

/// <summary>
/// Přístavek, kterým se jeden dům liší od sousedního. Schválně drobnosti
/// z pár pixelů — je to nepravidelnost, ne ikonka k přečtení.
/// </summary>
public enum BuildingExtra
{
    /// <summary>Nic — většina domů.</summary>
    None = 0,

    /// <summary>Komín na střeše (a v zimě se z něj kouří).</summary>
    Chimney = 1,

    /// <summary>Markýza nad vchodem.</summary>
    Awning = 2,

    /// <summary>Prádlo na šňůře podél zdi.</summary>
    Laundry = 3,
}
