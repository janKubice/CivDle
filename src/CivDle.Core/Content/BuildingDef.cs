namespace CivDle.Core.Content;

/// <summary>Množství jedné suroviny — surovina jako index, ne string (hot path).</summary>
public sealed record ResourceAmount(int ResourceIndex, int Amount);

/// <summary>
/// Výrobní recept budovy: každých <paramref name="TimeTicks"/> tiků spotřebuje
/// vstupy a vyrobí výstupy. Bez vstupů = těžba/produkce z terénu.
/// </summary>
public sealed record Recipe(
    IReadOnlyList<ResourceAmount> Inputs,
    IReadOnlyList<ResourceAmount> Outputs,
    int TimeTicks);

/// <summary>
/// Zvalidovaná definice budovy z <c>data/buildings.json</c> (typ; instance jsou
/// struktury v plochém poli simulace). Jméno je v jazykových souborech pod
/// <c>building.&lt;Id&gt;</c>.
/// </summary>
/// <param name="Id">Stabilní ID.</param>
/// <param name="MapColor">Barva budovy na mapě (MVP vizuál).</param>
/// <param name="FootprintWidth">Šířka v dlaždicích.</param>
/// <param name="FootprintHeight">Výška v dlaždicích.</param>
/// <param name="WorkerSlots">Kolik pracovníků budova zaměstná (0 = nepracovní budova).</param>
/// <param name="HousingCapacity">O kolik zvýší kapacitu bydlení (domy).</param>
/// <param name="BuildCost">Cena stavby.</param>
/// <param name="Recipe">Výroba; <c>null</c> = budova nevyrábí (dům).</param>
/// <param name="AllowedBiomes">Maska povolených biomů indexovaná indexem biomu.</param>
/// <param name="StorageBonus">O kolik budova zvýší kapacitu skladu surovin (sklady).</param>
/// <param name="AutoBuild">Smí ji stavět civilizace sama (auto-domy dle poptávky, fáze 2)?</param>
public sealed record BuildingDef(
    string Id,
    RgbColor MapColor,
    int FootprintWidth,
    int FootprintHeight,
    int WorkerSlots,
    int HousingCapacity,
    IReadOnlyList<ResourceAmount> BuildCost,
    Recipe? Recipe,
    bool[] AllowedBiomes,
    IReadOnlyList<ResourceAmount> StorageBonus,
    bool AutoBuild)
{
    /// <summary>Lokalizační klíč jména budovy.</summary>
    public string NameKey => $"building.{Id}";

    /// <summary>Smí budova stát na dlaždici s daným biomem?</summary>
    public bool IsBiomeAllowed(int biomeIndex) => AllowedBiomes[biomeIndex];
}
