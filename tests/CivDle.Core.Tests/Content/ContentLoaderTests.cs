using CivDle.Core.Content;
using CivDle.Core.Sim;
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
    public void LoadFrom_MalformedJson_ShowsTheOffendingLine()
    {
        // Hláška od .NET („Expected either ',', '}', or ']'") je pravdivá, ale
        // v souboru o půldruhém tisíci řádcích je k ničemu. Hráč — i vývojář —
        // potřebuje vidět ten řádek. A protože chybějící čárka se ohlásí až na
        // NÁSLEDUJÍCÍM řádku, ukazují se oba.
        WriteAllValid();
        Write("biomes.json", string.Join('\n',
            "{",
            "  \"schemaVersion\": 1,",
            "  \"chybiCarka\": 1",
            "  \"biomes\": []",
            "}"));

        var ex = Assert.Throws<ContentLoadException>(Load);

        Assert.Contains("řádek 3", ex.Message);
        Assert.Contains("chybiCarka", ex.Message);
        Assert.Contains("řádek 4", ex.Message);
        Assert.Contains("← tady", ex.Message);
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
    public void LoadFrom_BuildingOnWaterAndLand_Throws()
    {
        // Na vodě se stavět SMÍ — od podmořské vrstvy. Co nesmí, je budova,
        // která by stála na louce i na dně: podmořská se pozná právě tím, že
        // jinam nesmí, takže dvojaká maska by tu vlastnost tiše zrušila.
        WriteAllValid();
        Write("buildings.json", """
        {
          "schemaVersion": 1,
          "buildings": [
            { "id": "house", "mapColor": "#B5651D", "footprint": [1, 1],
              "buildCost": { "wood": 5 }, "allowedBiomes": ["grass", "water"] }
          ]
        }
        """);

        var ex = Assert.Throws<ContentLoadException>(Load);

        Assert.Contains("vodní i pevninské", ex.Message);
    }

    [Fact]
    public void LoadFrom_BuildingOnWaterOnly_IsSubsea()
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

        var content = Load();

        Assert.True(content.Buildings[content.Buildings.IndexOf("house")].IsSubsea);
    }

    [Fact]
    public void LoadFrom_SubseaAnchorThatIsItselfSubsea_Throws()
    {
        WriteAllValid();
        Write("buildings.json", """
        {
          "schemaVersion": 1,
          "buildings": [
            { "id": "house", "mapColor": "#B5651D", "footprint": [1, 1],
              "buildCost": { "wood": 5 }, "allowedBiomes": ["water"], "subseaAnchor": true }
          ]
        }
        """);

        var ex = Assert.Throws<ContentLoadException>(Load);

        Assert.Contains("kotva", ex.Message);
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
    public void LoadFrom_RealGameData_HasDistrictTypes()
    {
        var content = TestData.LoadRealContent();

        Assert.True(content.Districts.IsEnabled);
        Assert.Contains(content.Districts.Types.All, d => d.Id == "industrial");

        // Průmyslová čtvrť musí mít obě strany: bonus i stinnou stránku. Bez toho
        // by shlukování byla jen správná odpověď, ne rozhodnutí.
        var industrial = content.Districts.Types[content.Districts.Types.IndexOf("industrial")];
        Assert.True(industrial.SynergyMax > 0);
        Assert.True(industrial.PollutionMult > 1.0);
    }

    [Fact]
    public void LoadFrom_DistrictWithUnknownCategory_ReportsIt()
    {
        // Překlep v kategorii by jinak jen tiše znamenal, že čtvrť nikdy nevznikne.
        WriteAllValid();
        Write("districts.json", """
        {
          "schemaVersion": 1,
          "districts": [
            { "id": "ghost", "categories": ["neexistuje"], "minBuildings": 3,
              "clusterDistance": 2, "synergyPerBuilding": 0.02, "synergyMax": 0.2,
              "pollutionMult": 1.0, "mapColor": "#808080" }
          ]
        }
        """);

        var ex = Assert.Throws<ContentLoadException>(Load);

        Assert.Contains("neexistuje", ex.Message);
    }

    [Fact]
    public void LoadFrom_DistrictOfOneBuilding_Throws()
    {
        // Čtvrť o jedné budově není čtvrť, je to budova.
        WriteAllValid();
        Write("districts.json", """
        {
          "schemaVersion": 1,
          "districts": [
            { "id": "solo", "categories": ["other"], "minBuildings": 1,
              "clusterDistance": 2, "synergyPerBuilding": 0.02, "synergyMax": 0.2,
              "pollutionMult": 1.0, "mapColor": "#808080" }
          ]
        }
        """);

        var ex = Assert.Throws<ContentLoadException>(Load);

        Assert.Contains("minBuildings", ex.Message);
    }

    [Fact]
    public void LoadFrom_WithoutDistrictsFile_LeavesThemOff()
    {
        WriteAllValid();

        var content = Load();

        Assert.False(content.Districts.IsEnabled);
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

    [Theory]
    [InlineData("\"softGrowth\": 0.9", "", "softGrowth")]
    [InlineData("\"softGrowth\": 1.2", "", "softGrowth")]
    [InlineData("\"softGrowth\": 1.02", ", \"legacyPoints\": 50", "legacyPoints")]
    [InlineData("\"softGrowth\": 1.02", ", \"legacyPoints\": -1", "legacyPoints")]
    public void LoadFrom_ContractSoftGrowthOrLegacyOutOfRange_Throws(string boardField, string contractField, string expected)
    {
        // Měkký strop, který zrychluje, a desítky bodů Odkazu za zakázku jsou
        // překlepy — Odkaz by se tím rozpadl.
        WriteAllValid();
        Write("contracts.json", $$"""
        {
          "schemaVersion": 1,
          "board": { "slots": 2, "restockSeconds": 30, "scaleGrowth": 1.06, "maxScale": 20, {{boardField}} },
          "contracts": [
            { "id": "ok", "resource": "wood", "amount": 20,
              "reward": { "food": 10 }, "durationSeconds": 120 {{contractField}} }
          ]
        }
        """);

        var ex = Assert.Throws<ContentLoadException>(Load);

        Assert.Contains(expected, ex.Message);
    }

    [Theory]
    [InlineData("\"districts\": [\"nowhere\"]", "nowhere")]
    [InlineData("\"unlockedBy\": \"challenge:nope\"", "neexistující")]
    [InlineData("\"unlockedBy\": \"tech:x\"", "unlockedBy")]
    public void LoadFrom_BadDistrictStyle_Throws(string field, string expected)
    {
        WriteAllValid();
        Write("districts.json", $$"""
        {
          "schemaVersion": 1,
          "districts": [
            { "id": "homes", "categories": ["other"], "minBuildings": 3, "clusterDistance": 2,
              "synergyPerBuilding": 0, "synergyMax": 0, "pollutionMult": 1, "mapColor": "#806080" }
          ],
          "styles": [ { "id": "brick", "mapColor": "#A0533E", "tint": "#F2D8CC", {{field}} } ]
        }
        """);

        var ex = Assert.Throws<ContentLoadException>(Load);

        Assert.Contains(expected, ex.Message);
    }

    [Fact]
    public void LoadFrom_WithoutNetworksFile_HasOnlyPower()
    {
        // Domovina a starší mody sítě mimo elektřinu nemají — nesmí to být chyba.
        WriteAllValid();

        var content = Load();

        Assert.Equal(1, content.Networks.Count);
        Assert.Equal(NetworkTypeDef.PowerId, content.Networks[NetworkCatalog.PowerIndex].Id);
    }

    [Fact]
    public void LoadFrom_BuildingUsingANetwork_Resolves()
    {
        WriteAllValid();
        Write("networks.json", """
        { "schemaVersion": 1, "networks": [
          { "id": "water", "range": 5, "shortage": "slowdown", "overlayColor": "#3FA7E0" },
          { "id": "heat", "range": 4, "shortage": "cutoff", "cutoffBelow": 0.5, "overlayColor": "#F08A3C" } ] }
        """);
        Write("buildings.json", """
        {
          "schemaVersion": 1,
          "buildings": [
            { "id": "house", "mapColor": "#B5651D", "footprint": [1, 1], "housingCapacity": 4,
              "buildCost": { "wood": 10 }, "allowedBiomes": ["grass"],
              "networks": { "heat": { "demand": 2 }, "water": { "relay": 3 } } }
          ]
        }
        """);

        var content = Load();
        var house = content.Buildings[0];

        Assert.Equal(3, content.Networks.Count);
        Assert.Equal(2, house.DemandOf(content.Networks.IndexOf("heat")));
        Assert.Equal(3, house.Networks.Single(n => n.NetworkIndex == content.Networks.IndexOf("water")).RelayRange);
        Assert.Equal(NetworkShortage.Cutoff, content.Networks[content.Networks.IndexOf("heat")].Shortage);
    }

    [Theory]
    [InlineData("\"power\": { \"demand\": 2 }", "powerSupply")]
    [InlineData("\"lava\": { \"demand\": 2 }", "lava")]
    [InlineData("\"water\": { }", "překlep")]
    [InlineData("\"water\": { \"demand\": 2, \"cutoffBelow\": 1.5 }", "cutoffBelow")]
    [InlineData("\"water\": { \"supply\": 2, \"cutoffBelow\": 0.5 }", "cutoffBelow")]
    public void LoadFrom_BadBuildingNetwork_Throws(string field, string expected)
    {
        WriteAllValid();
        Write("networks.json", """
        { "schemaVersion": 1, "networks": [ { "id": "water", "range": 5, "overlayColor": "#3FA7E0" } ] }
        """);
        Write("buildings.json", $$"""
        {
          "schemaVersion": 1,
          "buildings": [
            { "id": "house", "mapColor": "#B5651D", "footprint": [1, 1], "housingCapacity": 4,
              "buildCost": { "wood": 10 }, "allowedBiomes": ["grass"], "networks": { {{field}} } }
          ]
        }
        """);

        var ex = Assert.Throws<ContentLoadException>(Load);

        Assert.Contains(expected, ex.Message);
    }

    [Theory]
    [InlineData("{ \"id\": \"power\", \"range\": 5, \"overlayColor\": \"#FFFFFF\" }", "gameplay.json")]
    [InlineData("{ \"id\": \"heat\", \"range\": 0, \"overlayColor\": \"#FFFFFF\" }", "range")]
    [InlineData("{ \"id\": \"heat\", \"range\": 4, \"shortage\": \"cutoff\", \"overlayColor\": \"#FFFFFF\" }", "cutoffBelow")]
    [InlineData("{ \"id\": \"heat\", \"range\": 4, \"shortage\": \"explode\", \"overlayColor\": \"#FFFFFF\" }", "explode")]
    [InlineData("{ \"id\": \"water\", \"range\": 4, \"overlayColor\": \"#FFFFFF\", \"terrainSources\": [ { \"biome\": \"lava\", \"supplyPerTile\": 1 } ] }", "lava")]
    [InlineData("{ \"id\": \"water\", \"range\": 4, \"overlayColor\": \"#FFFFFF\", \"terrainSources\": [ { \"biome\": \"grass\", \"supplyPerTile\": 0 } ] }", "supplyPerTile")]
    [InlineData("{ \"id\": \"water\", \"range\": 4, \"overlayColor\": \"#FFFFFF\", \"housing\": { \"growthPenalty\": 2, \"happinessPenalty\": 0.1 } }", "housing")]
    [InlineData("{ \"id\": \"water\", \"range\": 4, \"overlayColor\": \"#FFFFFF\", \"ground\": { \"color\": \"#70A050\" } }", "'on'")]
    [InlineData("{ \"id\": \"water\", \"range\": 4, \"overlayColor\": \"#FFFFFF\", \"ground\": { \"color\": \"#70A050\", \"on\": [\"lava\"] } }", "lava")]
    [InlineData("{ \"id\": \"water\", \"range\": 4, \"overlayColor\": \"#FFFFFF\", \"ground\": { \"color\": \"#70A050\", \"on\": [\"grass\"], \"density\": 3 } }", "density")]
    [InlineData("{ \"id\": \"heat\", \"range\": 4, \"overlayColor\": \"#FFFFFF\", \"shortageLook\": \"lava\" }", "shortageLook")]
    public void LoadFrom_BadNetworkType_Throws(string network, string expected)
    {
        WriteAllValid();
        Write("networks.json", $$"""{ "schemaVersion": 1, "networks": [ {{network}} ] }""");

        var ex = Assert.Throws<ContentLoadException>(Load);

        Assert.Contains(expected, ex.Message);
    }

    [Theory]
    [InlineData("\"daylight\": 0.01", "daylight")]
    [InlineData("\"daylight\": 1.0", "daylight")]
    [InlineData("\"networkDemand\": { \"steam\": 1.5 }", "steam")]
    [InlineData("\"networkDemand\": { \"heat\": 50 }", "networkDemand.heat")]
    public void LoadFrom_BadPolarSeason_Throws(string field, string expected)
    {
        // Polární noc (Mráz): délka dne a zimní poptávka po síti se ověřují při načtení.
        WriteAllValid();
        Write("networks.json", """{ "schemaVersion": 1, "networks": [ { "id": "heat", "range": 2, "overlayColor": "#F08C3C" } ] }""");
        Write("seasons.json", $$"""
        { "schemaVersion": 1, "daysPerSeason": 2, "seasons": [
          { "id": "winter", "foodProductionMult": 1, "harvestMult": 1, "growthMult": 1, "coldGrowthMult": 1, {{field}} } ] }
        """);

        var ex = Assert.Throws<ContentLoadException>(Load);

        Assert.Contains(expected, ex.Message);
    }

    [Theory]
    [InlineData("", false)]
    [InlineData(", \"networks\": { \"heat\": { \"supply\": 10 } }", true)]
    [InlineData(", \"powerSupply\": 5", true)]
    public void LoadFrom_RecipeWithoutOutput_OnlyForAFuelBurningSource(string source, bool loads)
    {
        // Rašelinová pec: recept jen se vstupem (pálí palivo) smí mít jen zdroj
        // sítě — jinde by budova tiše spotřebovávala.
        WriteAllValid();
        Write("networks.json", """{ "schemaVersion": 1, "networks": [ { "id": "heat", "range": 2, "overlayColor": "#F08C3C" } ] }""");
        Write("buildings.json", $$"""
        {
          "schemaVersion": 1,
          "buildings": [
            { "id": "house", "mapColor": "#B5651D", "footprint": [1, 1],
              "buildCost": { "wood": 10 }, "allowedBiomes": ["grass"],
              "recipe": { "input": { "wood": 1 }, "timeTicks": 60 }{{source}} }
          ]
        }
        """);

        if (loads)
        {
            Assert.Empty(Load().Buildings[0].Recipe!.Outputs);
        }
        else
        {
            var ex = Assert.Throws<ContentLoadException>(Load);
            Assert.Contains("výstup", ex.Message);
        }
    }

    [Fact]
    public void LoadFrom_TimedSourcesThresholdsAndTerrainSources_Resolve()
    {
        WriteAllValid();
        Write("networks.json", """
        { "schemaVersion": 1, "networks": [
          { "id": "water", "range": 3, "overlayColor": "#3FA7E0",
            "terrainSources": [ { "biome": "grass", "supplyPerTile": 0.5 } ],
            "housing": { "growthPenalty": 0.7, "happinessPenalty": 0.15 },
            "ground": { "color": "#70A050", "on": ["grass"], "density": 0.4 } } ] }
        """);
        Write("buildings.json", """
        {
          "schemaVersion": 1,
          "buildings": [
            { "id": "house", "mapColor": "#B5651D", "footprint": [1, 1], "housingCapacity": 4,
              "buildCost": { "wood": 10 }, "allowedBiomes": ["grass"], "supplyTime": "night",
              "networks": { "water": { "supply": 3, "demand": 2, "cutoffBelow": 0.4 } } }
          ]
        }
        """);

        var content = Load();
        var water = content.Networks[content.Networks.IndexOf("water")];
        var house = content.Buildings[0];

        Assert.Equal(SupplyTime.Night, house.SupplyTime);
        Assert.Equal(0.4, house.Networks[0].CutoffBelow);
        Assert.Equal(0.5, Assert.Single(water.TerrainSources).SupplyPerTile);
        Assert.Equal(0.7, water.Housing!.GrowthPenalty);
        Assert.True(water.Ground!.BiomeMask[content.Biomes.IndexOf("grass")]);
        Assert.Equal(0.4, water.Ground.Density);
    }

    [Fact]
    public void LoadFrom_TargetedUpgradeOnlyForProduction_Throws()
    {
        WriteAllValid();
        string prestige = File.ReadAllText(Path.Combine(_tempDir, "prestige.json"));
        Write("prestige.json", prestige.Replace("\"upgrades\": []",
            "\"upgrades\": [ { \"id\": \"x\", \"effect\": \"growth_mult\", \"magnitude\": 0.1, \"cost\": 1, \"targetResource\": \"wood\" } ]"));

        var ex = Assert.Throws<ContentLoadException>(Load);

        Assert.Contains("targetResource", ex.Message);
    }

    [Fact]
    public void LoadFrom_UnknownSupplyTime_Throws()
    {
        WriteAllValid();
        Write("buildings.json", """
        {
          "schemaVersion": 1,
          "buildings": [
            { "id": "house", "mapColor": "#B5651D", "footprint": [1, 1], "housingCapacity": 4,
              "buildCost": { "wood": 10 }, "allowedBiomes": ["grass"], "supplyTime": "dusk" }
          ]
        }
        """);

        var ex = Assert.Throws<ContentLoadException>(Load);

        Assert.Contains("dusk", ex.Message);
    }

    private const string ValidHazard = """
      { "id": "sandstorm", "behavior": "weather_burial", "firstAfterSeconds": 540, "intervalSeconds": 720,
        "intervalJitter": 0.3, "warningSeconds": 60, "sweepSeconds": 45, "bandTiles": 18, "burySeconds": 150 }
    """;

    [Fact]
    public void LoadFrom_HazardAndShelter_Resolve()
    {
        WriteAllValid();
        Write("hazards.json", $$"""{ "schemaVersion": 1, "hazards": [ {{ValidHazard}} ] }""");
        Write("buildings.json", """
        {
          "schemaVersion": 1,
          "buildings": [
            { "id": "house", "mapColor": "#B5651D", "footprint": [1, 1], "housingCapacity": 4,
              "buildCost": { "wood": 10 }, "allowedBiomes": ["grass"], "shelter": { "sandstorm": 6 } }
          ]
        }
        """);
        var keys = new[]
        {
            "hazard.sandstorm", "hazard.sandstorm.warning", "hazard.sandstorm.passed", "hazard.calm", "hazard.buried",
            "hazard.from.0", "hazard.from.1", "hazard.from.2", "hazard.from.3", "hazard.from.4", "hazard.from.5",
            "hazard.from.6", "hazard.from.7",
        };
        Write(Path.Combine("lang", "cs.json"), LangJson("cs", "Čeština", extraKeys: keys));
        Write(Path.Combine("lang", "en.json"), LangJson("en", "English", extraKeys: keys));

        var content = Load();

        var hazard = Assert.Single(content.Hazards.Hazards);
        Assert.Equal(HazardBehavior.WeatherBurial, hazard.Behavior);
        Assert.Equal(720, hazard.Burial!.IntervalSeconds);
        Assert.Equal(6, content.Buildings[0].ShelterRadius(0));
    }

    [Theory]
    [InlineData("\"behavior\": \"meteor\"", "meteor")]
    [InlineData("\"sweepSeconds\": 700", "sweepSeconds")]
    [InlineData("\"intervalSeconds\": 10", "intervalSeconds")]
    [InlineData("\"weather\": \"acid_rain\"", "acid_rain")]
    public void LoadFrom_BadHazard_Throws(string field, string expected)
    {
        WriteAllValid();
        string hazard = ValidHazard.TrimEnd().TrimEnd('}') + ", " + field + " }";
        Write("hazards.json", $$"""{ "schemaVersion": 1, "hazards": [ {{hazard}} ] }""");

        var ex = Assert.Throws<ContentLoadException>(Load);

        Assert.Contains(expected, ex.Message);
    }

    [Fact]
    public void LoadFrom_ShelterFromAnUnknownHazard_Throws()
    {
        WriteAllValid();
        Write("buildings.json", """
        {
          "schemaVersion": 1,
          "buildings": [
            { "id": "house", "mapColor": "#B5651D", "footprint": [1, 1], "housingCapacity": 4,
              "buildCost": { "wood": 10 }, "allowedBiomes": ["grass"], "shelter": { "blizzard": 6 } }
          ]
        }
        """);

        var ex = Assert.Throws<ContentLoadException>(Load);

        Assert.Contains("blizzard", ex.Message);
    }

    [Fact]
    public void LoadFrom_HazardWithoutTexts_Throws()
    {
        WriteAllValid();
        Write("hazards.json", $$"""{ "schemaVersion": 1, "hazards": [ {{ValidHazard}} ] }""");

        var ex = Assert.Throws<ContentLoadException>(Load);

        Assert.Contains("hazard.", ex.Message);
    }

    // ----- světy galaxie (svety-design.md 7.1) -----

    [Fact]
    public void LoadWorld_GetsItsOwnAndSharedContentOnly()
    {
        WriteDuneWorld();

        var dune = LoadWorld("dune");
        var home = Load();

        Assert.Equal(new[] { "food", "adobe" }, dune.Resources.All.Select(r => r.Id));
        Assert.Equal(new[] { "store", "landing", "hut" }, dune.Buildings.All.Select(b => b.Id));
        Assert.Equal(new[] { "wood", "food" }, home.Resources.All.Select(r => r.Id));
        Assert.Equal(new[] { "house", "store" }, home.Buildings.All.Select(b => b.Id));
        Assert.True(home.World.IsHome);
        Assert.Equal("dune", dune.World.Id);
    }

    [Fact]
    public void LoadWorld_SharedBuildingPaysInTheWorldsMaterial()
    {
        WriteDuneWorld();

        var dune = LoadWorld("dune");
        var store = dune.Buildings[dune.Buildings.IndexOf("store")];

        Assert.Equal(new[] { (dune.Resources.IndexOf("adobe"), 10) },
            store.BuildCost.Select(c => (c.ResourceIndex, c.Amount)));
    }

    [Fact]
    public void LoadWorld_ProfileResolvesAgainstTheWorldsContent()
    {
        WriteDuneWorld();

        var dune = LoadWorld("dune");

        Assert.Equal(dune.Buildings.IndexOf("landing"), dune.World.LandingModuleIndex);
        Assert.Equal(new[] { dune.Resources.IndexOf("adobe") }, dune.World.ExportIndices);
        Assert.Equal(50, dune.World.StartingKit.Single().Amount);
        Assert.Equal(0, dune.World.PresetIndex);
    }

    [Fact]
    public void LoadWorld_WithoutWorldFile_Throws()
    {
        WriteDuneWorld();
        File.Delete(Path.Combine(_tempDir, "worlds", "dune", "world.json"));

        var ex = Assert.Throws<ContentLoadException>(() => LoadWorld("dune"));

        Assert.Contains("world.json", ex.Message);
    }

    [Theory]
    [InlineData("\"landingModule\": \"house\"", "přistávací modul")]   // budova Domoviny, ne Duny
    [InlineData("\"startingKit\": { \"wood\": 5 }", "wood")]           // dřevo na Duně není
    [InlineData("\"exports\": [\"wood\"]", "vývoz")]
    [InlineData("\"withoutSystems\": [\"buildings\"]", "withoutSystems")]
    [InlineData("\"preset\": \"mars\"", "mars")]
    public void LoadWorld_BadWorldFile_Throws(string field, string expected)
    {
        WriteDuneWorld(extraWorldField: field);

        var ex = Assert.Throws<ContentLoadException>(() => LoadWorld("dune"));

        Assert.Contains(expected, ex.Message);
    }

    [Fact]
    public void LoadWorld_ThatCannotFeedItself_Throws()
    {
        // Modul nevyrábí jídlo a nic jiného bez výzkumu taky ne: kolonie by
        // po minutě vyhladověla. Musí to spadnout při načtení.
        WriteDuneWorld(landingOutput: "adobe");

        var ex = Assert.Throws<ContentLoadException>(() => LoadWorld("dune"));

        Assert.Contains("rozjet", ex.Message);
    }

    [Fact]
    public void LoadGalaxy_ReadsTheWorldsAndTheirColonyCost()
    {
        WriteDuneWorld();
        WriteWorldsJson("""{ "wood": 500 }""");

        var home = Load();

        Assert.True(home.Galaxy.IsEnabled);
        Assert.Equal(new[] { "home", "dune" }, home.Galaxy.Worlds.Select(w => w.Id));
        var dune = home.Galaxy.Find("dune")!;
        Assert.True(dune.RequiresGate);
        Assert.Equal(500, dune.ColonyCost.Single().Cost.Single().Amount);
    }

    [Fact]
    public void LoadGalaxy_ColonyCostIsPaidInHomeResources()
    {
        WriteDuneWorld();
        WriteWorldsJson("""{ "adobe": 500 }"""); // cihla je surovina Duny, ne Domoviny

        var ex = Assert.Throws<ContentLoadException>(Load);

        Assert.Contains("adobe", ex.Message);
    }

    [Fact]
    public void LoadGalaxy_WorldWithoutNames_Throws()
    {
        WriteDuneWorld(worldNames: false);
        WriteWorldsJson("""{ "wood": 500 }""");

        var ex = Assert.Throws<ContentLoadException>(Load);

        Assert.Contains("'world.", ex.Message); // karta světa by ukázala holý klíč
    }

    [Fact]
    public void LoadGalaxy_ReadsHomeTradeAndTheTradeConfig()
    {
        WriteDuneWorld();
        WriteWorldsJson("""{ "wood": 500 }""", homeExtra: """, "exports": ["wood"], "port": "store" """,
            trade: """ "trade": { "travelSecondsPerStep": 30, "dispatchSeconds": 5 }, """);

        var home = Load();

        Assert.Equal(new[] { home.Resources.IndexOf("wood") }, home.World.ExportIndices);
        Assert.Equal(home.Buildings.IndexOf("store"), home.World.PortIndex);
        Assert.Equal(new TradeConfig(30, 5), home.Galaxy.Trade);
        Assert.Equal(1, home.Galaxy.StepsBetween("home", "dune"));
    }

    [Theory]
    [InlineData(""", "exports": ["adobe"] """, "adobe")]  // cihla je surovina Duny
    [InlineData(""", "port": "landing" """, "landing")]   // modul je budova Duny
    public void LoadGalaxy_HomeTradeMustPointAtHomeContent(string homeExtra, string expected)
    {
        WriteDuneWorld();
        WriteWorldsJson("""{ "wood": 500 }""", homeExtra: homeExtra);

        var ex = Assert.Throws<ContentLoadException>(Load);

        Assert.Contains(expected, ex.Message);
    }

    [Fact]
    public void LoadGalaxy_ColonyTradeBelongsToItsWorldFile()
    {
        WriteDuneWorld();
        WriteWorldsJson("""{ "wood": 500 }""", duneExtra: """, "exports": ["adobe"] """);

        var ex = Assert.Throws<ContentLoadException>(Load);

        Assert.Contains("world.json", ex.Message);
    }

    [Fact]
    public void LoadGalaxy_NonsenseTradeConfig_Throws()
    {
        WriteDuneWorld();
        WriteWorldsJson("""{ "wood": 500 }""", trade: """ "trade": { "travelSecondsPerStep": 0, "dispatchSeconds": 5 }, """);

        var ex = Assert.Throws<ContentLoadException>(Load);

        Assert.Contains("travelSecondsPerStep", ex.Message);
    }

    [Fact]
    public void LoadFrom_NegativeTradeCapacity_Throws()
    {
        WriteAllValid();
        string buildings = File.ReadAllText(Path.Combine(_tempDir, "buildings.json"));
        Write("buildings.json", buildings.Replace("\"id\": \"house\",", "\"id\": \"house\", \"tradeCapacity\": -1,"));

        var ex = Assert.Throws<ContentLoadException>(Load);

        Assert.Contains("tradeCapacity", ex.Message);
    }

    [Fact]
    public void LoadGalaxy_WithoutWorldsFile_HasNoGalaxy()
    {
        WriteAllValid();

        Assert.False(Load().Galaxy.IsEnabled);
    }

    private GameContent LoadWorld(string worldId) =>
        new ContentLoader().LoadFrom(_tempDir, Array.Empty<CivDle.Core.Content.Mods.ModPackage>(), worldId);

    /// <summary>
    /// Malá Duna: jídlo sdílené, dřevo jen doma, cihla jen na Duně; sdílený
    /// sklad stojí dřevo a Duna ho platí cihlou.
    /// </summary>
    private void WriteDuneWorld(
        string? extraWorldField = null, string landingOutput = "food", bool worldNames = true)
    {
        WriteAllValid();
        Write("resources.json", """
        {
          "schemaVersion": 1,
          "resources": [
            { "id": "wood", "mapColor": "#8B5A2B", "startAmount": 30, "baseStorage": 200 },
            { "id": "food", "worlds": ["*"], "mapColor": "#E0B040", "startAmount": 20, "baseStorage": 150 }
          ]
        }
        """);
        Write("buildings.json", """
        {
          "schemaVersion": 1,
          "buildings": [
            { "id": "house", "mapColor": "#B5651D", "footprint": [1, 1], "housingCapacity": 4,
              "buildCost": { "wood": 10 }, "allowedBiomes": ["grass"] },
            { "id": "store", "worlds": ["*"], "mapColor": "#806040", "footprint": [1, 1],
              "buildCost": { "wood": 10 }, "allowedBiomes": ["grass"] }
          ]
        }
        """);

        string dir = Path.Combine(_tempDir, "worlds", "dune");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "resources.json"), """
        { "schemaVersion": 1, "resources": [
          { "id": "adobe", "mapColor": "#C08050", "startAmount": 0, "baseStorage": 300 } ] }
        """);
        File.WriteAllText(Path.Combine(dir, "buildings.json"), $$"""
        { "schemaVersion": 1, "buildings": [
          { "id": "landing", "mapColor": "#C0C0C0", "footprint": [2, 2], "housingCapacity": 30,
            "buildCost": { "adobe": 1 }, "allowedBiomes": ["grass"], "buildable": false,
            "recipe": { "output": { "{{landingOutput}}": 1 }, "timeTicks": 20 } },
          { "id": "hut", "mapColor": "#D09060", "footprint": [1, 1], "housingCapacity": 4,
            "buildCost": { "adobe": 5 }, "allowedBiomes": ["grass"] } ] }
        """);
        string extra = extraWorldField is null ? string.Empty : "," + extraWorldField;
        // Pozdější klíč v JSON přebije dřívější, takže vadné pole z testu vyhraje.
        File.WriteAllText(Path.Combine(dir, "world.json"), $$"""
        { "schemaVersion": 1, "preset": "p", "landingModule": "landing",
          "startingKit": { "adobe": 50 }, "exports": ["adobe"],
          "substitutes": { "wood": "adobe" } {{extra}} }
        """);

        var keys = new List<string>
        {
            "resource.adobe", "building.store", "building.store.desc",
            "building.landing", "building.landing.desc", "building.hut", "building.hut.desc",
        };
        if (worldNames)
        {
            keys.AddRange(new[]
            {
                "world.home", "world.home.desc", "world.home.rule",
                "world.dune", "world.dune.desc", "world.dune.rule",
            });
        }

        Write(Path.Combine("lang", "cs.json"), LangJson("cs", "Čeština", extraKeys: keys));
        Write(Path.Combine("lang", "en.json"), LangJson("en", "English", extraKeys: keys));
    }

    private void WriteWorldsJson(string colonyCost, string homeExtra = "", string duneExtra = "", string trade = "") =>
        Write("worlds.json", $$"""
        { "schemaVersion": 1, {{trade}} "worlds": [
          { "id": "home", "order": 0, "planet": { "surface": "#4A7A3A", "accent": "#2E5D8A", "size": 1 } {{homeExtra}} },
          { "id": "dune", "order": 1, "requiresGate": true, "colonyCost": [ { "cost": {{colonyCost}} } ],
            "colonyCostGrowth": 1.5, "planet": { "surface": "#D8A860", "accent": "#3FA7A0", "size": 0.8 } {{duneExtra}} } ] }
        """);

    [Fact]
    public void LoadFrom_PresetWithClimateShiftAndPatch_Loads()
    {
        WriteAllValid();
        WriteWorldGen(presetExtra: """
            , "temperatureShift": 0.2, "moistureShift": -0.3,
            "patches": [{ "biome": "water", "on": ["grass"], "threshold": 0.7,
              "noise": { "frequency": 2, "octaves": 2, "persistence": 0.5, "lacunarity": 2 } }]
            """);

        var preset = Load().WorldGen.Presets[0];

        Assert.Equal(0.2f, preset.TemperatureShift);
        Assert.Equal(-0.3f, preset.MoistureShift);
        var patch = Assert.Single(preset.Patches);
        Assert.Equal(0.7f, patch.Threshold);
    }

    [Theory]
    [InlineData("\"temperatureShift\": 1.5", "temperatureShift")]
    [InlineData("\"patches\": [{ \"biome\": \"lava\", \"on\": [\"grass\"], \"threshold\": 0.5, \"noise\": { \"frequency\": 1, \"octaves\": 1, \"persistence\": 0.5, \"lacunarity\": 2 } }]", "lava")]
    [InlineData("\"patches\": [{ \"biome\": \"water\", \"on\": [], \"threshold\": 0.5, \"noise\": { \"frequency\": 1, \"octaves\": 1, \"persistence\": 0.5, \"lacunarity\": 2 } }]", "'on'")]
    [InlineData("\"patches\": [{ \"biome\": \"water\", \"on\": [\"grass\"], \"threshold\": 1, \"noise\": { \"frequency\": 1, \"octaves\": 1, \"persistence\": 0.5, \"lacunarity\": 2 } }]", "threshold")]
    public void LoadFrom_BadPresetClimate_Throws(string field, string expected)
    {
        WriteAllValid();
        WriteWorldGen(presetExtra: ", " + field);

        var ex = Assert.Throws<ContentLoadException>(Load);

        Assert.Contains(expected, ex.Message);
    }

    [Fact]
    public void LoadFrom_WithoutAtmospheres_LooksLikeTheHomeworld()
    {
        WriteAllValid();

        Assert.Same(AtmosphereProfile.Home, Load().Atmosphere);
    }

    [Theory]
    [InlineData("\"particles\": \"lasers\"", "lasers")]
    [InlineData("\"morningAlpha\": 0.9", "0–0,5")]
    [InlineData("\"moons\": 9", "moons")]
    public void LoadFrom_BadAtmosphere_Throws(string field, string expected)
    {
        WriteAllValid();
        Write("atmospheres.json", $$"""
        { "schemaVersion": 1, "atmospheres": [
          { "id": "home", "morning": "#FFC480", "noon": "#FFFCF0", "evening": "#7E76D2", {{field}} } ] }
        """);

        var ex = Assert.Throws<ContentLoadException>(Load);

        Assert.Contains(expected, ex.Message);
    }

    [Fact]
    public void LoadWorld_PicksItsAtmosphereOrFailsWhenMissing()
    {
        WriteDuneWorld(extraWorldField: "\"atmosphere\": \"dune\"");
        Write("atmospheres.json", """
        { "schemaVersion": 1, "atmospheres": [
          { "id": "home", "morning": "#FFC480", "noon": "#FFFCF0", "evening": "#7E76D2" },
          { "id": "dune", "morning": "#FFB060", "noon": "#FFFFFF", "evening": "#FF9A50", "particles": "sand", "particleDensity": 0.3 } ] }
        """);

        Assert.Equal("sand", LoadWorld("dune").Atmosphere.Particles);
        Assert.Equal("home", Load().Atmosphere.Id);

        WriteDuneWorld(extraWorldField: "\"atmosphere\": \"mars\"");
        Write("atmospheres.json", """
        { "schemaVersion": 1, "atmospheres": [ { "id": "home", "morning": "#FFC480", "noon": "#FFFCF0", "evening": "#7E76D2" } ] }
        """);
        var ex = Assert.Throws<ContentLoadException>(() => LoadWorld("dune"));
        Assert.Contains("mars", ex.Message);
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
        WriteGameplayWith("""
          "pollution": { "intervalTicks": 50, "spreadRate": 0.08, "decayRate": 0.02,
                         "fullEffectAt": 60, "happinessPenalty": 0.25, "productionPenalty": 1.0 }
        """);

        var ex = Assert.Throws<ContentLoadException>(Load);

        Assert.Contains("productionPenalty", ex.Message);
    }

    [Fact]
    public void LoadFrom_PopulationFillRate_IsParsed_AndMissingMeansOldGrowth()
    {
        WriteAllValid();
        WriteGameplayWith("""
          "populationFillRate": 0.003,
        """.TrimEnd().TrimEnd(','));

        Assert.Equal(0.003, Load().Gameplay.PopulationFillRate, 6);

        WriteGameplayWith(string.Empty);
        Assert.Equal(0.0, Load().Gameplay.PopulationFillRate, 6);
    }

    [Fact]
    public void LoadFrom_Onboarding_IsParsed_AndMissingMeansOff()
    {
        WriteAllValid();
        WriteGameplayWith("""
          "onboarding": {
            "quickStartSeeds": [42, 7],
            "startSite": { "radius": 8, "searchRadius": 60, "nodes": { "wood": 6 }, "buildings": ["house"] },
            "firstDay": { "seconds": 270, "until": 0.72 }
          }
        """);

        var onboarding = Load().Gameplay.Onboarding;

        Assert.Equal(new long[] { 42, 7 }, onboarding.QuickStartSeeds);
        Assert.Equal(8, onboarding.StartRadius);
        Assert.Equal(6, Assert.Single(onboarding.StartNodes).Amount);
        Assert.Single(onboarding.StartBuildings);
        Assert.Equal(270, onboarding.FirstDaySeconds);

        WriteGameplayWith(string.Empty);
        var off = Load().Gameplay.Onboarding;
        Assert.False(off.HasStartSite);
        Assert.False(off.HasSlowFirstDay);
    }

    [Theory]
    [InlineData("""{ "startSite": { "radius": 8, "searchRadius": 60, "nodes": { "gold": 3 } } }""", "gold")]
    [InlineData("""{ "startSite": { "radius": 8, "searchRadius": 60, "buildings": ["castle"] } }""", "castle")]
    [InlineData("""{ "startSite": { "radius": 1, "searchRadius": 60 } }""", "radius")]
    [InlineData("""{ "firstDay": { "seconds": 270, "until": 0.2 } }""", "until")]
    public void LoadFrom_BrokenOnboarding_Throws(string block, string expected)
    {
        // Konec pomalého dne před ranním startem by znamenal čas, který jde pozpátku.
        WriteAllValid();
        WriteGameplayWith($"\"onboarding\": {block}");

        var ex = Assert.Throws<ContentLoadException>(Load);

        Assert.Contains(expected, ex.Message);
    }

    [Fact]
    public void LoadFrom_Governor_IsParsed_AndMissingMeansTheClassicGovernor()
    {
        WriteAllValid();
        WriteGameplayWith("""
          "governor": {
            "buildsByRole": true,
            "storage": { "fullShare": 0.9 },
            "knowledge": { "resource": "wood", "minPopulation": 30, "targetMinutes": 5 },
            "landscape": { "minNodes": 6 },
            "power": { "minCoverage": 0.8 }
          }
        """);

        var governor = Load().Gameplay.Governor;

        Assert.True(governor.BuildsByRole);
        Assert.Equal(0.9, governor.Storage.FullShare, 6);
        Assert.True(governor.Knowledge.IsEnabled);
        Assert.Equal(300, governor.Knowledge.TargetSeconds, 6);
        Assert.False(governor.Faith.IsEnabled); // chybějící cíl je vypnutý, ne výchozí
        Assert.Equal(6, governor.Landscape.MinNodes);
        Assert.Equal(0.8, governor.Power.MinCoverage, 6);

        // Bez bloku guvernér zůstane, jaký byl: jen autoBuild, žádné nové cíle.
        WriteGameplayWith(string.Empty);
        var classic = Load().Gameplay.Governor;
        Assert.False(classic.BuildsByRole);
        Assert.False(classic.Storage.IsEnabled);
        Assert.False(classic.Knowledge.IsEnabled);
    }

    [Theory]
    [InlineData("""{ "knowledge": { "resource": "mana", "minPopulation": 0, "targetMinutes": 5 } }""", "mana")]
    [InlineData("""{ "storage": { "fullShare": 0.2 } }""", "fullShare")]
    [InlineData("""{ "faith": { "resource": "food", "minPopulation": 0, "targetMinutes": 0.1 } }""", "targetMinutes")]
    [InlineData("""{ "landscape": { "minNodes": 0 } }""", "minNodes")]
    [InlineData("""{ "power": { "minCoverage": 1.5 } }""", "minCoverage")]
    public void LoadFrom_BrokenGovernor_Throws(string block, string expected)
    {
        // Překlep v datech guvernéra má spadnout při startu se jménem pole,
        // ne tiše vypnout cíl, o kterém si autor myslí, že běží.
        WriteAllValid();
        WriteGameplayWith($"\"governor\": {block}");

        var ex = Assert.Throws<ContentLoadException>(Load);

        Assert.Contains(expected, ex.Message);
    }

    [Fact]
    public void LoadFrom_PopulationFillRateAboveOne_Throws()
    {
        // Nad jedna by se za sekundu nastěhovalo víc lidí, než je volných míst.
        WriteAllValid();
        WriteGameplayWith("""
          "populationFillRate": 1.5
        """);

        var ex = Assert.Throws<ContentLoadException>(Load);

        Assert.Contains("populationFillRate", ex.Message);
    }

    [Fact]
    public void LoadFrom_HappinessWithReachAndThreshold_ParsesThem()
    {
        WriteAllValid();
        WriteGameplayWith("""
          "happiness": { "intervalTicks": 50, "baseHappiness": 0.55, "serviceWeight": 0.45,
                         "overcrowdingPenalty": 0.25, "peoplePerServicePoint": 12, "growthFloor": 0.15,
                         "freePopulation": 25, "crowdingThreshold": 0.85, "serviceReachTiles": 14 }
        """);

        var happiness = Load().Gameplay.Happiness;

        Assert.Equal(0.85, happiness.CrowdingThreshold, 6);
        Assert.Equal(14, happiness.ServiceReachTiles);
    }

    [Fact]
    public void LoadFrom_HappinessWithoutReach_KeepsTheOldCitywideServices()
    {
        // Starší data i mody bez nových polí se chovají jako dřív.
        WriteAllValid();
        WriteGameplayWith("""
          "happiness": { "intervalTicks": 50, "baseHappiness": 0.55, "serviceWeight": 0.45,
                         "overcrowdingPenalty": 0.25, "peoplePerServicePoint": 12, "growthFloor": 0.15,
                         "freePopulation": 25 }
        """);

        var happiness = Load().Gameplay.Happiness;

        Assert.False(happiness.HasServiceReach);
        Assert.Equal(0.0, happiness.CrowdingThreshold, 6);
    }

    [Fact]
    public void LoadFrom_CrowdingThresholdOfOne_Throws()
    {
        // Práh 1 by dělil nulou — přelidnění by nešlo spočítat.
        WriteAllValid();
        WriteGameplayWith("""
          "happiness": { "intervalTicks": 50, "baseHappiness": 0.55, "serviceWeight": 0.45,
                         "overcrowdingPenalty": 0.25, "peoplePerServicePoint": 12, "growthFloor": 0.15,
                         "freePopulation": 25, "crowdingThreshold": 1.0 }
        """);

        var ex = Assert.Throws<ContentLoadException>(Load);

        Assert.Contains("crowdingThreshold", ex.Message);
    }

    [Fact]
    public void LoadFrom_PollutionSpreadAboveOne_Throws()
    {
        WriteAllValid();
        WriteGameplayWith("""
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

    // ----- podívané megastruktur -----

    [Fact]
    public void LoadFrom_UnknownSpectacleEffect_ReportsBuilding()
    {
        // Budova by se tvářila, že něco umí, a nikdy nic neudělala.
        WriteAllValid();
        Write("buildings.json", """
        {
          "schemaVersion": 1,
          "buildings": [
            { "id": "wonder", "mapColor": "#B5651D", "footprint": [1, 1],
              "buildCost": { "wood": 5 }, "allowedBiomes": ["grass"],
              "spectacle": { "effect": "disco_lights", "intervalSeconds": 10 } }
          ]
        }
        """);

        var ex = Assert.Throws<ContentLoadException>(Load);

        Assert.Contains("wonder", ex.Message);
        Assert.Contains("spectacle.effect", ex.Message);
    }

    [Fact]
    public void LoadFrom_TooFrequentSpectacle_Throws()
    {
        // Pod vteřinu už to není podívaná, ale blikání.
        WriteAllValid();
        Write("buildings.json", """
        {
          "schemaVersion": 1,
          "buildings": [
            { "id": "wonder", "mapColor": "#B5651D", "footprint": [1, 1],
              "buildCost": { "wood": 5 }, "allowedBiomes": ["grass"],
              "spectacle": { "effect": "rocket_launch", "intervalSeconds": 0.2 } }
          ]
        }
        """);

        var ex = Assert.Throws<ContentLoadException>(Load);

        Assert.Contains("intervalSeconds", ex.Message);
    }

    [Fact]
    public void LoadFrom_BuildingWithoutSpectacle_JustStandsThere()
    {
        WriteAllValid();

        Assert.All(Load().Buildings.All, b => Assert.False(b.HasSpectacle));
    }

    // ----- těžební laser -----

    [Fact]
    public void LoadFrom_GameplayWithoutLaser_LeavesItOff()
    {
        // Starší data laser neznají a ruční sběr musí zůstat klikáním.
        WriteAllValid();

        Assert.False(Load().Gameplay.Laser.IsEnabled);
    }

    [Fact]
    public void LoadFrom_AbsurdLaserRate_Throws()
    {
        // Příliš rychlý paprsek by z krajiny udělal jednorázovou zásobárnu.
        WriteAllValid();
        WriteGameplayWith("""
          "laser": { "harvestsPerSecond": 500, "radiusTiles": 1, "feature": "laser" }
        """);

        var ex = Assert.Throws<ContentLoadException>(Load);

        Assert.Contains("harvestsPerSecond", ex.Message);
    }

    [Fact]
    public void LoadFrom_LaserWithoutAGate_Throws()
    {
        // Bez brány by laser platil od první minuty.
        WriteAllValid();
        WriteGameplayWith("""
          "laser": { "harvestsPerSecond": 8, "radiusTiles": 1 }
        """);

        var ex = Assert.Throws<ContentLoadException>(Load);

        Assert.Contains("laser.feature", ex.Message);
    }

    // ----- vozidla -----

    [Fact]
    public void LoadFrom_WithoutVehiclesFile_LoadsAnyway()
    {
        // Kulisa nesmí být podmínkou spuštění — bez souboru se prostě nic nehýbe.
        WriteAllValid();

        Assert.Empty(Load().Vehicles);
    }

    [Fact]
    public void LoadFrom_ValidVehicles_AreParsed()
    {
        WriteAllValid();
        Write("vehicles.json", """
        {
          "schemaVersion": 1,
          "vehicles": [
            { "id": "cart", "color": "#8A6A44", "width": 3, "length": 5, "speed": 26, "minEra": 0, "maxEra": 2 }
          ]
        }
        """);

        var vehicle = Assert.Single(Load().Vehicles);

        Assert.Equal("cart", vehicle.Id);
        Assert.Equal(26f, vehicle.Speed);
        Assert.True(vehicle.FitsEra(1));
        Assert.False(vehicle.FitsEra(3));
    }

    [Fact]
    public void LoadFrom_VehicleWithInvertedEraRange_Throws()
    {
        // Vozidlo, které nikdy nevyjede, je tichá chyba obsahu.
        WriteAllValid();
        Write("vehicles.json", """
        {
          "schemaVersion": 1,
          "vehicles": [
            { "id": "ghost", "color": "#8A6A44", "width": 3, "length": 5, "speed": 26, "minEra": 4, "maxEra": 2 }
          ]
        }
        """);

        var ex = Assert.Throws<ContentLoadException>(Load);

        Assert.Contains("ghost", ex.Message);
        Assert.Contains("maxEra", ex.Message);
    }

    [Fact]
    public void LoadFrom_MotionlessVehicle_Throws()
    {
        WriteAllValid();
        Write("vehicles.json", """
        {
          "schemaVersion": 1,
          "vehicles": [
            { "id": "brick", "color": "#8A6A44", "width": 3, "length": 5, "speed": 0, "minEra": 0 }
          ]
        }
        """);

        var ex = Assert.Throws<ContentLoadException>(Load);

        Assert.Contains("speed", ex.Message);
    }

    [Fact]
    public void LoadFrom_DuplicateVehicleId_ReportsId()
    {
        WriteAllValid();
        Write("vehicles.json", """
        {
          "schemaVersion": 1,
          "vehicles": [
            { "id": "cart", "color": "#8A6A44", "width": 3, "length": 5, "speed": 26, "minEra": 0 },
            { "id": "cart", "color": "#8A6A44", "width": 3, "length": 5, "speed": 26, "minEra": 0 }
          ]
        }
        """);

        var ex = Assert.Throws<ContentLoadException>(Load);

        Assert.Contains("cart", ex.Message);
    }

    // ----- hromadná stavba -----

    [Fact]
    public void LoadFrom_GameplayWithoutBulkBuild_UsesDefaults()
    {
        // Starší data blok neznají — hráč o hromadnou stavbu přijít nesmí.
        WriteAllValid();

        var config = Load().Gameplay.BulkBuild;

        Assert.True(config.HasBatches);
        Assert.True(config.MaxPerAction > 0);
    }

    [Fact]
    public void LoadFrom_UnsortedBatches_Throws()
    {
        // Lišta násobičů se čte zleva doprava; přeházená čísla by z ní udělala hádanku.
        WriteAllValid();
        WriteGameplayWith("""
          "bulkBuild": { "batches": [1, 25, 5], "maxPerAction": 400 }
        """);

        var ex = Assert.Throws<ContentLoadException>(Load);

        Assert.Contains("batches", ex.Message);
    }

    [Fact]
    public void LoadFrom_EmptyBatches_Throws()
    {
        WriteAllValid();
        WriteGameplayWith("""
          "bulkBuild": { "batches": [], "maxPerAction": 400 }
        """);

        var ex = Assert.Throws<ContentLoadException>(Load);

        Assert.Contains("batches", ex.Message);
    }

    [Fact]
    public void LoadFrom_BulkBuildWithoutCap_Throws()
    {
        // Bez stropu by jedno tažení přes mapu položilo tisíce budov naráz.
        WriteAllValid();
        WriteGameplayWith("""
          "bulkBuild": { "batches": [1, 5], "maxPerAction": 0 }
        """);

        var ex = Assert.Throws<ContentLoadException>(Load);

        Assert.Contains("maxPerAction", ex.Message);
    }

    // ----- milníky za počet budov -----

    [Fact]
    public void LoadFrom_BuildingWithoutMilestones_StaysWithoutThem()
    {
        // Blok je volitelný: většina budov milníky mít nemá a starší data je neznají.
        WriteAllValid();

        var content = Load();

        Assert.All(content.Buildings.All, b => Assert.Null(b.Milestones));
    }

    [Fact]
    public void LoadFrom_ValidMilestones_AreParsed()
    {
        WriteAllValid();
        WriteBuildingWithMilestones("""{ "every": 5, "bonusPerStep": 0.1, "maxSteps": 10 }""");

        var milestones = Load().Buildings[0].Milestones;

        Assert.NotNull(milestones);
        Assert.Equal(5, milestones!.Every);
        Assert.Equal(0.1, milestones.BonusPerStep, 6);
        Assert.Equal(10, milestones.MaxSteps);
    }

    [Fact]
    public void LoadFrom_MilestoneEveryZero_Throws()
    {
        // „Každou nultou budovu" je tichá chyba obsahu — milník by se nikdy neprojevil.
        WriteAllValid();
        WriteBuildingWithMilestones("""{ "every": 0, "bonusPerStep": 0.1, "maxSteps": 10 }""");

        var ex = Assert.Throws<ContentLoadException>(Load);

        Assert.Contains("every", ex.Message);
        Assert.Contains("house", ex.Message);
    }

    [Fact]
    public void LoadFrom_MilestoneWithoutBonus_Throws()
    {
        WriteAllValid();
        WriteBuildingWithMilestones("""{ "every": 5, "bonusPerStep": 0, "maxSteps": 10 }""");

        var ex = Assert.Throws<ContentLoadException>(Load);

        Assert.Contains("bonusPerStep", ex.Message);
    }

    [Fact]
    public void LoadFrom_MilestoneWithoutCeiling_Throws()
    {
        // Bez stropu by šla výroba škálovat donekonečna jedním typem budovy.
        WriteAllValid();
        WriteBuildingWithMilestones("""{ "every": 5, "bonusPerStep": 0.1, "maxSteps": 0 }""");

        var ex = Assert.Throws<ContentLoadException>(Load);

        Assert.Contains("maxSteps", ex.Message);
    }

    /// <summary>Přepíše budovy jedinou budovou s dodaným blokem milníků.</summary>
    private void WriteBuildingWithMilestones(string milestones)
    {
        Write("buildings.json", $$"""
        {
          "schemaVersion": 1,
          "buildings": [
            { "id": "house", "mapColor": "#B5651D", "footprint": [1, 1],
              "buildCost": { "wood": 5 }, "allowedBiomes": ["grass"],
              "milestones": {{milestones}} }
          ]
        }
        """);
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
    public void LoadFrom_AbsurdVisualHeight_Fails()
    {
        // Budova vysoká dvacet dlaždic by zakryla půl obrazovky a vypadalo by
        // to jako chyba vykreslování, ne jako mrakodrap.
        WriteAllValid();
        Write("buildings.json", """
        {
          "schemaVersion": 1,
          "buildings": [
            { "id": "house", "mapColor": "#B5651D", "footprint": [1, 1], "housingCapacity": 4,
              "buildCost": { "wood": 10 }, "allowedBiomes": ["grass"], "visualHeight": 40 }
          ]
        }
        """);

        var ex = Assert.Throws<ContentLoadException>(Load);

        Assert.Contains("visualHeight", ex.Message);
    }

    [Fact]
    public void LoadFrom_RockyWaterBiome_Fails()
    {
        // Hladina je vodorovná, ať je pod ní cokoli. 'rocky' u vody znamená,
        // že si to někdo v datech rozmyslel napůl — a tiše by se to nikdy
        // neprojevilo, protože voda se stejně stínovat nesmí.
        WriteAllValid();
        Write("biomes.json", """
        {
          "schemaVersion": 1,
          "biomes": [
            { "id": "water", "mapColor": "#1C4E7A", "isWater": true, "depthRange": [0, 1], "rocky": true },
            { "id": "grass", "mapColor": "#6FA045", "elevationRange": [0, 1] }
          ]
        }
        """);

        var ex = Assert.Throws<ContentLoadException>(Load);

        Assert.Contains("rocky", ex.Message);
    }

    [Fact]
    public void LoadFrom_RoadSurfaceWithUnknownKind_NamesTheKnownOnes()
    {
        // Překlep v druhu povrchu by jinak tiše spadl na hlínu a hráč by se
        // v moderní éře divil, proč má město polní cesty.
        WriteAllValid();
        WriteRoadSurfaces("""[{ "fromEra": 0, "kind": "kocicihlavy", "color": "#9A9284" }]""");

        var ex = Assert.Throws<ContentLoadException>(Load);

        Assert.Contains("kocicihlavy", ex.Message);
        Assert.Contains("dirt", ex.Message);
    }

    [Fact]
    public void LoadFrom_RoadSurfacesOutOfOrder_Fails()
    {
        // Pořadí je to jediné, podle čeho se povrch pro éru vybírá. Na přeskáčku
        // by hra v půlce dějin přeskočila zpátky na polní cestu.
        WriteAllValid();
        WriteRoadSurfaces("""
            [
              { "fromEra": 0, "kind": "dirt", "color": "#96754E" },
              { "fromEra": 4, "kind": "paved", "color": "#7C7E86" },
              { "fromEra": 2, "kind": "cobble", "color": "#9A9284" }
            ]
            """);

        var ex = Assert.Throws<ContentLoadException>(Load);

        Assert.Contains("vzestupně", ex.Message);
    }

    [Fact]
    public void LoadFrom_RoadSurfacesNotStartingAtZero_Fails()
    {
        // Bez pokrytí nulté éry by první věk neměl povrch žádný.
        WriteAllValid();
        WriteRoadSurfaces("""[{ "fromEra": 2, "kind": "cobble", "color": "#9A9284" }]""");

        var ex = Assert.Throws<ContentLoadException>(Load);

        Assert.Contains("éry 0", ex.Message);
    }

    [Fact]
    public void LoadFrom_WithoutRoadSurfaces_LoadsAnyway()
    {
        // Povrchy jsou nepovinné: obsah bez nich je platný obsah a silnice se
        // kreslí jednou barvou jako dřív.
        WriteAllValid();

        var content = Load();

        Assert.NotNull(content.Gameplay.Roads);
    }

    [Fact]
    public void LoadFrom_DecorationWithoutScale_GetsTheDefaultOne()
    {
        // Zvětšení je nepovinné: drtivá většina drobností na zemi ho nepotřebuje
        // a nikdo je nemá psát do každého záznamu. Nula ze schématu ale nesmí
        // projít jako „kresli nic".
        WriteAllValid();

        var content = Load();

        Assert.All(content.Decorations, d => Assert.True(d.Scale >= 1));
    }

    [Fact]
    public void LoadFrom_DecorationWithAbsurdScale_Fails()
    {
        // Strop je pojistka proti překlepu. Dvacetkrát zvětšený sprite přetáhne
        // přes sebe půl obrazovky a vypadá to jako chyba vykreslování, ne jako
        // špatné číslo v datech — takže se to musí ozvat hned při načtení.
        WriteAllValid();
        Write("decorations.json", """
        {
          "schemaVersion": 1,
          "decorations": [
            { "id": "flowers", "biomes": ["grass"], "colors": ["#E7E26B"], "density": 0.05, "minSize": 1, "maxSize": 2, "scale": 40 }
          ]
        }
        """);

        var ex = Assert.Throws<ContentLoadException>(Load);

        Assert.Contains("scale", ex.Message);
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
    public void LoadFrom_PartialLanguage_FallsBackToTheBaseLanguage()
    {
        // Dřív musel nový jazyk přinést všechny klíče, jinak hra vůbec nenaběhla.
        // Rozpracovaný překlad tak buď někdo dotáhl do posledního tooltipu, nebo
        // nevznikl vůbec — a každý nový řetězec ve hře rozbil všechny hotové.
        WriteAllValid();
        Write(Path.Combine("lang", "en.json"), LangJson("en", "English", includeExtraKey: false));

        var content = Load();
        var english = content.Languages[content.Languages.IndexOf("en")];

        Assert.False(english.IsComplete);
        Assert.True(english.Coverage is > 0 and < 1);
        Assert.True(english.Strings.ContainsKey("ui.hello"), "Chybějící klíč se má doplnit ze základního jazyka.");
    }

    [Fact]
    public void LoadFrom_LanguageWithAnUnknownKey_ReportsLanguage()
    {
        // Klíč navíc je skoro vždycky překlep: hra si takový řetězec nikdy
        // nevyžádá, takže by se na něj jinak nepřišlo.
        WriteAllValid();
        Write(Path.Combine("lang", "en.json"), LangJson("en", "English") .Replace("\"ui.hello\"", "\"ui.helo\""));

        var ex = Assert.Throws<ContentLoadException>(Load);

        Assert.Contains("'en'", ex.Message);
        Assert.Contains("ui.helo", ex.Message);
    }

    // ----- významné osobnosti -----

    [Fact]
    public void Figures_LoadWithTheirMilestoneAndStatue()
    {
        WriteWorldWithFigure("""
        { "id": "hero", "effect": "production_mult", "magnitude": 0.5,
          "lifeSeconds": 600, "milestone": "first", "statue": "house" }
        """);

        var content = Load();

        Assert.True(content.Figures.IsEnabled);
        var figure = content.Figures[0];
        Assert.Equal("hero", figure.Id);
        Assert.Equal(0, figure.MilestoneIndex);
        Assert.Equal(content.Buildings.IndexOf("house"), figure.StatueBuildingIndex);

        // Vteřiny z dat se převedly na tiky simulace — tohle je jediné místo,
        // kde se ty dvě jednotky potkávají.
        Assert.Equal(600 * Simulation.TicksPerSecond, figure.LifeTicks);
    }

    [Fact]
    public void Figure_WithAnUnknownEffect_Throws()
    {
        // Překlep v efektu by jinak tiše nedělal vůbec nic: osobnost by se
        // narodila, oslavila a nezvedla by ani jedno číslo.
        WriteWorldWithFigure("""
        { "id": "hero", "effect": "produkce_navic", "magnitude": 0.5,
          "lifeSeconds": 600, "milestone": "first" }
        """);

        var ex = Assert.Throws<ContentLoadException>(Load);

        Assert.Contains("figures.json", ex.Message);
        Assert.Contains("produkce_navic", ex.Message);
    }

    [Fact]
    public void Figure_WithAnUnknownMilestone_Throws()
    {
        WriteWorldWithFigure("""
        { "id": "hero", "effect": "production_mult", "magnitude": 0.5,
          "lifeSeconds": 600, "milestone": "neexistuje" }
        """);

        var ex = Assert.Throws<ContentLoadException>(Load);

        Assert.Contains("neexistuje", ex.Message);
    }

    [Fact]
    public void Figure_WithAnUnknownStatue_Throws()
    {
        WriteWorldWithFigure("""
        { "id": "hero", "effect": "production_mult", "magnitude": 0.5,
          "lifeSeconds": 600, "milestone": "first", "statue": "socha_ktera_neni" }
        """);

        var ex = Assert.Throws<ContentLoadException>(Load);

        Assert.Contains("socha_ktera_neni", ex.Message);
    }

    [Fact]
    public void Figure_WithNoLifespan_Throws()
    {
        // Osobnost, která žije nula vteřin, by hráči jen dvakrát bliknula
        // v rohu obrazovky.
        WriteWorldWithFigure("""
        { "id": "hero", "effect": "production_mult", "magnitude": 0.5,
          "lifeSeconds": 0, "milestone": "first" }
        """);

        var ex = Assert.Throws<ContentLoadException>(Load);

        Assert.Contains("lifeSeconds", ex.Message);
    }

    [Fact]
    public void Figure_WithoutAName_Throws()
    {
        // Bez jména by se v toastu ohlásila doslova jako „figure.hero".
        WriteAllValid();
        WriteMilestones();
        Write(Path.Combine("lang", "cs.json"), LangJson("cs", "Čeština", extraKeys: MilestoneKeys));
        Write(Path.Combine("lang", "en.json"), LangJson("en", "English", extraKeys: MilestoneKeys));
        Write("figures.json", """
        {
          "schemaVersion": 1,
          "figures": [
            { "id": "hero", "effect": "production_mult", "magnitude": 0.5,
              "lifeSeconds": 600, "milestone": "first" }
          ]
        }
        """);

        var ex = Assert.Throws<ContentLoadException>(Load);

        Assert.Contains("figure.hero", ex.Message);
    }

    /// <summary>Minimální data + jeden milník + jedna osobnost (i s jejími jmény v jazycích).</summary>
    private void WriteWorldWithFigure(string figureJson)
    {
        WriteAllValid();
        WriteMilestones();
        Write(Path.Combine("lang", "cs.json"), LangJson("cs", "Čeština", extraKeys: FigureKeys));
        Write(Path.Combine("lang", "en.json"), LangJson("en", "English", extraKeys: FigureKeys));
        Write("figures.json", $$"""
        {
          "schemaVersion": 1,
          "figures": [ {{figureJson}} ]
        }
        """);
    }

    private static readonly string[] MilestoneKeys = { "milestone.first" };

    private static readonly string[] FigureKeys = { "milestone.first", "figure.hero", "figure.hero.desc" };

    private void WriteMilestones() => Write("milestones.json", """
    {
      "schemaVersion": 1,
      "milestones": [ { "id": "first", "condition": { "metric": "buildings", "target": 1 } } ]
    }
    """);

    // ----- kronika -----

    [Fact]
    public void Chronicle_LoadsItsSentenceTemplates()
    {
        WriteWorldWithChronicle("""{ "id": "today", "moment": "today" }""");

        var content = Load();

        Assert.True(content.Chronicle.IsEnabled);
        Assert.Equal(ChronicleMoment.Today, content.Chronicle[0].Moment);
        Assert.Equal("chronicle.line.today", content.Chronicle[0].TextKey);
    }

    [Fact]
    public void Chronicle_WithAnUnknownMoment_Throws()
    {
        // Okamžik pozná kód. Překlep by jinak znamenal větu, která se nikdy
        // nenapíše — a prázdné místo na stránce nikoho nenapadne hlásit.
        WriteWorldWithChronicle("""{ "id": "today", "moment": "kdysi_davno" }""");

        var ex = Assert.Throws<ContentLoadException>(Load);

        Assert.Contains("chronicle.json", ex.Message);
        Assert.Contains("kdysi_davno", ex.Message);
    }

    [Fact]
    public void Chronicle_WithAThresholdOutsideZeroToOne_Throws()
    {
        // Spokojenost je podíl. Práh 50 by znamenal větu, která se nespustí
        // nikdy, a přišlo by se na to až po hodinách hraní.
        WriteWorldWithChronicle("""{ "id": "today", "moment": "hardship", "threshold": 50 }""");

        var ex = Assert.Throws<ContentLoadException>(Load);

        Assert.Contains("threshold", ex.Message);
    }

    [Fact]
    public void Chronicle_WithoutItsSentenceInTheLanguage_Throws()
    {
        WriteAllValid();
        Write("chronicle.json", """
        { "schemaVersion": 1, "lines": [ { "id": "today", "moment": "today" } ] }
        """);

        var ex = Assert.Throws<ContentLoadException>(Load);

        Assert.Contains("chronicle.line.today", ex.Message);
    }

    /// <summary>Minimální data + jedna věta kroniky (i s jejím textem v jazycích).</summary>
    private void WriteWorldWithChronicle(string lineJson)
    {
        WriteAllValid();
        Write(Path.Combine("lang", "cs.json"), LangJson("cs", "Čeština", extraKeys: ChronicleKeys));
        Write(Path.Combine("lang", "en.json"), LangJson("en", "English", extraKeys: ChronicleKeys));
        Write("chronicle.json", $$"""
        {
          "schemaVersion": 1,
          "lines": [ {{lineJson}} ]
        }
        """);
    }

    private static readonly string[] ChronicleKeys = { "chronicle.line.today" };

    // ----- zvonohra -----

    [Fact]
    public void Carillon_LoadsItsBuildingAndDefaultTune()
    {
        WriteAllValid();
        Write("carillon.json", """
        {
          "schemaVersion": 1, "building": "house",
          "defaultTune": [0, 2, 4, -1], "baseFrequency": 523.25, "noteSeconds": 0.45
        }
        """);

        var content = Load();

        Assert.True(content.Carillon.IsEnabled);
        Assert.Equal(content.Buildings.IndexOf("house"), content.Carillon.BuildingIndex);
        Assert.Equal(new[] { 0, 2, 4, -1 }, content.Carillon.DefaultTune);
    }

    [Fact]
    public void Carillon_WithAnUnknownBuilding_Throws()
    {
        WriteAllValid();
        Write("carillon.json", """
        { "schemaVersion": 1, "building": "zvonice_ktera_neni", "baseFrequency": 523.25, "noteSeconds": 0.45 }
        """);

        var ex = Assert.Throws<ContentLoadException>(Load);

        Assert.Contains("zvonice_ktera_neni", ex.Message);
    }

    [Fact]
    public void Carillon_WithANoteOutsideTheScale_Throws()
    {
        // Tón mimo stupnici by se tiše přehrál jako pauza a hráč by měl
        // v melodii díru, kterou nezpůsobil.
        WriteAllValid();
        Write("carillon.json", """
        {
          "schemaVersion": 1, "building": "house",
          "defaultTune": [0, 99], "baseFrequency": 523.25, "noteSeconds": 0.45
        }
        """);

        var ex = Assert.Throws<ContentLoadException>(Load);

        Assert.Contains("carillon.json", ex.Message);
        Assert.Contains("99", ex.Message);
    }

    [Fact]
    public void Carillon_WithAnImpossibleFrequency_Throws()
    {
        WriteAllValid();
        Write("carillon.json", """
        { "schemaVersion": 1, "building": "house", "baseFrequency": 0, "noteSeconds": 0.45 }
        """);

        var ex = Assert.Throws<ContentLoadException>(Load);

        Assert.Contains("baseFrequency", ex.Message);
    }

    // ----- scénáře -----

    [Fact]
    public void Scenario_LoadsItsGoalRulesAndOverrides()
    {
        WriteWorldWithScenario("""
        {
          "id": "test", "seed": 42, "preset": "p",
          "gameplay": { "startingPopulation": 3 },
          "startingResources": { "wood": 25 },
          "goal": { "metric": "population", "target": 100 },
          "failBelow": { "metric": "population", "target": 0 },
          "timeLimitSeconds": 600,
          "rules": ["noAscension"]
        }
        """);

        var content = Load();
        var scenario = content.Scenarios[0];

        Assert.Equal(42, scenario.Seed);
        Assert.Equal(3, scenario.Gameplay.StartingPopulation);
        Assert.Equal(100, scenario.Goal.Target);
        Assert.True(scenario.Has(ScenarioRule.NoAscension));
        Assert.True(scenario.HasTimeLimit);

        // Prohra „lidí klesne na nulu" musí projít — běžný cíl s prahem 0 by
        // neprošel a tohle je přesně ta výjimka.
        Assert.Equal(0, scenario.FailBelow!.Value.Target);
    }

    [Fact]
    public void Scenario_WithoutAGoal_Throws()
    {
        // Bez cíle to není scénář, ale jinak nastavená volná hra — a hráč by
        // čekal konec, který nikdy nepřijde.
        WriteWorldWithScenario("""{ "id": "test", "seed": 1 }""");

        var ex = Assert.Throws<ContentLoadException>(Load);

        Assert.Contains("goal", ex.Message);
    }

    [Fact]
    public void Scenario_WithAnUnknownRule_Throws()
    {
        WriteWorldWithScenario("""
        {
          "id": "test", "seed": 1,
          "goal": { "metric": "population", "target": 10 },
          "rules": ["zakazano_vsechno"]
        }
        """);

        var ex = Assert.Throws<ContentLoadException>(Load);

        Assert.Contains("zakazano_vsechno", ex.Message);
    }

    [Fact]
    public void Scenario_WithAnUnknownPreset_Throws()
    {
        WriteWorldWithScenario("""
        {
          "id": "test", "seed": 1, "preset": "svet_ktery_neni",
          "goal": { "metric": "population", "target": 10 }
        }
        """);

        var ex = Assert.Throws<ContentLoadException>(Load);

        Assert.Contains("svet_ktery_neni", ex.Message);
    }

    [Fact]
    public void Scenario_WithoutItsNameInTheLanguage_Throws()
    {
        WriteAllValid();
        Write("scenarios.json", """
        {
          "schemaVersion": 1,
          "scenarios": [
            { "id": "test", "seed": 1, "goal": { "metric": "population", "target": 10 } }
          ]
        }
        """);

        var ex = Assert.Throws<ContentLoadException>(Load);

        Assert.Contains("scenario.test", ex.Message);
    }

    [Theory]
    [InlineData("\"rules\": [\"singleBiome\"]", "biome")]
    [InlineData("\"biome\": \"grass\"", "singleBiome")]
    [InlineData("\"rules\": [\"singleBiome\"], \"biome\": \"water\"", "voda")]
    [InlineData("\"rules\": [\"singleBiome\"], \"biome\": \"lava\"", "lava")]
    public void Scenario_BadSingleBiome_Throws(string fields, string expected)
    {
        // Pravidlo bez biomu neví, čím svět zaplnit; biom bez pravidla by tiše
        // nic nedělal; voda jako souš nedává smysl. Všechno spadne při startu.
        WriteWorldWithScenario($$"""
        { "id": "test", "seed": 1, "goal": { "metric": "population", "target": 10 }, {{fields}} }
        """);

        var ex = Assert.Throws<ContentLoadException>(Load);

        Assert.Contains(expected, ex.Message);
    }

    [Fact]
    public void Scenario_SingleBiome_LoadsTheBiome()
    {
        WriteWorldWithScenario("""
        { "id": "test", "seed": 1, "goal": { "metric": "population", "target": 10 },
          "rules": ["singleBiome", "noRoads", "floodedWorld", "defenceFromStart", "noResearch", "highUpkeep", "nightWorld"],
          "biome": "grass" }
        """);

        var scenario = Load().Scenarios[0];

        Assert.Equal(1, scenario.BiomeIndex);
        Assert.True(scenario.Has(ScenarioRule.NightWorld));
        Assert.Equal(7, scenario.Rules.Count);
    }

    [Fact]
    public void Scenario_NamedAll_Throws()
    {
        // „all" je vyhrazené: challenge:all znamená „všechny výzvy" (odměna Mistra).
        WriteWorldWithScenario("""{ "id": "all", "seed": 1, "goal": { "metric": "population", "target": 10 } }""");

        var ex = Assert.Throws<ContentLoadException>(Load);

        Assert.Contains("all", ex.Message);
    }

    [Theory]
    [InlineData("challenge:nope", "neexistující")]
    [InlineData("tech:writing", "unlockedBy")]
    public void Policy_BadUnlock_Throws(string unlock, string expected)
    {
        // Politika zamčená na výzvu, která neexistuje, by se nedala zapnout nikdy.
        WriteAllValid();
        Write("policies.json", $$"""
        {
          "schemaVersion": 1,
          "policies": [ { "id": "frugal", "effect": "upkeep_discount", "magnitude": 40, "unlockedBy": "{{unlock}}" } ]
        }
        """);

        var ex = Assert.Throws<ContentLoadException>(Load);

        Assert.Contains(expected, ex.Message);
    }

    [Theory]
    [InlineData("\"highUpkeepMult\": 0.5", "highUpkeepMult")]
    [InlineData("\"nightFoodMult\": 0", "nightFoodMult")]
    [InlineData("\"floodSeaLevelRise\": 0.7", "floodSeaLevelRise")]
    [InlineData("\"oasisShare\": 0", "oasisShare")]
    [InlineData("\"defenceWaveIntervalMult\": 2", "defenceWaveIntervalMult")]
    public void ChallengeRules_OutOfRange_Throws(string field, string expected)
    {
        // „Drahý provoz", který zlevní, nebo noc, ve které nic neroste vůbec,
        // jsou chyby v datech — ne výzvy.
        WriteAllValid();
        WriteGameplayWith($$"""
          "challengeRules": { {{field}} }
        """);

        var ex = Assert.Throws<ContentLoadException>(Load);

        Assert.Contains(expected, ex.Message);
    }

    [Fact]
    public void ChallengeRules_MissingFieldsKeepDefaults()
    {
        WriteAllValid();
        WriteGameplayWith("""
          "challengeRules": { "highUpkeepMult": 4 }
        """);

        var rules = Load().Gameplay.ChallengeRules;

        Assert.Equal(4, rules.HighUpkeepMult);
        Assert.Equal(ChallengeRulesConfig.Default.NightFoodMult, rules.NightFoodMult);
    }

    [Fact]
    public void RealContracts_KeepGrowingPastTheCapAndPayLegacyLate()
    {
        var content = TestData.LoadRealContent();
        var board = content.Contracts.Board;

        Assert.True(board.SoftGrowth > 1.0, "nad stropem má nabídka dál růst");
        Assert.Contains(content.Contracts.Contracts.All, c => c.LegacyPoints > 0);

        // Body Odkazu jen za pozdní zakázky: kdo dostane Odkaz za dřevo na zimu,
        // tomu se Odkaz rozpadne pod rukama.
        foreach (var contract in content.Contracts.Contracts.All.Where(c => c.LegacyPoints > 0))
        {
            Assert.NotNull(contract.Requirement);
        }
    }

    /// <summary>Minimální data + jeden scénář (i s jeho jménem a popisem v jazycích).</summary>
    private void WriteWorldWithScenario(string scenarioJson)
    {
        WriteAllValid();
        Write(Path.Combine("lang", "cs.json"), LangJson("cs", "Čeština", extraKeys: ScenarioKeys));
        Write(Path.Combine("lang", "en.json"), LangJson("en", "English", extraKeys: ScenarioKeys));
        Write("scenarios.json", $$"""
        {
          "schemaVersion": 1,
          "scenarios": [ {{scenarioJson}} ]
        }
        """);
    }

    private static readonly string[] ScenarioKeys = { "scenario.test", "scenario.test.desc" };

    // ----- dozvuky voleb v událostech -----

    [Fact]
    public void EventChoice_LoadsItsEffect()
    {
        WriteWorldWithEvent("""
        { "id": "a", "effect": { "kind": "production", "resource": "food", "multiplier": 0.75, "seconds": 180 } }
        """);

        var content = Load();

        var effect = content.Events[0].Choices[0].Effect;
        Assert.NotNull(effect);
        Assert.Equal(EventEffectKind.Production, effect!.Kind);
        Assert.Equal(content.Resources.IndexOf("food"), effect.ResourceIndex);
        Assert.Equal(0.75, effect.Multiplier);
        Assert.Equal(180, effect.Seconds);
    }

    [Fact]
    public void EventChoice_WithoutResource_AffectsEverything()
    {
        WriteWorldWithEvent("""{ "id": "a", "effect": { "kind": "production", "multiplier": 0.8, "seconds": 60 } }""");

        Assert.Equal(-1, Load().Events[0].Choices[0].Effect!.ResourceIndex);
    }

    [Theory]
    [InlineData("""{ "kind": "earthquake", "multiplier": 0.8, "seconds": 60 }""", "earthquake")]
    [InlineData("""{ "kind": "production", "resource": "gold", "multiplier": 0.8, "seconds": 60 }""", "gold")]
    [InlineData("""{ "kind": "growth", "resource": "food", "multiplier": 0.8, "seconds": 60 }""", "production")]
    [InlineData("""{ "kind": "production", "multiplier": 0, "seconds": 60 }""", "multiplier")]
    [InlineData("""{ "kind": "production", "multiplier": 0.8, "seconds": 0 }""", "seconds")]
    public void EventChoice_WithABrokenEffect_Throws(string effectJson, string expected)
    {
        // Násobič nula by výrobu zastavil úplně a nekonečný efekt by nikdy
        // neskončil — obojí je proti relaxačnímu tónu, takže to loader nepustí.
        WriteWorldWithEvent($$"""{ "id": "a", "effect": {{effectJson}} }""");

        var ex = Assert.Throws<ContentLoadException>(Load);

        Assert.Contains(expected, ex.Message);
    }

    /// <summary>Minimální data + jedna událost s jedinou volbou (i s texty v jazycích).</summary>
    private void WriteWorldWithEvent(string choiceJson)
    {
        WriteAllValid();
        Write(Path.Combine("lang", "cs.json"), LangJson("cs", "Čeština", extraKeys: EventKeys));
        Write(Path.Combine("lang", "en.json"), LangJson("en", "English", extraKeys: EventKeys));
        Write("events.json", $$"""
        {
          "schemaVersion": 1,
          "events": [ { "id": "test", "choices": [ {{choiceJson}} ] } ]
        }
        """);
    }

    private static readonly string[] EventKeys = { "event.test", "event.test.desc", "event.test.a" };

    [Theory]
    [InlineData("retireWhen", "{ \"metric\": \"nesmysl\", \"target\": 1 }", "retireWhen")]
    [InlineData("activeWhen", "{ \"metric\": \"nesmysl\", \"target\": 1 }", "activeWhen")]
    [InlineData("group", "\"jinde\"", "skupina")]
    public void Quest_BadRetireActiveOrGroup_Throws(string field, string value, string expected)
    {
        // Chybně napsaná podmínka uzavření by úkol tiše nechala viset navždy —
        // přesně to, co pole má opravit. Proto spadne při startu.
        WriteAllValid();
        Write("quests.json", $$"""
        {
          "schemaVersion": 1,
          "quests": [ { "id": "q", "condition": { "metric": "population", "target": 5 }, "{{field}}": {{value}} } ],
          "dynamic": {
            "condition": { "metric": "population", "target": 20 },
            "targetGrowth": 1.5, "rewardGrowth": 1.5, "reward": { "food": 10 }
          }
        }
        """);

        var ex = Assert.Throws<ContentLoadException>(Load);

        Assert.Contains(expected, ex.Message);
    }

    [Theory]
    [InlineData("\"unlockedBy\": \"quest:neni\"", "neexistující")]
    [InlineData("\"unlockedBy\": \"odmena\"", "unlockedBy")]
    [InlineData("\"look\": { \"shape\": \"pagoda\", \"wall\": \"#FFFFFF\", \"roof\": \"#000000\", \"accent\": \"#FF0000\" }", "tvar")]
    [InlineData("\"look\": { \"shape\": \"hut\", \"wall\": \"#FFFFFF\", \"roof\": \"#000000\", \"accent\": \"#FF0000\", \"features\": [\"disco\"] }", "prvek")]
    public void Building_BadUnlockOrLook_Throws(string field, string expected)
    {
        // Pomník zamčený na neexistující cíl by zůstal zamčený navždy; tvar,
        // který malíř neumí, by spadl až při startu grafiky. Obojí musí spadnout
        // hned při načtení, se srozumitelnou hláškou.
        WriteAllValid();
        string buildings = File.ReadAllText(Path.Combine(_tempDir, "buildings.json"));
        int first = buildings.IndexOf("\"id\"", StringComparison.Ordinal);
        Write("buildings.json", buildings.Insert(first, field + ", "));

        var ex = Assert.Throws<ContentLoadException>(Load);

        Assert.Contains(expected, ex.Message);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(50_000)]
    public void Frontier_AttackerCeilingOutOfRange_Throws(int ceiling)
    {
        // Strop útočníků drží tik i paměť v mezích; nula nebo obří číslo by ho
        // tiše vyřadily a pozdní vlny by hru zase položily.
        WriteAllValid();
        Write("frontier.json", $$"""
        {
          "schemaVersion": 1,
          "firstWaveTick": 100,
          "waveIntervalTicks": 100,
          "strengthGrowth": 1.1,
          "spawnDistance": 20,
          "repairTicks": 10,
          "maxAttackersAlive": {{ceiling}},
          "attackers": [ { "id": "raider", "health": 5, "speed": 0.1, "damage": 10, "attackIntervalTicks": 5 } ],
          "waves": [ [ { "attacker": "raider", "count": 2 } ] ]
        }
        """);

        var ex = Assert.Throws<ContentLoadException>(Load);

        Assert.Contains("maxAttackersAlive", ex.Message);
    }

    // ----- pomůcky -----

    private GameContent Load() => new ContentLoader().LoadFrom(_tempDir);

    private void Write(string relativePath, string json) =>
        File.WriteAllText(Path.Combine(_tempDir, relativePath), json);

    /// <summary>Minimální kompletní validní sada dat; testy pak přepisují jednotlivé soubory.</summary>
    /// <summary>Platný gameplay.json plus dodaný blok navíc — pro testy volitelných vrstev.</summary>
    /// <summary>Zapíše gameplay.json s dodaným blokem navíc (znečištění, hromadná stavba…).</summary>
    private void WriteGameplayWith(string extraBlock)
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

    /// <summary>
    /// Přepíše gameplay.json týmž platným obsahem, jen se zadanými povrchy
    /// silnic. Bez toho by každý test na povrchy opisoval celý blok znovu
    /// a první změna schématu by je všechny rozbila.
    /// </summary>
    private void WriteRoadSurfaces(string surfacesJson) =>
        Write("gameplay.json", $$"""
        {
          "schemaVersion": 1,
          "startingPopulation": 5,
          "baseHousingCapacity": 6,
          "populationGrowthPerSecond": 0.12,
          "foodPerPersonPerSecond": 0.04,
          "foodResource": "food",
          "autoBuild": { "intervalTicks": 60, "searchRadius": 6, "populationHeadroom": 2 },
          "roads": { "mapColor": "#9A9284", "maxSearchDistance": 60, "surfaces": {{surfacesJson}} },
          "settlements": { "minBuildings": 3, "clusterDistance": 3, "updateIntervalTicks": 50 },
          "dayNight": { "dayLengthSeconds": 240, "startTimeOfDay": 0.32, "nightColor": "#0A1430",
                        "duskColor": "#E8862F", "nightAlpha": 0.45, "duskAlpha": 0.18 }
        }
        """);

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

    private void WriteWorldGen(string fallbackBiome = "grass", string? defaultPreset = null, string presetExtra = "")
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
            {{presetExtra}}
          }]
        }
        """);
    }

    private static string LangJson(
        string id, string nativeName, bool includeBuildingName = true, bool includeExtraKey = true,
        IEnumerable<string>? extraKeys = null)
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

            // Kategorie budovy je od té doby, co ji hlídá loader, taky jméno
            // obsahu. Testovací budovy kategorii nemají, takže spadnou do
            // „other" — a to musí mít fixture stejně jako skutečná data.
            "\"category.other\": \"-\"",
        };
        if (includeBuildingName)
        {
            keys.Add("\"building.house\": \"-\"");

            // Věta „k čemu ta budova je" je povinná stejně jako jméno —
            // fixture musí splnit tentýž kontrakt jako skutečná data.
            keys.Add("\"building.house.desc\": \"-\"");
        }

        if (includeExtraKey)
        {
            keys.Add("\"ui.hello\": \"-\"");
        }

        foreach (string key in extraKeys ?? Array.Empty<string>())
        {
            keys.Add($"\"{key}\": \"-\"");
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
