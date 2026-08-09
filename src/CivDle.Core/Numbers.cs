using System.Globalization;

namespace CivDle.Core;

/// <summary>
/// Formátování velkých čísel pro late-game měřítko (miliony → miliardy → …).
/// Zůstáváme u <c>double</c> (žádný přepis hot-path), jen krátký zápis se
/// suffixy K/M/B/T/… až po dvojpísmenné (aa, ab…). Deterministické, bez alokací
/// mimo výsledný string. Používá invariantní kulturu (tečka desetinná).
/// </summary>
public static class Numbers
{
    // Krátké suffixy: 10^3, 10^6, … pak dvojpísmenné aa=10^18, ab=10^21, …
    private static readonly string[] ShortSuffixes = { "", "K", "M", "B", "T" };

    /// <summary>Krátký zápis (např. 1234 → „1.2K", 2_500_000 → „2.5M"). Malé hodnoty jako celé číslo.</summary>
    public static string Format(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            return "0";
        }

        double abs = Math.Abs(value);
        if (abs < 1000)
        {
            return ((long)value).ToString(CultureInfo.InvariantCulture);
        }

        int magnitude = (int)Math.Floor(Math.Log10(abs) / 3);
        double scaled = value / Math.Pow(1000, magnitude);
        // Přetečení mohlo zaokrouhlit na 1000.0 → posuň o řád výš.
        if (Math.Abs(scaled) >= 1000 && magnitude < int.MaxValue - 1)
        {
            magnitude++;
            scaled = value / Math.Pow(1000, magnitude);
        }

        string suffix = SuffixFor(magnitude);
        // 2–3 platné číslice: <10 → 2 desetiny, <100 → 1, jinak 0.
        string number = Math.Abs(scaled) < 10
            ? scaled.ToString("0.00", CultureInfo.InvariantCulture)
            : Math.Abs(scaled) < 100
                ? scaled.ToString("0.0", CultureInfo.InvariantCulture)
                : scaled.ToString("0", CultureInfo.InvariantCulture);

        return number + suffix;
    }

    /// <summary>
    /// Bezpečný převod na <c>long</c> pro metriky, savy a rekordy.
    ///
    /// <para>Hodnoty v pozdní hře přerostou i <c>long</c> (kapacita bydlení
    /// s vymaxovaným Vzestupem jde do 10^16 a výš). Přímé přetypování by
    /// v takovém případě dalo <b>zápornou</b> hodnotu a cíl nebo achievement by
    /// se tvářil jako nesplněný. Ořez na kraj rozsahu je jediná odpověď, která
    /// hráče nezmate.</para>
    /// </summary>
    public static long ToLong(double value)
    {
        if (double.IsNaN(value))
        {
            return 0;
        }

        if (value >= long.MaxValue)
        {
            return long.MaxValue;
        }

        return value <= long.MinValue ? long.MinValue : (long)value;
    }

    /// <summary>Zápis „aktuální / kapacita" krátce (HUD surovin).</summary>
    public static string FormatRatio(double current, double capacity) => $"{Format(current)}/{Format(capacity)}";

    private static string SuffixFor(int magnitude)
    {
        if (magnitude < ShortSuffixes.Length)
        {
            return ShortSuffixes[magnitude];
        }

        // Dvojpísmenné: aa, ab, …, az, ba, … (magnitude 5 = aa = 10^15).
        int index = magnitude - ShortSuffixes.Length; // 0 = aa
        char first = (char)('a' + index / 26 % 26);
        char second = (char)('a' + index % 26);
        return $"{first}{second}";
    }
}
