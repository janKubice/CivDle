namespace CivDle.Core.Content;

/// <summary>
/// Chyba při načítání herního obsahu. Vyhazuje se okamžitě při startu (fail-fast),
/// aby chybný odkaz v datech nespadl až za hodinu hraní — viz data-driven-content.md.
/// </summary>
public sealed class ContentLoadException : Exception
{
    /// <summary>Soubor, ve kterém byla chyba nalezena.</summary>
    public string File { get; }

    public ContentLoadException(string file, string message)
        : base($"Chyba v herních datech '{System.IO.Path.GetFileName(file)}': {message}")
    {
        File = file;
    }
}
