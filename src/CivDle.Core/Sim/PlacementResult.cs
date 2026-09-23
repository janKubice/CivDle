namespace CivDle.Core.Sim;

/// <summary>Výsledek kontroly umístění budovy — UI si z něj odvodí lokalizovanou hlášku.</summary>
public enum PlacementResult
{
    /// <summary>Lze postavit.</summary>
    Ok,

    /// <summary>Část půdorysu leží mimo mapu.</summary>
    OutOfBounds,

    /// <summary>Na některé dlaždici už něco stojí.</summary>
    Occupied,

    /// <summary>Biom pod budovou není v 'allowedBiomes'.</summary>
    WrongBiome,

    /// <summary>Nedostatek surovin na cenu stavby.</summary>
    NotEnoughResources,

    /// <summary>
    /// Cena je vyšší, než se vůbec vejde do skladu — našetřit se na ni nedá,
    /// ať se čeká jak dlouho chce. Řeší se sklad, ne trpělivost. (Zatím jen
    /// u výzkumu, kde cena roste s každou hotovou technologií.)
    /// </summary>
    ExceedsStorage,

    /// <summary>Budova není odemčená (chybí technologie) nebo se nedá stavět přímo.</summary>
    NotUnlocked,

    /// <summary>Budova potřebuje sousedit s vodou (přístav, rybolov), ale břeh tam není.</summary>
    NeedsWaterAccess,

    /// <summary>
    /// Obranná věž ve hře, kde se nikdo nebrání. Není to chyba hráče — je to
    /// budova, která má smysl jen ve volitelném režimu obrany.
    /// </summary>
    NeedsDefenceMode,

    /// <summary>
    /// Podmořská stavba je mimo dosah přístavu — moře tam sice je, ale nikdo ho
    /// nezásobuje. Řeší se přístavem blíž, ne jiným místem na dně.
    /// </summary>
    NoSubseaLink,

    /// <summary>
    /// Sídlo v okolí je na tuhle stavbu moc malé (letiště nepatří do osady o třech
    /// chalupách). Roste s hierarchií sídel — stačí kolem dostavět.
    /// </summary>
    SettlementTooSmall,
}
