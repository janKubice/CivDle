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
    double[]? MoistureRange);

/// <summary>Obsah souboru <c>data/resources.json</c>.</summary>
public sealed record ResourcesFileDto(int SchemaVersion, List<ResourceDto>? Resources);

/// <summary>Jedna surovina tak, jak leží v JSON.</summary>
public sealed record ResourceDto(string? Id, string? MapColor, double StartAmount);

/// <summary>Obsah souboru <c>data/buildings.json</c>.</summary>
public sealed record BuildingsFileDto(int SchemaVersion, List<BuildingDto>? Buildings);

/// <summary>Jedna budova tak, jak leží v JSON.</summary>
public sealed record BuildingDto(
    string? Id,
    string? MapColor,
    int[]? Footprint,
    int WorkerSlots,
    int HousingCapacity,
    Dictionary<string, int>? BuildCost,
    RecipeDto? Recipe,
    string[]? AllowedBiomes);

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
    string? FoodResource);

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
