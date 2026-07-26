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

    /// <summary>Budova není odemčená (chybí technologie) nebo se nedá stavět přímo.</summary>
    NotUnlocked,

    /// <summary>Budova potřebuje sousedit s vodou (přístav, rybolov), ale břeh tam není.</summary>
    NeedsWaterAccess,
}
