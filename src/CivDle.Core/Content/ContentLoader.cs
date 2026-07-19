using System.Text.Json;
using CivDle.Core.Content.Dto;

namespace CivDle.Core.Content;

/// <summary>
/// Načte JSON definice ze složky <c>data/</c> a fail-fast je zvaliduje:
/// chybný odkaz nebo hodnota = <see cref="ContentLoadException"/> hned při startu
/// se jménem souboru a srozumitelnou hláškou (viz data-driven-content.md, sekce 8).
/// </summary>
public sealed class ContentLoader
{
    /// <summary>Verze schématu, které tento loader rozumí. Starší/novější data jsou chyba.</summary>
    public const int SupportedSchemaVersion = 1;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    /// <summary>Načte kompletní herní obsah ze složky s daty.</summary>
    public GameContent LoadFrom(string dataDirectory)
    {
        if (!Directory.Exists(dataDirectory))
        {
            throw new ContentLoadException(dataDirectory, $"Složka s herními daty '{dataDirectory}' neexistuje.");
        }

        var biomes = LoadBiomes(Path.Combine(dataDirectory, "biomes.json"));
        var worldGen = LoadWorldGen(Path.Combine(dataDirectory, "worldgen.json"), biomes);
        return new GameContent(biomes, worldGen);
    }

    private static BiomeRegistry LoadBiomes(string path)
    {
        var file = ReadFile<BiomesFileDto>(path);
        CheckSchemaVersion(path, file.SchemaVersion);

        if (file.Biomes is not { Count: > 0 })
        {
            throw new ContentLoadException(path, "Soubor neobsahuje žádný biom.");
        }

        if (file.Biomes.Count > BiomeRegistry.MaxBiomes)
        {
            throw new ContentLoadException(path, $"Příliš mnoho biomů ({file.Biomes.Count}), maximum je {BiomeRegistry.MaxBiomes}.");
        }

        var seenIds = new HashSet<string>(StringComparer.Ordinal);
        var biomes = new List<Biome>(file.Biomes.Count);
        for (int i = 0; i < file.Biomes.Count; i++)
        {
            var biome = ValidateBiome(path, file.Biomes[i], i);
            if (!seenIds.Add(biome.Id))
            {
                throw new ContentLoadException(path, $"Duplicitní ID biomu '{biome.Id}'.");
            }

            biomes.Add(biome);
        }

        ValidateWaterDepthCoverage(path, biomes);
        return new BiomeRegistry(biomes);
    }

    private static Biome ValidateBiome(string path, BiomeDto dto, int index)
    {
        if (string.IsNullOrWhiteSpace(dto.Id))
        {
            throw new ContentLoadException(path, $"Biom na pozici {index} nemá vyplněné 'id'.");
        }

        var id = dto.Id.Trim();
        if (string.IsNullOrWhiteSpace(dto.Name))
        {
            throw new ContentLoadException(path, $"Biom '{id}' nemá vyplněné 'name'.");
        }

        if (!RgbColor.TryParse(dto.MapColor, out var color))
        {
            throw new ContentLoadException(path, $"Biom '{id}' má neplatnou barvu 'mapColor' = '{dto.MapColor}' (očekávám '#RRGGBB').");
        }

        if (dto.ColorVariation is < 0 or > 0.5)
        {
            throw new ContentLoadException(path, $"Biom '{id}': 'colorVariation' musí být v rozsahu 0–0.5, je {dto.ColorVariation}.");
        }

        ValueRange depth = ValueRange.Full;
        ValueRange elevation = ValueRange.Full;
        ValueRange moisture = ValueRange.Full;

        if (dto.IsWater)
        {
            depth = ParseRange(path, id, "depthRange", dto.DepthRange, required: true);
        }
        else
        {
            elevation = ParseRange(path, id, "elevationRange", dto.ElevationRange, required: true);
            moisture = ParseRange(path, id, "moistureRange", dto.MoistureRange, required: false);
        }

        return new Biome(id, dto.Name.Trim(), color, (float)dto.ColorVariation, dto.IsWater, depth, elevation, moisture);
    }

