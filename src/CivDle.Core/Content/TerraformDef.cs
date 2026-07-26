namespace CivDle.Core.Content;

/// <summary>
/// Jeden způsob, jak hráč přetvoří krajinu (<c>data/terraform.json</c>). Sázení
/// stromů bylo jediné, čím šlo do mapy zasáhnout — tohle z toho dělá plnohodnotný
/// nástroj: zavlažit poušť, vysušit bažinu, srovnat skály na pláň.
///
/// <para>Odemyká se technologií, takže terraformace patří až k vyspělé civilizaci
/// a nerozbije ranou hru. Jméno a popis v jazycích pod <c>terraform.&lt;Id&gt;</c>.</para>
/// </summary>
/// <param name="Id">Stabilní ID.</param>
/// <param name="TargetBiomeIndex">Na jaký biom se dlaždice promění.</param>
/// <param name="SourceBiomeIndices">Z jakých biomů to jde; prázdné = z jakéhokoli.</param>
/// <param name="Cost">Cena za jednu dlaždici.</param>
/// <param name="UnlockTechIndex">Technologie, která nástroj odemkne; −1 = od začátku.</param>
public sealed record TerraformDef(
    string Id,
    int TargetBiomeIndex,
    IReadOnlyList<int> SourceBiomeIndices,
    IReadOnlyList<ResourceAmount> Cost,
    int UnlockTechIndex)
{
    /// <summary>Lokalizační klíč jména nástroje.</summary>
    public string NameKey => $"terraform.{Id}";

    /// <summary>Lokalizační klíč popisu nástroje.</summary>
    public string DescriptionKey => $"terraform.{Id}.desc";

    /// <summary>Lze tenhle nástroj použít na dlaždici s daným biomem?</summary>
    public bool AppliesTo(int biomeIndex) =>
        SourceBiomeIndices.Count == 0 || SourceBiomeIndices.Contains(biomeIndex);
}
