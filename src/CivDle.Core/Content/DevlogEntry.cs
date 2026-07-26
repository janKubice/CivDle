namespace CivDle.Core.Content;

/// <summary>
/// Jeden záznam vývojového deníku z <c>data/devlog.json</c> — zobrazuje se
/// v rolovacím panelu v menu. Autorský text (nepřekládá se, jako jména osad).
/// </summary>
/// <param name="Version">Označení verze/milníku.</param>
/// <param name="Date">Datum záznamu (volný text).</param>
/// <param name="Lines">Odrážky změn.</param>
public sealed record DevlogEntry(string Id, string Date, int LineCount)
{
    /// <summary>Lokalizační klíč nadpisu vydání.</summary>
    public string TitleKey => $"devlog.{Id}.title";

    /// <summary>Lokalizační klíč n-tého řádku (číslováno od 1).</summary>
    public string LineKey(int index) => $"devlog.{Id}.line{index + 1}";
}