    private static ValueRange ParseRange(string path, string biomeId, string field, double[]? values, bool required)
    {
        if (values is null)
        {
            return required
                ? throw new ContentLoadException(path, $"Biom '{biomeId}' nemá vyplněné pole '{field}'.")
                : ValueRange.Full;
        }

        if (values.Length != 2)
        {
            throw new ContentLoadException(path, $"Biom '{biomeId}': pole '{field}' musí mít přesně 2 hodnoty [min, max].");
        }

        double min = values[0], max = values[1];
        if (min < 0 || max > 1 || min > max)
        {
            throw new ContentLoadException(path, $"Biom '{biomeId}': '{field}' = [{min}, {max}] musí splňovat 0 ≤ min ≤ max ≤ 1.");
        }

        return new ValueRange((float)min, (float)max);
    }

    /// <summary>
    /// Vodní biomy musí hloubkami pokrýt celý interval 0–1, jinak by generátor pro některou
    /// hloubku neměl co vybrat. Kontroluje se tady, aby díra v datech spadla při startu.
    /// </summary>
    private static void ValidateWaterDepthCoverage(string path, List<Biome> biomes)
    {
        var water = biomes.Where(b => b.IsWater).OrderBy(b => b.DepthRange.Min).ToList();
        if (water.Count == 0)
        {
            throw new ContentLoadException(path, "Chybí vodní biom (isWater = true) — moře by nemělo jak vypadat.");
        }

        const float epsilon = 0.0001f;
        if (water[0].DepthRange.Min > epsilon)
        {
            throw new ContentLoadException(path, $"Hloubky vodních biomů nepokrývají mělčinu: nejnižší 'depthRange' začíná na {water[0].DepthRange.Min}, musí od 0.");
        }

        float covered = water[0].DepthRange.Max;
        foreach (var biome in water.Skip(1))
        {
            if (biome.DepthRange.Min > covered + epsilon)
            {
                throw new ContentLoadException(path, $"Díra v pokrytí hloubek vodních biomů mezi {covered} a {biome.DepthRange.Min} (biom '{biome.Id}').");
            }

            covered = MathF.Max(covered, biome.DepthRange.Max);
        }

        if (covered < 1f - epsilon)
        {
            throw new ContentLoadException(path, $"Hloubky vodních biomů pokrývají jen 0–{covered}, hlubina až do 1 chybí.");
        }
    }

    private static WorldGenCatalog LoadWorldGen(string path, BiomeRegistry biomes)
    {
        var file = ReadFile<WorldGenFileDto>(path);
        CheckSchemaVersion(path, file.SchemaVersion);

        if (file.Sizes is not { Count: > 0 })
        {
            throw new ContentLoadException(path, "Soubor neobsahuje žádnou velikost světa ('sizes').");
        }

        if (file.Presets is not { Count: > 0 })
        {
            throw new ContentLoadException(path, "Soubor neobsahuje žádný preset generátoru ('presets').");
        }

        var sizes = new List<WorldSize>(file.Sizes.Count);
        var sizeIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var dto in file.Sizes)
        {
            sizes.Add(ValidateSize(path, dto, sizeIds));
        }

