namespace CivDle.Core.Config;

/// <summary>
/// Jak dlouho při oddalování vydrží detaily (LOD).
///
/// <para>Hra běží od jednoho domku po aglomeraci přes celou obrazovku a jeden
/// pevný práh nemůže vyhovět všem: na slabším stroji je i vyvážené nastavení
/// při pohledu na velké město trhané, na silném naopak hráče mrzí, že mu
/// chodci a stromy zmizí dřív, než by museli.</para>
///
/// <para>Nastavení nemění, <b>co</b> se kreslí zblízka — mění <b>kdy</b> se
/// jednotlivé vrstvy při oddálení vzdají. Simulace o něm neví; je to čistě
/// věc renderu.</para>
/// </summary>
public enum DetailQuality
{
    /// <summary>Detaily mizí nejdřív. Pro slabší stroje a velká města.</summary>
    Performance,

    /// <summary>Výchozí kompromis — prahy, se kterými se hra ladila.</summary>
    Balanced,

    /// <summary>Detaily vydrží déle. Pro pohodlný stroj.</summary>
    Detailed,

    /// <summary>Detaily vydrží skoro až k agregátnímu pohledu. Žere výkon.</summary>
    Maximum,
}
