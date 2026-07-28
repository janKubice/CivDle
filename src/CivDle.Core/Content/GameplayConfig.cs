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

/// <summary>
/// Svoz zboží do skladu. Budova daleko od nejbližšího sběrného místa vyrábí míň —
/// sklad tím dostává jiný smysl než jen „větší číslo kapacity".
///
/// <para>Trest je měkký a má podlahu: vzdálená kolonie zpomalí, nikdy neumře
/// (idle konvence — hra netrestá za nepozornost, odměňuje za pozornost).</para>
/// </summary>
/// <param name="FreeDistance">Do téhle vzdálenosti (Manhattan, dlaždice) se sváží zadarmo.</param>
/// <param name="Range">O kolik dlaždic navíc se výroba pokaždé zhruba půlí.</param>
/// <param name="MinMultiplier">Podlaha násobiče — pod tohle výroba neklesne.</param>
public sealed record HaulConfig(int FreeDistance, int Range, double MinMultiplier)
{
    /// <summary>Vypnutý svoz — hra bez téhle vrstvy (výchozí pro starší data).</summary>
    public static HaulConfig Disabled { get; } = new(0, 0, 1.0);

    /// <summary>Má smysl svoz počítat?</summary>
    public bool IsEnabled => Range > 0 && MinMultiplier < 1.0;

    /// <summary>
    /// Násobič výroby pro danou vzdálenost k nejbližšímu sběrnému místu.
    /// Klesá hyperbolicky (1 / (1 + přesah / dosah)), takže první dlaždice navíc
    /// bolí nejvíc a pak se to zplošťuje — jinak by byla mapa rozdělená na ostrou
    /// hranici „tady ano, tady ne".
    /// </summary>
    public double Multiplier(int distance)
    {
        if (!IsEnabled || distance <= FreeDistance)
        {
            return 1.0;
        }

        double over = distance - FreeDistance;
        return Math.Max(MinMultiplier, 1.0 / (1.0 + over / Range));
    }
}

/// <summary>
/// Nástroje jako živá surovina, ne jednorázová měna.
///
/// <para>Proč to v hře je: nástroje se do téhle chvíle vyráběly, jednou dvakrát
/// utratily za stavbu a pak se jen hromadily do stropu skladu. Tím pádem byla
/// celá jejich větev slepá. S opotřebením a pokrytím mají trvalý odbyt: čím
/// větší město, tím víc nástrojů potřebuje, a dobře vybavení lidé odvedou víc
/// práce.</para>
///
/// <para>Je to čistě bonusová vrstva — bez nástrojů se hraje jako dřív, jen bez
/// bonusu. Žádný trest za to, že je hráč ještě neobjevil.</para>
/// </summary>
/// <param name="ResourceIndex">Která surovina jsou „nástroje"; −1 = vrstva vypnutá.</param>
/// <param name="PerPerson">Kolik nástrojů na obyvatele znamená plné pokrytí.</param>
/// <param name="WearPerWorkerPerSecond">Kolik nástrojů za sekundu ohladí jeden pracující člověk.</param>
/// <param name="ProductionBonus">O kolik zvedne výrobu plné pokrytí (0.2 = +20 %).</param>
/// <param name="HarvestBonus">O kolik zvedne ruční sběr plné pokrytí (0.5 = +50 %).</param>
public sealed record ToolsConfig(
    int ResourceIndex,
    double PerPerson,
    double WearPerWorkerPerSecond,
    double ProductionBonus,
    double HarvestBonus)
{
    /// <summary>Vypnuté nástroje — hra bez téhle vrstvy (výchozí pro starší data).</summary>
    public static ToolsConfig Disabled { get; } = new(-1, 0, 0, 0, 0);

    /// <summary>Má smysl nástroje vůbec počítat?</summary>
    public bool IsEnabled => ResourceIndex >= 0 && PerPerson > 0;

    /// <summary>
    /// Pokrytí 0–1: kolik lidí má nástroje. Nad plné pokrytí se nesčítá —
    /// hromada nástrojů navíc už nikomu nepřidá a jinak by šlo bonus škálovat
    /// donekonečna jednou surovinou.
    /// </summary>
    public double Coverage(double tools, double population)
    {
        if (!IsEnabled || population <= 0)
        {
            return 0;
        }

        return Math.Clamp(tools / (population * PerPerson), 0, 1);
    }
}

/// <summary>
/// Klikací kombo: rychlá série sběrů zvedá výnos.
///
/// <para>Proč to v hře je: krit je náhoda, na kterou se čeká. Kombo je dovednost,
/// kterou hráč cítí hned — po třetím kliknutí za sebou vidí větší číslo a ví, že
/// za to může on. V idle hře, kde je klikání volitelné, je tohle důvod si ho
/// občas dopřát.</para>
///
/// <para>Série se počítá z tiků simulace, ne z reálného času — zůstává tím
/// deterministická jako všechno ostatní.</para>
/// </summary>
/// <param name="WindowSeconds">Jak dlouho po sběru ještě série drží.</param>
/// <param name="BonusPerStep">O kolik zvedne výnos každý další sběr v sérii.</param>
/// <param name="MaxSteps">Kolik kroků série se počítá (strop bonusu).</param>
public sealed record ComboConfig(double WindowSeconds, double BonusPerStep, int MaxSteps)
{
    /// <summary>Vypnuté kombo — hra bez téhle vrstvy (výchozí pro starší data).</summary>
    public static ComboConfig Disabled { get; } = new(0, 0, 0);

