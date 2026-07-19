namespace CivDle.Core.Content;

/// <summary>
/// Zvalidovaná definice biomu (typ, ne instance — viz data-driven-content.md).
/// Neměnný record; instance na mapě na něj odkazují přes index v <see cref="BiomeRegistry"/>.
/// </summary>
/// <param name="Id">Stabilní ID (jednou v savech, nikdy nepřejmenovávat).</param>
/// <param name="Name">Jméno pro hráče (může se měnit).</param>
/// <param name="MapColor">Barva dlaždice na mapě — MVP vizuál, později nahradí sprity z atlasu.</param>
/// <param name="ColorVariation">Jemné kolísání jasu dlaždic ±, anti-repetice (living-map.md, sekce 6).</param>
/// <param name="IsWater">Vodní biomy se vybírají podle hloubky, pevninské podle výšky a vlhkosti.</param>
/// <param name="DepthRange">Jen voda: normalizovaná hloubka pod hladinou 0–1.</param>
/// <param name="ElevationRange">Jen pevnina: normalizovaná výška nad hladinou 0–1.</param>
/// <param name="MoistureRange">Jen pevnina: vlhkost 0–1 (chybí-li v datech, platí celý rozsah).</param>
public sealed record Biome(
    string Id,
    string Name,
    RgbColor MapColor,
    float ColorVariation,
    bool IsWater,
    ValueRange DepthRange,
    ValueRange ElevationRange,
    ValueRange MoistureRange);
