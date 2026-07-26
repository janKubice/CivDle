namespace CivDle.Core.Content.Dto;

// Syrové tvary JSON souborů (1:1 s tím, co je na disku). Loader je zvaliduje
// a převede na runtime modely (Biome, BuildingDef, ...) — díky oddělení DTO/model
// zůstává zbytek kódu bez nullable polí a bez surových stringů.

/// <summary>Obsah souboru <c>data/biomes.json</c>.</summary>
public sealed record BiomesFileDto(int SchemaVersion, List<BiomeDto>? Biomes);

/// <summary>Jedna definice biomu tak, jak leží v JSON.</summary>
public sealed record BiomeDto(
    string? Id,
    string? MapColor,
    double ColorVariation,
    bool IsWater,
    double[]? DepthRange,
    double[]? ElevationRange,
    double[]? MoistureRange,
    double[]? TemperatureRange,
    ClickYieldDto? ClickYield,
    double ProductionMult);

/// <summary>Výnos ručního kliknutí na biom tak, jak leží v JSON.</summary>
public sealed record ClickYieldDto(string? Resource, int Amount);

/// <summary>Obsah souboru <c>data/resources.json</c>.</summary>
public sealed record ResourcesFileDto(int SchemaVersion, List<ResourceDto>? Resources);

/// <summary>Jedna surovina tak, jak leží v JSON.</summary>
public sealed record ResourceDto(string? Id, string? MapColor, double StartAmount, double BaseStorage);

/// <summary>Obsah souboru <c>data/buildings.json</c>.</summary>
public sealed record BuildingsFileDto(int SchemaVersion, List<BuildingDto>? Buildings);

/// <summary>Jedna budova tak, jak leží v JSON.</summary>
public sealed record BuildingDto(
    string? Id,
    string? Category,
    string? MapColor,
    int[]? Footprint,
    int WorkerSlots,
    int HousingCapacity,
    Dictionary<string, int>? BuildCost,
    RecipeDto? Recipe,
    string[]? AllowedBiomes,
    Dictionary<string, int>? Storage,
    bool AutoBuild,
    bool? Buildable,
    string? UpgradesTo,
    Dictionary<string, int>? UpgradeCost,
    int PowerSupply,
    int PowerDemand,
    bool RequiresAdjacentWater);

/// <summary>Výrobní recept budovy tak, jak leží v JSON.</summary>
public sealed record RecipeDto(
    Dictionary<string, int>? Input,
    Dictionary<string, int>? Output,
    int TimeTicks);

/// <summary>Obsah souboru <c>data/gameplay.json</c>.</summary>
public sealed record GameplayFileDto(
    int SchemaVersion,
    double StartingPopulation,
    int BaseHousingCapacity,
    double PopulationGrowthPerSecond,
    double FoodPerPersonPerSecond,
    string? FoodResource,
    AutoBuildDto? AutoBuild,
    RoadsDto? Roads,
    SettlementsDto? Settlements,
    DayNightDto? DayNight,
    BoostDto? Boost,
    HarvestDto? Harvest,
    DailyRewardDto? DailyReward,
    PlantingDto? Planting);

/// <summary>Nastavení slavnosti (dočasný boost) tak, jak leží v JSON.</summary>
public sealed record BoostDto(int DurationSeconds, int CooldownSeconds, double Multiplier);

/// <summary>Nastavení kritického sběru tak, jak leží v JSON.</summary>
public sealed record HarvestDto(double CritChance, double CritMultiplier, double JackpotMultiplier);

/// <summary>Denní odměna tak, jak leží v JSON.</summary>
public sealed record DailyRewardDto(Dictionary<string, int>? Reward, int StreakCap);

/// <summary>Sázení tak, jak leží v JSON.</summary>
public sealed record PlantingDto(Dictionary<string, int>? Cost, string? Resource, int Amount);

/// <summary>Denní/noční cyklus tak, jak leží v JSON.</summary>
public sealed record DayNightDto(
    double DayLengthSeconds,
    double StartTimeOfDay,
    string? NightColor,
    string? DuskColor,
    double NightAlpha,
    double DuskAlpha);

/// <summary>Obsah souboru <c>data/decorations.json</c>.</summary>
public sealed record DecorationsFileDto(int SchemaVersion, List<DecorationDto>? Decorations);

/// <summary>Jedna dekorace tak, jak leží v JSON.</summary>
public sealed record DecorationDto(
    string? Id,
    string[]? Biomes,
    string[]? Colors,
    double Density,
    int MinSize,
    int MaxSize);

/// <summary>Obsah souboru <c>data/fauna.json</c>.</summary>
public sealed record FaunaFileDto(int SchemaVersion, List<FaunaDto>? Fauna);

