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
public sealed record RoadConfig(RgbColor MapColor, int MaxSearchDistance);

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

/// <summary>Ruční sběr: šance na „krit" (velký výnos) — aktivní klikání se vyplatí.</summary>
/// <param name="CritChance">Pravděpodobnost kritu (0–1) na jeden sběr.</param>
/// <param name="CritMultiplier">Násobič výnosu při kritu.</param>
public sealed record HarvestConfig(double CritChance, double CritMultiplier);

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
    HarvestConfig Harvest);
