namespace CivDle.Core.Content;

/// <summary>
/// Typ zóny z <c>data/zones.json</c> (automatizace, stupeň 3 dle living-city.md).
/// Hráč maluje obdélníkové zóny daného typu a systém je sám zaplňuje vhodnými
/// budovami — designace záměru („sem patří bydlení/výroba/pole"), o zbytek se
/// stará <see cref="Sim.ZoneFillSystem"/>.
///
/// <para><see cref="BuildingIndices"/> je priorita: fill zkouší budovy v pořadí a
/// položí první, která na daném místě sedí (biom, místo, suroviny). Odkazy na
/// budovy se překládají z ID na index při načtení (data-driven: instance = index).</para>
/// Jméno v jazycích pod <c>zone.&lt;Id&gt;</c>.
/// </summary>
/// <param name="Id">Stabilní ID.</param>
/// <param name="MapColor">Zabarvení zóny při vykreslení (jemný tint přes dlaždice).</param>
/// <param name="BuildingIndices">Budovy, kterými se zóna zaplňuje, v pořadí priority.</param>
public sealed record ZoneTypeDef(string Id, RgbColor MapColor, IReadOnlyList<int> BuildingIndices)
{
    /// <summary>Lokalizační klíč jména typu zóny.</summary>
    public string NameKey => $"zone.{Id}";
}
