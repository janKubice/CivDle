namespace CivDle.Core.Content;

/// <summary>Parametry fBm šumu jedné terénní vrstvy. Frekvence = počet „vln" na 100 dlaždic.</summary>
public sealed record NoiseSpec(float Frequency, int Octaves, float Persistence, float Lacunarity);

/// <summary>Volitelná velikost světa (položka v menu nové hry). Jméno je v jazycích pod <c>worldsize.&lt;Id&gt;</c>.</summary>
public sealed record WorldSize(string Id, int Width, int Height)
{
    /// <summary>Lokalizační klíč jména velikosti.</summary>
    public string NameKey => $"worldsize.{Id}";
}

/// <summary>
/// Zvalidovaný preset generátoru světa. <paramref name="FallbackBiomeIndex"/> je pevninský biom,
/// který se použije, když žádná definice nepokryje kombinaci výška × vlhkost.
/// Jméno je v jazycích pod <c>preset.&lt;Id&gt;</c>.
///
/// <para>Řeky: <paramref name="RiverWidth"/> = 0 je vypne. Vznikají z „hřebene" šumu
/// (viz <see cref="World.ProceduralTerrain"/>), takže zůstávají čistou funkcí souřadnic —
/// nekonečná mapa je nemusí ukládat.</para>
/// </summary>
public sealed record TerrainPreset(
    string Id,
    float SeaLevel,
    int FallbackBiomeIndex,
    NoiseSpec ElevationNoise,
    NoiseSpec MoistureNoise,
    NoiseSpec? RiverNoise = null,
    float RiverWidth = 0f,
    float RiverMaxElevation = 1f)
{
    /// <summary>Lokalizační klíč jména presetu.</summary>
    public string NameKey => $"preset.{Id}";
}

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
