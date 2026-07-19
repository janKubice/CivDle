namespace CivDle.Core.Content.Dto;

// Syrové tvary JSON souborů (1:1 s tím, co je na disku). Loader je zvaliduje
// a převede na runtime modely (Biome, TerrainPreset, ...) — díky oddělení DTO/model
// zůstává zbytek kódu bez nullable polí a bez surových stringů.

/// <summary>Obsah souboru <c>data/biomes.json</c>.</summary>
public sealed record BiomesFileDto(int SchemaVersion, List<BiomeDto>? Biomes);

/// <summary>Jedna definice biomu tak, jak leží v JSON.</summary>
public sealed record BiomeDto(
    string? Id,
    string? Name,
    string? MapColor,
    double ColorVariation,
    bool IsWater,
    double[]? DepthRange,
    double[]? ElevationRange,
    double[]? MoistureRange);

/// <summary>Obsah souboru <c>data/worldgen.json</c>.</summary>
public sealed record WorldGenFileDto(
    int SchemaVersion,
    string? DefaultSize,
    string? DefaultPreset,
    List<WorldSizeDto>? Sizes,
    List<TerrainPresetDto>? Presets);

/// <summary>Volitelná velikost světa v menu nové hry.</summary>
public sealed record WorldSizeDto(string? Id, string? Name, int Width, int Height);

/// <summary>Parametry frekvenčního šumu (fBm) pro jednu vrstvu terénu.</summary>
public sealed record NoiseDto(double Frequency, int Octaves, double Persistence, double Lacunarity);

/// <summary>Preset generátoru („Kontinenty", „Ostrovy", …).</summary>
public sealed record TerrainPresetDto(
    string? Id,
    string? Name,
    double SeaLevel,
    string? FallbackBiome,
    NoiseDto? ElevationNoise,
    NoiseDto? MoistureNoise);