    /// <summary>Má smysl kombo počítat?</summary>
    public bool IsEnabled => WindowSeconds > 0 && BonusPerStep > 0 && MaxSteps > 0;

    /// <summary>Jak dlouho série drží, v ticích simulace.</summary>
    public int WindowTicks => (int)Math.Round(WindowSeconds * Sim.Simulation.TicksPerSecond);

    /// <summary>Násobič výnosu pro sérii dané délky (1 = první sběr, bez bonusu).</summary>
    public double Multiplier(int streak) =>
        IsEnabled ? 1.0 + Math.Clamp(streak - 1, 0, MaxSteps) * BonusPerStep : 1.0;
}

/// <summary>
/// Znečištění: jediná mechanika, kde po hráči zůstane stopa v krajině — a jediná,
/// kterou jde vzít zpátky.
///
/// <para>Proč to v hře je: do industriální éry byl růst čistě dobrý. Továrna byla
/// jen další budova s lepšími čísly. Se znečištěním má rozvoj cenu, kterou je
/// vidět na mapě: hutě dýmají, doly otravují půdu, přístavní průmysl kalí vodu.
/// A protože na to existují čističky, není to trest, ale <b>úkol</b> — město se
/// dá vyčistit a hráč u toho vidí, jak se mapa vrací k barvě.</para>
///
/// <para>Bronzová doba zůstává čistá sama od sebe: znečištění nevzniká z kódu,
/// ale z dat konkrétních budov (viz <c>pollution</c> v <c>buildings.json</c>),
/// a ty ho mají až od hutí dál.</para>
/// </summary>
/// <param name="IntervalTicks">Jak často se znečištění přepočítává (pomalý systém).</param>
/// <param name="SpreadRate">Jaká část hodnoty se za přepočet rozlije do sousedních buněk (0–1).</param>
/// <param name="DecayRate">Jaká část se za přepočet rozptýlí sama (0–1) — bez toho by šlo jen přitěžovat.</param>
/// <param name="FullEffectAt">Při jaké hodnotě je dopad plný; níž se škáluje lineárně.</param>
/// <param name="HappinessPenalty">Kolik spokojenosti ubere plně zamořený vzduch nad městem.</param>
/// <param name="ProductionPenalty">Kolik výroby ubere plně zamořená půda/voda pod budovou.</param>
public sealed record PollutionConfig(
    int IntervalTicks,
    double SpreadRate,
    double DecayRate,
    double FullEffectAt,
    double HappinessPenalty,
    double ProductionPenalty)
{
    /// <summary>Vypnuté znečištění — hra bez téhle vrstvy (výchozí pro starší data).</summary>
    public static PollutionConfig Disabled { get; } = new(0, 0, 0, 1, 0, 0);

    /// <summary>Má smysl znečištění vůbec počítat?</summary>
    public bool IsEnabled => IntervalTicks > 0 && FullEffectAt > 0;

    /// <summary>Kolik sekund uplyne mezi dvěma přepočty — emise se počítají „za sekundu".</summary>
    public double IntervalSeconds => IntervalTicks / (double)Sim.Simulation.TicksPerSecond;

    /// <summary>Jak zle je na tom místo s danou hodnotou (0 = čisto, 1 = plný dopad).</summary>
    public double Severity(double level) =>
        FullEffectAt <= 0 ? 0 : Math.Clamp(level / FullEffectAt, 0.0, 1.0);

    /// <summary>O kolik klesne spokojenost při dané špíně ve vzduchu nad městem.</summary>
    public double HappinessDrop(double air) => Severity(air) * HappinessPenalty;

    /// <summary>
    /// Násobič výroby budovy, které vadí zamoření pod ní. Nikdy nejde na nulu, jen
    /// dolů o <see cref="ProductionPenalty"/> — otrávené pole hůř rodí, ale
    /// nepřestane; hra netrestá tvrdě, jen ukazuje směr (soft pressure).
    /// </summary>
    public double ProductionMultiplier(double level) => 1.0 - Severity(level) * ProductionPenalty;
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
    StaffingConfig? StaffingOrNull = null,
    HaulConfig? HaulOrNull = null,
    ToolsConfig? ToolsOrNull = null,
    ComboConfig? ComboOrNull = null,
    PollutionConfig? PollutionOrNull = null)
{
    /// <summary>Nastavení znečištění; chybí-li v datech, je vrstva vypnutá.</summary>
    public PollutionConfig Pollution => PollutionOrNull ?? PollutionConfig.Disabled;

    /// <summary>Nastavení klikacího komba; chybí-li v datech, je vrstva vypnutá.</summary>
    public ComboConfig Combo => ComboOrNull ?? ComboConfig.Disabled;

    /// <summary>Nastavení nástrojů; chybí-li v datech, je vrstva vypnutá.</summary>
    public ToolsConfig Tools => ToolsOrNull ?? ToolsConfig.Disabled;

    /// <summary>Nastavení spokojenosti; chybí-li v datech, je vrstva vypnutá.</summary>
    public HappinessConfig Happiness => HappinessOrNull ?? HappinessConfig.Disabled;

    /// <summary>Nastavení přidělování dělníků; chybí-li v datech, platí výchozí.</summary>
    public StaffingConfig Staffing => StaffingOrNull ?? StaffingConfig.Default;

    /// <summary>Nastavení svozu do skladu; chybí-li v datech, je vrstva vypnutá.</summary>
    public HaulConfig Haul => HaulOrNull ?? HaulConfig.Disabled;
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
