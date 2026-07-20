namespace CivDle.Core.Content;

/// <summary>
/// Definice suroviny z <c>data/resources.json</c>. Zásoby v simulaci jsou pole
/// indexované indexem suroviny, ne slovník podle ID.
/// </summary>
/// <param name="Id">Stabilní ID (odkazují na něj budovy a gameplay config).</param>
/// <param name="MapColor">Barva ikony v HUD (MVP — později ikony z atlasu).</param>
/// <param name="StartAmount">Počáteční zásoba při nové hře.</param>
public sealed record Resource(string Id, RgbColor MapColor, double StartAmount)
{
    /// <summary>Lokalizační klíč jména suroviny.</summary>
    public string NameKey => $"resource.{Id}";
}
