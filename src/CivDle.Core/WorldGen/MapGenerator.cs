using CivDle.Core.Content;
using CivDle.Core.World;

namespace CivDle.Core.WorldGen;

/// <summary>Zadání pro generátor: seed + rozměry + terénní preset (z katalogu nebo testu).</summary>
public sealed record WorldGenRequest(long Seed, int Width, int Height, TerrainPreset Preset);

/// <summary>
/// Deterministický generátor světa: dvě vrstvy fBm šumu (výška, vlhkost) a výběr biomu
/// podle rozsahů z JSON definic. Stejný seed + stejná data = identická mapa.
///
/// Výběr biomu: pod hladinou moře rozhoduje hloubka (vodní biomy), nad hladinou
/// přeškálovaná výška × vlhkost (pevninské biomy, první shoda v pořadí souboru).
/// Algoritmus je v kódu, všechna čísla v datech — hranice dat/logiky dle data-driven-content.md.
/// </summary>
public sealed class MapGenerator
{
    // Frekvence v presetech je „počet vln na 100 dlaždic" → dlaždicové souřadnice dělíme 100.
    private const float FrequencyScale = 1f / 100f;

    // Sůl pro odvození seedu vlhkosti, aby výška a vlhkost nebyly korelované.
    private const ulong MoistureSeedSalt = 0x9E3779B97F4A7C15UL;

    /// <summary>Vygeneruje kompletní mapu světa podle zadání.</summary>
    public WorldMap Generate(GameContent content, WorldGenRequest request)
    {
        var biomes = content.Biomes;
        var preset = request.Preset;
        var map = new WorldMap(request.Width, request.Height);

        var elevationNoise = new FractalNoise(request.Seed, preset.ElevationNoise);
        var moistureNoise = new FractalNoise(DeriveMoistureSeed(request.Seed), preset.MoistureNoise);

        // Kandidáti se předpočítají jednou — vnitřní smyčka pak jen porovnává rozsahy.
        var waterBiomes = new List<int>();
        var landBiomes = new List<int>();
        for (int i = 0; i < biomes.Count; i++)
        {
            (biomes[i].IsWater ? waterBiomes : landBiomes).Add(i);
        }

        for (int y = 0; y < map.Height; y++)
        {
            for (int x = 0; x < map.Width; x++)
            {
                float noiseX = x * FrequencyScale;
                float noiseY = y * FrequencyScale;
                float elevation = elevationNoise.Sample01(noiseX, noiseY);
                float moisture = moistureNoise.Sample01(noiseX, noiseY);

                int index = map.Index(x, y);
                map.Elevation[index] = elevation;
                map.Moisture[index] = moisture;
                map.BiomeIndices[index] = (byte)PickBiome(biomes, preset, waterBiomes, landBiomes, elevation, moisture);
            }
        }

        return map;
    }

    private static int PickBiome(
        BiomeRegistry biomes,
        TerrainPreset preset,
        List<int> waterBiomes,
        List<int> landBiomes,
        float elevation,
        float moisture)
    {
        if (elevation < preset.SeaLevel)
        {
            // Hloubka 0 = u pobřeží, 1 = nejhlubší oceán.
            float depth = (preset.SeaLevel - elevation) / preset.SeaLevel;
            foreach (int index in waterBiomes)
            {
                if (biomes[index].DepthRange.Contains(depth))
                {
                    return index;
                }
            }

            // Pokrytí hloubek 0–1 hlídá validace při načtení; sem se lze dostat
            // jen zaokrouhlením na hraně intervalu.
            return waterBiomes[^1];
        }

        // Výška nad mořem přeškálovaná na 0–1, aby rozsahy biomů nezávisely na seaLevel.
        float landElevation = (elevation - preset.SeaLevel) / (1f - preset.SeaLevel);
        foreach (int index in landBiomes)
        {
            var biome = biomes[index];
            if (biome.ElevationRange.Contains(landElevation) && biome.MoistureRange.Contains(moisture))
            {
                return index;
            }
        }

        return preset.FallbackBiomeIndex;
    }

    private static long DeriveMoistureSeed(long seed)
    {
        var rng = new SplitMix64(unchecked((ulong)seed ^ MoistureSeedSalt));
        return unchecked((long)rng.Next());
    }
}
