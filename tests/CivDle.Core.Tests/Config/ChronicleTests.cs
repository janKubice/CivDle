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
            profile.RecordRun(Run(1234, 56, 2, settlements: 4, eraOrder: 3, eraId: "iron",
                contracts: 21, wonders: 2, runSeconds: 4000));
            profile.AddPlaytime(3600);
            profile.RecordBiome("badlands");
            new ProfileStore(path).Save(profile);

            var loaded = new ProfileStore(path).Load();

            Assert.Equal(1234, loaded.BestPopulation);
            Assert.Equal(56, loaded.BestBuildings);
            Assert.Equal(2, loaded.BestAscension);
            Assert.Equal(7, loaded.ChallengesCompleted);
            Assert.Equal(4, loaded.BestSettlements);
            Assert.Equal("iron", loaded.BestEraId);
            Assert.Equal(21, loaded.BestContracts);
            Assert.Equal(2, loaded.BestWonders);
            Assert.Equal(4000, loaded.LongestRunSeconds);
            Assert.Equal(3600, loaded.TotalPlaySeconds);
            Assert.Contains("badlands", loaded.SettledBiomes);
        }
        finally
        {
            File.Delete(path);
        }
    }

    // ----- rozšířené rekordy -----

    private static RunRecord Run(
        double population = 0, int buildings = 0, int ascension = 0, int settlements = 0,
        int eraOrder = -1, string eraId = "", long contracts = 0, long wonders = 0,
        double runSeconds = 0) =>
        new(population, buildings, ascension, settlements, eraOrder, eraId, contracts, wonders, runSeconds);

    [Fact]
    public void AWholeRunLandsInTheChronicleAtOnce()
    {
        var profile = new PlayerProfile();

        Assert.True(profile.RecordRun(Run(900, 44, 1, settlements: 3, eraOrder: 2, eraId: "bronze",
            contracts: 12, wonders: 1, runSeconds: 5400)));

        Assert.Equal(3, profile.BestSettlements);
        Assert.Equal(2, profile.BestEraOrder);
        Assert.Equal("bronze", profile.BestEraId);
        Assert.Equal(12, profile.BestContracts);
        Assert.Equal(1, profile.BestWonders);
        Assert.Equal(5400, profile.LongestRunSeconds);
    }

    [Fact]
    public void AShorterRunNeverShrinksTheHallOfFame()
    {
        // Přesně tenhle případ nastane vždycky po Vzestupu: nový svět je malý
        // a kdyby přepsal rekordy, kronika by se sama mazala.
        var profile = new PlayerProfile();
        profile.RecordRun(Run(900, 44, 3, settlements: 6, eraOrder: 4, eraId: "industrial",
            contracts: 30, wonders: 3, runSeconds: 9000));

        Assert.False(profile.RecordRun(Run(10, 2, 0, settlements: 1, eraOrder: 0, eraId: "stone",
            contracts: 0, wonders: 0, runSeconds: 30)));

        Assert.Equal(6, profile.BestSettlements);
        Assert.Equal("industrial", profile.BestEraId);
        Assert.Equal(30, profile.BestContracts);
        Assert.Equal(9000, profile.LongestRunSeconds);
    }

    [Fact]
    public void TheEraNameTravelsWithItsOrder()
    {
        // Jméno bez pořadí by se dalo přepsat starší érou s vyšším ID.
        var profile = new PlayerProfile();
        profile.RecordRun(Run(eraOrder: 5, eraId: "atomic"));

        profile.RecordRun(Run(eraOrder: 1, eraId: "bronze"));

        Assert.Equal(5, profile.BestEraOrder);
        Assert.Equal("atomic", profile.BestEraId);
    }

    [Fact]
    public void ARunWithoutAKnownEraLeavesTheRecordAlone()
    {
        var profile = new PlayerProfile();
        profile.RecordRun(Run(eraOrder: 2, eraId: "bronze"));

        profile.RecordRun(Run(eraOrder: 9, eraId: ""));

        Assert.Equal("bronze", profile.BestEraId);
        Assert.Equal(2, profile.BestEraOrder);
    }

    [Fact]
    public void PlaytimeAddsUpInsteadOfBeingOverwritten()
    {
        // Čas ve hře je jediný údaj kroniky, který se SČÍTÁ — proto se předává
        // přírůstek, ne celek.
        var profile = new PlayerProfile();

        profile.AddPlaytime(120);
        profile.AddPlaytime(180);
        profile.AddPlaytime(-50); // hodiny se nesmí vracet

        Assert.Equal(300, profile.TotalPlaySeconds);
    }
}
