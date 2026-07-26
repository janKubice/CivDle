namespace CivDle.Core.Content;

/// <summary>
/// Nastavení auto-stavby (civilizace roste sama — jádro idle smyčky, fáze 2 roadmapy).
/// Běží na nízké frekvenci, ne každý tik (CLAUDE.md, výkon).
/// </summary>
/// <param name="IntervalTicks">Jak často se auto-stavba pokusí stavět.</param>
/// <param name="SearchRadius">V jaké vzdálenosti (dlaždice) od existující zástavby hledá místo.</param>
/// <param name="PopulationHeadroom">Staví se, když populace ≥ kapacita − headroom (poptávka po bydlení).</param>
public sealed record AutoBuildConfig(int IntervalTicks, int SearchRadius, int PopulationHeadroom);

/// <summary>
/// Nastavení auto-silnic (fáze 4: nová budova se sama napojí cestou).
/// </summary>
/// <param name="MapColor">Barva cesty na mapě (MVP vizuál).</param>
/// <param name="MaxSearchDistance">Strop délky hledané cesty v dlaždicích — dál se nespojuje.</param>
/// <param name="MaxBridgeSpan">Kolik vodních dlaždic v řadě umí cesta přemostit (0 = bez mostů).</param>
/// <param name="DisconnectedProductionMult">
/// Násobič výroby budovy, která nesousedí se silniční sítí. 1.0 = silnice jsou
/// jen dekorace (původní stav), nižší hodnota z nich dělá skutečnou infrastrukturu:
/// odvézt zboží bez cesty je dražší. Auto-stavba silnice buduje sama, takže je to
/// odměna za fungující síť, ne trest za nepozornost.
/// </param>
public sealed record RoadConfig(
    RgbColor MapColor,
    int MaxSearchDistance,
    int MaxBridgeSpan = 0,
    double DisconnectedProductionMult = 1.0);

/// <summary>
/// Nastavení detekce osad (fáze 4: shluk budov se pozná jako sídlo se jménem).
/// Běží na nízké frekvenci a jen při změně zástavby (CLAUDE.md, výkon).
/// </summary>
/// <param name="MinBuildings">Od kolika budov se shluk počítá za osadu.</param>
/// <param name="ClusterDistance">Max. mezera (Čebyševova) mezi budovami jednoho shluku.</param>
/// <param name="UpdateIntervalTicks">Jak často se shluky přepočítávají.</param>
public sealed record SettlementConfig(int MinBuildings, int ClusterDistance, int UpdateIntervalTicks);

/// <summary>
/// Denní/noční cyklus (fáze 5: v noci se město rozsvítí). Čas je čistě odvozený
/// z tiků simulace — deterministický a zadarmo v savu; efekt je jen vizuální
/// (living-map.md doporučuje držet den/noc hlavně jako atmosféru).
/// </summary>
/// <param name="DayLengthSeconds">Délka celého dne v sekundách reálného času.</param>
/// <param name="StartTimeOfDay">Čas při startu nové hry (0 = půlnoc, 0.5 = poledne).</param>
/// <param name="NightColor">Barva nočního ztmavení scény.</param>
/// <param name="DuskColor">Barva svítání/soumraku.</param>
/// <param name="NightAlpha">Maximální síla nočního overlaye (0–1).</param>
/// <param name="DuskAlpha">Maximální síla oranžového nádechu při svítání/soumraku (0–1).</param>
public sealed record DayNightConfig(
    double DayLengthSeconds,
    double StartTimeOfDay,
    RgbColor NightColor,
    RgbColor DuskColor,
    double NightAlpha,
    double DuskAlpha);

/// <summary>
/// Globální parametry herní smyčky z <c>data/gameplay.json</c> — čísla balancu
/// patří do dat, ne do kódu. Hodnoty „za sekundu" si systémy přepočítávají
/// na tiky přes <c>Simulation.TicksPerSecond</c>.
/// </summary>
/// <param name="StartingPopulation">Počáteční populace nové hry.</param>
/// <param name="BaseHousingCapacity">Kapacita bydlení bez domů (výchozí tábor).</param>
/// <param name="PopulationGrowthPerSecond">Přírůstek populace za sekundu (když je jídlo a kapacita).</param>
/// <param name="FoodPerPersonPerSecond">Spotřeba jídla na osobu za sekundu (soft pressure).</param>
/// <param name="FoodResourceIndex">Která surovina je „jídlo" (odkaz z dat, ne natvrdo).</param>
/// <param name="AutoBuild">Nastavení automatického růstu zástavby.</param>
/// <param name="Roads">Nastavení auto-silnic.</param>
/// <summary>
/// Slavnost: aktivní tlačítko, které na chvíli zrychlí výrobu i sběr (engagement
/// bez grindu). Přechodný stav — neukládá se.
/// </summary>
/// <param name="DurationSeconds">Jak dlouho slavnost trvá.</param>
/// <param name="CooldownSeconds">Za jak dlouho od spuštění lze zas.</param>
/// <param name="Multiplier">Násobič výroby a sběru během slavnosti.</param>
public sealed record BoostConfig(int DurationSeconds, int CooldownSeconds, double Multiplier);

