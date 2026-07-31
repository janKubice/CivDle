namespace CivDle.Core.Content;

/// <summary>
/// Druh čtvrti z <c>data/districts.json</c>: co ji tvoří, co za ni město dostane
/// a co ho to stojí.
///
/// <para>Proč to ve hře je (living-city.md §5): shlukovat budovy stejného druhu
/// bylo do téhle chvíle čistě kosmetické rozhodnutí. Se čtvrtěmi má obojí
/// stránku — pět továren vedle sebe vyrábí líp, ale taky víc dýmá, takže si
/// řeknou o park nebo čističku. Shlukování se tím mění z estetiky v rozhodnutí
/// a mapa dostává místa se jménem místo anonymní kaše budov.</para>
/// </summary>
/// <param name="Id">Stabilní ID (do lokalizace).</param>
/// <param name="Categories">Kategorie budov, které se do téhle čtvrti počítají.</param>
/// <param name="MinBuildings">Od kolika budov se shluk pozná jako čtvrť.</param>
/// <param name="ClusterDistance">Největší mezera mezi budovami jednoho shluku (dlaždice).</param>
/// <param name="SynergyPerBuilding">O kolik zvedne výrobu každá další budova ve čtvrti.</param>
/// <param name="SynergyMax">Strop bonusu — bez něj by se vyplatilo stavět jen jeden obří blok.</param>
/// <param name="PollutionMult">
/// Čím čtvrť násobí znečištění svých členů. 1.0 = bez dopadu, &gt;1 = stinná
/// stránka soustředěného průmyslu. To je ta druhá strana synergie: bonus není zadarmo.
/// </param>
/// <param name="MapColor">Barva jemného zabarvení země pod čtvrtí.</param>
public sealed record DistrictTypeDef(
    string Id,
    IReadOnlyList<string> Categories,
    int MinBuildings,
    int ClusterDistance,
    double SynergyPerBuilding,
    double SynergyMax,
    double PollutionMult,
    RgbColor MapColor)
{
    /// <summary>Lokalizační klíč jména čtvrti.</summary>
    public string NameKey => $"district.{Id}";

    /// <summary>Patří budova téhle kategorie do čtvrti?</summary>
    public bool Accepts(string category)
    {
        for (int i = 0; i < Categories.Count; i++)
        {
            if (string.Equals(Categories[i], category, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Násobič výroby pro čtvrť o dané velikosti. První budova bonus nedává —
    /// „čtvrť" o jedné budově je jen budova.
    /// </summary>
    public double SynergyFor(int buildingCount) =>
        1.0 + Math.Min(SynergyMax, Math.Max(0, buildingCount - 1) * SynergyPerBuilding);
}

/// <summary>
/// Druhy čtvrtí z dat. Prázdný katalog je legitimní stav (hra bez čtvrtí).
/// </summary>
public sealed record DistrictCatalog(DefRegistry<DistrictTypeDef> Types)
{
    /// <summary>Prázdný katalog — pro starší data i pro testy, které čtvrti neřeší.</summary>
    public static DistrictCatalog Empty { get; } = new(
        new DefRegistry<DistrictTypeDef>(
            Array.Empty<DistrictTypeDef>(), t => t.Id, "druh čtvrti", allowEmpty: true));

    /// <summary>Má smysl čtvrti vůbec hledat?</summary>
    public bool IsEnabled => Types.Count > 0;
}
