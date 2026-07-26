namespace CivDle.Core.Content;

/// <summary>
/// Počasí z <c>data/weather.json</c> (living-map.md §2). Je vázané na biom —
/// každý má svoje, což posiluje jeho identitu.
///
/// <para>Dvě úrovně: <b>ambientní</b> (déšť, mlha, žár) je čistá atmosféra
/// (<see cref="ProductionMult"/> = 1), <b>extrémní</b> (tornádo, vánice, písečná
/// bouře) je zároveň událost, která DOČASNĚ sníží flow výroby — a nikdy nic
/// nezničí (soft pressure, konzistentní s anti-frustrací celé hry).</para>
///
/// Jméno v jazycích pod <c>weather.&lt;Id&gt;</c>.
/// </summary>
/// <param name="Id">Stabilní ID.</param>
/// <param name="BiomeMask">Ve kterých biomech se počasí vyskytuje (index = biom).</param>
/// <param name="Extreme">Je to extrémní jev (katastrofa), nebo jen atmosféra?</param>
/// <param name="ProductionMult">Násobič výroby po dobu trvání (1 = bez vlivu).</param>
/// <param name="DurationSeconds">Jak dlouho jev trvá.</param>
/// <param name="Weight">Relativní četnost výskytu mezi ostatními jevy daného biomu.</param>
/// <param name="TintColor">Barva závoje přes scénu (render).</param>
/// <param name="TintAlpha">Síla závoje 0–1.</param>
/// <param name="Particle">ID částicového efektu pro render („rain", „snow", „sand", „none").</param>
public sealed record WeatherDef(
    string Id,
    IReadOnlyList<bool> BiomeMask,
    bool Extreme,
    double ProductionMult,
    double DurationSeconds,
    double Weight,
    RgbColor TintColor,
    double TintAlpha,
    string Particle)
{
    /// <summary>Lokalizační klíč jména počasí.</summary>
    public string NameKey => $"weather.{Id}";

    /// <summary>Vyskytuje se tenhle jev v daném biomu?</summary>
    public bool AppliesTo(int biomeIndex) =>
        biomeIndex >= 0 && biomeIndex < BiomeMask.Count && BiomeMask[biomeIndex];
}
