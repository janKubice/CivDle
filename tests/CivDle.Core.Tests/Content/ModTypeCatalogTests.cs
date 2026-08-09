using CivDle.Core.Content;
using CivDle.Core.Content.Mods;
using CivDle.Core.Tests.Support;
using Xunit;

namespace CivDle.Core.Tests.Content;

/// <summary>
/// Katalog typů obsahu pro ingame tvůrce (bod 2).
///
/// <para>Nejdůležitější je poslední test: co tvůrce složí, musí <b>opravdu</b>
/// projít skutečným loaderem. Kdyby zapisování a načítání šly každé svou
/// cestou, tvůrce by uměl vyrobit mod, který hru shodí při startu.</para>
/// </summary>
public sealed class ModTypeCatalogTests : IDisposable
{
    private readonly string _modsDirectory =
        Path.Combine(Path.GetTempPath(), "civdle-modtypes-" + Path.GetRandomFileName());

    public void Dispose()
    {
        if (Directory.Exists(_modsDirectory))
        {
            Directory.Delete(_modsDirectory, recursive: true);
        }
    }

    private static ModTypeCatalog RealCatalog() =>
        ModTypeCatalog.LoadFrom(Path.Combine(TestData.RealDataDirectory, "mod-types.json"));

    [Fact]
    public void TheCreatorKnowsEveryTypeThePlayerAskedFor()
    {
        // „budova, surovina, událost, výzkum, fauna, jména měst, úkol"
        var catalog = RealCatalog();

        foreach (string id in new[]
                 { "building", "resource", "event", "tech", "fauna", "settlementName", "quest" })
        {
            Assert.NotNull(catalog.Find(id));
        }
    }

    [Fact]
    public void ABuildingHasEverythingItNeedsToBeConfigured()
    {
        // Půdorys, co vyrábí, co spotřebuje, bydlení, práce, cena, biomy — a sprite.
        var building = RealCatalog().Find("building");
        Assert.NotNull(building);

        var keys = building!.Fields.Select(f => f.Key).ToHashSet(StringComparer.Ordinal);
        foreach (string key in new[]
                 {
                     "footprintWidth", "footprintHeight", "recipeInput", "recipeOutput",
                     "housingCapacity", "workerSlots", "buildCost", "allowedBiomes", "sprite",
                 })
        {
            Assert.Contains(key, keys);
        }

        Assert.True(building.HasSprite, "budova má obrázek, takže i kreslítko");
    }

    [Fact]
    public void ReferenceFieldsSayWhatTheyPointAt()
    {
        // Bez toho by editor u „prerekvizit" nevěděl, jaký seznam nabídnout.
        var catalog = RealCatalog();
        var tech = catalog.Find("tech")!;

        var prerequisites = tech.Fields.Single(f => f.Key == "prerequisites");
        Assert.Equal(ModFieldKind.References, prerequisites.Kind);
        Assert.Equal("tech", prerequisites.Reference);

        var cost = tech.Fields.Single(f => f.Key == "cost");
        Assert.Equal(ModFieldKind.Amounts, cost.Kind);
        Assert.Equal("resource", cost.Reference);
    }

    [Fact]
    public void NestedPathsBecomeNestedJson()
    {
        var building = RealCatalog().Find("building")!;
        var entry = new ModEntry("building")
            .With("id", "glassworks")
            .With("footprintWidth", "2")
            .With("footprintHeight", "3")
            .With("recipeOutput", "planks=2")
            .With("recipeTicks", "40");

        var json = ModEntryWriter.ToJson(entry, building)!.AsObject();

        Assert.Equal(2, json["footprint"]!.AsArray()[0]!.GetValue<int>());
        Assert.Equal(3, json["footprint"]!.AsArray()[1]!.GetValue<int>());
        Assert.Equal(2, json["recipe"]!["output"]!["planks"]!.GetValue<int>());
        Assert.Equal(40, json["recipe"]!["timeTicks"]!.GetValue<int>());
    }

    [Fact]
    public void EmptyFieldsAreLeftOutEntirely()
    {
        // Prázdný recept nebo prázdný seznam biomů má jiný význam než chybějící
        // klíč — zapsat „recipe: {}" by z budovy udělalo rozbitou výrobnu.
        var building = RealCatalog().Find("building")!;
        var json = ModEntryWriter.ToJson(new ModEntry("building").With("id", "hut"), building)!.AsObject();

        Assert.False(json.ContainsKey("recipe"));
        Assert.False(json.ContainsKey("allowedBiomes"));
        Assert.Equal("hut", json["id"]!.GetValue<string>());
    }

    [Fact]
    public void CityNamesAreAPlainList()
    {
        var names = RealCatalog().Find("settlementName")!;
        Assert.True(names.PlainList);

        var json = ModEntryWriter.ToJson(new ModEntry("settlementName").With("value", "Kotěhůlky"), names);
        Assert.Equal("Kotěhůlky", json!.GetValue<string>());
    }

    [Fact]
    public void AnUnknownFieldKindIsALoadError()
    {
        string path = Path.Combine(_modsDirectory, "mod-types.json");
        Directory.CreateDirectory(_modsDirectory);
        File.WriteAllText(path, """
            {
              "schemaVersion": 1,
              "types": [
                { "id": "weird", "file": "weird.json", "arrayKey": "weird",
                  "fields": [ { "key": "id", "kind": "hologram", "path": "id" } ] }
              ]
            }
            """);

        var error = Assert.Throws<ContentLoadException>(() => ModTypeCatalog.LoadFrom(path));
        Assert.Contains("hologram", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AModBuiltFromTheCatalogLoadsIntoTheRealGame()
    {
        var catalog = RealCatalog();
        var draft = new ModDraft("catalog_mod", "Katalogový mod") { Types = catalog };

        draft.Entries.Add(new ModEntry("resource")
            .With("id", "amber").With("name", "Jantar")
            .With("mapColor", "#E0A040").With("baseStorage", "400"));

        draft.Entries.Add(new ModEntry("building")
            .With("id", "amber_hut").With("name", "Jantarová bouda")
            .With("description", "Sbírá jantar z pobřeží.")
            .With("category", "production").With("mapColor", "#D8B060")
            .With("footprintWidth", "1").With("footprintHeight", "1")
            .With("workerSlots", "2").With("buildCost", "wood=15, planks=5")
            .With("recipeOutput", "amber=1").With("recipeTicks", "60")
            .With("allowedBiomes", "grassland, beach").With("buildable", "true"));

        draft.Entries.Add(new ModEntry("fauna")
            .With("id", "amber_beetle").With("name", "Jantarový brouk")
            .With("biomes", "grassland").With("color", "#E0A040")
            .With("size", "2").With("speed", "12").With("timeOfDay", "day"));

        draft.Entries.Add(new ModEntry("settlementName").With("value", "Jantarov"));

        string directory = draft.WriteTo(_modsDirectory);
        var check = ModValidator.Check(TestData.RealDataDirectory, directory);
        Assert.True(check.Ok, check.Message);

        var content = new ContentLoader().LoadFrom(
            TestData.RealDataDirectory,
            new[] { new ModPackage("catalog_mod", "Katalogový mod", "1.0.0", directory) });

        Assert.True(content.Resources.TryIndexOf("amber", out _));
        Assert.True(content.Buildings.TryIndexOf("amber_hut", out int hut));
        Assert.Equal(2, content.Buildings[hut].WorkerSlots);
        Assert.Contains(content.Fauna, f => f.Id == "amber_beetle");
        Assert.Contains("Jantarov", content.SettlementNames);
    }
}
