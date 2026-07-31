using CivDle.Core.Content;
using CivDle.Core.Sim;
using CivDle.Core.Tests.Support;
using CivDle.Core.World;
using Xunit;

namespace CivDle.Core.Tests.Sim;

/// <summary>
/// Čtvrti: shluk budov stejného ražení se sám pozná, dostane synergii — a u
/// průmyslu i stinnou stránku.
///
/// <para>Testuje se hlavně to, co z toho dělá rozhodnutí, a ne jen odměnu:
/// bonus roste s velikostí, ale má strop; průmyslová čtvrť víc dýmá; a když se
/// shluk rozpadne, bonus zmizí s ním.</para>
/// </summary>
public class DistrictTests
{
    private const int Wood = 0;

    private static DistrictCatalog Catalog(double pollutionMult = 1.5, int minBuildings = 3)
    {
        var types = new[]
        {
            new DistrictTypeDef(
                "industrial", new[] { "production" }, minBuildings, ClusterDistance: 2,
                SynergyPerBuilding: 0.10, SynergyMax: 0.30,
                PollutionMult: pollutionMult, MapColor: new RgbColor(140, 106, 74)),
        };

        return new DistrictCatalog(new DefRegistry<DistrictTypeDef>(types, d => d.Id, "druh čtvrti"));
    }

    private static Simulation NewSim(DistrictCatalog? catalog = null)
    {
        var biomes = new[] { TestContent.WaterBiome(), TestContent.LandBiome("plain") };
        var resources = new[]
        {
            new Resource("wood", new RgbColor(1, 1, 1), StartAmount: 100, BaseStorage: 1_000_000),
        };

        var mill = new BuildingDef(
            "mill", "production", new RgbColor(120, 160, 90), 1, 1,
            WorkerSlots: 1, HousingCapacity: 0,
            BuildCost: Array.Empty<ResourceAmount>(),
            Recipe: new Recipe(
                Array.Empty<ResourceAmount>(), new[] { new ResourceAmount(Wood, 10) }, TimeTicks: 1),
            AllowedBiomes: new[] { false, true },
            StorageBonus: Array.Empty<ResourceAmount>(),
            AutoBuild: false, Buildable: true, UpgradesToIndex: -1,
            UpgradeCost: Array.Empty<ResourceAmount>(), PowerSupply: 0, PowerDemand: 0,
            PollutionOrNull: new PollutionOutput(Air: 2.0, Water: 0, Soil: 0));

        var hut = mill with { Id = "hut", Category = "housing", Recipe = null, PollutionOrNull = null };

        var gameplay = TestContent.DefaultGameplay with
        {
            FoodPerPersonPerSecond = 0,
            PopulationGrowthPerSecond = 0,
            PollutionOrNull = new PollutionConfig(
                IntervalTicks: 10, SpreadRate: 0.0, DecayRate: 0.0,
                FullEffectAt: 10_000, HappinessPenalty: 0, ProductionPenalty: 0),
        };

        var content = TestContent.Build(
            biomes, 1, resources, new[] { mill, hut }, gameplay, districts: catalog ?? Catalog());
        return new Simulation(content, new UniformTerrain(1));
    }

    private const int Mill = 0;
    private const int Hut = 1;

    private static void Tick(Simulation sim, int ticks)
    {
        for (int i = 0; i < ticks; i++)
        {
            sim.Tick();
        }
    }

