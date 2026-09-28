using CivDle.Core.Config;
using Xunit;

namespace CivDle.Core.Tests.Config;

/// <summary>
/// Galaktická sbírka v profilu (Muzeum světů, svety-design.md 2.6): jen
/// přibývá — horší hra ani Nová hra+ ji nezmenší — a přežije uložení.
/// </summary>
public class WorldCollectionTests
{
    [Fact]
    public void TheFirstVisitCreatesTheWorldsCollection()
    {
        var profile = new PlayerProfile();

        bool changed = profile.RecordWorld("dune", new[] { "dune_star_settled" }, new[] { "camel" }, 5200, 1);

        Assert.True(changed);
        var dune = profile.Galaxy["dune"];
        Assert.Equal(new[] { "dune_star_settled" }, dune.Stars);
        Assert.Equal(new[] { "camel" }, dune.Fauna);
        Assert.Equal(5200, dune.PeakPopulation);
        Assert.Equal(1, dune.Wonders);
    }

    [Fact]
    public void AWorseRunNeverShrinksTheCollection()
    {
        var profile = new PlayerProfile();
        profile.RecordWorld("frost", new[] { "a", "b" }, new[] { "reindeer", "owl" }, 9000, 2);

        bool changed = profile.RecordWorld("frost", new[] { "a" }, Array.Empty<string>(), 300, 0);

        Assert.False(changed); // nic nového — profil se nemusí ukládat
        var frost = profile.Galaxy["frost"];
        Assert.Equal(2, frost.Stars.Count);
        Assert.Equal(2, frost.Fauna.Count);
        Assert.Equal(9000, frost.PeakPopulation);
        Assert.Equal(2, frost.Wonders);
    }

    [Fact]
    public void OnlyWhatIsNewIsAdded()
    {
        var profile = new PlayerProfile();
        profile.RecordWorld("xeno", new[] { "a" }, new[] { "firefly" }, 100, 0);

        Assert.True(profile.RecordWorld("xeno", new[] { "a", "b" }, new[] { "firefly" }, 50, 0));

        Assert.Equal(new[] { "a", "b" }, profile.Galaxy["xeno"].Stars);
        Assert.Equal(new[] { "firefly" }, profile.Galaxy["xeno"].Fauna);
        Assert.Equal(100, profile.Galaxy["xeno"].PeakPopulation);
    }

    [Fact]
    public void TheCollectionSurvivesASave()
    {
        string path = Path.Combine(Path.GetTempPath(), $"civdle-museum-{Guid.NewGuid():N}.json");
        try
        {
            var profile = new PlayerProfile();
            profile.RecordWorld("archipelago", new[] { "arch_star_settled" }, new[] { "gull" }, 7000, 1);
            profile.RecordWorld("home", Array.Empty<string>(), new[] { "deer" }, 2_000_000, 9);
            new ProfileStore(path).Save(profile);

            var loaded = new ProfileStore(path).Load();

            Assert.Equal(2, loaded.Galaxy.Count);
            Assert.Equal(new[] { "arch_star_settled" }, loaded.Galaxy["archipelago"].Stars);
            Assert.Equal(2_000_000, loaded.Galaxy["home"].PeakPopulation);
            Assert.Equal(9, loaded.Galaxy["home"].Wonders);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
