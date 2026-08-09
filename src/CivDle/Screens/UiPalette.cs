using Microsoft.Xna.Framework;

namespace CivDle.Screens;

/// <summary>
/// Barvy rozhraní. <b>Jedna</b> akcentní a zbytek do šedi podle jasu.
///
/// <para>Proč to vzniklo: obrazovky vznikaly postupně a každá si namíchala
/// vlastní odstíny — jen v nich bylo přes sto devadesát různých barev, mimo
/// jiné patnáct skoro stejných zlatých. Výsledek nevypadá bohatě, ale
/// <b>nesourodě</b>: když je barevné všechno, nevynikne nic a hráč nemá kam
/// dát oči. Rozhraní má být tiché a nechat křičet hru.</para>
///
/// <para>Princip: text a panely žijí v šedé škále a liší se <b>jasem</b>
/// (důležité světlé, vedlejší tmavé). Barva se šetří pro dvě věci — pro
/// akcent (to, co má hráč vidět první) a pro <b>význam</b>.</para>
///
/// <para><b>Významové barvy zůstávají.</b> Zelená „mám na to" a červená
/// „nemám" nejsou ozdoba, ale informace, kterou hráč čte periferně u každé
/// ceny; kdyby zšedly, musel by číst čísla. Jsou proto vědomá výjimka
/// z pravidla, ne jeho porušení — a je jich schválně jen trojice.</para>
///
/// <para>Akcent je studený schválně: svět hry je teplý (hlína, dřevo, noční
/// okna), takže je tyrkysová jediná barva, která se s ním nikdy neslije.
/// Rozhraní tak jde od scény odlišit bez rámečků a stínů.</para>
/// </summary>
internal static class UiPalette
{
    /// <summary>Jediná akcentní barva. To, co má hráč najít první.</summary>
    public static readonly Color Accent = new(96, 196, 220);

    /// <summary>Ztlumený akcent — neaktivní karta, obtažení, podtitulek.</summary>
    public static readonly Color AccentDim = new(64, 132, 150);

    /// <summary>Nejsvětlejší text: nadpisy, hodnoty, to, co se čte jako první.</summary>
    public static readonly Color TextBright = new(236, 238, 242);

    /// <summary>Běžný text.</summary>
    public static readonly Color Text = new(196, 200, 208);

    /// <summary>Vedlejší text: popisky, jednotky, vysvětlivky.</summary>
    public static readonly Color TextDim = new(148, 152, 162);

    /// <summary>Sotva čitelný text: zašedlé, nedostupné, neaktivní.</summary>
    public static readonly Color TextFaint = new(108, 112, 122);

    /// <summary>Výplň panelu.</summary>
    public static readonly Color Panel = new(24, 28, 38, 235);

    /// <summary>Tmavší výplň (vnořené panely, řádky tabulky).</summary>
    public static readonly Color PanelDeep = new(16, 19, 26, 235);

    // Trojice významových barev je od sebe odstupňovaná i JASEM, ne jenom
    // odstínem. Pro hráče s poruchou barvocitu jsou zelená a červená o stejném
    // jasu táž barva — a je to zrovna dvojice, kterou hra používá u cen, tedy
    // u něčoho, co se čte koutkem oka desetkrát za minutu. Jas je to jediné,
    // co jim zbývá, takže: dobrá světlá, výstraha střední, špatná tmavá.

    /// <summary>Význam: povedlo se, mám na to, je hotovo.</summary>
    public static readonly Color Good = new(146, 220, 152);

    /// <summary>Význam: pozor, chybí, blíží se strop.</summary>
    public static readonly Color Warn = new(226, 158, 70);

    /// <summary>Význam: nemám na to, něco se rozbilo.</summary>
    public static readonly Color Bad = new(198, 82, 74);

    /// <summary>Výplň zvýrazněného tlačítka (hlavní akce obrazovky).</summary>
    public static readonly Color PanelAccent = new(38, 78, 92, 240);

    /// <summary>Výplň panelu s dobrou zprávou (dokončeno, splněno).</summary>
    public static readonly Color PanelGood = new(38, 72, 48, 240);

    /// <summary>Výplň panelu se špatnou zprávou (chyba, nedostupné).</summary>
    public static readonly Color PanelBad = new(78, 38, 42, 240);

    /// <summary>
    /// Šedá o daném jasu (0–255). Pro místa, kde je odstínů potřeba víc než
    /// čtyři pojmenované — třeba u pruhů grafu.
    /// </summary>
    public static Color Tone(int brightness)
    {
        byte value = (byte)Math.Clamp(brightness, 0, 255);

        // Ne dokonale neutrální: špetka modré drží rozhraní studené proti teplé
        // scéně. Čistá šedá vedle hnědé krajiny působí špinavě.
        return new Color(value, value, (byte)Math.Min(255, value + 8));
    }
}