/// <summary>Jeden tvor tak, jak leží v JSON.</summary>
public sealed record FaunaDto(
    string? Id,
    string[]? Biomes,
    string? Color,
    int Size,
    double Speed,
    string? TimeOfDay,
    bool Glow);

/// <summary>Nastavení auto-stavby tak, jak leží v JSON.</summary>
public sealed record AutoBuildDto(int IntervalTicks, int SearchRadius, int PopulationHeadroom);

/// <summary>Nastavení auto-silnic tak, jak leží v JSON.</summary>
public sealed record RoadsDto(string? MapColor, int MaxSearchDistance, int MaxBridgeSpan);

/// <summary>Nastavení detekce osad tak, jak leží v JSON.</summary>
public sealed record SettlementsDto(int MinBuildings, int ClusterDistance, int UpdateIntervalTicks);

/// <summary>Obsah souboru <c>data/settlement-names.json</c>.</summary>
public sealed record SettlementNamesFileDto(int SchemaVersion, List<string>? Names);

/// <summary>Obsah souboru <c>data/tech.json</c>.</summary>
public sealed record TechFileDto(int SchemaVersion, List<TechDto>? Techs);

/// <summary>Jedna technologie tak, jak leží v JSON.</summary>
public sealed record TechDto(
    string? Id,
    Dictionary<string, int>? Cost,
    string[]? Prerequisites,
    string[]? Unlocks,
    string? Effect,
    double Magnitude);

/// <summary>
/// Podmínka cíle/achievementu/Vzestupu tak, jak leží v JSON: metrika + práh + volitelný
/// odkaz (surovina/budova/technologie). Sdílené napříč prestige, úkoly a achievementy.
/// </summary>
public sealed record GoalConditionDto(
    string? Metric,
    string? Resource,
    string? Building,
    string? Tech,
    long Target);

/// <summary>Obsah souboru <c>data/prestige.json</c> (Vzestup + trvalé upgrady).</summary>
public sealed record PrestigeFileDto(
    int SchemaVersion,
    PrestigeAscensionDto? Ascension,
    List<PrestigeUpgradeDto>? Upgrades);

/// <summary>Nastavení dostupnosti a odměny Vzestupu tak, jak leží v JSON.</summary>
public sealed record PrestigeAscensionDto(GoalConditionDto? Requirement, PrestigePointsDto? Points, double RequirementGrowth);

/// <summary>Jak se z metriky počítají body Vzestupu.</summary>
public sealed record PrestigePointsDto(string? Metric, string? Resource, long Divisor);

/// <summary>Jeden trvalý upgrade Vzestupu tak, jak leží v JSON.</summary>
public sealed record PrestigeUpgradeDto(
    string? Id,
    string? Effect,
    double Magnitude,
    int Cost,
    string[]? Prerequisites);

/// <summary>Obsah souboru <c>data/quests.json</c> (pevné úkoly + dynamické).</summary>
public sealed record QuestFileDto(
    int SchemaVersion,
    List<QuestDto>? Quests,
    DynamicQuestDto? Dynamic);

/// <summary>Jeden pevný úkol tak, jak leží v JSON.</summary>
public sealed record QuestDto(string? Id, GoalConditionDto? Condition, Dictionary<string, int>? Reward);

/// <summary>Nastavení dynamických úkolů tak, jak leží v JSON.</summary>
public sealed record DynamicQuestDto(
    GoalConditionDto? Condition,
    double TargetGrowth,
    Dictionary<string, int>? Reward,
    double RewardGrowth);

/// <summary>Obsah souboru <c>data/eras.json</c> (éry civilizace T0–T6).</summary>
public sealed record ErasFileDto(int SchemaVersion, List<EraDto>? Eras);

/// <summary>Jedna éra tak, jak leží v JSON.</summary>
public sealed record EraDto(string? Id, int Order, string? UnlockTech);

/// <summary>Obsah souboru <c>data/zones.json</c> (typy zón pro automatizaci).</summary>
public sealed record ZonesFileDto(int SchemaVersion, List<ZoneTypeDto>? Zones);

/// <summary>Jeden typ zóny tak, jak leží v JSON.</summary>
public sealed record ZoneTypeDto(string? Id, string? MapColor, List<string>? Buildings);

/// <summary>Obsah souboru <c>data/policies.json</c> (politiky růstu pro automatizaci).</summary>
public sealed record PoliciesFileDto(int SchemaVersion, List<PolicyDto>? Policies);

/// <summary>Jedna politika růstu tak, jak leží v JSON.</summary>
public sealed record PolicyDto(string? Id, string? Effect, double Magnitude);

/// <summary>Obsah souboru <c>data/features.json</c> (odemykatelné herní funkce).</summary>
public sealed record FeaturesFileDto(int SchemaVersion, List<FeatureDto>? Features);

