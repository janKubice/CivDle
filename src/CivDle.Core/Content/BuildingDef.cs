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
/// <param name="MergesToIndex">Index budovy, na kterou se sloučí blok 2×2 stejných; −1 = neslučuje se.</param>
/// <param name="MergeCostOrNull">Cena sloučení (nad rámec už postavených budov).</param>
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
    int PowerDemand,
    bool RequiresAdjacentWater = false,
    int ServiceValue = 0,
    IReadOnlyList<ResourceAmount>? UpkeepOrNull = null,
    int MergesToIndex = -1,
    IReadOnlyList<ResourceAmount>? MergeCostOrNull = null,
    AdjacencyRule? AdjacencyOrNull = null,
    int BuildTicks = 0,
    int TerrainHarvestRadius = 0)
{
    /// <summary>
    /// Bere budova surovinu přímo z krajiny? &gt;0 = dosah v dlaždicích, ve kterém
    /// těží uzly (les, ložisko). 0 = budova zpracovává dovezené vstupy a na
    /// okolní krajině jí nezáleží.
    ///
    /// <para>Tohle je ten rozdíl mezi „klikáním ubývá les" a „ubývá les". Pila
    /// nekácí proto, že na ni hráč klikne — kácí proto, že v ní pracují lidé.
    /// Čím větší město, tím víc lidí, tím rychleji mizí okolní porost.</para>
    /// </summary>
    public bool HarvestsTerrain => TerrainHarvestRadius > 0;

    /// <summary>
    /// Jak dlouho se budova staví (v ticích). 0 = stojí hned, jako všechno ostatní.
    ///
    /// <para>Existuje kvůli divům světa: megastruktura, která vyroste jedním
    /// kliknutím, je jen drahá budova. S odpočtem je z ní událost — na mapě stojí
    /// staveniště, hráč se k němu vrací a dokončení něco znamená.</para>
    /// </summary>
    public bool TakesTimeToBuild => BuildTicks > 0;

    /// <summary>
    /// Pravidlo bonusu za okolí (pila u lesa, lom u hor); <c>null</c> = budově
    /// na okolí nezáleží a vyrábí všude stejně.
    /// </summary>
    public AdjacencyRule? Adjacency => AdjacencyOrNull;

    /// <summary>Záleží téhle budově na tom, co má kolem sebe?</summary>
    public bool HasAdjacencyBonus => AdjacencyOrNull is not null;

    /// <summary>
    /// Cena za sloučení bloku 2×2 do jedné velké budovy. Doplácí se k tomu, co už
    /// stojí — hráč čtyři domy nestaví znovu, jen je přestaví na jeden velký.
    /// </summary>
    public IReadOnlyList<ResourceAmount> MergeCost => MergeCostOrNull ?? Array.Empty<ResourceAmount>();

    /// <summary>Dá se čtveřice těchhle budov v bloku 2×2 sloučit v jednu větší?</summary>
    public bool CanMergeIntoBigger => MergesToIndex >= 0;

    /// <summary>
    /// Kolik „bodů služby" budova poskytuje (trh, sýpka, lázně…). 0 = budova
    /// obyvatele neobsluhuje. Přepočet na lidi řídí gameplay.json.
    /// </summary>
    public int Services => ServiceValue;

    /// <summary>
    /// Opakovaná cena za provoz. Bez zaplacení budova přestane sloužit (nic se
    /// neboří) — díky tomu jsou služby rozhodnutí, ne jednorázový nákup.
    /// </summary>
    public IReadOnlyList<ResourceAmount> Upkeep => UpkeepOrNull ?? Array.Empty<ResourceAmount>();

    /// <summary>
    /// Musí budova sousedit s vodou? Přístavy a rybolov dávají smysl jen na břehu —
    /// tím dostává pobřeží ekonomickou identitu (living-map.md §5).
    /// </summary>
    public bool NeedsWaterAccess => RequiresAdjacentWater;

    /// <summary>Potřebuje budova ke své výrobě elektřinu?</summary>
    public bool NeedsPower => PowerDemand > 0;

    /// <summary>Lokalizační klíč jména budovy.</summary>
    public string NameKey => $"building.{Id}";

    /// <summary>Smí budova stát na dlaždici s daným biomem?</summary>
    public bool IsBiomeAllowed(int biomeIndex) => AllowedBiomes[biomeIndex];

    /// <summary>Má budova další úroveň, na kterou lze vylepšit?</summary>
    public bool HasUpgrade => UpgradesToIndex >= 0;
}
