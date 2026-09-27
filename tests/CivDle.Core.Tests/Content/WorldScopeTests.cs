using System.Text.Json.Nodes;
using CivDle.Core.Content;
using Xunit;

namespace CivDle.Core.Tests.Content;

/// <summary>
/// Výběr obsahu podle světa (svety-design.md 7.1). Čistá funkce nad textem:
/// co patří kterému světu, a jak se sdílený obsah přizpůsobí surovinám světa.
///
/// <para>Hlídá se hlavně to, co by se pokazilo potichu: Domovina musí dostat
/// přesně totéž co dřív, obsah bez značky do kolonie nesmí proklouznout a náhrada
/// surovin nesmí nic ztratit.</para>
/// </summary>
public class WorldScopeTests
{
    private const string Buildings = """
    { "schemaVersion": 1, "buildings": [
      { "id": "house", "buildCost": { "wood": 10 } },
      { "id": "warehouse", "worlds": ["*"], "buildCost": { "wood": 20, "stone": 5 },
        "recipe": { "input": { "wood": 1 }, "output": { "food": 2 } } },
      { "id": "dune_hut", "worlds": ["dune"], "buildCost": { "adobe": 4 } },
      { "id": "shared_home_dune", "worlds": ["home", "dune"] }
    ] }
    """;

    [Fact]
    public void AFileWithoutTagsIsUntouchedForHome()
    {
        const string json = """{ "schemaVersion": 1, "buildings": [ { "id": "house" } ] }""";

        Assert.Same(json, WorldScope.Filter("buildings.json", json, WorldScope.HomeId));
    }

    [Fact]
    public void HomeKeepsUntaggedAndSharedButNotOtherWorlds()
    {
        var ids = Ids(WorldScope.Filter("buildings.json", Buildings, WorldScope.HomeId), "buildings");

        Assert.Equal(new[] { "house", "warehouse", "shared_home_dune" }, ids);
    }

    [Fact]
    public void AColonyGetsOnlyWhatIsMarkedForIt()
    {
        var ids = Ids(WorldScope.Filter("buildings.json", Buildings, "dune"), "buildings");

        Assert.Equal(new[] { "warehouse", "dune_hut", "shared_home_dune" }, ids);
    }

    [Fact]
    public void StructureWithoutTagsIsEverywhere()
    {
        const string json = """
        { "schemaVersion": 1, "biomes": [ { "id": "grass" }, { "id": "salt", "worlds": ["dune"] } ] }
        """;

        Assert.Equal(new[] { "grass" }, Ids(WorldScope.Filter("biomes.json", json, WorldScope.HomeId), "biomes"));
        Assert.Equal(new[] { "grass", "salt" }, Ids(WorldScope.Filter("biomes.json", json, "dune"), "biomes"));
        Assert.Equal(new[] { "grass" }, Ids(WorldScope.Filter("biomes.json", json, "frost"), "biomes"));
    }

    [Fact]
    public void TheTagIsRemovedFromWhatStays()
    {
        string filtered = WorldScope.Filter("buildings.json", Buildings, "dune");

        Assert.DoesNotContain("\"worlds\"", filtered);
    }

    [Fact]
    public void SubstitutesRenameAndSumSharedCosts()
    {
        var substitutes = new Dictionary<string, string> { ["wood"] = "adobe", ["stone"] = "adobe" };

        var root = JsonNode.Parse(WorldScope.Filter("buildings.json", Buildings, "dune", substitutes))!;
        var warehouse = root["buildings"]!.AsArray().First(b => b!["id"]!.GetValue<string>() == "warehouse")!;

        Assert.Equal(25, warehouse["buildCost"]!["adobe"]!.GetValue<long>()); // 20 dřeva + 5 kamene
        Assert.Null(warehouse["buildCost"]!["wood"]);
        Assert.Equal(1, warehouse["recipe"]!["input"]!["adobe"]!.GetValue<long>());
        Assert.Equal(2, warehouse["recipe"]!["output"]!["food"]!.GetValue<long>()); // bez náhrady zůstává
    }

