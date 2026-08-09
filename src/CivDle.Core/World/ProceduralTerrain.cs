using CivDle.Core.Content;
using CivDle.Core.WorldGen;

namespace CivDle.Core.World;

/// <summary>
/// Nekonečný procedurální terén: tři vrstvy (výška, vlhkost, teplota) a výběr biomu
/// podle rozsahů z JSON definic. Terén je čistá funkce (seed + preset) — pro libovolnou
/// dlaždici (i zápornou) vrací stejný biom, nikdy se neukládá.
///
/// Výběr biomu: pod hladinou moře rozhoduje hloubka × teplota (vodní biomy), nad
/// hladinou přeškálovaná výška × vlhkost × teplota (pevninské biomy, první shoda
/// v pořadí souboru). Algoritmus v kódu, čísla v datech (data-driven-content.md).
///
/// <para>Teplota není jen další šum: základ dává zeměpisná šířka (pásma o zadané
/// délce), takže mapa má klimatické pruhy — na sever tundra a tajga, na rovníku
/// džungle a poušť. Výška navíc ochlazuje, takže hory jsou studené i v tropech.
/// Bez téhle vrstvy vycházela krajina jednotvárná (pořád tráva a les).</para>
/// </summary>
public sealed class ProceduralTerrain : ITerrain
{
    // Frekvence v presetech je „počet vln na 100 dlaždic" → dlaždicové souřadnice dělíme 100.
    private const float FrequencyScale = 1f / 100f;

    // Sůl pro odvození seedu vlhkosti, aby výška a vlhkost nebyly korelované.
    private const ulong MoistureSeedSalt = 0x9E3779B97F4A7C15UL;

    // Sůl pro seed řek — nezávislý na výšce i vlhkosti.
    private const ulong RiverSeedSalt = 0xD1B54A32D192ED03UL;

    // Sůl pro seed teploty — nezávislá na ostatních vrstvách.
    private const ulong TemperatureSeedSalt = 0xA24BAED4963EE407UL;

    // Jak moc kolísá teplota od šumu; zbytek nese zeměpisná šířka. Málo šumu =
    // rozmazaná pásma, moc šumu = klima bez řádu (poušť vedle ledovce).
    private const float TemperatureNoiseWeight = 0.3f;

    private readonly BiomeRegistry _biomes;
    private readonly TerrainPreset _preset;
    private readonly FractalNoise _elevationNoise;
    private readonly FractalNoise _moistureNoise;
    private readonly FractalNoise? _riverNoise;
    private readonly FractalNoise? _temperatureNoise;
    private readonly int _riverBiome;
    private readonly int[] _waterBiomes;
    private readonly int[] _landBiomes;

    public ProceduralTerrain(BiomeRegistry biomes, TerrainPreset preset, long seed)
    {
        _biomes = biomes;
        _preset = preset;
        Seed = seed;
        _elevationNoise = new FractalNoise(seed, preset.ElevationNoise);
        _moistureNoise = new FractalNoise(DeriveMoistureSeed(seed), preset.MoistureNoise);
        _riverNoise = preset.RiverNoise is null || preset.RiverWidth <= 0f
            ? null
            : new FractalNoise(DeriveSeed(seed, RiverSeedSalt), preset.RiverNoise);
        _temperatureNoise = preset.TemperatureNoise is null
            ? null
            : new FractalNoise(DeriveSeed(seed, TemperatureSeedSalt), preset.TemperatureNoise);

        var water = new List<int>();
        var land = new List<int>();
        for (int i = 0; i < biomes.Count; i++)
        {
            if (!biomes[i].IsNaturallyGenerated)
            {
                continue; // biom, který vzniká jen přepisem (kráter, zátopa)
            }

            (biomes[i].IsWater ? water : land).Add(i);
        }

        _waterBiomes = water.ToArray();
        _landBiomes = land.ToArray();

        _riverBiome = ResolveRiverBiome(biomes, preset, _waterBiomes);
    }

    /// <summary>
    /// Biom v řečišti. Preset ho smí určit napřímo (<c>riverBiome</c>); jinak se vezme
    /// první KLIMATICKY UNIVERZÁLNÍ mělká voda — jinak by řeky v datech s teplotními
    /// pásmy braly třeba korálový útes, který patří do teplého moře, ne do potoka.
    /// </summary>
    private static int ResolveRiverBiome(BiomeRegistry biomes, TerrainPreset preset, int[] waterBiomes)
    {
        if (preset.RiverBiomeIndex >= 0)
        {
            return preset.RiverBiomeIndex;
        }

        foreach (int index in waterBiomes)
        {
            if (biomes[index].DepthRange.Contains(0f) && biomes[index].TemperatureRange.IsFull)
            {
                return index;
            }
        }

        return waterBiomes.Length > 0 ? waterBiomes[^1] : 0;
    }

    /// <summary>Seed, ze kterého terén vznikl (pro serializaci savu).</summary>
    public long Seed { get; }

    /// <summary>Preset, ze kterého terén vznikl.</summary>
    public TerrainPreset Preset => _preset;

