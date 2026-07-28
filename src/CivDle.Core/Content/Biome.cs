namespace CivDle.Core.Content;

/// <summary>
/// Výnos ručního kliknutí na dlaždici biomu („klik na strom → dřevo",
/// mvp-roadmap.md fáze 1). Surovina jako index, ne string (hot path).
///
/// <para><b>Uzel se vytěží.</b> Do téhle chvíle byl každý strom nekonečný, takže
/// hráč neměl důvod se hnout z místa. Teď má uzel omezený počet sběrů a po
/// vytěžení chvíli dorůstá — z krajiny se tím stává něco, s čím se hospodaří:
/// vyplatí se expandovat, ale i sázet háje a nechat les vydechnout.</para>
/// </summary>
/// <param name="ResourceIndex">Kterou surovinu uzel dává.</param>
/// <param name="Amount">Kolik dá jeden sběr.</param>
/// <param name="Charges">Kolik sběrů uzel vydrží, než zmizí. 0 = nevyčerpatelný (staré chování).</param>
/// <param name="RegrowSeconds">Za jak dlouho doroste. 0 = nedoroste nikdy (ložiska rud).</param>
public sealed record ClickYield(int ResourceIndex, int Amount, int Charges = 0, double RegrowSeconds = 0)
{
    /// <summary>Dá se uzel vytěžit do zmizení?</summary>
    public bool IsExhaustible => Charges > 0;

    /// <summary>Vrátí se uzel po čase sám?</summary>
    public bool IsRenewable => IsExhaustible && RegrowSeconds > 0;

    /// <summary>Doba dorůstání v ticích simulace.</summary>
    public long RegrowTicks => (long)Math.Round(RegrowSeconds * Sim.Simulation.TicksPerSecond);
}

/// <summary>
/// Zvalidovaná definice biomu (typ, ne instance — viz data-driven-content.md).
/// Neměnný record; instance na mapě na něj odkazují přes index v <see cref="BiomeRegistry"/>.
/// Jméno pro hráče není součástí definice — překlady žijí v jazykových souborech
/// pod klíčem <c>biome.&lt;Id&gt;</c> (multilanguage).
/// </summary>
/// <param name="Id">Stabilní ID (jednou v savech, nikdy nepřejmenovávat).</param>
/// <param name="MapColor">Barva dlaždice na mapě — MVP vizuál, později nahradí sprity z atlasu.</param>
/// <param name="ColorVariation">Jemné kolísání jasu dlaždic ±, anti-repetice (living-map.md, sekce 6).</param>
/// <param name="IsWater">Vodní biomy se vybírají podle hloubky, pevninské podle výšky a vlhkosti.</param>
/// <param name="DepthRange">Jen voda: normalizovaná hloubka pod hladinou 0–1.</param>
/// <param name="ElevationRange">Jen pevnina: normalizovaná výška nad hladinou 0–1.</param>
/// <param name="MoistureRange">Jen pevnina: vlhkost 0–1 (chybí-li v datech, platí celý rozsah).</param>
/// <param name="TemperatureRange">
/// Teplota 0–1 (0 = polární, 1 = rovníková). Chybí-li v datech, platí celý rozsah —
/// biom je pak klimaticky univerzální. Díky téhle vrstvě má mapa pásma: sever mrzne,
/// rovník je horký, a stejná výška × vlhkost dá jinou krajinu podle zeměpisné šířky.
/// </param>
/// <param name="ClickYield">Co dá ruční klik na dlaždici; <c>null</c> = nic.</param>
public sealed record Biome(
    string Id,
    RgbColor MapColor,
    float ColorVariation,
    bool IsWater,
    ValueRange DepthRange,
    ValueRange ElevationRange,
    ValueRange MoistureRange,
    ValueRange TemperatureRange,
    ClickYield? ClickYield = null,
    double ProductionMult = 1.0)
{
    /// <summary>
    /// Násobič výroby budov stojících na tomhle biomu (living-map.md §5 — biomy
    /// nejsou jen jiná grafika, mají jinou ekonomiku). 1.0 = neutrální.
    /// </summary>
    public double Production => ProductionMult;

    /// <summary>Lokalizační klíč jména biomu (existence ve všech jazycích je validovaná při startu).</summary>
    public string NameKey => $"biome.{Id}";
}
