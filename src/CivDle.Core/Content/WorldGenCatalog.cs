namespace CivDle.Core.Content;

/// <summary>Parametry fBm šumu jedné terénní vrstvy. Frekvence = počet „vln" na 100 dlaždic.</summary>
public sealed record NoiseSpec(float Frequency, int Octaves, float Persistence, float Lacunarity);

/// <summary>Volitelná velikost světa (položka v menu nové hry).</summary>
public sealed record WorldSize(string Id, string Name, int Width, int Height);

/// <summary>
/// Zvalidovaný preset generátoru světa. <paramref name="FallbackBiomeIndex"/> je pevninský biom,
/// který se použije, když žádná definice nepokryje kombinaci výška × vlhkost.
/// </summary>
public sealed record TerrainPreset(
    string Id,
    string Name,
    float SeaLevel,
    int FallbackBiomeIndex,
    NoiseSpec ElevationNoise,
    NoiseSpec MoistureNoise);

/// <summary>
/// Katalog nastavení generátoru z <c>data/worldgen.json</c>: velikosti světa a terénní presety,
/// včetně výchozích voleb pro menu nové hry.
/// </summary>
public sealed class WorldGenCatalog
{
    public WorldGenCatalog(
        IReadOnlyList<WorldSize> sizes,
        IReadOnlyList<TerrainPreset> presets,
        int defaultSizeIndex,
        int defaultPresetIndex)
    {
        if (sizes.Count == 0) throw new ArgumentException("Chybí velikosti světa.", nameof(sizes));
        if (presets.Count == 0) throw new ArgumentException("Chybí presety generátoru.", nameof(presets));
        if (defaultSizeIndex < 0 || defaultSizeIndex >= sizes.Count) throw new ArgumentOutOfRangeException(nameof(defaultSizeIndex));
        if (defaultPresetIndex < 0 || defaultPresetIndex >= presets.Count) throw new ArgumentOutOfRangeException(nameof(defaultPresetIndex));

        Sizes = sizes;
        Presets = presets;
        DefaultSizeIndex = defaultSizeIndex;
        DefaultPresetIndex = defaultPresetIndex;
    }

    /// <summary>Nabídka velikostí světa v pořadí souboru.</summary>
    public IReadOnlyList<WorldSize> Sizes { get; }

    /// <summary>Nabídka terénních presetů v pořadí souboru.</summary>
    public IReadOnlyList<TerrainPreset> Presets { get; }

    /// <summary>Předvybraná velikost v menu nové hry.</summary>
    public int DefaultSizeIndex { get; }

    /// <summary>Předvybraný preset v menu nové hry.</summary>
    public int DefaultPresetIndex { get; }
}
