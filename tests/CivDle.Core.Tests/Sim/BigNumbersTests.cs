using CivDle.Core.Content;
using CivDle.Core.Sim;
using CivDle.Core.Tests.Support;
using CivDle.Core.World;
using Xunit;

namespace CivDle.Core.Tests.Sim;

/// <summary>
/// Velká čísla v pozdní hře.
///
/// <para>Tady byla chyba, kterou hráč viděl jako <b>populaci −2,15 miliardy</b>:
/// kapacita bydlení se držela v <c>int</c>, jenže násobič z Vzestupu se skládá
/// násobně přes desítky úrovní. Jediný dům pak přidal víc, než se do <c>int</c>
/// vejde, kapacita přetekla do záporu — a protože je stropem růstu populace,
/// stáhla populaci s sebou.</para>
///
/// <para>Hra je o aglomeracích s miliony+ obyvatel (CLAUDE.md), takže žádné
/// počítadlo, které roste s městem, nesmí být 32bitové.</para>
/// </summary>
public class BigNumbersTests
{
    private static readonly Resource[] Planks =
    {
        new("planks", new RgbColor(200, 170, 110), StartAmount: 1_000_000, BaseStorage: 10_000_000),
    };

    private static readonly PrestigeConfig EarlyAscension =
        new(new GoalCondition(MetricKind.Population, -1, 5), MetricKind.Population, -1, 5);

    /// <summary>Dům s velkou kapacitou a opakovatelný upgrade bydlení (jako v datech).</summary>
    private static GameContent Content()
    {
        var house = new BuildingDef(
            "house", "housing", new RgbColor(180, 100, 60), 1, 1,
            WorkerSlots: 0, HousingCapacity: 10_000,
            BuildCost: new[] { new ResourceAmount(0, 1) },
            Recipe: null,
            AllowedBiomes: new[] { false, true },
            StorageBonus: Array.Empty<ResourceAmount>(),
            AutoBuild: false, Buildable: true,
            UpgradesToIndex: -1, UpgradeCost: Array.Empty<ResourceAmount>(),
            PowerSupply: 0, PowerDemand: 0);

        var spacious = new PrestigeUpgradeDef(
            "spacious", "housing_mult", 0.4, 1, Array.Empty<int>(), MaxLevel: 80, CostGrowth: 1.0);

        return TestContent.Build(
            biomes: new[] { TestContent.WaterBiome(), TestContent.LandBiome("grass") },
            resources: Planks,
            buildings: new[] { house },
            prestige: EarlyAscension,
            prestigeUpgrades: new[] { spacious });
    }

    /// <summary>Simulace s vykoupeným bydlením do maxima.</summary>
    private static Simulation Maxed(out GameContent content)
    {
        content = Content();
        var sim = new Simulation(content, new UniformTerrain((byte)1));
        sim.DebugGrantPrestigePoints(1000);
        while (sim.TryBuyUpgrade(0) == PlacementResult.Ok)
        {
            // koupit všechny úrovně
        }

        Assert.True(sim.IsUpgradeMaxed(0), "test má smysl jen s vymaxovaným upgradem");
        return sim;
    }

    [Fact]
    public void MaxedHousingUpgradesDoNotOverflowTheCapacity()
    {
        var sim = Maxed(out _);
        double before = sim.HousingCapacity;

        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuilding(0, 5, 5));

        Assert.True(sim.HousingCapacity > before,
            $"kapacita po postavení domu klesla z {before} na {sim.HousingCapacity}");
        Assert.True(sim.HousingCapacity > int.MaxValue,
            $"kapacita {sim.HousingCapacity} se vešla do int — přetečení číhá dál");
    }

    [Fact]
    public void PopulationNeverGoesNegative()
    {
        // Vlastní příznak chyby, jak ho hráč viděl: záporná populace.
        var sim = Maxed(out _);
        sim.TryPlaceBuilding(0, 5, 5);

        for (int i = 0; i < 200; i++)
        {
            sim.Tick();
        }

        Assert.True(sim.Population >= 0, $"populace spadla na {sim.Population}");
    }

    [Fact]
    public void CapacitySurvivesARecompute()
    {
        // Přepočet z nuly (po koupi upgradu, Vzestupu, načtení savu) musí dát
        // totéž co postupné přičítání — jinak se chyba vrátí jinými dveřmi.
        var sim = Maxed(out _);
        sim.TryPlaceBuilding(0, 5, 5);
        double afterPlacing = sim.HousingCapacity;

        sim.RecomputeDerivedStateForTests();

        Assert.Equal(afterPlacing, sim.HousingCapacity, 3);
    }

    [Fact]
    public void DemolishingTakesBackExactlyWhatItGave()
    {
        var sim = Maxed(out _);
        double before = sim.HousingCapacity;
        sim.TryPlaceBuilding(0, 5, 5);

        Assert.Equal(PlacementResult.Ok, sim.TryDemolish(0));

        Assert.Equal(before, sim.HousingCapacity, 3);
    }

    [Fact]
    public void MetricStaysReadableForGoals()
    {
        // Cíle a achievementy čtou metriku jako long — nesmí z ní vypadnout
        // záporné číslo, i když je kapacita větší než long.
        var sim = Maxed(out _);
        sim.TryPlaceBuilding(0, 5, 5);

        Assert.True(sim.EvaluateMetric(MetricKind.HousingCapacity, -1) > 0);
    }
}
