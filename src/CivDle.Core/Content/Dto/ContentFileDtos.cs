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
    ClickYieldDto? ClickYield);

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
    Dictionary<string, int>? UpgradeCost);

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
    DayNightDto? DayNight);

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
public sealed record RoadsDto(string? MapColor, int MaxSearchDistance);

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
    string[]? Unlocks);

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
public sealed record PrestigeAscensionDto(GoalConditionDto? Requirement, PrestigePointsDto? Points);

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
    NoiseDto? MoistureNoise);
