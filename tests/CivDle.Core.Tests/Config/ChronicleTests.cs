using CivDle.Core.Config;
using Xunit;

namespace CivDle.Core.Tests.Config;

/// <summary>
/// Kronika je síň slávy napříč hrami: rekordy se smí jen zlepšovat a sbírka
/// krajů se nesmí opakovat. Slabší hra do ní nikdy nesmí zasáhnout.
/// </summary>
public class ChronicleTests
{
    [Fact]
    public void FirstGame_SetsAllRecords()
    {
        var profile = new PlayerProfile();

        Assert.True(profile.RecordBest(population: 120, buildings: 30, ascension: 1));
        Assert.Equal(120, profile.BestPopulation);
        Assert.Equal(30, profile.BestBuildings);
        Assert.Equal(1, profile.BestAscension);
    }

    [Fact]
    public void WorseGame_LeavesRecordsAlone()
    {
        var profile = new PlayerProfile();
        profile.RecordBest(500, 90, 3);

        Assert.False(profile.RecordBest(population: 40, buildings: 5, ascension: 0));
        Assert.Equal(500, profile.BestPopulation);
        Assert.Equal(90, profile.BestBuildings);
        Assert.Equal(3, profile.BestAscension);
    }

    [Fact]
    public void PartialImprovement_MovesOnlyWhatImproved()
    {
        var profile = new PlayerProfile();
        profile.RecordBest(500, 90, 3);

        Assert.True(profile.RecordBest(population: 600, buildings: 10, ascension: 1));
        Assert.Equal(600, profile.BestPopulation);
        Assert.Equal(90, profile.BestBuildings);
        Assert.Equal(3, profile.BestAscension);
    }

    [Fact]
    public void Biomes_AreCollectedOnce()
    {
        var profile = new PlayerProfile();

        Assert.True(profile.RecordBiome("tundra"));
        Assert.False(profile.RecordBiome("tundra"));
        Assert.True(profile.RecordBiome("volcanic"));

        Assert.Equal(new[] { "tundra", "volcanic" }, profile.SettledBiomes);
    }

    [Fact]
    public void Roundtrip_KeepsTheChronicle()
    {
        string path = Path.Combine(Path.GetTempPath(), $"civdle-chronicle-{Guid.NewGuid():N}.json");
        try
        {
            var profile = new PlayerProfile { ChallengesCompleted = 7 };
            profile.RecordBest(1234, 56, 2);
            profile.RecordBiome("badlands");
            new ProfileStore(path).Save(profile);

            var loaded = new ProfileStore(path).Load();

            Assert.Equal(1234, loaded.BestPopulation);
            Assert.Equal(56, loaded.BestBuildings);
            Assert.Equal(2, loaded.BestAscension);
            Assert.Equal(7, loaded.ChallengesCompleted);
            Assert.Contains("badlands", loaded.SettledBiomes);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