        var presets = new List<TerrainPreset>(file.Presets.Count);
        var presetIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var dto in file.Presets)
        {
            presets.Add(ValidatePreset(path, dto, presetIds, biomes));
        }

        int defaultSize = ResolveDefault(path, "defaultSize", file.DefaultSize, sizes, s => s.Id);
        int defaultPreset = ResolveDefault(path, "defaultPreset", file.DefaultPreset, presets, p => p.Id);
        return new WorldGenCatalog(sizes, presets, defaultSize, defaultPreset);
    }

    private static WorldSize ValidateSize(string path, WorldSizeDto dto, HashSet<string> seenIds)
    {
        if (string.IsNullOrWhiteSpace(dto.Id))
        {
            throw new ContentLoadException(path, "Velikost světa nemá vyplněné 'id'.");
        }

        var id = dto.Id.Trim();
        if (!seenIds.Add(id))
        {
            throw new ContentLoadException(path, $"Duplicitní ID velikosti světa '{id}'.");
        }

        if (string.IsNullOrWhiteSpace(dto.Name))
        {
            throw new ContentLoadException(path, $"Velikost světa '{id}' nemá vyplněné 'name'.");
        }

        if (dto.Width is < 16 or > 4096 || dto.Height is < 16 or > 4096)
        {
            throw new ContentLoadException(path, $"Velikost světa '{id}': rozměry {dto.Width}×{dto.Height} musí být v rozsahu 16–4096.");
        }

        return new WorldSize(id, dto.Name.Trim(), dto.Width, dto.Height);
    }

    private static TerrainPreset ValidatePreset(string path, TerrainPresetDto dto, HashSet<string> seenIds, BiomeRegistry biomes)
    {
        if (string.IsNullOrWhiteSpace(dto.Id))
        {
            throw new ContentLoadException(path, "Preset generátoru nemá vyplněné 'id'.");
        }

        var id = dto.Id.Trim();
        if (!seenIds.Add(id))
        {
            throw new ContentLoadException(path, $"Duplicitní ID presetu '{id}'.");
        }

        if (string.IsNullOrWhiteSpace(dto.Name))
        {
            throw new ContentLoadException(path, $"Preset '{id}' nemá vyplněné 'name'.");
        }

        if (dto.SeaLevel is <= 0 or >= 1)
        {
            throw new ContentLoadException(path, $"Preset '{id}': 'seaLevel' musí být mezi 0 a 1 (bez krajů), je {dto.SeaLevel}.");
        }

        if (string.IsNullOrWhiteSpace(dto.FallbackBiome))
        {
            throw new ContentLoadException(path, $"Preset '{id}' nemá vyplněný 'fallbackBiome'.");
        }

        if (!biomes.TryIndexOf(dto.FallbackBiome.Trim(), out var fallbackIndex))
        {
            throw new ContentLoadException(path, $"Preset '{id}' odkazuje na neexistující biom '{dto.FallbackBiome}' ve 'fallbackBiome'.");
        }

        if (biomes[fallbackIndex].IsWater)
        {
            throw new ContentLoadException(path, $"Preset '{id}': 'fallbackBiome' ('{dto.FallbackBiome}') musí být pevninský biom, ne vodní.");
        }

        var elevation = ValidateNoise(path, id, "elevationNoise", dto.ElevationNoise);
        var moisture = ValidateNoise(path, id, "moistureNoise", dto.MoistureNoise);
        return new TerrainPreset(id, dto.Name.Trim(), (float)dto.SeaLevel, fallbackIndex, elevation, moisture);
    }

    private static NoiseSpec ValidateNoise(string path, string presetId, string field, NoiseDto? dto)
    {
        if (dto is null)
        {
            throw new ContentLoadException(path, $"Preset '{presetId}' nemá vyplněný blok '{field}'.");
        }

        if (dto.Frequency is <= 0 or > 100)
        {
            throw new ContentLoadException(path, $"Preset '{presetId}', '{field}': 'frequency' musí být v (0, 100], je {dto.Frequency}.");
        }

        if (dto.Octaves is < 1 or > 10)
        {
            throw new ContentLoadException(path, $"Preset '{presetId}', '{field}': 'octaves' musí být 1–10, je {dto.Octaves}.");
        }

        if (dto.Persistence is <= 0 or > 1)
        {
            throw new ContentLoadException(path, $"Preset '{presetId}', '{field}': 'persistence' musí být v (0, 1], je {dto.Persistence}.");
        }

        if (dto.Lacunarity is < 1 or > 8)
        {
            throw new ContentLoadException(path, $"Preset '{presetId}', '{field}': 'lacunarity' musí být 1–8, je {dto.Lacunarity}.");
        }

        return new NoiseSpec((float)dto.Frequency, dto.Octaves, (float)dto.Persistence, (float)dto.Lacunarity);
    }

    private static int ResolveDefault<T>(string path, string field, string? id, List<T> items, Func<T, string> idSelector)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return 0;
        }

        var wanted = id.Trim();
        for (int i = 0; i < items.Count; i++)
        {
            if (idSelector(items[i]) == wanted)
            {
                return i;
            }
        }

        throw new ContentLoadException(path, $"'{field}' odkazuje na neexistující ID '{id}'.");
    }

    private static void CheckSchemaVersion(string path, int version)
    {
        if (version != SupportedSchemaVersion)
        {
            throw new ContentLoadException(path, $"Nepodporovaná verze schématu {version}, tato verze hry rozumí verzi {SupportedSchemaVersion}.");
        }
    }

    private static T ReadFile<T>(string path)
    {
        if (!File.Exists(path))
        {
            throw new ContentLoadException(path, "Soubor nenalezen.");
        }

        try
        {
            var parsed = JsonSerializer.Deserialize<T>(File.ReadAllText(path), JsonOptions);
            return parsed ?? throw new ContentLoadException(path, "Soubor obsahuje jen 'null'.");
        }
        catch (JsonException ex)
        {
            throw new ContentLoadException(path, $"Neplatný JSON: {ex.Message}");
        }
    }
}
