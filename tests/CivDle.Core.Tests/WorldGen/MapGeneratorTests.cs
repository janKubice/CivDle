using CivDle.Core.Content;
using CivDle.Core.Tests.Content;
using CivDle.Core.World;
using CivDle.Core.WorldGen;
using Xunit;

namespace CivDle.Core.Tests.WorldGen;

public class MapGeneratorTests
{
    private static GameContent LoadRealContent() =>
        new ContentLoader().LoadFrom(ContentLoaderTests.RealDataDirectory);

    private static TerrainPreset PresetById(GameContent content, string id) =>
        content.WorldGen.Presets.Single(p => p.Id == id);

    [Fact]
    public void Generate_HasRequestedDimensions()
    {
        var content = LoadRealContent();
        var request = new WorldGenRequest(Seed: 42, Width: 64, Height: 48, PresetById(content, "continents"));

        var map = new MapGenerator().Generate(content, request);

        Assert.Equal(64, map.Width);
        Assert.Equal(48, map.Height);
        Assert.Equal(64 * 48, map.BiomeIndices.Length);
    }

    [Fact]
    public void Generate_SameSeed_IdenticalMap()
    {
        var content = LoadRealContent();
        var request = new WorldGenRequest(Seed: 1234, Width: 96, Height: 96, PresetById(content, "continents"));
        var generator = new MapGenerator();

        var first = generator.Generate(content, request);
        var second = generator.Generate(content, request);

        Assert.Equal(first.BiomeIndices, second.BiomeIndices);
        Assert.Equal(first.Elevation, second.Elevation);
        Assert.Equal(first.Moisture, second.Moisture);
    }

    [Fact]
    public void Generate_DifferentSeed_DifferentMap()
    {
        var content = LoadRealContent();
        var generator = new MapGenerator();
        var preset = PresetById(content, "continents");

        var first = generator.Generate(content, new WorldGenRequest(1, 96, 96, preset));
        var second = generator.Generate(content, new WorldGenRequest(2, 96, 96, preset));

        Assert.NotEqual(first.BiomeIndices, second.BiomeIndices);
    }

    [Fact]
    public void Generate_AllBiomeIndicesAreValid()
    {
        var content = LoadRealContent();
        var map = new MapGenerator().Generate(
            content, new WorldGenRequest(7, 128, 128, PresetById(content, "islands")));

        foreach (byte biomeIndex in map.BiomeIndices)
        {
            Assert.InRange(biomeIndex, 0, content.Biomes.Count - 1);
        }
    }

    [Theory]
    [InlineData("continents")]
    [InlineData("islands")]
    [InlineData("pangaea")]
    public void Generate_RealPresets_ProduceBothLandAndWater(string presetId)
    {
        var content = LoadRealContent();
        var generator = new MapGenerator();

        foreach (long seed in new[] { 1L, 42L, 987654321L })
        {
            var map = generator.Generate(
                content, new WorldGenRequest(seed, 128, 128, PresetById(content, presetId)));

            double waterFraction = WaterFraction(content, map);
            Assert.InRange(waterFraction, 0.02, 0.98);
        }
    }

    [Fact]
    public void Generate_HigherSeaLevel_MeansMoreWater()
    {
        var content = LoadRealContent();
        var basePreset = PresetById(content, "continents");
        var lowSea = basePreset with { SeaLevel = 0.3f };
        var highSea = basePreset with { SeaLevel = 0.7f };
        var generator = new MapGenerator();

        var lowMap = generator.Generate(content, new WorldGenRequest(42, 128, 128, lowSea));
        var highMap = generator.Generate(content, new WorldGenRequest(42, 128, 128, highSea));

        Assert.True(
            WaterFraction(content, highMap) > WaterFraction(content, lowMap),
            "Vyšší hladina moře musí dát víc vodních dlaždic.");
    }

    [Fact]
    public void Generate_NoLandBiomeMatches_UsesFallback()
    {
        // Syntetický obsah: jediný pevninský biom s prázdným pokrytím výšek → všechno
        // nad hladinou musí spadnout do fallbacku.
        var water = new Biome(
            "water", "Voda", new RgbColor(0, 0, 128), 0f, IsWater: true,
            DepthRange: ValueRange.Full, ElevationRange: ValueRange.Full, MoistureRange: ValueRange.Full);
        var unreachable = new Biome(
            "nikde", "Nikde", new RgbColor(1, 2, 3), 0f, IsWater: false,
            DepthRange: ValueRange.Full, ElevationRange: new ValueRange(2f, 3f), MoistureRange: ValueRange.Full);
        var fallback = new Biome(
            "fallback", "Záchrana", new RgbColor(0, 128, 0), 0f, IsWater: false,
            DepthRange: ValueRange.Full, ElevationRange: new ValueRange(2f, 3f), MoistureRange: ValueRange.Full);

        var biomes = new BiomeRegistry(new[] { water, unreachable, fallback });
        var noise = new NoiseSpec(1.5f, 4, 0.5f, 2f);
        var preset = new TerrainPreset("test", "Test", SeaLevel: 0.5f, FallbackBiomeIndex: 2, noise, noise);
        var content = new GameContent(biomes, new WorldGenCatalog(
            new[] { new WorldSize("s", "Malý", 64, 64) }, new[] { preset }, 0, 0));

        var map = new MapGenerator().Generate(content, new WorldGenRequest(5, 64, 64, preset));

        int fallbackCount = 0;
        for (int i = 0; i < map.BiomeIndices.Length; i++)
        {
            Assert.NotEqual(1, map.BiomeIndices[i]); // „nikde" nesmí padnout nikdy
            if (map.BiomeIndices[i] == 2)
            {
                fallbackCount++;
            }
        }

        Assert.True(fallbackCount > 0, "Na mapě má být aspoň kus pevniny s fallback biomem.");
    }

    private static double WaterFraction(GameContent content, WorldMap map)
    {
        int water = 0;
        foreach (byte biomeIndex in map.BiomeIndices)
        {
            if (content.Biomes[biomeIndex].IsWater)
            {
                water++;
            }
        }

        return (double)water / map.BiomeIndices.Length;
    }
}
