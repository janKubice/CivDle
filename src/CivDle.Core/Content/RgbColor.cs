namespace CivDle.Core.Content;

/// <summary>
/// Barva bez závislosti na render frameworku — Core nesmí znát MonoGame,
/// takže si nese vlastní minimální typ. Render vrstva si ji převede na svou barvu.
/// </summary>
public readonly record struct RgbColor(byte R, byte G, byte B)
{
    /// <summary>
    /// Naparsuje barvu ve formátu <c>#RRGGBB</c> (mřížka volitelná).
    /// Vyhazuje <see cref="FormatException"/> se srozumitelnou hláškou — používá se
    /// při fail-fast validaci obsahu.
    /// </summary>
    public static RgbColor Parse(string value)
    {
        if (!TryParse(value, out var color))
        {
            throw new FormatException($"Neplatná barva '{value}' — očekávám formát '#RRGGBB'.");
        }

        return color;
    }

    /// <summary>Zkusí naparsovat barvu ve formátu <c>#RRGGBB</c>.</summary>
    public static bool TryParse(string? value, out RgbColor color)
    {
        color = default;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var hex = value.Trim();
        if (hex.StartsWith('#'))
        {
            hex = hex[1..];
        }

        if (hex.Length != 6)
        {
            return false;
        }

        if (!byte.TryParse(hex[..2], System.Globalization.NumberStyles.HexNumber, null, out var r) ||
            !byte.TryParse(hex[2..4], System.Globalization.NumberStyles.HexNumber, null, out var g) ||
            !byte.TryParse(hex[4..6], System.Globalization.NumberStyles.HexNumber, null, out var b))
        {
            return false;
        }

        color = new RgbColor(r, g, b);
        return true;
    }
}
