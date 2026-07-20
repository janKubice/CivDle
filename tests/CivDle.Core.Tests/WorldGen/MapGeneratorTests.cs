using CivDle.Core.Content;
using CivDle.Core.Tests.Support;
using CivDle.Core.World;
using CivDle.Core.WorldGen;
using Xunit;

namespace CivDle.Core.Tests.WorldGen;

public class MapGeneratorTests
{
    private static TerrainPreset PresetById(GameContent content, string id) =>
        content.WorldGen.Presets.Single(p => p.Id == id);

    [Fact]
    public void Generate_HasRequestedDimensions()
    {
        var content = TestData.LoadRealContent();
        var request = new WorldGenRequest(Seed: 42, Width: 64, Height: 48, PresetById(content, "continents"));

        var map = new MapGenerator().Generate(content, request);

        Assert.Equal(64, map.Width);
        Assert.Equal(48, map.Height);
        Assert.Equal(64 * 48, map.BiomeIndices.Length);
    }

    [Fact]
    public void Generate_SameSeed_IdenticalMap()
    {
        var content = TestData.LoadRealContent();
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
        var content = TestData.LoadRealContent();
        var generator = new MapGenerator();
        var preset = PresetById(content, "continents");

        var first = generator.Generate(content, new WorldGenRequest(1, 96, 96, preset));
        var second = generator.Generate(content, new WorldGenRequest(2, 96, 96, preset));

        Assert.NotEqual(first.BiomeIndices, second.BiomeIndices);
    }

    [Fact]
    public void Generate_AllBiomeIndicesAreValid()
    {
        var content = TestData.LoadRealContent();
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
        var content = TestData.LoadRealContent();
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
        var content = TestData.LoadRealContent();
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
        // Syntetický obsah: pevninské biomy s prázdným pokrytím výšek → všechno
        // nad hladinou musí spadnout do fallbacku (index 2).
        var biomes = new[]
        {
            TestContent.WaterBiome(),
            TestContent.LandBiome("nikde", new ValueRange(2f, 3f)),
            TestContent.LandBiome("fallback", new ValueRange(2f, 3f)),
        };
        var content = TestContent.Build(biomes, fallbackBiomeIndex: 2);
        var preset = content.WorldGen.Presets[0];

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
