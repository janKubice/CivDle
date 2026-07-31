using CivDle.Core.Content;
using CivDle.Core.Content.Mods;
using CivDle.Core.Tests.Support;
using Xunit;

namespace CivDle.Core.Tests.Content;

/// <summary>
/// Moddovatelnost: obsah hry je celý v JSON, tohle je ten chybějící kus —
/// způsob, jak k němu <b>přidat</b> nebo v něm <b>přepsat</b> položku, aniž by
/// mod musel dodat celý soubor.
///
/// <para>Testuje se hlavně to, co by se jinak projevilo až po aktualizaci hry:
/// mod nesmí přemazat, co nezmiňuje; nesmí přeházet pořadí položek (éry, stupně
/// měřítka na něm stojí); a slévání musí být předvídatelné.</para>
/// </summary>
public class ModTests : IDisposable
{
    private readonly string _tempDir;

    public ModTests()
    {
        _tempDir = Path.Combine(AppContext.BaseDirectory, "tmp-mod-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose() => Directory.Delete(_tempDir, recursive: true);

    // ----- slévání -----

    [Fact]
    public void WithoutModsTheFileIsUntouched()
    {
        const string original = """{ "a": 1 }""";

        Assert.Equal(original, JsonOverlay.Merge(original, Array.Empty<string>()));
    }

    [Fact]
    public void AModChangesOnlyWhatItMentions()
    {
        // Tohle je celé jádro: kdyby mod musel dodat celou budovu, zamrzl by
        // na verzi, ve které vznikl.
        string merged = JsonOverlay.Merge(
            """{ "buildings": [ { "id": "house", "cost": 5, "workers": 2 } ] }""",
            new[] { """{ "buildings": [ { "id": "house", "cost": 3 } ] }""" });

        Assert.Contains("\"cost\":3", merged);
        Assert.Contains("\"workers\":2", merged);
    }

    [Fact]
    public void ANewEntryIsAppended()
    {
        string merged = JsonOverlay.Merge(
            """{ "buildings": [ { "id": "house" } ] }""",
            new[] { """{ "buildings": [ { "id": "observatory" } ] }""" });

        Assert.Contains("house", merged);
        Assert.Contains("observatory", merged);
    }

    [Fact]
    public void ChangedEntriesKeepTheirPlace()
    {
        // Pořadí nese význam (éry, stupně měřítka) — mod, který mění cenu,
        // nemá položku posunout na konec.
        string merged = JsonOverlay.Merge(
            """{ "eras": [ { "id": "first" }, { "id": "second" }, { "id": "third" } ] }""",
            new[] { """{ "eras": [ { "id": "first", "note": "x" } ] }""" });

        Assert.True(merged.IndexOf("first", StringComparison.Ordinal)
            < merged.IndexOf("second", StringComparison.Ordinal));
    }

    [Fact]
    public void NestedSettingsMergeKeyByKey()
    {
        // Mod smí změnit jediné číslo v gameplay.json a zbytku se nedotknout.
        string merged = JsonOverlay.Merge(
            """{ "pollution": { "spreadRate": 0.08, "decayRate": 0.02 } }""",
            new[] { """{ "pollution": { "decayRate": 0.5 } }""" });

        Assert.Contains("\"spreadRate\":0.08", merged);
        Assert.Contains("\"decayRate\":0.5", merged);
    }

    [Fact]
    public void ListsWithoutIdsAreReplacedWholesale()
    {
        // U barev a jmen není co s čím párovat — půlka staré palety mezi
        // novými barvami by byla horší než jasné nahrazení.
        string merged = JsonOverlay.Merge(
            """{ "colors": ["#111111", "#222222", "#333333"] }""",
            new[] { """{ "colors": ["#FFFFFF"] }""" });

        Assert.DoesNotContain("111111", merged);
        Assert.Contains("FFFFFF", merged);
    }

    [Fact]
    public void LaterModsWinOverEarlierOnes()
    {
        string merged = JsonOverlay.Merge(
            """{ "buildings": [ { "id": "house", "cost": 5 } ] }""",
            new[]
            {
                """{ "buildings": [ { "id": "house", "cost": 3 } ] }""",
                """{ "buildings": [ { "id": "house", "cost": 1 } ] }""",
            });

        Assert.Contains("\"cost\":1", merged);
    }

    // ----- hledání modů -----

    [Fact]
    public void WithoutAModFolderNothingHappens()
    {
        // Naprostá většina hráčů žádný mod nemá — chybějící složka není chyba.
        Assert.Empty(ModCatalog.Discover(Path.Combine(_tempDir, "neexistuje")));
    }

    [Fact]
    public void AFolderWithoutAManifestIsNotAMod()
    {
        Directory.CreateDirectory(Path.Combine(_tempDir, "jen-slozka"));

        Assert.Empty(ModCatalog.Discover(_tempDir));
    }

    [Fact]
    public void ADisabledModStaysOnDisk()
    {
        WriteMod("vypnuty", """{ "id": "vypnuty", "enabled": false }""");

        Assert.Empty(ModCatalog.Discover(_tempDir));
    }

    [Fact]
    public void AModWithoutAnIdIsReported()
    {
        // Bez ID by nešlo poznat, který mod hru rozbil.
        WriteMod("bezid", """{ "name": "Bez ID" }""");

        var ex = Assert.Throws<ContentLoadException>(() => ModCatalog.Discover(_tempDir));

        Assert.Contains("id", ex.Message);
    }

    [Fact]
    public void ModsApplyInAlphabeticalOrder()
    {
        // Deterministické pořadí: jinak by se stejná dvojice modů chovala
        // pokaždé jinak podle toho, jak je vrátil souborový systém.
        WriteMod("b-druhy", """{ "id": "b", "name": "Druhý" }""");
        WriteMod("a-prvni", """{ "id": "a", "name": "První" }""");

        var mods = ModCatalog.Discover(_tempDir);

        Assert.Equal(new[] { "a", "b" }, mods.Select(m => m.Id).ToArray());
    }

    [Fact]
    public void TwoModsWithTheSameIdAreRejected()
    {
        WriteMod("prvni", """{ "id": "stejny" }""");
        WriteMod("druhy", """{ "id": "stejny" }""");

        var ex = Assert.Throws<ContentLoadException>(() => ModCatalog.Discover(_tempDir));

        Assert.Contains("stejny", ex.Message);
    }

    [Fact]
    public void AModWithoutANameFallsBackToItsId()
    {
        WriteMod("bezjmena", """{ "id": "bezjmena" }""");

        var mod = Assert.Single(ModCatalog.Discover(_tempDir));

        Assert.Equal("bezjmena", mod.Name);
        Assert.False(string.IsNullOrWhiteSpace(mod.Version));
    }

    // ----- mod az do nactenych dat -----

    [Fact]
    public void AModReachesTheLoadedGame()
    {
        // Konec cele cesty: mod ve slozce -> slity JSON -> hotovy obsah.
        WriteMod("test", @"{ ""id"": ""test"", ""name"": ""Testovaci"" }");
        File.WriteAllText(Path.Combine(_tempDir, "test", "resources.json"), @"
        {
          ""schemaVersion"": 1,
          ""resources"": [
            { ""id"": ""wood"", ""startAmount"": 150 },
            { ""id"": ""obsidian"", ""mapColor"": ""#2B2B33"", ""startAmount"": 0, ""baseStorage"": 500 }
          ]
        }");

        Directory.CreateDirectory(Path.Combine(_tempDir, "test", "lang"));
        foreach (string language in new[] { "cs", "en" })
        {
            File.WriteAllText(
                Path.Combine(_tempDir, "test", "lang", language + ".json"),
                @"{ ""schemaVersion"": 1, ""id"": """ + language + @""", ""nativeName"": ""x"",
                    ""strings"": { ""resource.obsidian"": ""Obsidian"" } }");
        }

        var mods = ModCatalog.Discover(_tempDir);
        var content = new ContentLoader().LoadFrom(TestData.RealDataDirectory, mods);

        Assert.True(content.Resources.TryIndexOf("obsidian", out int added), "Mod mel pridat surovinu.");
        Assert.Equal(500, content.Resources[added].BaseStorage);
        Assert.Equal(150, content.Resources[content.Resources.IndexOf("wood")].StartAmount);
        Assert.Equal("test", Assert.Single(content.Mods).Id);
    }

    [Fact]
    public void WithoutModsTheGameLoadsExactlyAsBefore()
    {
        var plain = new ContentLoader().LoadFrom(TestData.RealDataDirectory);
        var withEmptyModList = new ContentLoader().LoadFrom(TestData.RealDataDirectory, Array.Empty<ModPackage>());

        Assert.Equal(plain.Buildings.Count, withEmptyModList.Buildings.Count);
        Assert.Equal(plain.Resources.Count, withEmptyModList.Resources.Count);
        Assert.Empty(plain.Mods);
    }

    private void WriteMod(string folder, string manifest)
    {
        string directory = Path.Combine(_tempDir, folder);
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "mod.json"), manifest);
    }
}
