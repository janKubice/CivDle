namespace CivDle.Core.Content;

/// <summary>
/// Druh cizího města: co nabízí k obchodu, kolik má lidí a jak vypadá na mapě.
/// </summary>
/// <param name="Id">Stabilní ID (lokalizace pod <c>npc.&lt;Id&gt;</c>).</param>
/// <param name="MapColor">Barva značky na mapě.</param>
/// <param name="Population">Kolik lidí přinese, když se město stane součástí říše.</param>
/// <param name="Trade">Co pošle v jedné dodávce.</param>
public sealed record NpcCityArchetype(
    string Id,
    RgbColor MapColor,
    int Population,
    IReadOnlyList<ResourceAmount> Trade)
{
    /// <summary>Lokalizační klíč druhu města.</summary>
    public string NameKey => $"npc.{Id}";
}

/// <summary>
/// Pravidla soužití s cizími městy z <c>data/npc-cities.json</c>.
///
/// <para>Proč je všechno v datech: čísla jako „za kolik se dá město koupit"
/// nebo „kolik budov kolem znamená, že ho město pohltilo" jsou balanc, ne
/// mechanika. Bez souboru je mechanika vypnutá a hra běží jako dřív.</para>
/// </summary>
public sealed class NpcCityCatalog
{
    public NpcCityCatalog(
        IReadOnlyList<ResourceAmount> giftCost,
        int giftRelation,
        IReadOnlyList<ResourceAmount> roadCost,
        int tradeIntervalTicks,
        int buyRelation,
        IReadOnlyList<ResourceAmount> buyCost,
        int surroundRadius,
        int surroundBuildings,
        DefRegistry<NpcCityArchetype> archetypes,
        IReadOnlyList<string> names)
    {
        GiftCost = giftCost;
        GiftRelation = giftRelation;
        RoadCost = roadCost;
        TradeIntervalTicks = Math.Max(1, tradeIntervalTicks);
        BuyRelation = buyRelation;
        BuyCost = buyCost;
        SurroundRadius = surroundRadius;
        SurroundBuildings = surroundBuildings;
        Archetypes = archetypes;
        Names = names;
    }

    /// <summary>Co stojí jeden dar.</summary>
    public IReadOnlyList<ResourceAmount> GiftCost { get; }

    /// <summary>O kolik dar zvedne vztah.</summary>
    public int GiftRelation { get; }

    /// <summary>Co stojí postavit k městu cestu.</summary>
    public IReadOnlyList<ResourceAmount> RoadCost { get; }

    /// <summary>Jak často dorazí dodávka od spřáteleného města.</summary>
    public int TradeIntervalTicks { get; }

    /// <summary>Jaký vztah je potřeba, aby šlo město odkoupit.</summary>
    public int BuyRelation { get; }

    /// <summary>Za kolik se dá město odkoupit.</summary>
    public IReadOnlyList<ResourceAmount> BuyCost { get; }

    /// <summary>V jakém okruhu se počítají hráčovy budovy při obestavění.</summary>
    public int SurroundRadius { get; }

    /// <summary>Kolik budov v okruhu znamená, že město srostlo s hráčovým.</summary>
    public int SurroundBuildings { get; }

    /// <summary>Druhy měst.</summary>
    public DefRegistry<NpcCityArchetype> Archetypes { get; }

    /// <summary>Jména měst.</summary>
    public IReadOnlyList<string> Names { get; }

    /// <summary>Je mechanika v datech zapnutá?</summary>
    public bool IsEnabled => Archetypes.Count > 0 && Names.Count > 0;

    /// <summary>Vypnutá mechanika — pro testy a data bez npc-cities.json.</summary>
    public static NpcCityCatalog Empty { get; } = new(
        Array.Empty<ResourceAmount>(), 0, Array.Empty<ResourceAmount>(), 600, 100,
        Array.Empty<ResourceAmount>(), 0, 0,
        new DefRegistry<NpcCityArchetype>(Array.Empty<NpcCityArchetype>(), a => a.Id, "cizí město", allowEmpty: true),
        Array.Empty<string>());
}
