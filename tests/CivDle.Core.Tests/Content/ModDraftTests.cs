using CivDle.Core.Content.Mods;
using CivDle.Core.Tests.Support;
using Xunit;

namespace CivDle.Core.Tests.Content;

/// <summary>
/// Editor modů: to, co v něm hráč složí, musí jít <b>opravdu načíst</b>.
///
/// <para>Nejdůležitější test je <see cref="AModMadeInTheEditorLoadsIntoTheRealGame"/>:
/// projde celou cestu editor → JSON na disku → skutečný <c>ContentLoader</c>.
/// Kdyby zapisování a načítání šla každá svou cestou, editor by uměl vyrobit
/// mod, který hru shodí při startu — tedy ve chvíli, kdy už ho hráč nemá kde
/// vypnout.</para>
/// </summary>
public sealed class ModDraftTests : IDisposable
{
    private readonly string _modsDirectory =
        Path.Combine(Path.GetTempPath(), "civdle-mod-test-" + Path.GetRandomFileName());

    public void Dispose()
    {
        if (Directory.Exists(_modsDirectory))
        {
            Directory.Delete(_modsDirectory, recursive: true);
        }
    }

    /// <summary>Mod, který přidá surovinu a budovu, co ji vyrábí ze dřeva.</summary>
    private static ModDraft SampleDraft()
    {
        var draft = new ModDraft("test_mod", "Testovací mod");
        draft.Resources.Add(new ResourceDraft("glass", "Sklo", "#9FD8E0", StartAmount: 0, BaseStorage: 300));
        draft.Buildings.Add(new BuildingDraft(
            "glassworks",
            "Sklárna",
            "Taví písek na sklo.",
            Category: "production",
            WorkerSlots: 2,
            BuildCost: new[] { new AmountDraft("wood", 20), new AmountDraft("stone", 10) },
            RecipeInputs: new[] { new AmountDraft("stone", 2) },
            RecipeOutputs: new[] { new AmountDraft("glass", 1) },
            RecipeSeconds: 4.0));
        return draft;
    }

    [Fact]
    public void AModMadeInTheEditorLoadsIntoTheRealGame()
    {
        string directory = SampleDraft().WriteTo(_modsDirectory);

        var check = ModValidator.Check(TestData.RealDataDirectory, directory);

        Assert.True(check.Ok, check.Message);
    }

    [Fact]
    public void TheNewResourceAndBuildingActuallyArriveInTheContent()
    {
        string directory = SampleDraft().WriteTo(_modsDirectory);
        var package = new ModPackage("test_mod", "Testovací mod", "1.0.0", directory);

        var content = new CivDle.Core.Content.ContentLoader()
            .LoadFrom(TestData.RealDataDirectory, new[] { package });

        Assert.True(content.Resources.TryIndexOf("glass", out int resourceIndex));
        Assert.True(content.Buildings.TryIndexOf("glassworks", out int buildingIndex));

        var building = content.Buildings[buildingIndex];
        Assert.Equal(2, building.WorkerSlots);
        Assert.NotNull(building.Recipe);

        // Editor mluví v sekundách, data v ticích — 4 s při 10 tik/s je 40 tiků.
        Assert.Equal(40, building.Recipe!.TimeTicks);
        Assert.Contains(building.Recipe.Outputs, o => o.ResourceIndex == resourceIndex);
    }

    [Fact]
    public void EveryLanguageGetsTheNewKeys()
    {
        // Hra kontroluje úplnost překladů. Mod, který doplní klíč jen do jednoho
        // jazyka, by shodil start ve všech ostatních.
        string directory = SampleDraft().WriteTo(_modsDirectory);

        foreach (string language in ModDraft.KnownLanguages)
        {
            string path = Path.Combine(directory, "lang", $"{language}.json");
            Assert.True(File.Exists(path), $"chybí jazyk {language}");
            Assert.Contains("building.glassworks", File.ReadAllText(path), StringComparison.Ordinal);
        }
    }

    [Fact]
    public void AModThatChangesNothingWritesNoDataFiles()
    {
        // Prázdný resources.json by přebil základní hru prázdným seznamem
        // a smazal všechny suroviny.
        string directory = new ModDraft("empty_mod", "Prázdný").WriteTo(_modsDirectory);

        Assert.True(File.Exists(Path.Combine(directory, "mod.json")));
        Assert.False(File.Exists(Path.Combine(directory, "resources.json")));
        Assert.False(File.Exists(Path.Combine(directory, "buildings.json")));
    }

    [Fact]
    public void ABuildingThatReferencesAMissingResourceIsCaught()
    {
        // Přesně ta chyba, kterou hráč v editoru udělá nejsnáz: překlep v ID
        // suroviny. Musí ji vidět hned, ne až mu příště nenaběhne hra.
        var draft = new ModDraft("broken_mod", "Rozbitý");
        draft.Buildings.Add(new BuildingDraft(
            "ghost_works",
            "Duchárna",
            BuildCost: new[] { new AmountDraft("neexistujici_surovina", 5) }));

        var check = ModValidator.Check(TestData.RealDataDirectory, draft.WriteTo(_modsDirectory));

        Assert.False(check.Ok);
        Assert.Contains("neexistujici_surovina", check.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void CheckingAMissingFolderSaysSoInsteadOfThrowing()
    {
        var check = ModValidator.Check(TestData.RealDataDirectory, Path.Combine(_modsDirectory, "nic"));

        Assert.False(check.Ok);
        Assert.Contains("neexistuje", check.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void InspectorReportsABrokenModInsteadOfThrowing()
    {
        // Správce modů musí ukázat i to, co je rozbité — jinak hráč nemá kde
        // vadný mod vypnout.
        Directory.CreateDirectory(Path.Combine(_modsDirectory, "kaboom"));
        File.WriteAllText(Path.Combine(_modsDirectory, "kaboom", "mod.json"), "{ tohle není JSON");

        var found = ModInspector.Inspect(_modsDirectory);

        Assert.Single(found);
        Assert.Equal(ModStatus.Broken, found[0].Status);
        Assert.NotEmpty(found[0].Problem);
    }

    [Fact]
    public void TogglingAModRewritesOnlyTheEnabledFlag()
    {
        string directory = SampleDraft().WriteTo(_modsDirectory);
        var mod = ModInspector.Inspect(_modsDirectory).Single();
        Assert.Equal(ModStatus.Enabled, mod.Status);

        Assert.True(ModInspector.SetEnabled(mod, false));

        var after = ModInspector.Inspect(_modsDirectory).Single();
        Assert.Equal(ModStatus.Disabled, after.Status);

        // Ostatní pole patří autorovi modu a nesmí se ztratit.
        Assert.Equal("Testovací mod", after.Name);
        Assert.Equal("1.0.0", after.Version);
        _ = directory;
    }
}
