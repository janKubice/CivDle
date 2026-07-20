using CivDle.Core.Content;
using CivDle.Core.Tests.Support;
using Xunit;

namespace CivDle.Core.Tests.Content;

/// <summary>
/// Testy content loaderu: skutečná herní data se musí načíst, rozbitá data musí
/// spadnout hned a se srozumitelnou hláškou (fail-fast dle CLAUDE.md).
/// Pomocné metody staví minimální kompletní sadu dat; každý negativní test
/// pak rozbije právě jeden soubor.
/// </summary>
public class ContentLoaderTests : IDisposable
{
    private readonly string _tempDir;

    public ContentLoaderTests()
    {
        _tempDir = Path.Combine(AppContext.BaseDirectory, "tmp-content-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        Directory.CreateDirectory(Path.Combine(_tempDir, "lang"));
    }

    public void Dispose()
    {
        Directory.Delete(_tempDir, recursive: true);
    }

    // ----- skutečná herní data -----

    [Fact]
    public void LoadFrom_RealGameData_LoadsAndValidates()
    {
        var content = TestData.LoadRealContent();

        Assert.True(content.Biomes.Count >= 5, "Herní data mají obsahovat aspoň 5 biomů.");
        Assert.Contains(content.Biomes.All, b => b.IsWater);
        Assert.True(content.Resources.Count >= 3);
        Assert.True(content.Buildings.Count >= 3);
        Assert.True(content.WorldGen.Sizes.Count >= 2);
        Assert.True(content.WorldGen.Presets.Count >= 2);
        Assert.True(content.Languages.Count >= 2, "Hra má mít aspoň češtinu a angličtinu.");

        Assert.InRange(content.WorldGen.DefaultSizeIndex, 0, content.WorldGen.Sizes.Count - 1);
        Assert.InRange(content.WorldGen.DefaultPresetIndex, 0, content.WorldGen.Presets.Count - 1);
        Assert.InRange(content.Gameplay.FoodResourceIndex, 0, content.Resources.Count - 1);
    }

    [Fact]
    public void LoadFrom_RealGameData_LookupsWork()
    {
        var content = TestData.LoadRealContent();

        Assert.False(content.Biomes[content.Biomes.IndexOf("grassland")].IsWater);
        Assert.Equal("wood", content.Resources[content.Resources.IndexOf("wood")].Id);
        Assert.NotNull(content.Buildings[content.Buildings.IndexOf("farm")].Recipe);
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
    public void LoadFrom_EmptyDirectory_ReportsFirstMissingFile()
    {
        // Suroviny se načítají první (odkazují na ně biomy i budovy).
        var ex = Assert.Throws<ContentLoadException>(() => new ContentLoader().LoadFrom(_tempDir));

        Assert.Contains("resources.json", ex.Message);
    }

    [Fact]
    public void LoadFrom_MissingBiomesFile_Throws()
    {
        WriteAllValid();
        File.Delete(Path.Combine(_tempDir, "biomes.json"));

        var ex = Assert.Throws<ContentLoadException>(() => new ContentLoader().LoadFrom(_tempDir));

        Assert.Contains("biomes.json", ex.Message);
    }

    [Fact]
    public void LoadFrom_MalformedJson_ReportsFile()
    {
        WriteAllValid();
        Write("biomes.json", "{ tohle není json ");

        var ex = Assert.Throws<ContentLoadException>(Load);

        Assert.Contains("biomes.json", ex.Message);
        Assert.Contains("JSON", ex.Message);
    }

    [Fact]
    public void LoadFrom_WrongSchemaVersion_Throws()
    {
        WriteAllValid();
        Write("biomes.json", """{ "schemaVersion": 99, "biomes": [] }""");

        var ex = Assert.Throws<ContentLoadException>(Load);

        Assert.Contains("99", ex.Message);
    }

    [Fact]
    public void LoadFrom_DuplicateBiomeId_ReportsId()
    {
        WriteAllValid();
        Write("biomes.json", """
        {
          "schemaVersion": 1,
          "biomes": [
            { "id": "water", "mapColor": "#1C4E7A", "isWater": true, "depthRange": [0, 1] },
            { "id": "water", "mapColor": "#1C4E7A", "isWater": true, "depthRange": [0, 1] }
          ]
        }
        """);

        var ex = Assert.Throws<ContentLoadException>(Load);

        Assert.Contains("water", ex.Message);
        Assert.Contains("Duplicitní", ex.Message);
    }

    [Fact]
    public void LoadFrom_InvalidColor_ReportsBiomeAndValue()
    {
        WriteAllValid();
        Write("biomes.json", """
        {
          "schemaVersion": 1,
          "biomes": [
            { "id": "water", "mapColor": "modrá", "isWater": true, "depthRange": [0, 1] }
          ]
        }
        """);

        var ex = Assert.Throws<ContentLoadException>(Load);

        Assert.Contains("water", ex.Message);
        Assert.Contains("modrá", ex.Message);
    }

    [Fact]
    public void LoadFrom_WaterDepthGap_Throws()
    {
        WriteAllValid();
        Write("biomes.json", """
        {
          "schemaVersion": 1,
          "biomes": [
            { "id": "shallow", "mapColor": "#3E85B8", "isWater": true, "depthRange": [0, 0.3] },
            { "id": "deep", "mapColor": "#1C4E7A", "isWater": true, "depthRange": [0.5, 1] },
            { "id": "grass", "mapColor": "#6FA045", "elevationRange": [0, 1] }
          ]
        }
        """);

        var ex = Assert.Throws<ContentLoadException>(Load);

        Assert.Contains("hloubek", ex.Message);
    }

    [Fact]
    public void LoadFrom_LandBiomeWithoutElevation_Throws()
    {
        WriteAllValid();
        Write("biomes.json", """
        {
          "schemaVersion": 1,
          "biomes": [
            { "id": "water", "mapColor": "#1C4E7A", "isWater": true, "depthRange": [0, 1] },
            { "id": "grass", "mapColor": "#6FA045" }
          ]
        }
        """);

        var ex = Assert.Throws<ContentLoadException>(Load);

        Assert.Contains("elevationRange", ex.Message);
    }

    [Fact]
    public void LoadFrom_UnknownFallbackBiome_ReportsId()
    {
        WriteAllValid();
        WriteWorldGen(fallbackBiome: "cooper");

        var ex = Assert.Throws<ContentLoadException>(Load);

        Assert.Contains("cooper", ex.Message);
    }

    [Fact]
    public void LoadFrom_WaterFallbackBiome_Throws()
    {
        WriteAllValid();
        WriteWorldGen(fallbackBiome: "water");

        var ex = Assert.Throws<ContentLoadException>(Load);

        Assert.Contains("pevninský", ex.Message);
    }

    [Fact]
    public void LoadFrom_UnknownDefaultPreset_Throws()
    {
        WriteAllValid();
        WriteWorldGen(defaultPreset: "neexistuje");

        var ex = Assert.Throws<ContentLoadException>(Load);

        Assert.Contains("defaultPreset", ex.Message);
    }

    [Fact]
    public void LoadFrom_BuildingWithUnknownCostResource_ReportsId()
    {
        WriteAllValid();
        Write("buildings.json", """
        {
          "schemaVersion": 1,
          "buildings": [
            { "id": "house", "mapColor": "#B5651D", "footprint": [1, 1],
              "buildCost": { "cooper": 5 }, "allowedBiomes": ["grass"] }
          ]
        }
        """);

        var ex = Assert.Throws<ContentLoadException>(Load);

        Assert.Contains("cooper", ex.Message);
    }

    [Fact]
    public void LoadFrom_BuildingOnWaterBiome_Throws()
    {
        WriteAllValid();
        Write("buildings.json", """
        {
          "schemaVersion": 1,
          "buildings": [
            { "id": "house", "mapColor": "#B5651D", "footprint": [1, 1],
              "buildCost": { "wood": 5 }, "allowedBiomes": ["water"] }
          ]
        }
        """);

        var ex = Assert.Throws<ContentLoadException>(Load);

        Assert.Contains("vodní", ex.Message);
    }

    [Fact]
    public void LoadFrom_GameplayWithUnknownFoodResource_Throws()
    {
        WriteAllValid();
        Write("gameplay.json", """
        {
          "schemaVersion": 1,
          "startingPopulation": 5,
          "baseHousingCapacity": 6,
          "populationGrowthPerSecond": 0.12,
          "foodPerPersonPerSecond": 0.04,
          "foodResource": "maso"
        }
        """);

        var ex = Assert.Throws<ContentLoadException>(Load);

        Assert.Contains("maso", ex.Message);
    }

    [Fact]
    public void LoadFrom_LanguageMissingContentName_Throws()
    {
        WriteAllValid();
        // Čeština bez jména budovy → musí spadnout s výčtem chybějících klíčů.
        Write(Path.Combine("lang", "cs.json"), LangJson("cs", "Čeština", includeBuildingName: false));

        var ex = Assert.Throws<ContentLoadException>(Load);

        Assert.Contains("building.house", ex.Message);
    }

    [Fact]
    public void LoadFrom_LanguageKeySetMismatch_ReportsLanguage()
    {
        WriteAllValid();
        // Angličtině chybí klíč navíc přítomný v češtině.
        Write(Path.Combine("lang", "en.json"), LangJson("en", "English", includeExtraKey: false));

        var ex = Assert.Throws<ContentLoadException>(Load);

        Assert.Contains("'en'", ex.Message);
        Assert.Contains("ui.hello", ex.Message);
    }

    // ----- pomůcky -----

    private GameContent Load() => new ContentLoader().LoadFrom(_tempDir);

    private void Write(string relativePath, string json) =>
        File.WriteAllText(Path.Combine(_tempDir, relativePath), json);

    /// <summary>Minimální kompletní validní sada dat; testy pak přepisují jednotlivé soubory.</summary>
    private void WriteAllValid()
    {
        Write("biomes.json", """
        {
          "schemaVersion": 1,
          "biomes": [
            { "id": "water", "mapColor": "#1C4E7A", "isWater": true, "depthRange": [0, 1] },
            { "id": "grass", "mapColor": "#6FA045", "elevationRange": [0, 1] }
          ]
        }
        """);
        Write("resources.json", """
        {
          "schemaVersion": 1,
          "resources": [
            { "id": "wood", "mapColor": "#8B5A2B", "startAmount": 30, "baseStorage": 200 },
            { "id": "food", "mapColor": "#E0B040", "startAmount": 20, "baseStorage": 150 }
          ]
        }
        """);
        Write("buildings.json", """
        {
          "schemaVersion": 1,
          "buildings": [
            { "id": "house", "mapColor": "#B5651D", "footprint": [1, 1], "housingCapacity": 4,
              "buildCost": { "wood": 10 }, "allowedBiomes": ["grass"] }
          ]
        }
        """);
        Write("gameplay.json", """
        {
          "schemaVersion": 1,
          "startingPopulation": 5,
          "baseHousingCapacity": 6,
          "populationGrowthPerSecond": 0.12,
          "foodPerPersonPerSecond": 0.04,
          "foodResource": "food",
          "autoBuild": { "intervalTicks": 60, "searchRadius": 6, "populationHeadroom": 2 }
        }
        """);
        WriteWorldGen();
        Write(Path.Combine("lang", "cs.json"), LangJson("cs", "Čeština"));
        Write(Path.Combine("lang", "en.json"), LangJson("en", "English"));
    }

    private void WriteWorldGen(string fallbackBiome = "grass", string? defaultPreset = null)
    {
        string defaultPresetLine = defaultPreset is null ? string.Empty : $"\"defaultPreset\": \"{defaultPreset}\",";
        Write("worldgen.json", $$"""
        {
          "schemaVersion": 1,
          {{defaultPresetLine}}
          "sizes": [{ "id": "s", "width": 64, "height": 64 }],
          "presets": [{
            "id": "p", "seaLevel": 0.5, "fallbackBiome": "{{fallbackBiome}}",
            "elevationNoise": { "frequency": 1, "octaves": 3, "persistence": 0.5, "lacunarity": 2 },
            "moistureNoise": { "frequency": 1, "octaves": 3, "persistence": 0.5, "lacunarity": 2 }
          }]
        }
        """);
    }

    private static string LangJson(string id, string nativeName, bool includeBuildingName = true, bool includeExtraKey = true)
    {
        var keys = new List<string>
        {
            "\"biome.water\": \"-\"",
            "\"biome.grass\": \"-\"",
            "\"resource.wood\": \"-\"",
            "\"resource.food\": \"-\"",
            "\"worldsize.s\": \"-\"",
            "\"preset.p\": \"-\"",
        };
        if (includeBuildingName)
        {
            keys.Add("\"building.house\": \"-\"");
        }

        if (includeExtraKey)
        {
            keys.Add("\"ui.hello\": \"-\"");
        }

        return $$"""
        {
          "schemaVersion": 1,
          "id": "{{id}}",
          "nativeName": "{{nativeName}}",
          "strings": { {{string.Join(", ", keys)}} }
        }
        """;
    }
}
