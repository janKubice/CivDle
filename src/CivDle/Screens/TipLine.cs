using Microsoft.Xna.Framework;

namespace CivDle.Screens;

/// <summary>
/// Druh informace v bublině u kurzoru.
///
/// <para>Popisek budovy má osm různých druhů řádků — cena, výroba, spotřeba,
/// lidé, sklad, proud, omezení, rada. Všechny byly stejně bílé, takže hráč
/// musel u každé budovy číst celý blok znovu a hledat v něm, co ho zajímá.
/// S barvou stačí oko: cena je vždycky zlatá, výroba vždycky zelená.</para>
/// </summary>
public enum TipKind
{
    /// <summary>Bez zařazení — nadpis, poznámka, cokoli neoznačeného.</summary>
    Plain = 0,

    /// <summary>Co to stojí postavit.</summary>
    Cost,

    /// <summary>Co budova vyrábí.</summary>
    Produces,

    /// <summary>Co budova spotřebovává (vstupy i údržba).</summary>
    Consumes,

    /// <summary>Lidé: bydlení, pracovní místa, obsloužení obyvatelé.</summary>
    People,

    /// <summary>Kolik místa přidá do skladu.</summary>
    Storage,

    /// <summary>Elektřina — dodává i spotřebovává.</summary>
    Power,

    /// <summary>Kde a za jakých podmínek smí stát.</summary>
    Limit,

    /// <summary>Rada nebo vysvětlení mechaniky.</summary>
    Hint,
}

/// <summary>
/// Značkování řádků bubliny.
///
/// <para>Myra umí u widgetu jen <b>jeden</b> řetězec bez formátování, takže se
/// barva do textu musí dostat jinak. Řádek si proto nese na začátku
/// neviditelnou značku, kterou <see cref="RichTooltip"/> odloupne a přeloží na
/// barvu. Značka je řídicí znak, ne písmeno — do překladů se nikdy nedostane,
/// a kdyby přece jen prosákla ven, nic nerozbije: nevykreslí se.</para>
///
/// <para>Vrstva: UI. Skládá text, nic nerozhoduje.</para>
/// </summary>
internal static class TipLine
{
    /// <summary>Řídicí znak, kterým každá značka začíná.</summary>
    private const char Marker = '\u0001';

    /// <summary>Označí řádek druhem informace.</summary>
    public static string Tag(TipKind kind, string text) => $"{Marker}{(char)('0' + (int)kind)}{text}";

    /// <summary>
    /// Odloupne značku a vrátí, o jaký druh šlo. Neoznačený řádek je
    /// <see cref="TipKind.Plain"/> — starý kód i mody tak fungují dál.
    /// </summary>
    public static TipKind Split(string line, out string text)
    {
        if (line.Length >= 2 && line[0] == Marker)
        {
            int kind = line[1] - '0';
            if (kind >= 0 && kind <= (int)TipKind.Hint)
            {
                text = line[2..];
                return (TipKind)kind;
            }
        }

        text = line;
        return TipKind.Plain;
    }

    /// <summary>
    /// Barva pro druh informace. Jedno místo pro celou hru — kdyby si ji každá
    /// obrazovka volila sama, rozešly by se a barevné třídění by přestalo dávat
    /// smysl.
    /// </summary>
    public static Color ColorFor(TipKind kind) => kind switch
    {
        TipKind.Cost => new Color(232, 196, 108),      // zlatá: co to stojí
        TipKind.Produces => UiPalette.Good,            // zelená: co z toho leze
        TipKind.Consumes => new Color(226, 158, 70),   // oranžová: co to spotřebuje
        TipKind.People => new Color(132, 186, 236),    // modrá: lidé
        TipKind.Storage => new Color(186, 158, 232),   // fialová: místo
        TipKind.Power => new Color(240, 220, 120),     // žlutá: proud
        TipKind.Limit => UiPalette.TextDim,            // šedá: kde to smí stát
        TipKind.Hint => UiPalette.TextFaint,           // nejtišší: rada
        _ => UiPalette.TextBright,
    };

    /// <summary>
    /// Odstraní značky z textu — pro místa, kde barva k dispozici není
    /// (jednoduchý štítek, sdílená karta, test).
    /// </summary>
    public static string Strip(string text)
    {
        if (!text.Contains(Marker))
        {
            return text;
        }

        var lines = text.Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            Split(lines[i], out lines[i]);
        }

        return string.Join('\n', lines);
    }
}
