using CivDle.Core.Content;
using CivDle.Core.WorldGen;

namespace CivDle.Core.World;

/// <summary>
/// Nekonečný procedurální terén: dvě vrstvy fBm šumu (výška, vlhkost) a výběr biomu
/// podle rozsahů z JSON definic. Terén je čistá funkce (seed + preset) — pro libovolnou
/// dlaždici (i zápornou) vrací stejný biom, nikdy se neukládá.
///
/// Výběr biomu: pod hladinou moře rozhoduje hloubka (vodní biomy), nad hladinou
/// přeškálovaná výška × vlhkost (pevninské biomy, první shoda v pořadí souboru).
/// Algoritmus v kódu, čísla v datech (data-driven-content.md).
/// </summary>
public sealed class ProceduralTerrain : ITerrain
{
    // Frekvence v presetech je „počet vln na 100 dlaždic" → dlaždicové souřadnice dělíme 100.
    private const float FrequencyScale = 1f / 100f;

    // Sůl pro odvození seedu vlhkosti, aby výška a vlhkost nebyly korelované.
    private const ulong MoistureSeedSalt = 0x9E3779B97F4A7C15UL;

    private readonly BiomeRegistry _biomes;
    private readonly TerrainPreset _preset;
    private readonly FractalNoise _elevationNoise;
    private readonly FractalNoise _moistureNoise;
    private readonly int[] _waterBiomes;
    private readonly int[] _landBiomes;

    public ProceduralTerrain(BiomeRegistry biomes, TerrainPreset preset, long seed)
    {
        _biomes = biomes;
        _preset = preset;
        Seed = seed;
        _elevationNoise = new FractalNoise(seed, preset.ElevationNoise);
        _moistureNoise = new FractalNoise(DeriveMoistureSeed(seed), preset.MoistureNoise);

        var water = new List<int>();
        var land = new List<int>();
        for (int i = 0; i < biomes.Count; i++)
        {
            (biomes[i].IsWater ? water : land).Add(i);
        }

        _waterBiomes = water.ToArray();
        _landBiomes = land.ToArray();
    }

    /// <summary>Seed, ze kterého terén vznikl (pro serializaci savu).</summary>
    public long Seed { get; }

    /// <summary>Preset, ze kterého terén vznikl.</summary>
    public TerrainPreset Preset => _preset;

    /// <summary>Normalizovaná výška 0–1 (pod hladinou moře = voda).</summary>
    public float ElevationAt(int x, int y) => _elevationNoise.Sample01(x * FrequencyScale, y * FrequencyScale);

    /// <summary>Vlhkost 0–1 (řídí vegetaci, suroviny a dekorace).</summary>
    public float MoistureAt(int x, int y) => _moistureNoise.Sample01(x * FrequencyScale, y * FrequencyScale);

    public byte BiomeAt(int x, int y)
    {
        float elevation = ElevationAt(x, y);
        float moisture = MoistureAt(x, y);

        if (elevation < _preset.SeaLevel)
        {
            // Hloubka 0 = u pobřeží, 1 = nejhlubší oceán.
            float depth = (_preset.SeaLevel - elevation) / _preset.SeaLevel;
            foreach (int index in _waterBiomes)
            {
                if (_biomes[index].DepthRange.Contains(depth))
                {
                    return (byte)index;
                }
            }

            return (byte)_waterBiomes[^1];
        }

        // Výška nad mořem přeškálovaná na 0–1, aby rozsahy biomů nezávisely na seaLevel.
        float landElevation = (elevation - _preset.SeaLevel) / (1f - _preset.SeaLevel);
        foreach (int index in _landBiomes)
        {
            var biome = _biomes[index];
            if (biome.ElevationRange.Contains(landElevation) && biome.MoistureRange.Contains(moisture))
            {
                return (byte)index;
            }
        }

        return (byte)_preset.FallbackBiomeIndex;
    }

    private static long DeriveMoistureSeed(long seed)
    {
        var rng = new SplitMix64(unchecked((ulong)seed ^ MoistureSeedSalt));
        return unchecked((long)rng.Next());
    }
}
