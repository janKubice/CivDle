using System.Globalization;

namespace CivDle.Core.WorldGen;

/// <summary>
/// Převod uživatelského vstupu na seed. Číslo projde beze změny, libovolný text
/// se hashuje (FNV-1a 64) — hash je stabilní napříč verzemi, takže „muj svet"
/// vygeneruje stejnou mapu i za rok.
/// </summary>
public static class SeedUtil
{
    /// <summary>Prázdný vstup → náhodný seed; číslo → hodnota; jiný text → stabilní hash.</summary>
    public static long Parse(string? text)
    {
        var trimmed = text?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return NewRandom();
        }

        if (long.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out var numeric))
        {
            return numeric;
        }

        return Fnv1a64(trimmed);
    }

    /// <summary>
    /// Náhodný seed. Záměrně jen 9 číslic — hráč si ho snadno opíše a nasdílí.
    /// </summary>
    public static long NewRandom() => Random.Shared.Next(1, 1_000_000_000);

    private static long Fnv1a64(string text)
    {
        ulong hash = 14695981039346656037UL;
        foreach (char c in text)
        {
            hash ^= c;
            hash *= 1099511628211UL;
        }

        return unchecked((long)hash);
    }
}
