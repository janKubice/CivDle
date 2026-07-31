using CivDle.Core.Content;
using CivDle.Core.Sim;
using CivDle.Core.Tests.Support;
using CivDle.Core.World;
using Xunit;

namespace CivDle.Core.Tests.Sim;

/// <summary>
/// Prosperita místa: číslo, ze kterého render dělá obraz. Skládá se z toho, jak
/// se ve městě žije (spokojenost), a z toho, jak to vypadá pod nohama (zamoření).
///
/// <para>Testuje se hlavně to, na čem stojí čitelnost: musí být <b>místní</b>
/// (jinak by celé město vypadalo stejně a smog nad hutěmi by nikdo nespojil
/// s hutěmi) a vypnuté vrstvy nesmí nic pokazit.</para>
/// </summary>
public class ProsperityTests
{
    private const int Mill = 0;

    private static GameContent Content(bool pollution = true, bool happiness = true, bool serviceStarved = false)
    {
        var biomes = new[] { TestContent.WaterBiome(), TestContent.LandBiome("plain") };
        var resources = new[]
        {
            new Resource("wood", new RgbColor(1, 1, 1), StartAmount: 1000, BaseStorage: 1_000_000),
        };

        var mill = new BuildingDef(
            "mill", "production", new RgbColor(120, 160, 90), 1, 1,
            WorkerSlots: 0, HousingCapacity: 0,
            BuildCost: Array.Empty<ResourceAmount>(),
            Recipe: new Recipe(
                Array.Empty<ResourceAmount>(), new[] { new ResourceAmount(0, 1) }, TimeTicks: 1),
            AllowedBiomes: new[] { false, true },
            StorageBonus: Array.Empty<ResourceAmount>(),
            AutoBuild: false, Buildable: true, UpgradesToIndex: -1,
            UpgradeCost: Array.Empty<ResourceAmount>(), PowerSupply: 0, PowerDemand: 0,
            PollutionOrNull: pollution ? new PollutionOutput(Air: 40.0, Water: 0, Soil: 20.0) : null);

        var gameplay = TestContent.DefaultGameplay with
        {
            FoodPerPersonPerSecond = 0,
            PopulationGrowthPerSecond = 0,
            PollutionOrNull = pollution
                ? new PollutionConfig(
                    IntervalTicks: 5, SpreadRate: 0.0, DecayRate: 0.0,
                    FullEffectAt: 60, HappinessPenalty: 0.0, ProductionPenalty: 0.0)
                : null,
            // Spokojenost se dolů dostane přes skutečnou mechaniku (neobsloužení
            // lidé), ne nastavením zvenčí — test tak měří to, co hráč zažije.
            HappinessOrNull = happiness
                ? new HappinessConfig(
                    IntervalTicks: 10,
                    BaseHappiness: serviceStarved ? 0.4 : 1.0,
                    ServiceWeight: serviceStarved ? 0.6 : 0.0,
                    OvercrowdingPenalty: 0.0, PeoplePerServicePoint: 1.0, GrowthFloor: 0.2)
                : null,
        };

        return TestContent.Build(biomes, 1, resources, new[] { mill }, gameplay);
    }

    private static Simulation NewSim(bool pollution = true, bool happiness = true, bool serviceStarved = false) =>
        new(Content(pollution, happiness, serviceStarved), new UniformTerrain(1));

    private static void Tick(Simulation sim, int ticks)
    {
        for (int i = 0; i < ticks; i++)
        {
            sim.Tick();
        }
    }

    [Fact]
    public void ACleanHappyPlaceIsFullyProsperous()
    {
        var sim = NewSim();

        Assert.Equal(1.0, sim.ProsperityAt(0, 0), 3);
    }

    [Fact]
    public void SmogDragsTheNeighbourhoodDown()
    {
        var sim = NewSim();
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuilding(Mill, 0, 0));

        Tick(sim, 200);

        Assert.True(sim.ProsperityAt(0, 0) < 1.0, "Zakouřené místo se nemá tvářit jako kvetoucí čtvrť.");
    }

    [Fact]
    public void ProsperityIsLocalNotGlobal()
    {
        // Tohle je celý smysl místní složky: čisté předměstí smí kvést, i když
        // nad hutěmi visí smog. Jinak by hráč neměl důvod továrny odsouvat.
        var sim = NewSim();
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuilding(Mill, 0, 0));

        Tick(sim, 200);

        double atTheFactory = sim.ProsperityAt(0, 0);
        double farAway = sim.ProsperityAt(400, 400);

        Assert.True(farAway > atTheFactory,
            $"Vzdálené předměstí ({farAway:F2}) nemá být na tom hůř než u hutí ({atTheFactory:F2}).");
        Assert.Equal(1.0, farAway, 3);
    }

    [Fact]
    public void UnhappyCityLooksWorseEverywhere()
    {
        // Spokojenost je globální — bída se pozná i na čistém kraji města,
        // daleko od jediné budovy.
        var sim = NewSim(serviceStarved: true);
        double before = sim.ProsperityAt(50, 50);

        Tick(sim, 60); // neobsloužení lidé → spokojenost padá

        Assert.True(sim.Happiness < 1.0, "Test nic neměří, dokud spokojenost neklesne.");
        Assert.True(sim.ProsperityAt(50, 50) < before);
        Assert.Equal(sim.Happiness, sim.ProsperityAt(50, 50), 3);
    }

    [Fact]
    public void ProsperityNeverLeavesTheZeroToOneRange()
    {
        // Obě tlaky naráz: nespokojené město i zamořená půda pod budovou.
        var sim = NewSim(serviceStarved: true);
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuilding(Mill, 0, 0));

        Tick(sim, 400);

        Assert.InRange(sim.ProsperityAt(0, 0), 0.0, 1.0);
        Assert.InRange(sim.ProsperityAt(999, 999), 0.0, 1.0);
    }

    [Fact]
    public void WithTheLayersOffTheCityJustLooksTidy()
    {
        // Starší data znečištění ani spokojenost neznají — nesmí kvůli tomu
        // vypadat jako slum.
        var sim = NewSim(pollution: false, happiness: false);
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuilding(Mill, 0, 0));

        Tick(sim, 200);

        Assert.Equal(1.0, sim.ProsperityAt(0, 0), 3);
    }
}
