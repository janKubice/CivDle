namespace CivDle.Core.Content;

/// <summary>
/// Jeden záznam vývojového deníku z <c>data/devlog.json</c> — zobrazuje se
/// v rolovacím panelu v menu. Autorský text (nepřekládá se, jako jména osad).
/// </summary>
/// <param name="Version">Označení verze/milníku.</param>
/// <param name="Date">Datum záznamu (volný text).</param>
/// <param name="Lines">Odrážky změn.</param>
public sealed record DevlogEntry(string Version, string Date, IReadOnlyList<string> Lines);
