namespace CivDle.Core.Sim;

/// <summary>
/// Co je v jednom sídle vidět z výšky: kolik se tam vejde lidí, kolik je práce,
/// kolik služeb a co se tam hlavně vyrábí.
///
/// <para>Je to <b>odvozený pohled</b>, ne stav světa — spočítá se, když se na
/// něj někdo zeptá, a nikam se neukládá. Populace je v téhle hře agregát pro
/// celou říši (viz CLAUDE.md) a rozpočítávat ji na sídla by znamenalo předstírat
/// přesnost, kterou simulace nemá; místo ní se ukazuje <see cref="Housing"/>,
/// tedy kolik lidí by se tam vešlo.</para>
/// </summary>
/// <param name="NameIndex">Jméno sídla (klíč i pro jeho vlastní plán).</param>
/// <param name="Buildings">Kolik budov k sídlu patří.</param>
/// <param name="Housing">Kolik lidí se do něj vejde.</param>
/// <param name="Jobs">Kolik je v něm pracovních míst.</param>
/// <param name="Services">Součet služeb (parky, trhy, chrámy).</param>
/// <param name="TopResourceIndex">Nejčastěji vyráběná surovina; −1 = nic se tam nevyrábí.</param>
public readonly record struct SettlementStat(
    int NameIndex,
    int Buildings,
    double Housing,
    int Jobs,
    int Services,
    int TopResourceIndex);
