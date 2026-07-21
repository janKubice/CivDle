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
/// <param name="Category">Kategorie do stavebního menu (housing, production, storage, civic…).</param>
/// <param name="MapColor">Barva budovy na mapě (fallback, když chybí sprite).</param>
/// <param name="FootprintWidth">Šířka v dlaždicích.</param>
/// <param name="FootprintHeight">Výška v dlaždicích.</param>
/// <param name="WorkerSlots">Kolik pracovníků budova zaměstná (0 = nepracovní budova).</param>
/// <param name="HousingCapacity">O kolik zvýší kapacitu bydlení (domy).</param>
/// <param name="BuildCost">Cena stavby.</param>
/// <param name="Recipe">Výroba; <c>null</c> = budova nevyrábí (dům).</param>
/// <param name="AllowedBiomes">Maska povolených biomů indexovaná indexem biomu.</param>
/// <param name="StorageBonus">O kolik budova zvýší kapacitu skladu surovin (sklady).</param>
/// <param name="AutoBuild">Smí ji stavět civilizace sama (auto-domy dle poptávky)?</param>
/// <param name="Buildable">Smí ji hráč přímo postavit (upgrade cíle = false, jen přes vylepšení)?</param>
/// <param name="UpgradesToIndex">Index budovy, na kterou lze vylepšit; −1 = konec řady.</param>
/// <param name="UpgradeCost">Cena vylepšení na další úroveň.</param>
/// <param name="PowerSupply">Kolik elektřiny budova dodává (elektrárny); 0 = žádnou.</param>
/// <param name="PowerDemand">Kolik elektřiny budova potřebuje; &gt;0 = její výroba škáluje s pokrytím sítě.</param>
public sealed record BuildingDef(
    string Id,
    string Category,
    RgbColor MapColor,
    int FootprintWidth,
    int FootprintHeight,
    int WorkerSlots,
    int HousingCapacity,
    IReadOnlyList<ResourceAmount> BuildCost,
    Recipe? Recipe,
    bool[] AllowedBiomes,
    IReadOnlyList<ResourceAmount> StorageBonus,
    bool AutoBuild,
    bool Buildable,
    int UpgradesToIndex,
    IReadOnlyList<ResourceAmount> UpgradeCost,
    int PowerSupply,
    int PowerDemand)
{
    /// <summary>Potřebuje budova ke své výrobě elektřinu?</summary>
    public bool NeedsPower => PowerDemand > 0;

    /// <summary>Lokalizační klíč jména budovy.</summary>
    public string NameKey => $"building.{Id}";

    /// <summary>Smí budova stát na dlaždici s daným biomem?</summary>
    public bool IsBiomeAllowed(int biomeIndex) => AllowedBiomes[biomeIndex];

    /// <summary>Má budova další úroveň, na kterou lze vylepšit?</summary>
    public bool HasUpgrade => UpgradesToIndex >= 0;
}