    /// <summary>Normalizovaná výška 0–1 (pod hladinou moře = voda).</summary>
    public float ElevationAt(int x, int y) => _elevationNoise.Sample01(x * FrequencyScale, y * FrequencyScale);

    /// <summary>Vlhkost 0–1 (řídí vegetaci, suroviny a dekorace).</summary>
    public float MoistureAt(int x, int y) => _moistureNoise.Sample01(x * FrequencyScale, y * FrequencyScale);

    /// <summary>
    /// Teplota 0–1: 0 = polární, 1 = rovníková. Skládá se ze zeměpisné šířky
    /// (kosinová pásma podle <c>y</c>), ze šumu (aby hranice pásem nebyly pravítkem)
    /// a z ochlazení s výškou — vrcholky hor jsou studené i v tropech.
    ///
    /// <para>Bez presetu s teplotou vrací 0.5, takže starý obsah bez klimatické
    /// vrstvy se chová jako dřív (data smí předběhnout kód i naopak).</para>
    /// </summary>
    public float TemperatureAt(int x, int y)
    {
        if (_temperatureNoise is null)
        {
            return 0.5f;
        }

        float latitude = _preset.TemperatureBandTiles > 0f
            ? 0.5f + 0.5f * MathF.Cos(y / _preset.TemperatureBandTiles * MathF.PI * 2f)
            : 0.5f;

        float noise = _temperatureNoise.Sample01(x * FrequencyScale, y * FrequencyScale);
        float temperature = latitude * (1f - TemperatureNoiseWeight) + noise * TemperatureNoiseWeight;

        float elevation = ElevationAt(x, y);
        if (elevation > _preset.SeaLevel && _preset.TemperatureLapse > 0f)
        {
            float landElevation = (elevation - _preset.SeaLevel) / (1f - _preset.SeaLevel);
            temperature -= landElevation * _preset.TemperatureLapse;
        }

        return Math.Clamp(temperature, 0f, 1f);
    }

    /// <summary>
    /// Je na dlaždici řeka? Řeka vzniká z „hřebene" šumu: hodnoty blízko 0.5 tvoří
    /// souvislé vinoucí se linie (ridged noise), takže řeka je spojitá a přitom
    /// pořád jen čistá funkce souřadnic — nemusí se generovat dopředu ani ukládat.
    /// Nad zadanou výškou (hory) řeky nevedou, ať krajina působí přirozeně.
    /// </summary>
    public bool IsRiver(int x, int y)
    {
        if (_riverNoise is null)
        {
            return false;
        }

        float elevation = ElevationAt(x, y);
        if (elevation < _preset.SeaLevel)
        {
            return false; // v moři nemá řeka smysl
        }

        float landElevation = (elevation - _preset.SeaLevel) / (1f - _preset.SeaLevel);
        if (landElevation > _preset.RiverMaxElevation)
        {
            return false;
        }

        float ridge = Math.Abs(_riverNoise.Sample01(x * FrequencyScale, y * FrequencyScale) - 0.5f);
        return ridge < _preset.RiverWidth;
    }

    public byte BiomeAt(int x, int y)
    {
        float elevation = ElevationAt(x, y);
        float temperature = TemperatureAt(x, y);

        // Řeka přebíjí pevninský biom — je to voda uprostřed souše.
        if (elevation >= _preset.SeaLevel && IsRiver(x, y))
        {
            return (byte)_riverBiome;
        }

        if (elevation < _preset.SeaLevel)
        {
            // Hloubka 0 = u pobřeží, 1 = nejhlubší oceán. I moře má klima: teplá
            // mělčina je útes, studená pobřežní voda zamrzá.
            float depth = (_preset.SeaLevel - elevation) / _preset.SeaLevel;
            foreach (int index in _waterBiomes)
            {
                if (_biomes[index].DepthRange.Contains(depth) && _biomes[index].TemperatureRange.Contains(temperature))
                {
                    return (byte)index;
                }
            }

            return (byte)_waterBiomes[^1];
        }

        // Výška nad mořem přeškálovaná na 0–1, aby rozsahy biomů nezávisely na seaLevel.
        float landElevation = (elevation - _preset.SeaLevel) / (1f - _preset.SeaLevel);
        float moisture = MoistureAt(x, y);
        foreach (int index in _landBiomes)
        {
            var biome = _biomes[index];
            if (biome.ElevationRange.Contains(landElevation)
                && biome.MoistureRange.Contains(moisture)
                && biome.TemperatureRange.Contains(temperature))
            {
                return (byte)index;
            }
        }

        return (byte)_preset.FallbackBiomeIndex;
    }

    private static long DeriveMoistureSeed(long seed) => DeriveSeed(seed, MoistureSeedSalt);

    /// <summary>Odvodí nezávislý seed pro další vrstvu šumu, ať spolu vrstvy nekorelují.</summary>
    private static long DeriveSeed(long seed, ulong salt)
    {
        var rng = new SplitMix64(unchecked((ulong)seed ^ salt));
        return unchecked((long)rng.Next());
    }
}
