using CivDle.Core.Content;
using CivDle.Core.Sim;

namespace CivDle.Core.Galaxy;

/// <summary>Co hráč se světem galaxie může dělat.</summary>
public enum WorldAvailability
{
    /// <summary>Ještě ne — chybí hvězdy nebo otevřená brána.</summary>
    Locked,

    /// <summary>Dá se kolonizovat.</summary>
    Available,

    /// <summary>Kolonie už stojí (Domovina je kolonie vždycky).</summary>
    Colony,
}

/// <summary>
/// Jeden založený svět (svety-design.md 7.5): kdy vznikl, kdy ho hráč opustil,
/// jeho snímek a souhrn. Aktivní svět žije v paměti jako <see cref="Simulation"/>
/// a snímek nemá.
/// </summary>
public sealed class WorldRecord
{
    public WorldRecord(string worldId, long seed)
    {
        WorldId = worldId;
        Seed = seed;
    }

    /// <summary>ID světa (<see cref="WorldDef.Id"/>).</summary>
    public string WorldId { get; }

    /// <summary>Seed terénu a „náhody" světa.</summary>
    public long Seed { get; }

    /// <summary>Předvolba terénu (ID) — bez ní se snímek nedá načíst, terén se neukládá.</summary>
    public string PresetId { get; set; } = string.Empty;

    /// <summary>Velikost světa (ID) — metadata snímku, jako u savu.</summary>
    public string SizeId { get; set; } = string.Empty;

    /// <summary>Kdy byla kolonie založena (galaktické sekundy).</summary>
    public double FoundedAtSeconds { get; set; }

    /// <summary>Kdy hráč svět naposled opustil (galaktické sekundy).</summary>
    public double LeftAtSeconds { get; set; }

    /// <summary>Kde kolonisté přistáli (střed přistávacího modulu); Domovina −1.</summary>
    public int LandingX { get; set; } = -1;

    /// <summary>Kde kolonisté přistáli; Domovina −1.</summary>
    public int LandingY { get; set; } = -1;

    /// <summary>
    /// Úplný save světa (stávající formát), dokud se na něj hráč nedívá;
    /// <c>null</c> = svět je aktivní a žije v paměti.
    /// </summary>
    public byte[]? Snapshot { get; set; }

    /// <summary>Souhrn z chvíle odchodu (zásoby, toky, lidé); <c>null</c> = ještě nebyl opuštěn.</summary>
    public WorldSummary? Summary { get; set; }

    /// <summary>
    /// Suroviny, které do neaktivního světa přivezly (kladné) nebo z něj odvezly
    /// (záporné) obchodní trasy — připíšou se při návratu.
    /// </summary>
    public Dictionary<string, double> PendingDelta { get; } = new(StringComparer.Ordinal);

    /// <summary>Splněné hvězdy (ID úkolů skupin hvězda a mistrovská hvězda).</summary>
    public HashSet<string> Stars { get; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Kolik za sekundu odbavily přístavy světa, když ho hráč opouštěl
    /// (obchod s neaktivním světem nemá zástavbu, ze které by to spočítal).
    /// </summary>
    public double PortCapacity { get; set; }

    /// <summary>
    /// Odhad zásoby teď: snímek + tok × čas nepřítomnosti + obchod, mezi nulou
    /// a skladem. Pro kartu světa a obchod s neaktivním světem.
    /// </summary>
    public double EstimatedStock(string resourceId, double nowSeconds)
    {
        if (Summary is null)
        {
            return 0;
        }

        double stock = Summary.Advance(nowSeconds - LeftAtSeconds).StockOf(resourceId);
        return Math.Max(0, stock + PendingDelta.GetValueOrDefault(resourceId));
    }

    /// <summary>Odhad obyvatel teď (lidé dorůstají k bydlení).</summary>
    public double EstimatedPopulation(double nowSeconds) =>
        Summary?.Advance(nowSeconds - LeftAtSeconds).Population ?? 0;
}

/// <summary>
/// Kolonizační loď ve stavbě (svety-design.md 2.2): kam poletí, kolikátý
/// stupeň se staví a co už je v něm vloženo. Platí se na Domovině.
/// </summary>
public sealed class ColonyShipState
{
    public ColonyShipState(string targetWorldId)
    {
        TargetWorldId = targetWorldId;
    }

    /// <summary>Cílový svět.</summary>
    public string TargetWorldId { get; }

    /// <summary>Stavěný stupeň (0 = první); rovno počtu stupňů = loď je hotová.</summary>
    public int StageIndex { get; set; }

    /// <summary>Vloženo do rozestavěného stupně, surovinami Domoviny podle ID.</summary>
    public Dictionary<string, double> Invested { get; } = new(StringComparer.Ordinal);
}

/// <summary>
/// Stav celé galaxie (svety-design.md 7.5): který svět je aktivní, záznamy
/// ostatních, galaktické hodiny, brána a kolonizační loď.
///
/// <para><b>Galaktické hodiny jsou jedny</b> a posouvají je tiky aktivního
/// světa: „teď" = kdy hráč na svět vstoupil + kolik od té doby svět odtikal.
/// Nemusí se nikde nic přičítat za tik — hodiny jsou odvozené, a proto se
/// nemohou rozejít se simulací (ani při dohánění offline, které tiky posouvá
/// taky).</para>
///
/// <para>Vrstva: jádro, čisté OOP nad simulací. Simulace o galaxii neví.</para>
/// </summary>
public sealed class GalaxyState
{
    private readonly Dictionary<string, WorldRecord> _records = new(StringComparer.Ordinal);