    [Fact]
    public void SubstitutesDoNotTouchHome()
    {
        var substitutes = new Dictionary<string, string> { ["wood"] = "adobe" };

        string filtered = WorldScope.Filter("buildings.json", Buildings, WorldScope.HomeId, substitutes);

        Assert.DoesNotContain("adobe\": 2", filtered);
        Assert.Contains("\"wood\"", filtered);
    }

    [Fact]
    public void SubstitutesAlsoRewriteResourceReferences()
    {
        const string json = """
        { "schemaVersion": 1, "upgrades": [ { "id": "lumber", "effect": "x", "targetResource": "wood" } ] }
        """;
        var substitutes = new Dictionary<string, string> { ["wood"] = "adobe" };

        var root = JsonNode.Parse(WorldScope.Filter("prestige.json", json, "dune", substitutes))!;

        Assert.Equal("adobe", root["upgrades"]![0]!["targetResource"]!.GetValue<string>());
    }

    [Fact]
    public void FilesOutsideTheTableAreNeverFiltered()
    {
        const string json = """{ "schemaVersion": 1, "things": [ { "id": "a", "worlds": ["frost"] } ] }""";

        Assert.Same(json, WorldScope.Filter("networks.json", json, "dune"));
        Assert.False(WorldScope.IsScoped("networks.json"));
    }

    [Fact]
    public void SharedStorageLosesResourcesTheWorldDoesNotHave()
    {
        const string json = """
        { "schemaVersion": 1, "buildings": [
          { "id": "warehouse", "worlds": ["*"], "buildCost": { "wood": 5 }, "storage": { "wood": 100, "grain": 50, "food": 80 } } ] }
        """;
        var known = new HashSet<string> { "adobe", "food" };
        var substitutes = new Dictionary<string, string> { ["wood"] = "adobe" };

        var root = JsonNode.Parse(WorldScope.Filter("buildings.json", json, "dune", substitutes, known))!;
        var storage = root["buildings"]![0]!["storage"]!.AsObject();

        Assert.Equal(new[] { "food", "adobe" }, storage.Select(p => p.Key).OrderByDescending(k => k).ToArray());
        Assert.Equal(5, root["buildings"]![0]!["buildCost"]!["adobe"]!.GetValue<long>()); // cena se neořezává
    }

    [Fact]
    public void HomeStorageIsNeverPruned()
    {
        const string json = """
        { "schemaVersion": 1, "buildings": [ { "id": "warehouse", "worlds": ["*"], "storage": { "grain": 50 } } ] }
        """;

        string filtered = WorldScope.Filter("buildings.json", json, WorldScope.HomeId, null, new HashSet<string>());

        Assert.Contains("grain", filtered);
    }

    [Fact]
    public void GameplayKeepsSettingsButOnlySharedPlantingAndRenamesResources()
    {
        const string json = """
        { "schemaVersion": 1, "dailyReward": { "reward": { "food": 25, "wood": 20 } },
          "planting": { "species": [ { "id": "grove", "resource": "wood" }, { "id": "palm", "worlds": ["dune"], "resource": "food" } ] } }
        """;
        var substitutes = new Dictionary<string, string> { ["wood"] = "clay" };

        var dune = JsonNode.Parse(WorldScope.Filter("gameplay.json", json, "dune", substitutes))!;
        var home = JsonNode.Parse(WorldScope.Filter("gameplay.json", json, WorldScope.HomeId, substitutes))!;

        Assert.Equal(20, dune["dailyReward"]!["reward"]!["clay"]!.GetValue<long>());
        Assert.Null(dune["dailyReward"]!["reward"]!["wood"]);
        Assert.Equal(new[] { "palm" }, dune["planting"]!["species"]!.AsArray().Select(x => x!["id"]!.GetValue<string>()));
        Assert.Equal(new[] { "grove" }, home["planting"]!["species"]!.AsArray().Select(x => x!["id"]!.GetValue<string>()));
        Assert.Equal(20, home["dailyReward"]!["reward"]!["wood"]!.GetValue<long>());
    }

    private static string[] Ids(string json, string array) =>
        JsonNode.Parse(json)![array]!.AsArray().Select(item => item!["id"]!.GetValue<string>()).ToArray();
}