/// <summary>
/// Spokojenost města: jediná vrstva, kde stavění není zadarmo. Prázdná (vypnutá)
/// konfigurace nechá hru chovat se jako dřív — spokojenost pořád 1.0.
/// </summary>
/// <param name="IntervalTicks">Jak často se přepočítává (pomalý systém, ne hot path).</param>
/// <param name="BaseHappiness">Základ bez služeb i bez přelidnění.</param>
/// <param name="ServiceWeight">Kolik k spokojenosti přidá plné pokrytí službami.</param>
/// <param name="OvercrowdingPenalty">Kolik ubere populace natlačená na strop bydlení.</param>
/// <param name="PeoplePerServicePoint">Kolik lidí obslouží jeden bod služby budovy.</param>
/// <param name="GrowthFloor">Násobič růstu při nulové spokojenosti (0 = růst se zastaví).</param>
/// <param name="FreePopulation">
/// Do téhle velikosti si obyvatelé vystačí sami — vesnice o dvaceti lidech trh
/// nepotřebuje. Bez toho by hra trestala hráče od první minuty za to, že ještě
/// nemá budovy, které se odemykají mnohem později.
/// </param>
public sealed record HappinessConfig(
    int IntervalTicks,
    double BaseHappiness,
    double ServiceWeight,
    double OvercrowdingPenalty,
    double PeoplePerServicePoint,
    double GrowthFloor,
    double FreePopulation = 0)
{
    /// <summary>Vypnutá spokojenost — hra bez téhle vrstvy (výchozí pro starší data).</summary>
    public static HappinessConfig Disabled { get; } = new(0, 1.0, 0.0, 0.0, 0.0, 1.0);

    /// <summary>Má smysl spokojenost počítat?</summary>
    public bool IsEnabled => IntervalTicks > 0;

    /// <summary>
    /// Násobič růstu populace při dané spokojenosti. Nikdy nejde pod
    /// <see cref="GrowthFloor"/> — nespokojené město stagnuje, ale neumírá.
    /// </summary>
    public double GrowthFactor(double happiness) =>
        GrowthFloor + (1.0 - GrowthFloor) * Math.Clamp(happiness, 0.0, 1.0);
}

/// <summary>Ruční sběr: šance na „krit" (velký výnos) — aktivní klikání se vyplatí.</summary>
/// <param name="CritChance">Pravděpodobnost kritu (0–1) na jeden sběr.</param>
/// <param name="CritMultiplier">Násobič výnosu při kritu.</param>
/// <param name="JackpotMultiplier">
/// Násobič „úlovku života" (velryba, obří žíla). Šanci na něj dávají až upgrady
/// Vzestupu — bez nich nenastane, takže tenhle násobič smí být pořádně velký.
/// </param>
public sealed record HarvestConfig(double CritChance, double CritMultiplier, double JackpotMultiplier = 25.0);

/// <summary>Denní odměna za návrat: základ × série dní (do stropu). Retenční háček.</summary>
/// <param name="BaseReward">Základní odměna v surovinách (za 1. den série).</param>
/// <param name="StreakCap">Nejvyšší násobek série (odměna neroste donekonečna).</param>
public sealed record DailyRewardConfig(IReadOnlyList<ResourceAmount> BaseReward, int StreakCap);

/// <summary>
/// Sázení: hráč za cenu vysadí obnovitelný zdroj (háj), který pak jde těžit klikem
/// jako přírodní strom/kámen. Agency nad krajinou (behavior „terraform" light).
/// </summary>
/// <param name="Cost">Cena zasazení.</param>
/// <param name="ResourceIndex">Kterou surovinu zasazený uzel dává.</param>
/// <param name="Amount">Výnos jednoho sběru zasazeného uzlu.</param>
public sealed record PlantingConfig(IReadOnlyList<ResourceAmount> Cost, int ResourceIndex, int Amount);

/// <param name="Settlements">Nastavení detekce osad.</param>
/// <param name="DayNight">Denní/noční cyklus.</param>
/// <param name="Boost">Nastavení slavnosti (dočasný boost).</param>
/// <param name="Harvest">Nastavení kritického sběru.</param>
public sealed record GameplayConfig(
    double StartingPopulation,
    int BaseHousingCapacity,
    double PopulationGrowthPerSecond,
    double FoodPerPersonPerSecond,
    int FoodResourceIndex,
    AutoBuildConfig AutoBuild,
    RoadConfig Roads,
    SettlementConfig Settlements,
    DayNightConfig DayNight,
    BoostConfig Boost,
    HarvestConfig Harvest,
    DailyRewardConfig DailyReward,
    PlantingConfig Planting,
    HappinessConfig? HappinessOrNull = null,
    StaffingConfig? StaffingOrNull = null)
{
    /// <summary>Nastavení spokojenosti; chybí-li v datech, je vrstva vypnutá.</summary>
    public HappinessConfig Happiness => HappinessOrNull ?? HappinessConfig.Disabled;

    /// <summary>Nastavení přidělování dělníků; chybí-li v datech, platí výchozí.</summary>
    public StaffingConfig Staffing => StaffingOrNull ?? StaffingConfig.Default;
}

/// <summary>
/// Jak město rozděluje lidi mezi budovy.
///
/// <para>Dřív se obsazenost počítala globálně (populace ÷ všechna pracovní místa),
/// takže každá další výrobna zpomalila i všechny předchozí — balanční nástroj
/// ukázal, že si tím hráč sám podřezával výrobu. Teď se dělníci přidělují budovu
/// po budově a přednost mají ty, jejichž surovina zrovna dochází: město se samo
/// přeorganizuje na to, čeho je nedostatek, což je přesně to, co by hráč dělal
/// ručně — a v idle hře to dělat ručně nechce.</para>
/// </summary>
/// <param name="ScarcityThreshold">
/// Pod jakým naplněním skladu (0–1) se surovina považuje za nedostatkovou a její
/// výrobny dostanou dělníky přednostně.
/// </param>
public sealed record StaffingConfig(double ScarcityThreshold)
{
    /// <summary>Výchozí nastavení pro data, která blok neuvádějí.</summary>
    public static StaffingConfig Default { get; } = new(0.6);
}
