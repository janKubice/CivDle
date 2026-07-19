using CivDle.Core.Content;
using Xunit;

namespace CivDle.Core.Tests.Content;

/// <summary>
/// Testy content loaderu: skutečná herní data se musí načíst, rozbitá data musí
/// spadnout hned a se srozumitelnou hláškou (fail-fast dle CLAUDE.md).
/// </summary>
public class ContentLoaderTests : IDisposable
{
    private readonly string _tempDir;

    public ContentLoaderTests()
    {
        _tempDir = Path.Combine(AppContext.BaseDirectory, "tmp-content-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        Directory.Delete(_tempDir, recursive: true);
    }

    // ----- skutečná herní data -----

    [Fact]
    public void LoadFrom_RealGameData_LoadsAndValidates()
    {
        var content = new ContentLoader().LoadFrom(RealDataDirectory);

        Assert.True(content.Biomes.Count >= 5, "Herní data mají obsahovat aspoň 5 biomů.");
        Assert.Contains(content.Biomes.All, b => b.IsWater);
        Assert.Contains(content.Biomes.All, b => !b.IsWater);
        Assert.True(content.WorldGen.Sizes.Count >= 2);
        Assert.True(content.WorldGen.Presets.Count >= 2);

        // Výchozí volby z JSON se musí přeložit na platné indexy.
        Assert.InRange(content.WorldGen.DefaultSizeIndex, 0, content.WorldGen.Sizes.Count - 1);
        Assert.InRange(content.WorldGen.DefaultPresetIndex, 0, content.WorldGen.Presets.Count - 1);
    }

    [Fact]
    public void LoadFrom_RealGameData_BiomeLookupWorks()
    {
        var content = new ContentLoader().LoadFrom(RealDataDirectory);

        int grassland = content.Biomes.IndexOf("grassland");
        Assert.Equal("grassland", content.Biomes[grassland].Id);
        Assert.False(content.Biomes[grassland].IsWater);
    }

    // ----- rozbitá data -----

    [Fact]
    public void LoadFrom_MissingDirectory_Throws()
    {
        var missing = Path.Combine(_tempDir, "neexistuje");

        var ex = Assert.Throws<ContentLoadException>(() => new ContentLoader().LoadFrom(missing));

        Assert.Contains("neexistuje", ex.Message);
    }

    [Fact]
    public void LoadFrom_MissingBiomesFile_Throws()
    {
        var ex = Assert.Throws<ContentLoadException>(() => new ContentLoader().LoadFrom(_tempDir));

        Assert.Contains("biomes.json", ex.Message);
    }

    [Fact]
    public void LoadFrom_MalformedJson_ReportsFile()
    {
        WriteBiomes("{ tohle není json ");

        var ex = Assert.Throws<ContentLoadException>(() => new ContentLoader().LoadFrom(_tempDir));

        Assert.Contains("biomes.json", ex.Message);
        Assert.Contains("JSON", ex.Message);
    }

    [Fact]
    public void LoadFrom_WrongSchemaVersion_Throws()
    {
        WriteBiomes("""{ "schemaVersion": 99, "biomes": [] }""");

        var ex = Assert.Throws<ContentLoadException>(() => new ContentLoader().LoadFrom(_tempDir));

        Assert.Contains("99", ex.Message);
    }

    [Fact]
    public void LoadFrom_DuplicateBiomeId_ReportsId()
    {
        WriteBiomes("""
        {
          "schemaVersion": 1,
          "biomes": [
            { "id": "ocean", "name": "Oceán", "mapColor": "#1C4E7A", "isWater": true, "depthRange": [0, 1] },
            { "id": "ocean", "name": "Oceán 2", "mapColor": "#1C4E7A", "isWater": true, "depthRange": [0, 1] }
          ]
        }
        """);

        var ex = Assert.Throws<ContentLoadException>(() => new ContentLoader().LoadFrom(_tempDir));

        Assert.Contains("ocean", ex.Message);
        Assert.Contains("Duplicitní", ex.Message);
    }

    [Fact]
    public void LoadFrom_InvalidColor_ReportsBiomeAndValue()
    {
        WriteBiomes("""
        {
          "schemaVersion": 1,
          "biomes": [
            { "id": "ocean", "name": "Oceán", "mapColor": "modrá", "isWater": true, "depthRange": [0, 1] }
          ]
        }
        """);

        var ex = Assert.Throws<ContentLoadException>(() => new ContentLoader().LoadFrom(_tempDir));

        Assert.Contains("ocean", ex.Message);
        Assert.Contains("modrá", ex.Message);
    }

    [Fact]
    public void LoadFrom_WaterDepthGap_Throws()
    {
        WriteBiomes("""
        {
          "schemaVersion": 1,
          "biomes": [
            { "id": "shallow", "name": "Mělčina", "mapColor": "#3E85B8", "isWater": true, "depthRange": [0, 0.3] },
            { "id": "deep", "name": "Hlubina", "mapColor": "#1C4E7A", "isWater": true, "depthRange": [0.5, 1] },
            { "id": "grass", "name": "Louka", "mapColor": "#6FA045", "elevationRange": [0, 1] }
          ]
        }
        """);

        var ex = Assert.Throws<ContentLoadException>(() => new ContentLoader().LoadFrom(_tempDir));

        Assert.Contains("hloubek", ex.Message);
    }

    [Fact]
    public void LoadFrom_LandBiomeWithoutElevation_Throws()
    {
        WriteBiomes("""
        {
          "schemaVersion": 1,
          "biomes": [
            { "id": "grass", "name": "Louka", "mapColor": "#6FA045" }
          ]
        }
        """);

        var ex = Assert.Throws<ContentLoadException>(() => new ContentLoader().LoadFrom(_tempDir));

        Assert.Contains("elevationRange", ex.Message);
    }

    [Fact]
    public void LoadFrom_UnknownFallbackBiome_ReportsId()
    {
        WriteValidBiomes();
        WriteWorldGen("""
        {
          "schemaVersion": 1,
          "sizes": [{ "id": "s", "name": "Malý", "width": 64, "height": 64 }],
          "presets": [{
            "id": "p", "name": "Preset", "seaLevel": 0.5, "fallbackBiome": "cooper",
            "elevationNoise": { "frequency": 1, "octaves": 3, "persistence": 0.5, "lacunarity": 2 },
            "moistureNoise": { "frequency": 1, "octaves": 3, "persistence": 0.5, "lacunarity": 2 }
          }]
        }
        """);

        var ex = Assert.Throws<ContentLoadException>(() => new ContentLoader().LoadFrom(_tempDir));

        Assert.Contains("cooper", ex.Message);
    }

    [Fact]
    public void LoadFrom_WaterFallbackBiome_Throws()
    {
        WriteValidBiomes();
        WriteWorldGen("""
        {
          "schemaVersion": 1,
          "sizes": [{ "id": "s", "name": "Malý", "width": 64, "height": 64 }],
          "presets": [{
            "id": "p", "name": "Preset", "seaLevel": 0.5, "fallbackBiome": "water",
            "elevationNoise": { "frequency": 1, "octaves": 3, "persistence": 0.5, "lacunarity": 2 },
            "moistureNoise": { "frequency": 1, "octaves": 3, "persistence": 0.5, "lacunarity": 2 }
          }]
        }
        """);

        var ex = Assert.Throws<ContentLoadException>(() => new ContentLoader().LoadFrom(_tempDir));

        Assert.Contains("pevninský", ex.Message);
    }

    [Fact]
    public void LoadFrom_UnknownDefaultPreset_Throws()
    {
        WriteValidBiomes();
        WriteWorldGen("""
        {
          "schemaVersion": 1,
          "defaultPreset": "neexistuje",
          "sizes": [{ "id": "s", "name": "Malý", "width": 64, "height": 64 }],
          "presets": [{
            "id": "p", "name": "Preset", "seaLevel": 0.5, "fallbackBiome": "grass",
            "elevationNoise": { "frequency": 1, "octaves": 3, "persistence": 0.5, "lacunarity": 2 },
            "moistureNoise": { "frequency": 1, "octaves": 3, "persistence": 0.5, "lacunarity": 2 }
          }]
        }
        """);

        var ex = Assert.Throws<ContentLoadException>(() => new ContentLoader().LoadFrom(_tempDir));

        Assert.Contains("defaultPreset", ex.Message);
    }

    // ----- pomůcky -----

    /// <summary>Herní data zkopírovaná do výstupu testů (viz csproj).</summary>
    internal static string RealDataDirectory => Path.Combine(AppContext.BaseDirectory, "data");

    private void WriteBiomes(string json) => File.WriteAllText(Path.Combine(_tempDir, "biomes.json"), json);

    private void WriteWorldGen(string json) => File.WriteAllText(Path.Combine(_tempDir, "worldgen.json"), json);

    private void WriteValidBiomes() => WriteBiomes("""
        {
          "schemaVersion": 1,
          "biomes": [
            { "id": "water", "name": "Voda", "mapColor": "#1C4E7A", "isWater": true, "depthRange": [0, 1] },
            { "id": "grass", "name": "Louka", "mapColor": "#6FA045", "elevationRange": [0, 1] }
          ]
        }
        """);
}