    /// <summary>Postaví řadu budov vedle sebe, takže tvoří jeden shluk.</summary>
    private static void BuildRow(Simulation sim, int defIndex, int count, int y = 10)
    {
        for (int i = 0; i < count; i++)
        {
            Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuilding(defIndex, 10 + i, y));
        }
    }

    [Fact]
    public void ScatteredBuildingsAreNotAQuarter()
    {
        // Čtvrť má vzniknout ze shluku, ne z počtu. Tři pily po celé mapě
        // nejsou průmyslová čtvrť.
        var sim = NewSim();
        sim.TryPlaceBuilding(Mill, 0, 0);
        sim.TryPlaceBuilding(Mill, 60, 0);
        sim.TryPlaceBuilding(Mill, 0, 60);

        Tick(sim, 120);

        Assert.Empty(sim.Districts);
    }

    [Fact]
    public void AClusterRecognisesItself()
    {
        var sim = NewSim();
        BuildRow(sim, Mill, 4);

        Tick(sim, 120);

        var district = Assert.Single(sim.Districts);
        Assert.Equal(4, district.BuildingCount);
        Assert.Equal(0, district.TypeIndex);
    }

    [Fact]
    public void TooFewBuildingsStayAnonymous()
    {
        var sim = NewSim(Catalog(minBuildings: 5));
        BuildRow(sim, Mill, 4);

        Tick(sim, 120);

        Assert.Empty(sim.Districts);
    }

    [Fact]
    public void OnlyMatchingCategoriesCount()
    {
        // Domy do průmyslové čtvrti nepatří, i když stojí uprostřed ní.
        var sim = NewSim();
        BuildRow(sim, Hut, 6, y: 20);

        Tick(sim, 120);

        Assert.Empty(sim.Districts);
    }

    [Fact]
    public void MembersProduceMoreThanALonelyMill()
    {
        var sim = NewSim();
        BuildRow(sim, Mill, 4);
        Tick(sim, 120);

        // 4 budovy → bonus (4−1) × 0.10 = 0.30, což je zrovna strop.
        Assert.Equal(1.30f, sim.Buildings[0].DistrictMult, 3);
        Assert.True(sim.DistrictOf(0).HasValue);
    }

    [Fact]
    public void TheBonusHasACeiling()
    {
        // Bez stropu by se vyplatilo stavět jediný obří blok a nic jiného.
        var sim = NewSim();
        BuildRow(sim, Mill, 12);

        Tick(sim, 120);

        Assert.Equal(1.30f, sim.Buildings[0].DistrictMult, 3); // ne 1 + 11 × 0.10
    }

    [Fact]
    public void ConcentratedIndustryFoulsMore()
    {
        // Tohle je ta druhá strana synergie — bonus není zadarmo.
        var clustered = NewSim();
        BuildRow(clustered, Mill, 4);
        Tick(clustered, 200);
        double inQuarter = clustered.PollutionMap.Peak(PollutionKind.Air);

        var scattered = NewSim(Catalog(minBuildings: 50)); // čtvrť nikdy nevznikne
        BuildRow(scattered, Mill, 4);
        Tick(scattered, 200);
        double alone = scattered.PollutionMap.Peak(PollutionKind.Air);

        Assert.True(inQuarter > alone);
    }

    [Fact]
    public void ABrokenUpClusterLosesItsBonus()
    {
        // Bonus nesmí zůstat viset po budově, která ze čtvrti vypadla.
        var sim = NewSim();
        BuildRow(sim, Mill, 4);
        Tick(sim, 120);
        Assert.True(sim.Buildings[0].DistrictMult > 1f);

        // Zbourat tolik, aby zbyly dvě — pod prahem tří.
        Assert.Equal(PlacementResult.Ok, sim.TryDemolish(3));
        Assert.Equal(PlacementResult.Ok, sim.TryDemolish(2));
        Tick(sim, 120);

        Assert.Empty(sim.Districts);
        Assert.Equal(1f, sim.Buildings[0].DistrictMult);
        Assert.Null(sim.DistrictOf(0));
    }

    [Fact]
    public void EmptyCatalogLeavesTheGameAsItWas()
    {
        var sim = NewSim(DistrictCatalog.Empty);
        BuildRow(sim, Mill, 6);

        Tick(sim, 120);

        Assert.Empty(sim.Districts);
        Assert.Equal(1f, sim.Buildings[0].DistrictMult);
    }
}
