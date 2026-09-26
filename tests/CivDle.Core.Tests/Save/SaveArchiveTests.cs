using CivDle.Core.Content;
using CivDle.Core.Save;
using CivDle.Core.Sim;
using CivDle.Core.Tests.Support;
using Xunit;

namespace CivDle.Core.Tests.Save;

/// <summary>
/// Nová hra+ a archiv měst (endgame.md, C3).
///
/// <para>To podstatné: Nová hra+ nesmí sáhnout na město první kapitoly.
/// Hlavní slot savu je jeden, takže se město napřed zkopíruje do archivu —
/// a výměna z archivu musí zase sama archivovat to, co se hrálo.</para>
/// </summary>
public sealed class SaveArchiveTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "civdle-archive-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }

    [Fact]
    public void ArchivingCopiesTheCityAndLeavesTheSaveAlone()
    {
        var content = TestData.LoadRealContent();
        var store = new SaveStore(Path.Combine(_dir, "save.civdle"));
        var city = ScenarioWorld.CreateFree(content, 7, content.WorldGen.DefaultPresetIndex, WorldRules.None);
        Assert.True(store.TrySave(city, Metadata(content, 7)));
        byte[] before = File.ReadAllBytes(Path.Combine(_dir, "save.civdle"));

        string? archived = store.TryArchive("Řeka 1", DateTime.UtcNow);

        Assert.NotNull(archived);
        Assert.Equal(before, File.ReadAllBytes(Path.Combine(_dir, "save.civdle")));
        Assert.Equal(before, File.ReadAllBytes(archived));
        Assert.Single(store.ArchivedFiles());
    }

    [Fact]
    public void RestoringArchivesWhatWasPlayedAndNothingIsLost()
    {
        var content = TestData.LoadRealContent();
        var store = new SaveStore(Path.Combine(_dir, "save.civdle"));
        var first = ScenarioWorld.CreateFree(content, 1, content.WorldGen.DefaultPresetIndex, WorldRules.None);
        store.TrySave(first, Metadata(content, 1));
        string archived = store.TryArchive("prvni", new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc))!;

        var second = ScenarioWorld.CreateFree(content, 2, content.WorldGen.DefaultPresetIndex, WorldRules.None);
        store.TrySave(second, Metadata(content, 2));

        Assert.True(store.TryRestoreFromArchive(archived, new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc)));

        Assert.Equal(1, store.TryLoad(content, out _)!.Metadata.Seed);
        Assert.Equal(2, store.ArchivedFiles().Count); // první i to, co se hrálo předtím
    }

    [Fact]
    public void ANewGamePlusWorldKeepsItsRulesThroughASave()
    {
        var content = TestData.LoadRealContent();
        var rules = new WorldRules(new[] { ScenarioRule.FloodedWorld, ScenarioRule.NoRoads });
        var world = ScenarioWorld.CreateFree(content, 1953, content.WorldGen.DefaultPresetIndex, rules);
        Assert.False(world.RoadsAllowed);
        Assert.False(world.InScenario);

        var serializer = new SaveGameSerializer();
        using var stream = new MemoryStream();
        serializer.Write(stream, world, Metadata(content, 1953));
        stream.Position = 0;
        var (loaded, _) = serializer.Read(stream, content);

        Assert.True(loaded.WorldRules.Has(ScenarioRule.FloodedWorld));
        Assert.False(loaded.RoadsAllowed);
        for (int y = -80; y <= 80; y += 10)
        {
            for (int x = -80; x <= 80; x += 10)
            {
                Assert.Equal(world.BiomeAt(x, y), loaded.BiomeAt(x, y));
            }
        }
    }

    [Fact]
    public void NewGamePlusNeverOffersAWorldWithoutAscension()
    {
        // Volná hra bez Vzestupu by se na prvním měřítku zasekla navždy.
        Assert.DoesNotContain(ScenarioRule.NoAscension, WorldRules.NewGamePlusChoices);
    }

    private static SaveMetadata Metadata(GameContent content, long seed) => new(
        seed, content.WorldGen.Sizes[content.WorldGen.DefaultSizeIndex].Id,
        content.WorldGen.Presets[content.WorldGen.DefaultPresetIndex].Id, DateTime.UtcNow);
}
