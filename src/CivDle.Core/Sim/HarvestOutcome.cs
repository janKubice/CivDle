namespace CivDle.Core.Sim;

/// <summary>
/// Jak dopadl jeden ruční sběr. UI podle toho volí efekt: obyčejný sběr je tichý,
/// krit má malou jiskru, „úlovek života" (velryba v moři, obří žíla v horách)
/// zaslouží velkou oslavu — proto to není jen <c>bool</c>.
/// </summary>
public enum HarvestOutcome
{
    /// <summary>Běžný výnos.</summary>
    Normal,

    /// <summary>Krit — několikanásobný výnos (aktivní klikání se vyplácí).</summary>
    Crit,

    /// <summary>Úlovek života — vzácný obří výnos, odemyká se přes Vzestup.</summary>
    Jackpot,
}
