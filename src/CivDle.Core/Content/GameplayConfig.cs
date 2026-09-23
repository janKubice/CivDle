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
    double DisconnectedProductionMult = 1.0,
    IReadOnlyList<RoadSurface>? Surfaces = null)
{
    /// <summary>
    /// Povrch platný v dané éře. Seznam je při načtení seřazený vzestupně,
    /// takže stačí projít od konce a vzít první, který už začal.
    ///
    /// <para>Když data povrchy nemají, vrací <c>null</c> a renderer si vystačí
    /// s <see cref="MapColor"/> — hra se kvůli chybějícímu vzhledu nemá
    /// rozbít.</para>
    /// </summary>
    public RoadSurface? SurfaceForEra(int eraIndex)
    {
        if (Surfaces is null)
        {
            return null;
        }

        for (int i = Surfaces.Count - 1; i >= 0; i--)
        {
            if (eraIndex >= Surfaces[i].FromEra)
            {
                return Surfaces[i];
            }
        }

        return Surfaces.Count > 0 ? Surfaces[0] : null;
    }
}

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
    double FreePopulation = 0,
    double CrowdingThreshold = 0.0,
    int ServiceReachTiles = 0)
{
    /// <summary>
    /// Kolik ubere přelidnění při dané obsazenosti bydlení (0–1).
    ///
    /// <para><b>Až nad prahem.</b> Dřív se trestala obsazenost od nuly — a protože
    /// populace vždycky doroste ke stropu bydlení, byl postih trvale skoro plný:
    /// konstanta −0,24, se kterou hráč nemohl nic udělat. Nad prahem je to signál
    /// „město se tlačí, stav" — a jde mu předejít stavbou dřív.</para>
    /// </summary>
    public double CrowdingPenalty(double occupancy)
    {
        double over = CrowdingThreshold <= 0
            ? occupancy
            : (occupancy - CrowdingThreshold) / (1.0 - CrowdingThreshold);
        return Math.Clamp(over, 0.0, 1.0) * OvercrowdingPenalty;
    }

    /// <summary>Mají služby omezený dosah (0 = obslouží celé město odkudkoli)?</summary>
    public bool HasServiceReach => ServiceReachTiles > 0;

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
    public double Multiplier(int streak) => Multiplier(streak, 1.0);

    /// <summary>
    /// Násobič výnosu pro sérii, zesílený bonusem <c>combo_power</c>.
    ///
    /// <para>Síla zvedá obojí — přírůstek za krok i strop série. Kdyby zvedala
    /// jen přírůstek, série by pořád skončila na desátém kliknutí a rytmus
    /// těžby by se nikdy nezměnil.</para>
    /// </summary>
    public double Multiplier(int streak, double power)
    {
        if (!IsEnabled)
        {
            return 1.0;
        }

        double scale = Math.Max(1.0, power);
        int steps = (int)Math.Round(MaxSteps * scale);
        return 1.0 + Math.Clamp(streak - 1, 0, steps) * BonusPerStep * scale;
    }
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
/// <summary>
/// Jeden druh, který jde zasadit.
///
/// <para>Sázení bylo do téhle chvíle jediná věc za pevnou cenu. Druhy z něj
/// dělají nástroj, který roste s hrou: háj hned na začátku, sad a posvátný háj
/// až za výzkumem. Odemčení je v datech (<c>requiresTech</c>), ne v kódu.</para>
/// </summary>
/// <param name="Id">Stabilní ID (do lokalizace a do UI).</param>
/// <param name="Cost">Co zasazení stojí.</param>
/// <param name="ResourceIndex">Co se z něj pak sbírá.</param>
/// <param name="Amount">Kolik dá jeden sběr.</param>
/// <param name="RequiredTechIndex">Technologie, která ho odemyká; −1 = od začátku.</param>
public sealed record PlantSpecies(
    string Id,
    IReadOnlyList<ResourceAmount> Cost,
    int ResourceIndex,
    int Amount,
    int RequiredTechIndex = -1)
{
    /// <summary>Lokalizační klíč jména druhu.</summary>
    public string NameKey => $"plant.{Id}";
}

/// <summary>Co všechno jde zasadit. Prázdný seznam = sázení je vypnuté.</summary>
public sealed record PlantingConfig(IReadOnlyList<PlantSpecies> Species)
{
    /// <summary>Je vůbec co sázet?</summary>
    public bool IsEnabled => Species.Count > 0;
}

/// <summary>
/// Hromadné stavění: násobiče v liště a strop na jedno gesto.
///
/// <para>Strop tu není kvůli balancu, ale kvůli hráči i výkonu: jedno tažení
/// přes půl mapy nesmí položit tisíc budov ani zamrznout snímek počítáním
/// náhledu. Násobiče jsou v datech, protože je to ladicí knoflík — kolik kusů
/// naráz dává smysl, se pozná až z hraní.</para>
/// </summary>
/// <param name="Batches">Nabídka násobičů v liště (×1, ×5, ×25…).</param>
/// <param name="MaxPerAction">Kolik kusů nejvýš vznikne jedním gestem.</param>
public sealed record BulkBuildConfig(IReadOnlyList<int> Batches, int MaxPerAction)
{
    /// <summary>Výchozí nastavení pro data, která hromadnou stavbu neznají.</summary>
    public static BulkBuildConfig Default { get; } = new(new[] { 1, 5, 25 }, 400);

    /// <summary>Je vůbec z čeho vybírat? (Samotné ×1 je „jako dřív".)</summary>
    public bool HasBatches => Batches.Count > 1;
}

/// <summary>
/// Orbitální těžební laser: pozdní podoba ručního sběru.
///
/// <para>Proč to ve hře je: klikat na jednotlivé stromy je v hodině páté stejná
/// činnost jako v minutě první — jen míň zajímavá. Laser tu činnost <b>nezruší</b>,
/// jen ji promění: hráč místo klikání táhne paprsek přes krajinu. Je to odměna
/// za dojití daleko, ne nová mechanika k naučení.</para>
///
/// <para>Sazba je v datech, protože je to ta nejcitlivější věc na balanc —
/// příliš rychlý paprsek by z krajiny udělal jednorázovou zásobárnu.</para>
/// </summary>
/// <param name="HarvestsPerSecond">Kolik sběrů za sekundu paprsek zvládne.</param>
/// <param name="RadiusTiles">Kolik dlaždic kolem zásahu ještě zachytí (0 = jen ta jedna).</param>
/// <param name="FeatureId">ID funkce z <c>features.json</c>, která laser odemyká.</param>
public sealed record LaserConfig(double HarvestsPerSecond, int RadiusTiles, string FeatureId)
{
    /// <summary>Vypnutý laser — hra bez téhle vrstvy (výchozí pro starší data).</summary>
    public static LaserConfig Disabled { get; } = new(0, 0, string.Empty);

    /// <summary>Má laser vůbec smysl počítat?</summary>
    public bool IsEnabled => HarvestsPerSecond > 0 && FeatureId.Length > 0;

    /// <summary>Jak dlouho trvá jeden zásah paprsku (sekundy).</summary>
    public double SecondsPerHarvest => HarvestsPerSecond > 0 ? 1.0 / HarvestsPerSecond : double.MaxValue;
}

/// <summary>
/// Časosběr: jak často se zaznamená podoba města a kolik snímků se drží.
///
/// <para>Interval je kompromis mezi plynulostí přehrávky a velikostí savu; strop
/// snímků drží obojí v mezích i po hodinách hraní (po naplnění se historie
/// prořídí, ne ořízne — začátek příběhu má zůstat).</para>
/// </summary>
/// <param name="IntervalSeconds">Jak často se snímá (herní sekundy).</param>
/// <param name="MaxFrames">Kolik snímků se nejvýš drží.</param>
public sealed record HistoryConfig(double IntervalSeconds, int MaxFrames)
{
    /// <summary>Vypnutý časosběr — hra bez téhle vrstvy (výchozí pro starší data).</summary>
    public static HistoryConfig Disabled { get; } = new(0, 0);

    /// <summary>Má smysl vůbec něco zaznamenávat?</summary>
    public bool IsEnabled => IntervalSeconds > 0 && MaxFrames > 1;
}

/// <summary>
/// Jak drahý je výzkum.
///
/// <para>Ceny v <c>tech.json</c> jsou <b>základ</b>, ne konečná částka. Násobič
/// je tu proto, aby se dal celý strom zdražit jedním číslem, a ne přepisováním
/// stovky uzlů. Růst za každou hotovou technologii dělá to, co idle hra
/// potřebuje: první výzkumy jsou svižné, pozdější stojí za rozmyšlenou.</para>
///
/// <para>Růst je záměrně <b>lineární</b>, ne složený. Složený by po stovce uzlů
/// vyskočil o dva řády a poslední větev stromu by se stala nedosažitelnou.</para>
/// </summary>
/// <param name="CostMultiplier">Čím se násobí základní cena z dat.</param>
/// <param name="CostGrowthPerTech">O kolik zdraží každá už hotová technologie (0.05 = +5 %).</param>
public sealed record ResearchConfig(
    double CostMultiplier,
    double CostGrowthPerTech,
    double LevelCostMultiplier = 1.0)
{
    /// <summary>Ceny přesně podle dat — výchozí stav pro obsah bez sekce.</summary>
    public static ResearchConfig Plain { get; } = new(1.0, 0.0);

    /// <summary>Násobič ceny po <paramref name="researched"/> hotových technologiích.</summary>
    public double ScaleAfter(int researched) =>
        CostMultiplier * (1.0 + CostGrowthPerTech * Math.Max(0, researched));

    /// <summary>
    /// Násobič ceny další úrovně opakovatelné technologie.
    ///
    /// <para>Bez něj by druhá úroveň stála totéž co první a „víc úrovní" by bylo
    /// jen víc klikání za stejné peníze. Roste mocninou, takže poslední úroveň
    /// je opravdové rozhodnutí, ne formalita.</para>
    /// </summary>
    public double ScaleForLevel(int level) =>
        Math.Pow(Math.Max(1.0, LevelCostMultiplier), Math.Max(0, level));
}

/// <param name="StartingBuildingIndices">
/// Budovy, které stojí na mapě hned po založení světa (indexy do registru).
///
/// <para>Prázdná mapa je nejhorší první dojem, jaký idle hra může udělat —
/// hráč nevidí, o čem hra je. Jeden domek u startu řekne „tohle stavíš" dřív,
/// než stihne kliknout.</para>
/// </param>
/// <param name="Settlements">Nastavení detekce osad.</param>
/// <param name="DayNight">Denní/noční cyklus.</param>
/// <param name="Boost">Nastavení slavnosti (dočasný boost).</param>
/// <param name="Harvest">Nastavení kritického sběru.</param>
public sealed record GameplayConfig(
    double StartingPopulation,
    IReadOnlyList<int> StartingBuildingIndices,
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
    PollutionConfig? PollutionOrNull = null,
    BulkBuildConfig? BulkBuildOrNull = null,
    LaserConfig? LaserOrNull = null,
    HistoryConfig? HistoryOrNull = null,
    ResearchConfig? ResearchOrNull = null,
    DemoConfig? DemoOrNull = null,
    GoldenConfig? GoldenOrNull = null,
    SubseaConfig? SubseaOrNull = null,
    PowerConfig? PowerOrNull = null,
    double PopulationFillRate = 0.0)
{
    /// <summary>
    /// Kolik lidí za sekundu přibude, když je v bydlení <paramref name="freeHousing"/>
    /// volných míst (před násobiči spokojenosti, Vzestupu, období…).
    ///
    /// <para><b>Proč ne jen pevné číslo:</b> pevný přírůstek znamenal, že vesnice
    /// i milionové město přibírají stejně lidí za sekundu — panelák pro čtyřicet
    /// se plnil pět minut a mrakodrap celé hodiny. Podíl volného bydlení dělá
    /// z nového bydlení událost, která je vidět hned, a z tempa růstu otázku
    /// „stíhá se stavět", ne „kolik je hodin". Pevný základ zůstává, aby se
    /// i malá vesnice pohnula.</para>
    /// </summary>
    public double GrowthPerSecond(double freeHousing) =>
        PopulationGrowthPerSecond + PopulationFillRate * Math.Max(0.0, freeHousing);

    /// <summary>Meze demoverze; chybí-li v datech, platí výchozí.</summary>
    public DemoConfig Demo => DemoOrNull ?? DemoConfig.Default;

    /// <summary>Zlaté úlovky; bez bloku v datech zůstane jeden bezejmenný třpyt.</summary>
    public GoldenConfig Golden => GoldenOrNull ?? GoldenConfig.Default;

    /// <summary>Podmořská síť; bez bloku v datech je vrstva vypnutá.</summary>
    public SubseaConfig Subsea => SubseaOrNull ?? SubseaConfig.Disabled;

    /// <summary>Rozvod proudu; bez bloku v datech platí jedno globální číslo jako dřív.</summary>
    public PowerConfig Power => PowerOrNull ?? PowerConfig.Global;

    /// <summary>Nastavení časosběru; chybí-li v datech, se nic nezaznamenává.</summary>
    public HistoryConfig History => HistoryOrNull ?? HistoryConfig.Disabled;

    /// <summary>Škálování cen výzkumu; chybí-li v datech, platí ceny tak, jak jsou.</summary>
    public ResearchConfig Research => ResearchOrNull ?? ResearchConfig.Plain;

    /// <summary>Nastavení těžebního laseru; chybí-li v datech, je vrstva vypnutá.</summary>
    public LaserConfig Laser => LaserOrNull ?? LaserConfig.Disabled;

    /// <summary>Nastavení znečištění; chybí-li v datech, je vrstva vypnutá.</summary>
    public PollutionConfig Pollution => PollutionOrNull ?? PollutionConfig.Disabled;

    /// <summary>Nastavení hromadné stavby; chybí-li v datech, platí výchozí násobiče.</summary>
    public BulkBuildConfig BulkBuild => BulkBuildOrNull ?? BulkBuildConfig.Default;

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

/// <summary>
/// Meze demoverze — kam až hráč v ukázce dojde.
///
/// <para>Jsou v datech, ne v kódu, schválně: demo se ladí podle toho, jak
/// dlouho má trvat (jinak před Next Festem, jinak natrvalo na store stránce),
/// a přeložit kvůli tomu hru je zbytečné.</para>
///
/// <para><b>Samy o sobě nic nedělají.</b> Platí, teprve když je spuštěná demo
/// edice (<see cref="GameContent.IsDemo"/>) — plná hra tenhle blok v datech má
/// taky a ignoruje ho.</para>
/// </summary>
/// <param name="PopulationCap">Strop obyvatel. Ukázka nemá být nekonečná.</param>
/// <param name="AscensionRequirement">
/// Práh <b>druhého</b> Vzestupu. První zůstává normální, aby si hráč prestiž
/// osahal celou; druhý je cíl, na kterém demo končí.
/// </param>
/// <param name="TechCount">
/// Kolik uzlů stromu výzkumu ukázka nabídne — <b>absolutní počet</b>, ne podíl.
///
/// <para>Býval to podíl (0,2 z celého stromu). Jenže strom plné hry roste, a
/// s ním tiše rostlo i demo: každých pět nových technologií v plné verzi
/// přidalo jednu do ukázky, aniž by to kdokoli chtěl. Kolik výzkumu má hráč
/// v demu projít, je rozhodnutí o délce ukázky — a to se nemá měnit samo.</para>
/// </param>
/// <summary>
/// Jeden druh zlatého úlovku — vzácný tvor, na kterého se dá kliknout.
///
/// <para>Data, ne kód: přidat fénixe nebo křišťálovou rybku má být záznam
/// v JSON, ne nová třída. Odměna je buď balík suroviny, nebo slavnost —
/// dvě čísla a jeden přepínač, žádná logika v datech.</para>
/// </summary>
/// <param name="Sprite">ID spritu ve knihovně (např. <c>fx.golden</c>).</param>
/// <param name="LifeSeconds">Jak dlouho je k mání, než zmizí.</param>
/// <param name="DriftTilesPerSecond">Jak rychle se posouvá; 0 = stojí.</param>
/// <param name="RewardFraction">Podíl kapacity skladu, který padne.</param>
/// <param name="MinReward">Spodní mez odměny, ať se malý sklad vyplatí taky.</param>
/// <param name="GrantsFestival">Místo surovin vyhlásí slavnost.</param>
public sealed record GoldenKindDef(
    string Id,
    string Sprite,
    double LifeSeconds,
    double DriftTilesPerSecond,
    double RewardFraction,
    int MinReward,
    bool GrantsFestival);

/// <summary>
/// Zlaté úlovky: jak často se objevují a co všechno se může objevit.
///
/// <para>Vzácnost je celý smysl — příliš krátký rozestup a přestane to být
/// událost. Proto je rozestup v datech, ne v kódu.</para>
/// </summary>
public sealed record GoldenConfig(
    double MinGapSeconds,
    double MaxGapSeconds,
    IReadOnlyList<GoldenKindDef> Kinds)
{
    /// <summary>Záloha, když blok v datech chybí — jeden bezejmenný třpyt jako dřív.</summary>
    public static GoldenConfig Default { get; } = new(
        65, 120,
        new[] { new GoldenKindDef("golden", "fx.golden", 7, 0, 0.08, 15, false) });

    /// <summary>Je vůbec co losovat?</summary>
    public bool IsEnabled => Kinds.Count > 0;
}

/// <summary>
/// Rozvod proudu.
///
/// <para>Dokud tenhle blok v datech nebyl, byla energie <b>jedno globální
/// číslo</b>: součet výroby děleno součet spotřeby, stejný pro celou říši.
/// Elektrárna postavená kdekoli zásobovala všechno, takže „kam s ní" nebyla
/// otázka. S dosahem se z ní stává rozhodnutí o místě.</para>
///
/// <para>Bez bloku v datech zůstane globální chování — starší data i mody
/// dostanou přesně tu hru, jakou měly.</para>
/// </summary>
/// <param name="Range">Kolik buněk 8×8 od elektrárny proud dosáhne. 0 = globální rozvod jako dřív.</param>
public sealed record PowerConfig(int Range)
{
    /// <summary>Jedno číslo pro celou říši (chování před prostorovým rozvodem).</summary>
    public static PowerConfig Global { get; } = new(0);

    /// <summary>Má proud dosah, nebo teče všude?</summary>
    public bool IsEnabled => Range > 0;
}

/// <summary>
/// Podmořská vrstva: jak daleko od přístavu se dá stavět na dně.
///
/// <para>Proč vůbec dosah: bez něj by šel dóm postavit uprostřed oceánu na
/// druhé polokouli. Podmořská stavba není nezávislá osada — visí na přístavu,
/// který ji zásobuje. Dosah tedy není jen omezení, je to <b>ta mechanika</b>:
/// hráč nejdřív rozhodne kam přístav, a moře kolem něj tím teprve otevře.</para>
///
/// <para>Šíří se to jen po vodě (viz <c>SubseaNetwork</c>), takže poloostrov je
/// hráz — dvě zátoky vedle sebe nejsou totéž co jedna.</para>
/// </summary>
/// <param name="Range">Kolik vodních dlaždic od kotvy síť dosáhne. 0 = vrstva vypnutá.</param>
public sealed record SubseaConfig(int Range)
{
    /// <summary>Bez bloku v datech se pod hladinou nestaví vůbec.</summary>
    public static SubseaConfig Disabled { get; } = new(0);

    /// <summary>Dá se na dně vůbec stavět?</summary>
    public bool IsEnabled => Range > 0;
}

/// <param name="TechIds">
/// Které technologie ukázka nabídne — <b>vyjmenované</b>, ne prvních N.
///
/// <para>Bez seznamu se bere prvních <see cref="DemoConfig.TechCount"/> uzlů
/// v pořadí souboru, a to je špatný vzorek: v prvních šestnácti je deset
/// takových, které neodemknou <b>nic viditelného</b> (jsou to pasivní
/// násobiče). Hráč v ukázce desetkrát klikne na výzkum a desetkrát se mu nic
/// nového neobjeví — a odemykání je přitom ta odměna, kvůli které se hraje
/// dál.</para>
///
/// <para>Prázdný seznam = chovej se jako dřív (prvních N).</para>
/// </param>
public sealed record DemoConfig(
    double PopulationCap,
    long AscensionRequirement,
    int TechCount,
    IReadOnlyList<string> TechIds)
{
    /// <summary>Výchozí meze, když si data neřeknou jinak.</summary>
    public static DemoConfig Default { get; } = new(1_500, 10_000, 16, Array.Empty<string>());

    /// <summary>Vybírá ukázka technologie jmenovitě?</summary>
    public bool HasCuratedTechs => TechIds.Count > 0;

    /// <summary>
    /// Kolik uzlů stromu je v ukázce dostupných. Vždy aspoň jeden — strom bez
    /// jediné dostupné technologie by vypadal jako rozbitá hra — a nikdy víc,
    /// než kolik jich vůbec je.
    /// </summary>
    public int TechCountFor(int totalTechs) =>
        Math.Clamp(TechCount, 1, Math.Max(1, totalTechs));
}
