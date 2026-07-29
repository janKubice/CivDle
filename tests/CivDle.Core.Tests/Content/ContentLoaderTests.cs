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
    public void LoadFrom_AdjacencyOnBuildingWithoutRecipe_Throws()
    {
        // Bonus za okolí u budovy, která nic nevyrábí, je tichá chyba obsahu:
        // v JSON to vypadá, že pravidlo platí, ale nikdy se neprojeví.
        WriteAllValid();
        Write("buildings.json", """
        {
          "schemaVersion": 1,
          "buildings": [
            { "id": "house", "mapColor": "#B5651D", "footprint": [1, 1],
              "buildCost": { "wood": 5 }, "allowedBiomes": ["grass"],
              "adjacency": { "biomes": ["grass"], "radius": 2, "perTile": 0.02, "max": 0.3 } }
          ]
        }
        """);

        var ex = Assert.Throws<ContentLoadException>(Load);

        Assert.Contains("adjacency", ex.Message);
    }

    [Fact]
    public void LoadFrom_AdjacencyWithUnknownBiome_ReportsId()
    {
        WriteAllValid();
        Write("buildings.json", """
        {
          "schemaVersion": 1,
          "buildings": [
            { "id": "camp", "mapColor": "#B5651D", "footprint": [1, 1],
              "buildCost": { "wood": 5 }, "allowedBiomes": ["grass"],
              "recipe": { "output": { "wood": 1 }, "timeTicks": 10 },
              "adjacency": { "biomes": ["bazina"], "radius": 2, "perTile": 0.02, "max": 0.3 } }
          ]
        }
        """);

        var ex = Assert.Throws<ContentLoadException>(Load);

        Assert.Contains("bazina", ex.Message);
    }

    [Fact]
    public void LoadFrom_AdjacencyWithZeroRadius_Throws()
    {
        WriteAllValid();
        Write("buildings.json", """
        {
          "schemaVersion": 1,
          "buildings": [
            { "id": "camp", "mapColor": "#B5651D", "footprint": [1, 1],
              "buildCost": { "wood": 5 }, "allowedBiomes": ["grass"],
              "recipe": { "output": { "wood": 1 }, "timeTicks": 10 },
              "adjacency": { "biomes": ["grass"], "radius": 0, "perTile": 0.02, "max": 0.3 } }
          ]
        }
        """);

        var ex = Assert.Throws<ContentLoadException>(Load);

        Assert.Contains("radius", ex.Message);
    }

    [Fact]
    public void LoadFrom_RealGameData_HasAContractBoard()
    {
        // Zakázky jsou krátká smyčka hry — když v datech nejsou, hráč mezi
        // událostmi jen kouká na čísla.
        var content = TestData.LoadRealContent();

        Assert.True(content.Contracts.IsEnabled);
        Assert.True(content.Contracts.Contracts.Count >= 5);
        Assert.True(content.Contracts.Board.Slots >= 1);
    }

    [Fact]
    public void LoadFrom_ContractPayingWithWhatItWants_Throws()
    {
        // „Dej mi 20 dřeva, dostaneš 20 dřeva" je jen složitý způsob, jak nedat nic.
        WriteAllValid();
        Write("contracts.json", """
        {
          "schemaVersion": 1,
          "board": { "slots": 2, "restockSeconds": 30, "scaleGrowth": 1.05, "maxScale": 20 },
          "contracts": [
            { "id": "silly", "resource": "wood", "amount": 20,
              "reward": { "wood": 20 }, "durationSeconds": 120 }
          ]
        }
        """);

        var ex = Assert.Throws<ContentLoadException>(Load);

        Assert.Contains("silly", ex.Message);
    }

    [Fact]
    public void LoadFrom_ContractWithUnknownResource_ReportsIt()
    {
        WriteAllValid();
        Write("contracts.json", """
        {
          "schemaVersion": 1,
          "board": { "slots": 2, "restockSeconds": 30, "scaleGrowth": 1.05, "maxScale": 20 },
          "contracts": [
            { "id": "mystery", "resource": "mithril", "amount": 20,
              "reward": { "food": 10 }, "durationSeconds": 120 }
          ]
        }
        """);

        var ex = Assert.Throws<ContentLoadException>(Load);

        Assert.Contains("mithril", ex.Message);
    }

    [Fact]
    public void LoadFrom_ContractScaleShrinkingOverTime_Throws()
    {
        // Růst pod 1 by nabídky s hraním zmenšoval — to je překlep, ne záměr.
        WriteAllValid();
        Write("contracts.json", """
        {
          "schemaVersion": 1,
          "board": { "slots": 2, "restockSeconds": 30, "scaleGrowth": 0.8, "maxScale": 20 },
          "contracts": [
            { "id": "ok", "resource": "wood", "amount": 20,
              "reward": { "food": 10 }, "durationSeconds": 120 }
          ]
        }
        """);

        var ex = Assert.Throws<ContentLoadException>(Load);

        Assert.Contains("scaleGrowth", ex.Message);
    }

    [Fact]
    public void LoadFrom_WithoutContractsFile_LeavesBoardOff()
    {
        // Soubor je volitelný: starší data se musí načíst a hrát jako dřív.
        WriteAllValid();

        var content = Load();

        Assert.False(content.Contracts.IsEnabled);
    }

    [Fact]
    public void LoadFrom_EmptyPollutionBlock_Throws()
    {
        // Blok samých nul slibuje mechaniku, která se nikdy neprojeví — tichá
        // chyba obsahu je horší než chybějící blok.
        WriteAllValid();
        Write("buildings.json", """
        {
          "schemaVersion": 1,
          "buildings": [
            { "id": "house", "mapColor": "#B5651D", "footprint": [1, 1],
              "buildCost": { "wood": 5 }, "allowedBiomes": ["grass"],
              "pollution": { "air": 0, "water": 0, "soil": 0 } }
          ]
        }
        """);

        var ex = Assert.Throws<ContentLoadException>(Load);

        Assert.Contains("pollution", ex.Message);
    }

    [Fact]
    public void LoadFrom_AbsurdPollutionValue_ReportsBuilding()
    {
        WriteAllValid();
        Write("buildings.json", """
        {
          "schemaVersion": 1,
          "buildings": [
            { "id": "smog_tower", "mapColor": "#B5651D", "footprint": [1, 1],
              "buildCost": { "wood": 5 }, "allowedBiomes": ["grass"],
              "pollution": { "air": 5000 } }
          ]
        }
        """);

        var ex = Assert.Throws<ContentLoadException>(Load);

        Assert.Contains("smog_tower", ex.Message);
    }

    [Fact]
    public void LoadFrom_PollutionPenaltyOfOne_Throws()
    {
        // Plný trest by zamořenou budovu úplně zastavil. Znečištění má brzdit,
        // ne zabíjet — jinak hráč přijde o výrobu dřív, než postaví čističku.
        WriteAllValid();
        WriteGameplayWithPollution("""
          "pollution": { "intervalTicks": 50, "spreadRate": 0.08, "decayRate": 0.02,
                         "fullEffectAt": 60, "happinessPenalty": 0.25, "productionPenalty": 1.0 }
        """);

        var ex = Assert.Throws<ContentLoadException>(Load);

        Assert.Contains("productionPenalty", ex.Message);
    }

    [Fact]
    public void LoadFrom_PollutionSpreadAboveOne_Throws()
    {
        WriteAllValid();
        WriteGameplayWithPollution("""
          "pollution": { "intervalTicks": 50, "spreadRate": 1.5, "decayRate": 0.02,
                         "fullEffectAt": 60, "happinessPenalty": 0.25, "productionPenalty": 0.35 }
        """);

        var ex = Assert.Throws<ContentLoadException>(Load);

        Assert.Contains("spreadRate", ex.Message);
    }

    [Fact]
    public void LoadFrom_GameplayWithoutPollution_LeavesLayerOff()
    {
        // Starší data znečištění neznají a musí se načíst beze změny chování.
        WriteAllValid();

        var content = Load();

        Assert.False(content.Gameplay.Pollution.IsEnabled);
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

    // ----- průvodce prvními kroky -----

    [Fact]
    public void LoadFrom_TutorialWithUnknownFocusKind_Throws()
    {
        WriteAllValid();
        Write("tutorial.json", """
        {
          "schemaVersion": 1,
          "steps": [
            { "id": "a", "condition": { "metric": "population", "target": 5 }, "focus": { "kind": "teleport" } }
          ]
        }
        """);

        var ex = Assert.Throws<ContentLoadException>(Load);

        Assert.Contains("teleport", ex.Message);
    }

    [Fact]
    public void LoadFrom_TutorialFocusOnUnknownBuilding_Throws()
    {
        WriteAllValid();
        Write("tutorial.json", """
        {
          "schemaVersion": 1,
          "steps": [
            { "id": "a", "condition": { "metric": "population", "target": 5 }, "focus": { "kind": "build", "building": "palace" } }
          ]
        }
        """);

        var ex = Assert.Throws<ContentLoadException>(Load);

        Assert.Contains("palace", ex.Message);
    }

    [Fact]
    public void LoadFrom_TutorialStepWithoutCondition_Throws()
    {
        WriteAllValid();
        Write("tutorial.json", """
        { "schemaVersion": 1, "steps": [ { "id": "a" } ] }
        """);

        var ex = Assert.Throws<ContentLoadException>(Load);

        Assert.Contains("condition", ex.Message);
    }

    [Fact]
    public void LoadFrom_DuplicateTutorialStepId_Throws()
    {
        WriteAllValid();
        Write("tutorial.json", """
        {
          "schemaVersion": 1,
          "steps": [
            { "id": "a", "condition": { "metric": "population", "target": 5 } },
            { "id": "a", "condition": { "metric": "population", "target": 9 } }
          ]
        }
        """);

        var ex = Assert.Throws<ContentLoadException>(Load);

        Assert.Contains("Duplicitní", ex.Message);
    }

    [Fact]
    public void LoadFrom_RealGameData_HasAGuidedOpening()
    {
        var content = TestData.LoadRealContent();

        // Průvodce je odpověď na „nevím, co po mně hra chce" — pár kroků nestačí,
        // a aspoň jeden musí umět hráče někam poslat.
        Assert.True(content.Tutorial.Count >= 5,
            $"Průvodce má mít aspoň 5 kroků, má {content.Tutorial.Count}.");
        Assert.Contains(content.Tutorial, step => step.Focus.Kind != FocusKind.None);
    }

    [Fact]
    public void LoadFrom_MissingSettlementNames_Throws()
    {
        WriteAllValid();
        File.Delete(Path.Combine(_tempDir, "settlement-names.json"));

        var ex = Assert.Throws<ContentLoadException>(Load);

        Assert.Contains("settlement-names.json", ex.Message);
    }

    [Fact]
    public void LoadFrom_EmptySettlementNames_Throws()
    {
        WriteAllValid();
        Write("settlement-names.json", """{ "schemaVersion": 1, "names": [] }""");

        var ex = Assert.Throws<ContentLoadException>(Load);

        Assert.Contains("names", ex.Message);
    }

    [Fact]
    public void LoadFrom_GameplayWithoutRoads_Throws()
    {
        WriteAllValid();
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

        var ex = Assert.Throws<ContentLoadException>(Load);

        Assert.Contains("roads", ex.Message);
    }

    [Fact]
    public void LoadFrom_DecorationWithUnknownBiome_ReportsId()
    {
        WriteAllValid();
        Write("decorations.json", """
        {
          "schemaVersion": 1,
          "decorations": [
            { "id": "flowers", "biomes": ["jungle"], "colors": ["#E7E26B"], "density": 0.05, "minSize": 1, "maxSize": 2 }
          ]
        }
        """);

        var ex = Assert.Throws<ContentLoadException>(Load);

        Assert.Contains("jungle", ex.Message);
    }

    [Fact]
    public void LoadFrom_FaunaWithInvalidTimeOfDay_Throws()
    {
        WriteAllValid();
        Write("fauna.json", """
        {
          "schemaVersion": 1,
          "fauna": [
            { "id": "deer", "biomes": ["grass"], "color": "#8A5A33", "size": 3, "speed": 10, "timeOfDay": "vecer" }
          ]
        }
        """);

        var ex = Assert.Throws<ContentLoadException>(Load);

        Assert.Contains("vecer", ex.Message);
    }

    [Fact]
    public void LoadFrom_GameplayWithoutDayNight_Throws()
    {
        WriteAllValid();
        Write("gameplay.json", """
        {
          "schemaVersion": 1,
          "startingPopulation": 5,
          "baseHousingCapacity": 6,
          "populationGrowthPerSecond": 0.12,
          "foodPerPersonPerSecond": 0.04,
          "foodResource": "food",
          "autoBuild": { "intervalTicks": 60, "searchRadius": 6, "populationHeadroom": 2 },
          "roads": { "mapColor": "#9A9284", "maxSearchDistance": 60 },
          "settlements": { "minBuildings": 3, "clusterDistance": 3, "updateIntervalTicks": 50 }
        }
        """);

        var ex = Assert.Throws<ContentLoadException>(Load);

        Assert.Contains("dayNight", ex.Message);
    }

    [Fact]
    public void LoadFrom_RealGameData_HasLivingMapContent()
    {
        var content = TestData.LoadRealContent();

        Assert.True(content.Decorations.Count >= 5, "Herní data mají mít dekorace pro většinu biomů.");
        Assert.True(content.Fauna.Count >= 3, "Herní data mají mít aspoň pár druhů fauny.");
        Assert.Contains(content.Fauna, f => f.Time == FaunaTime.Night && f.Glow);
    }

    [Fact]
    public void LoadFrom_RealGameData_HasDevlog()
    {
        var content = TestData.LoadRealContent();

        Assert.NotEmpty(content.Devlog);
        Assert.All(content.Devlog, e => Assert.False(string.IsNullOrWhiteSpace(e.Id)));
        Assert.All(content.Devlog, e => Assert.True(e.LineCount > 0));
    }

    [Fact]
    public void LoadFrom_MissingDevlog_IsOptional()
    {
        WriteAllValid();
        // Devlog je volitelný — bez souboru se hra načte, jen bez záznamů.
        var content = new ContentLoader().LoadFrom(_tempDir);

        Assert.Empty(content.Devlog);
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
    /// <summary>Platný gameplay.json plus dodaný blok navíc — pro testy volitelných vrstev.</summary>
    private void WriteGameplayWithPollution(string extraBlock)
    {
        Write("gameplay.json", $$"""
        {
          "schemaVersion": 1,
          "startingPopulation": 5,
          "baseHousingCapacity": 6,
          "populationGrowthPerSecond": 0.12,
          "foodPerPersonPerSecond": 0.04,
          "foodResource": "food",
          "autoBuild": { "intervalTicks": 60, "searchRadius": 6, "populationHeadroom": 2 },
          "roads": { "mapColor": "#9A9284", "maxSearchDistance": 60 },
          "settlements": { "minBuildings": 3, "clusterDistance": 3, "updateIntervalTicks": 50 },
          "dayNight": { "dayLengthSeconds": 240, "startTimeOfDay": 0.32, "nightColor": "#0A1430",
                        "duskColor": "#E8862F", "nightAlpha": 0.45, "duskAlpha": 0.18 },
        {{extraBlock}}
        }
        """);
    }

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
          "autoBuild": { "intervalTicks": 60, "searchRadius": 6, "populationHeadroom": 2 },
          "roads": { "mapColor": "#9A9284", "maxSearchDistance": 60 },
          "settlements": { "minBuildings": 3, "clusterDistance": 3, "updateIntervalTicks": 50 },
          "dayNight": { "dayLengthSeconds": 240, "startTimeOfDay": 0.32, "nightColor": "#0A1430",
                        "duskColor": "#E8862F", "nightAlpha": 0.45, "duskAlpha": 0.18 }
        }
        """);
        WriteWorldGen();
        Write(Path.Combine("lang", "cs.json"), LangJson("cs", "Čeština"));
        Write(Path.Combine("lang", "en.json"), LangJson("en", "English"));
        Write("settlement-names.json", """{ "schemaVersion": 1, "names": ["Testov", "Zkouškovice"] }""");
        Write("decorations.json", """
        {
          "schemaVersion": 1,
          "decorations": [
            { "id": "flowers", "biomes": ["grass"], "colors": ["#E7E26B"], "density": 0.05, "minSize": 1, "maxSize": 2 }
          ]
        }
        """);
        Write("fauna.json", """
        {
          "schemaVersion": 1,
          "fauna": [
            { "id": "deer", "biomes": ["grass"], "color": "#8A5A33", "size": 3, "speed": 10, "timeOfDay": "day" }
          ]
        }
        """);
        Write("prestige.json", """
        {
          "schemaVersion": 1,
          "ascension": {
            "requirement": { "metric": "population", "target": 50 },
            "points": { "metric": "population", "divisor": 15 }
          },
          "upgrades": []
        }
        """);
        Write("quests.json", """
        {
          "schemaVersion": 1,
          "quests": [],
          "dynamic": {
            "condition": { "metric": "population", "target": 20 },
            "targetGrowth": 1.5, "rewardGrowth": 1.5, "reward": { "food": 10 }
          }
        }
        """);
        Write("achievements.json", """{ "schemaVersion": 1, "achievements": [] }""");
        Write("events.json", """{ "schemaVersion": 1, "events": [] }""");
        Write("eras.json", """{ "schemaVersion": 1, "eras": [{ "id": "start", "order": 0 }] }""");
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
            "\"era.start\": \"-\"",
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
