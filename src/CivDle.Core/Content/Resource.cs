namespace CivDle.Core.Content;

/// <summary>
/// Definice suroviny z <c>data/resources.json</c>. Zásoby v simulaci jsou pole
/// indexované indexem suroviny, ne slovník podle ID.
/// </summary>
/// <param name="Id">Stabilní ID (odkazují na něj budovy a gameplay config).</param>
/// <param name="MapColor">Barva ikony v HUD (MVP — později ikony z atlasu).</param>
/// <param name="StartAmount">Počáteční zásoba při nové hře.</param>
/// <param name="BaseStorage">Kapacita skladu bez skladových budov (mvp-roadmap fáze 3).</param>
/// <param name="ImportOnly">
/// Svět ji nevyrábí ani netěží — přiváží ji obchodní trasa z jiného světa
/// (sklo a koření na Domovině). Kontroly obsahu ji proto nehlásí jako slepou
/// uličku; recept, který na ni čeká, bez dovozu prostě stojí.
/// </param>
public sealed record Resource(string Id, RgbColor MapColor, double StartAmount, double BaseStorage, bool ImportOnly = false)
{
    /// <summary>Lokalizační klíč jména suroviny.</summary>
    public string NameKey => $"resource.{Id}";
}
