namespace CivDle.Core.Content;

/// <summary>
/// Jak vypadá planeta na mapě galaxie (svety-design.md 5.4). Samotný povrch
/// se peče z terénu světa; tohle jsou jen barvy pro planetu, kterou hráč
/// ještě nezaložil, a prvky, které z mapy nevyplynou (prstenec, pásy).
/// </summary>
/// <param name="Surface">Převládající barva povrchu (silueta a planeta bez mapy).</param>
/// <param name="Accent">Druhá barva (moře, pásy, čepičky).</param>
/// <param name="Size">Velikost koule na mapě galaxie (1 = Domovina).</param>
/// <param name="Bands">Pásy jako u plynného obra.</param>
/// <param name="Ring">Prstenec.</param>
/// <param name="IceCaps">Bílé polární čepičky.</param>
public sealed record PlanetLook(RgbColor Surface, RgbColor Accent, double Size, bool Bands, bool Ring, bool IceCaps);

/// <summary>
/// Jeden svět galaxie tak, jak ho zná galaxie (<c>data/worlds.json</c>):
/// pořadí, odemčení a cena kolonizace. Co je <b>na</b> světě (budovy,
/// přistávací modul, hvězdy), nese jeho vlastní obsah — viz <see cref="WorldProfile"/>.
///
/// <para><b>Proč rozdělené:</b> cena kolonizační lodi se platí na Domovině
/// v jejích surovinách, kdežto přistávací modul je budova kolonie. Každá část
/// se tak validuje proti obsahu, ve kterém opravdu platí.</para>
/// </summary>
/// <param name="Id">Stabilní ID světa (<c>home</c>, <c>dune</c>…).</param>
/// <param name="Order">Pořadí na mapě galaxie a v odemykání.</param>
/// <param name="StarsRequired">Kolik hvězd (včetně mistrovských) svět odemkne.</param>
/// <param name="RequiresGate">Odemyká se otevřením Hvězdné brány (Duna).</param>
/// <param name="ColonyCost">Stupně kolonizační lodi v surovinách Domoviny.</param>
/// <param name="ColonyCostGrowth">O kolik je každá další kolonie dražší (× za už založené kolonie).</param>
/// <param name="AtmosphereId">Profil atmosféry (světlo, obloha, částice).</param>
/// <param name="Planet">Vzhled na mapě galaxie.</param>
public sealed record WorldDef(
    string Id,
    int Order,
    int StarsRequired,
    bool RequiresGate,
    IReadOnlyList<ProjectStage> ColonyCost,
    double ColonyCostGrowth,
    string AtmosphereId,
    PlanetLook Planet)
{
    /// <summary>Je to Domovina (hlavní město první kapitoly)?</summary>
    public bool IsHome => Id == WorldScope.HomeId;

    /// <summary>Lokalizační klíč jména světa.</summary>
    public string NameKey => $"world.{Id}";

    /// <summary>Lokalizační klíč popisu světa (karta světa).</summary>
    public string DescriptionKey => $"world.{Id}.desc";

    /// <summary>Lokalizační klíč hlavního pravidla světa (jedna věta na kartu).</summary>
    public string RuleKey => $"world.{Id}.rule";
}

/// <summary>
/// Obchod mezi světy (svety-design.md 2.5, <c>worlds.json</c> → <c>trade</c>).
/// </summary>
/// <param name="TravelSecondsPerStep">
/// Kolik galaktických sekund trvá cesta o jeden svět na mapě (sousední planety);
/// dál je to násobek. Zboží dorazí se zpožděním — trasa není teleport.
/// </param>
/// <param name="DispatchSeconds">
/// Jak často odplouvá dávka. Obchod je hrubý tok, ne tik simulace — deset sekund
/// stačí na plynulý přísun a stojí zanedbatelně.
/// </param>
public sealed record TradeConfig(double TravelSecondsPerStep, double DispatchSeconds)
{
    /// <summary>Výchozí: dvě minuty na krok, dávka za deset sekund.</summary>
    public static TradeConfig Default { get; } = new(120, 10);
}

/// <summary>
/// Světy galaxie v pořadí z <c>data/worlds.json</c>. Prázdný katalog = hra bez
/// galaxie (starší data, mody, obsah kolonie — ten katalog nenese).
/// </summary>
public sealed class WorldCatalog
{
    private readonly List<WorldDef> _worlds;

