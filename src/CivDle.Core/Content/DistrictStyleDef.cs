namespace CivDle.Core.Content;

/// <summary>
/// Styl čtvrti: jak čtvrť daného druhu vypadá — z výšky (barva na mapě
/// hustoty), zblízka (nádech střech a zdí) a v noci (barva světel).
///
/// <para><b>Proč:</b> velké město z výšky byla béžová šachovnice. Styl je
/// kosmetika, ale z plochy dělá „moje město": hráč si řekne, že obytné
/// čtvrti budou srubové a průmysl cihlový, a pozná je z oběžné dráhy
/// (endgame.md, B4). Některé styly jsou odměny výzev a Velkých cílů.</para>
///
/// <para>Přiřazuje se <b>druhu</b> čtvrti, ne jednotlivé čtvrti: čtvrti
/// se hledají znovu, jak město roste, a jednotlivá by svůj styl ztrácela
/// pokaždé, když se rozdělí nebo spojí.</para>
/// </summary>
/// <param name="Id">Stabilní ID (lokalizace <c>districtStyle.&lt;id&gt;</c>, save).</param>
/// <param name="MapColor">Barva čtvrti na mapě z výšky.</param>
/// <param name="Tint">Nádech spritů budov zblízka (násobí se s jejich barvou).</param>
/// <param name="NightColor">Barva nočních světel; <c>null</c> = výchozí teplé světlo.</param>
/// <param name="UnlockedBy">Odměna (<c>challenge:&lt;id&gt;</c>, <c>quest:&lt;id&gt;</c>); <c>null</c> = dostupný hned.</param>
/// <param name="TypeIndices">Druhy čtvrtí, kterým se smí přiřadit; prázdné = všem.</param>
public sealed record DistrictStyleDef(
    string Id,
    RgbColor MapColor,
    RgbColor Tint,
    RgbColor? NightColor,
    string? UnlockedBy,
    IReadOnlyList<int> TypeIndices)
{
    /// <summary>Lokalizační klíč jména.</summary>
    public string NameKey => $"districtStyle.{Id}";

    /// <summary>Smí se styl dát tomuhle druhu čtvrti?</summary>
    public bool FitsType(int typeIndex) => TypeIndices.Count == 0 || TypeIndices.Contains(typeIndex);
}