    /// <summary>Galaxie s jedinou Domovinou, která je právě aktivní.</summary>
    public static GalaxyState NewWithHome(Simulation home)
    {
        var state = new GalaxyState();
        state.Add(new WorldRecord(WorldScope.HomeId, home.Seed));
        state.ActiveWorldId = WorldScope.HomeId;
        state.ActiveEnteredAtTick = home.TickCount;
        state.ActiveEnteredAtSeconds = home.TickCount / Simulation.TicksPerSecond;
        state.LastSeenTick = home.TickCount;
        state.Refresh(home);
        return state;
    }

    /// <summary>Svět, na který se hráč právě dívá.</summary>
    public string ActiveWorldId { get; set; } = WorldScope.HomeId;

    /// <summary>Tik aktivního světa, kdy na něj hráč vstoupil.</summary>
    public long ActiveEnteredAtTick { get; set; }

    /// <summary>Galaktický čas vstupu na aktivní svět.</summary>
    public double ActiveEnteredAtSeconds { get; set; }

    /// <summary>Je Hvězdná brána Domoviny otevřená (galaxie přístupná)?</summary>
    public bool GateOpened { get; set; }

    /// <summary>Rozestavěná kolonizační loď; <c>null</c> = žádná.</summary>
    public ColonyShipState? Ship { get; set; }

    /// <summary>
    /// Ladicí: za otevřenou bránou jsou dostupné všechny světy bez ohledu na
    /// hvězdy (ladicí menu). Schválně se <b>neukládá</b> — po načtení platí
    /// zase hvězdy a založené kolonie zůstanou koloniemi, takže zkratka nemůže
    /// hráči trvale rozbít postup. Bránu neobchází: bez ní galaxie není.
    /// </summary>
    public bool DebugAllWorldsOpen { get; set; }

    /// <summary>Obchodní trasy a zboží na cestě.</summary>
    public TradeRouteSystem Trade { get; } = new();

    /// <summary>Založené světy (Domovina vždy).</summary>
    public IReadOnlyDictionary<string, WorldRecord> Records => _records;

    /// <summary>Záznam aktivního světa.</summary>
    public WorldRecord Active => _records[ActiveWorldId];

    /// <summary>Počet kolonií (bez Domoviny).</summary>
    public int ColonyCount => _records.Count - 1;

    /// <summary>Přidá záznam založeného světa.</summary>
    public WorldRecord Add(WorldRecord record)
    {
        _records[record.WorldId] = record;
        return record;
    }

    /// <summary>Poslední tik aktivního světa, který galaxie viděla (viz <see cref="Observe"/>).</summary>
    public long LastSeenTick { get; set; }

    /// <summary>Galaktický čas teď (sekundy), odvozený z tiků aktivního světa.</summary>
    public double NowSeconds(Simulation active) =>
        ActiveEnteredAtSeconds + Math.Max(0, active.TickCount - ActiveEnteredAtTick) / Simulation.TicksPerSecond;

    /// <summary>
    /// Pohlídá hodiny aktivního světa: Vzestup začíná novou éru od tiku nula,
    /// a galaktický čas by se tím vrátil. Když tiky klesnou, čas odtikaný do
    /// Vzestupu se přičte k času vstupu a měří se dál od nového začátku.
    /// Volá se každý snímek — je to jedno porovnání.
    /// </summary>
    public void Observe(Simulation active)
    {
        if (active.TickCount < LastSeenTick)
        {
            ActiveEnteredAtSeconds += Math.Max(0, LastSeenTick - ActiveEnteredAtTick) / Simulation.TicksPerSecond;
            ActiveEnteredAtTick = active.TickCount;
        }

        LastSeenTick = active.TickCount;
    }

    /// <summary>
    /// Přepíše z aktivního světa, co galaxie potřebuje vědět průběžně: jeho
    /// hvězdy a (na Domovině) jestli je otevřená brána. Levné — volá se při
    /// uložení, přepnutí a otevření mapy galaxie.
    /// </summary>
    public void Refresh(Simulation active)
    {
        var record = Active;
        record.Stars.Clear();
        foreach (string star in StarsOf(active))
        {
            record.Stars.Add(star);
        }

        if (ActiveWorldId == WorldScope.HomeId && active.IsGateOpened)
        {
            GateOpened = true;
        }
    }

    /// <summary>Hvězdy celé galaxie (včetně mistrovských).</summary>
    public int TotalStars()
    {
        int total = 0;
        foreach (var record in _records.Values)
        {
            total += record.Stars.Count;
        }

        return total;
    }

    /// <summary>
    /// Co se světem jde dělat: Domovina a založené kolonie jsou kolonie, první
    /// svět odemyká brána, další hvězdy (svety-design.md 2.6).
    /// </summary>
    public WorldAvailability AvailabilityOf(WorldDef world)
    {
        if (world.IsHome || _records.ContainsKey(world.Id))
        {
            return WorldAvailability.Colony;
        }

        if (!GateOpened)
        {
            return WorldAvailability.Locked; // bez brány není galaxie vůbec
        }

        if (DebugAllWorldsOpen)
        {
            return WorldAvailability.Available;
        }

        return TotalStars() >= world.StarsRequired ? WorldAvailability.Available : WorldAvailability.Locked;
    }

    /// <summary>ID splněných hvězd světa (úkoly skupin hvězda a mistrovská hvězda).</summary>
    public static IEnumerable<string> StarsOf(Simulation sim)
    {
        var quests = sim.Content.Quests;
        for (int i = 0; i < quests.Count; i++)
        {
            if (quests[i].Group is QuestGroup.Star or QuestGroup.StarMaster && sim.IsQuestCompleted(i))
            {
                yield return quests[i].Id;
            }
        }
    }
}