    public WorldCatalog(IReadOnlyList<WorldDef> worlds, TradeConfig? trade = null)
    {
        _worlds = worlds.OrderBy(w => w.Order).ToList();
        Trade = trade ?? TradeConfig.Default;
    }

    /// <summary>Obchod mezi světy (doba cesty, dávky).</summary>
    public TradeConfig Trade { get; }

    /// <summary>
    /// Kolik kroků po mapě dělí dva světy (rozdíl pořadí, aspoň 1). Mapa galaxie
    /// staví planety v pořadí, takže tohle je vzdálenost, kterou hráč vidí.
    /// </summary>
    public int StepsBetween(string a, string b)
    {
        var from = Find(a);
        var to = Find(b);
        return from is null || to is null ? 1 : Math.Max(1, Math.Abs(from.Order - to.Order));
    }

    /// <summary>Bez galaxie.</summary>
    public static WorldCatalog Empty { get; } = new(Array.Empty<WorldDef>());

    /// <summary>Světy seřazené podle pořadí (Domovina první).</summary>
    public IReadOnlyList<WorldDef> Worlds => _worlds;

    /// <summary>Má hra galaxii?</summary>
    public bool IsEnabled => _worlds.Count > 1;

    /// <summary>Svět podle ID, nebo <c>null</c>.</summary>
    public WorldDef? Find(string id)
    {
        for (int i = 0; i < _worlds.Count; i++)
        {
            if (_worlds[i].Id == id)
            {
                return _worlds[i];
            }
        }

        return null;
    }

    /// <summary>Kolonie (všechny světy kromě Domoviny), v pořadí.</summary>
    public IEnumerable<WorldDef> Colonies => _worlds.Where(w => !w.IsHome);
}

/// <summary>
/// Co svět říká o sobě ve svém obsahu (<c>data/worlds/&lt;id&gt;/world.json</c>):
/// terén, přistávací modul, startovní výbava, vývoz a přístav. Domovina má
/// výchozí profil — hráč si terén volí sám a začíná táborákem.
/// </summary>
/// <param name="Id">ID světa (shodné s <see cref="WorldDef.Id"/>).</param>
/// <param name="PresetIndex">Předvolba generátoru (index v <c>WorldGen.Presets</c>); −1 = volba hráče.</param>
/// <param name="LandingModuleIndex">Startovní budova kolonie; −1 = žádná (Domovina).</param>
/// <param name="StartingKit">Náklad lodi — suroviny, se kterými kolonie začíná.</param>
/// <param name="ExportIndices">Artikly, které svět vyváží (karta světa, obchod).</param>
/// <param name="PortIndex">Přístav světa — budova, jejíž kapacita omezuje obchodní trasy; −1 = žádný.</param>
/// <param name="Substitutes">Náhrady surovin pro sdílený obsah (dřevo → cihla).</param>
/// <param name="WithoutSystems">Volitelné systémy, které svět nemá (soubory bez <c>.json</c>).</param>
public sealed record WorldProfile(
    string Id,
    int PresetIndex,
    int LandingModuleIndex,
    IReadOnlyList<ResourceAmount> StartingKit,
    IReadOnlyList<int> ExportIndices,
    int PortIndex,
    IReadOnlyDictionary<string, string> Substitutes,
    IReadOnlySet<string> WithoutSystems)
{
    /// <summary>Profil Domoviny: vše jako v první kapitole.</summary>
    public static WorldProfile Home { get; } = new(
        WorldScope.HomeId, -1, -1, Array.Empty<ResourceAmount>(), Array.Empty<int>(), -1,
        new Dictionary<string, string>(), new HashSet<string>());

    /// <summary>Je to Domovina?</summary>
    public bool IsHome => Id == WorldScope.HomeId;

    /// <summary>
    /// Volitelné systémy, které smí svět vypnout. Jen ty, bez kterých hra umí
    /// běžet (loader chybějící soubor bere jako vypnutou mechaniku).
    /// </summary>
    public static IReadOnlySet<string> OptionalSystems { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        "npc-cities", "figures", "doctrines", "poi", "scenarios", "carillon", "chronicle", "frontier",
        "orbit", "grandwork", "faith", "seasons", "milestones", "elections", "citizens", "contracts",
        "challenges", "tutorial", "features", "landmarks", "ufo", "ambience",
    };
}
