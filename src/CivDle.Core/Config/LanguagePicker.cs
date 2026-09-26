using System.Globalization;

namespace CivDle.Core.Config;

/// <summary>
/// Jazyk hry při prvním spuštění: podle jazyka systému, jinak angličtina.
///
/// <para><b>Proč:</b> výchozí jazyk byl natvrdo čeština. Kdo hru stáhl kdekoli
/// jinde, viděl první obrazovku v jazyce, kterému nerozumí — a to je nejrychlejší
/// cesta, jak hru za pár vteřin zavřít. Nastavení si hráč pořád může změnit;
/// tohle rozhoduje jen o tom, co uvidí jako první.</para>
///
/// <para>Steam jazyk (<c>GetCurrentGameLanguage</c>) se přidá se Steamem — tady
/// se rozhoduje jen z kultury systému, ať to funguje i bez něj.</para>
/// </summary>
public static class LanguagePicker
{
    /// <summary>Záloha, když jazyk systému hra nemá — světový jazyk, ne jazyk vývojáře.</summary>
    public const string Fallback = "en";

    /// <summary>
    /// Vybere z nabízených jazyků ten, který odpovídá kultuře (i přes nadřazené
    /// kultury: „de-AT" → „de"). Když žádný nesedí, angličtina; když ani ta
    /// není, první nabízený.
    /// </summary>
    /// <param name="available">ID jazyků z <c>data/lang</c>.</param>
    /// <param name="culture">Kultura systému (typicky <see cref="CultureInfo.CurrentUICulture"/>).</param>
    public static string Pick(IReadOnlyList<string> available, CultureInfo culture)
    {
        if (available.Count == 0)
        {
            return Fallback;
        }

        for (var current = culture; !string.IsNullOrEmpty(current.Name); current = current.Parent)
        {
            string code = current.TwoLetterISOLanguageName;
            foreach (string id in available)
            {
                if (string.Equals(id, code, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(id, current.Name, StringComparison.OrdinalIgnoreCase))
                {
                    return id;
                }
            }
        }

        foreach (string id in available)
        {
            if (string.Equals(id, Fallback, StringComparison.OrdinalIgnoreCase))
            {
                return id;
            }
        }

        return available[0];
    }
}
