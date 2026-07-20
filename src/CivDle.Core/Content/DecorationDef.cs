namespace CivDle.Core.Content;

/// <summary>
/// Biomová dekorace z <c>data/decorations.json</c> (living-map.md: anti-repetice) —
/// kytky, keře, kameny… Čistě vizuální vrstva: render je rozmisťuje deterministicky
/// hashem dlaždice, simulace o nich neví a nic se neukládá.
/// </summary>
/// <param name="Id">Stabilní ID.</param>
/// <param name="BiomeMask">Na kterých biomech se objevuje (indexováno indexem biomu).</param>
/// <param name="Colors">Varianty barev — střídají se, ať prvek nevypadá dvakrát stejně.</param>
/// <param name="Density">Pravděpodobnost výskytu na dlaždici (0–1).</param>
/// <param name="MinSize">Nejmenší velikost v pixelech dlaždice.</param>
/// <param name="MaxSize">Největší velikost v pixelech dlaždice.</param>
public sealed record DecorationDef(
    string Id,
    bool[] BiomeMask,
    IReadOnlyList<RgbColor> Colors,
    float Density,
    int MinSize,
    int MaxSize);