/// <summary>Jedna odemykatelná funkce tak, jak leží v JSON.</summary>
public sealed record FeatureDto(string? Id, GoalConditionDto? Unlock);

/// <summary>Obsah souboru <c>data/landmarks.json</c> (vzácné body zájmu na mapě).</summary>
public sealed record LandmarksFileDto(int SchemaVersion, List<LandmarkDto>? Landmarks);

/// <summary>Jeden landmark tak, jak leží v JSON.</summary>
public sealed record LandmarkDto(
    string? Id,
    string[]? Biomes,
    string? MapColor,
    int Size,
    int Rarity,
    ClickYieldDto? ClickYield);

/// <summary>Obsah souboru <c>data/weather.json</c> (počasí vázané na biom).</summary>
public sealed record WeatherFileDto(int SchemaVersion, List<WeatherDto>? Weather);

/// <summary>Jeden jev počasí tak, jak leží v JSON.</summary>
public sealed record WeatherDto(
    string? Id,
    string[]? Biomes,
    bool Extreme,
    double ProductionMult,
    double DurationSeconds,
    double Weight,
    string? Tint,
    double TintAlpha,
    string? Particle);

/// <summary>Obsah souboru <c>data/ascension-tiers.json</c> (stupně měřítka).</summary>
public sealed record AscensionTiersFileDto(int SchemaVersion, List<AscensionTierDto>? Tiers);

/// <summary>Jeden stupeň měřítka tak, jak leží v JSON.</summary>
public sealed record AscensionTierDto(string? Id, int Order, double PopulationCap, List<string>? Unlocks);

/// <summary>Obsah souboru <c>data/events.json</c> (náhodné události s volbami).</summary>
public sealed record EventFileDto(int SchemaVersion, List<EventDto>? Events);

/// <summary>Jedna událost tak, jak leží v JSON.</summary>
public sealed record EventDto(string? Id, List<EventChoiceDto>? Choices);

/// <summary>Jedna volba události tak, jak leží v JSON.</summary>
public sealed record EventChoiceDto(string? Id, Dictionary<string, int>? Cost, Dictionary<string, int>? Gain);

/// <summary>Obsah souboru <c>data/achievements.json</c>.</summary>
public sealed record AchievementFileDto(int SchemaVersion, List<AchievementDto>? Achievements);

/// <summary>Jeden achievement tak, jak leží v JSON.</summary>
public sealed record AchievementDto(string? Id, GoalConditionDto? Condition, bool Hidden);

/// <summary>Obsah souboru <c>data/devlog.json</c>.</summary>
public sealed record DevlogFileDto(int SchemaVersion, List<DevlogEntryDto>? Entries);

/// <summary>Jeden záznam deníku tak, jak leží v JSON.</summary>
public sealed record DevlogEntryDto(string? Version, string? Date, List<string>? Lines);

/// <summary>Obsah jednoho jazyka <c>data/lang/*.json</c>.</summary>
public sealed record LanguageFileDto(
    int SchemaVersion,
    string? Id,
    string? NativeName,
    Dictionary<string, string>? Strings);

/// <summary>Obsah souboru <c>data/worldgen.json</c>.</summary>
public sealed record WorldGenFileDto(
    int SchemaVersion,
    string? DefaultSize,
    string? DefaultPreset,
    List<WorldSizeDto>? Sizes,
    List<TerrainPresetDto>? Presets);

/// <summary>Volitelná velikost světa v menu nové hry.</summary>
public sealed record WorldSizeDto(string? Id, int Width, int Height);

/// <summary>Parametry frekvenčního šumu (fBm) pro jednu vrstvu terénu.</summary>
public sealed record NoiseDto(double Frequency, int Octaves, double Persistence, double Lacunarity);

/// <summary>Preset generátoru („Kontinenty", „Ostrovy", …).</summary>
public sealed record TerrainPresetDto(
    string? Id,
    double SeaLevel,
    string? FallbackBiome,
    NoiseDto? ElevationNoise,
    NoiseDto? MoistureNoise,
    NoiseDto? RiverNoise,
    double RiverWidth,
    double RiverMaxElevation,
    NoiseDto? TemperatureNoise,
    double TemperatureBandTiles,
    double TemperatureLapse,
    string? RiverBiome);

/// <summary>Obsah souboru <c>data/ufo.json</c> (návštěvy UFO).</summary>
public sealed record UfoFileDto(int SchemaVersion, UfoConfigDto? Ufo);

/// <summary>Nastavení návštěv UFO tak, jak leží v JSON.</summary>
public sealed record UfoConfigDto(
    double WindowSeconds,
    double Chance,
    double VisitSeconds,
    int Radius,
    List<UfoActionDto>? Actions);

/// <summary>Jeden zásah UFO tak, jak leží v JSON.</summary>
public sealed record UfoActionDto(string? Id, string? Behavior, double Weight, double Magnitude);
