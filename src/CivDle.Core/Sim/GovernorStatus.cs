namespace CivDle.Core.Sim;

/// <summary>Co guvernér zrovna dělá.</summary>
public enum GovernorActivity
{
    /// <summary>Město nic nepotřebuje (nebo guvernér ještě nezačal).</summary>
    Idle,

    /// <summary>V posledním kole něco postavil nebo přestěhoval.</summary>
    Building,

    /// <summary>Šetří materiál na stavbu, na kterou zatím nemá.</summary>
    Saving,

    /// <summary>Něco potřebuje, ale neví jak — hráč by měl zasáhnout.</summary>
    Stuck,
}

/// <summary>Proč guvernér uvízl.</summary>
public enum GovernorBlocker
{
    /// <summary>Nic mu nebrání.</summary>
    None,

    /// <summary>Chybí surovina a žádná dostupná budova ji neumí vyrobit (ani přes řetěz).</summary>
    NoProducer,

    /// <summary>Budovu nemá kam postavit (není les, skála, pláž nebo volné místo).</summary>
    NoSite,

    /// <summary>Výrobny stojí, protože chybějí lidé — další budova by nepomohla.</summary>
    NeedsPeople,

    /// <summary>Surovina teče tak pomalu, že šetření trvá neúměrně dlouho.</summary>
    SlowSupply,

    /// <summary>
    /// Na výrobnu suroviny je potřeba právě ta surovina, a ta neteče (dřevo na
    /// dřevorubce). Automatika se z toho sama nedostane — trochu musí nasbírat hráč.
    /// </summary>
    Bootstrap,
}

/// <summary>
/// Stav guvernéra pro UI: co dělá a proč případně stojí.
///
/// <para>Proč to existuje: guvernér dřív uvízl potichu. Město stálo na stropu
/// bydlení a hráč neměl jak zjistit, jestli se čeká na dřevo, na lidi, nebo
/// jestli prostě není kam stavět. Tohle je ta odpověď — a z ní i hlášení.</para>
/// </summary>
/// <param name="Activity">Co dělá.</param>
/// <param name="Blocker">Proč stojí (jen u <see cref="GovernorActivity.Stuck"/>).</param>
/// <param name="DefIndex">Budova, o kterou jde (na co šetří / kam nemá místo); −1 = žádná.</param>
/// <param name="ResourceIndex">Surovina, která chybí; −1 = žádná.</param>
public readonly record struct GovernorStatus(
    GovernorActivity Activity, GovernorBlocker Blocker, int DefIndex, int ResourceIndex)
{
    /// <summary>Nic se neděje.</summary>
    public static GovernorStatus Idle { get; } = new(GovernorActivity.Idle, GovernorBlocker.None, -1, -1);
}
