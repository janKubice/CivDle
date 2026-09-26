using CivDle.Core.Content;
using CivDle.Core.Sim;
using CivDle.Core.Tests.Support;
using CivDle.Core.World;
using Xunit;

namespace CivDle.Core.Tests.Sim;

/// <summary>
/// Spokojenost, která se dá ovlivnit. Dřív byla prakticky konstantní: přelidnění
/// se trestalo od nuly (a populace vždycky doroste ke stropu, takže trvale −0,24)
/// a služby obsluhovaly celé město odkudkoli (guvernér je držel nad 75 % sám).
/// Teď přelidnění bere až nad prahem a služba obslouží jen domy ve svém dosahu.
/// </summary>
public class HappinessReachTests
{
    private const int Wood = 0;
    private const int House = 0;
    private const int Market = 1;

    private static HappinessConfig Config(int reach = 5, double threshold = 0.85) => new(
        IntervalTicks: 1, BaseHappiness: 0.5, ServiceWeight: 0.5, OvercrowdingPenalty: 0.25,
        PeoplePerServicePoint: 10, GrowthFloor: 0.2, FreePopulation: 0,
        CrowdingThreshold: threshold, ServiceReachTiles: reach);

    private static Simulation World(HappinessConfig config, double population, int woodForUpkeep = 1000)
    {
        var resources = new[] { new Resource("wood", new RgbColor(1, 1, 1), StartAmount: woodForUpkeep, BaseStorage: 100_000) };
        var house = TestContent.SimpleBuilding("house", 2, housing: 10) with { BuildCost = Array.Empty<ResourceAmount>() };
        var market = TestContent.Service("market", serviceValue: 100, upkeepResource: Wood, upkeepAmount: 1);
        var gameplay = TestContent.DefaultGameplay with
        {
            PopulationGrowthPerSecond = 0.0,
            FoodPerPersonPerSecond = 0.0,
            BaseHousingCapacity = 0,
            HappinessOrNull = config,
        };
        var content = TestContent.Build(resources: resources, buildings: new[] { house, market }, gameplay: gameplay);
        var sim = new Simulation(content, new UniformTerrain(1));
        sim.SetPopulationForTest(population);
        return sim;
    }

    [Fact]
    public void BelowTheThreshold_CrowdingCostsNothing()
    {
        var config = Config(threshold: 0.85);

        Assert.Equal(0.0, config.CrowdingPenalty(0.80), 6);
        Assert.Equal(0.0, config.CrowdingPenalty(0.85), 6);
        Assert.True(config.CrowdingPenalty(0.95) > 0);
        Assert.Equal(0.25, config.CrowdingPenalty(1.0), 6);
    }

    [Fact]
    public void WithoutAThreshold_ItBehavesAsBefore()
    {
        // Starší obsah bez prahu: trest od nuly, jako dřív.
        var config = Config(threshold: 0.0);

        Assert.Equal(0.125, config.CrowdingPenalty(0.5), 6);
    }

    [Fact]
    public void AMarketServesOnlyTheStreetsItReaches()
    {
        // Trh na druhém konci města tvým ulicím nepomůže.
        var sim = World(Config(reach: 5), population: 20);
        sim.TryPlaceBuildingFree(House, 0, 0);
        sim.TryPlaceBuildingFree(House, 40, 0);
        sim.TryPlaceBuildingFree(Market, 2, 0);

        sim.Tick();

        Assert.Equal(0.5, sim.HappinessParts.ServiceCoverage, 3);
    }

    [Fact]
    public void AMarketInTheMiddleServesEveryone()
    {
        var sim = World(Config(reach: 5), population: 20);
        sim.TryPlaceBuildingFree(House, 0, 0);
        sim.TryPlaceBuildingFree(House, 4, 0);
        sim.TryPlaceBuildingFree(Market, 2, 0);

        sim.Tick();

        Assert.Equal(1.0, sim.HappinessParts.ServiceCoverage, 3);
    }

    [Fact]
    public void UnpaidUpkeepShowsAsReachWithoutCoverage()
    {
        // Rozdíl „chybí služba" proti „chybí surovina na provoz" — guvernér
        // podle něj ví, jestli stavět další trh, nebo shánět dřevo.
        var sim = World(Config(reach: 5), population: 20, woodForUpkeep: 0);
        sim.TryPlaceBuildingFree(House, 0, 0);
        sim.TryPlaceBuildingFree(House, 4, 0);
        sim.TryPlaceBuildingFree(Market, 2, 0);

        sim.Tick();

        var parts = sim.HappinessParts;
        Assert.Equal(0.0, parts.ServiceCoverage, 3);
        Assert.Equal(1.0, parts.ServiceReach, 3);
    }

    [Fact]
    public void TheBreakdownIsTheOneTheGameUsed()
    {
        // Dřív se rozpad počítal znovu při dotazu — po strhnutí údržby — a tvrdil
        // „služby 0 %" vedle čísla, které počítalo s plnými službami.
        var sim = World(Config(reach: 5), population: 20, woodForUpkeep: 1);
        sim.TryPlaceBuildingFree(House, 0, 0);
        sim.TryPlaceBuildingFree(House, 4, 0);
        sim.TryPlaceBuildingFree(Market, 2, 0);

        sim.Tick(); // zaplatí poslední dřevo — sklad je teď prázdný

        Assert.Equal(sim.Happiness, sim.HappinessParts.Total, 6);
        Assert.Equal(1.0, sim.HappinessParts.ServiceCoverage, 3);
    }

    /// <summary>Svět s guvernérem: trh smí stavět sám.</summary>
    private static Simulation GovernedWorld(int woodForUpkeep)
    {
        var resources = new[] { new Resource("wood", new RgbColor(1, 1, 1), StartAmount: woodForUpkeep, BaseStorage: 100_000) };
        var house = TestContent.SimpleBuilding("house", 2, housing: 10) with { BuildCost = Array.Empty<ResourceAmount>() };
        var market = TestContent.Service("market", serviceValue: 2, upkeepResource: Wood, upkeepAmount: 1) with { AutoBuild = true };
        var gameplay = TestContent.DefaultGameplay with
        {
            PopulationGrowthPerSecond = 0.0,
            FoodPerPersonPerSecond = 0.0,
            BaseHousingCapacity = 0,
            HappinessOrNull = Config(reach: 5, threshold: 0.0) with { FreePopulation = 0 },
            AutoBuild = new AutoBuildConfig(IntervalTicks: 1, SearchRadius: 6, PopulationHeadroom: 0),
        };
        var content = TestContent.Build(resources: resources, buildings: new[] { house, market }, gameplay: gameplay);
        var sim = new Simulation(content, new UniformTerrain(1));
        sim.SetPopulationForTest(20); // 2 domy × 10 = plno, ale bez potřeby dalšího bydlení (rezerva 0)
        return sim;
    }

    [Fact]
    public void TheGovernorPutsTheMarketWhereNobodyIsServed()
    {
        var sim = GovernedWorld(woodForUpkeep: 1000);
        sim.TryPlaceBuildingFree(House, 0, 0);
        sim.TryPlaceBuildingFree(House, 40, 0);
        sim.TryPlaceBuildingFree(Market, 2, 0); // obslouží jen první dům

        for (int i = 0; i < 5; i++)
        {
            sim.Tick();
        }

        Assert.Contains(sim.Buildings.ToArray(),
            b => b.DefIndex == Market && Math.Abs(b.X - 40) <= 5 && Math.Abs(b.Y) <= 5);
    }

    [Fact]
    public void WhenOnlyUpkeepIsMissing_NoMoreMarketsAreBuilt()
    {
        // Knihovna za knihovnou, které pak stály bez údržby taky — přesně tohle
        // dřív guvernér dělal, když chybělo jídlo na provoz.
        var sim = GovernedWorld(woodForUpkeep: 0);
        sim.TryPlaceBuildingFree(House, 0, 0);
        sim.TryPlaceBuildingFree(House, 2, 0);
        sim.TryPlaceBuildingFree(Market, 1, 1);

        for (int i = 0; i < 20; i++)
        {
            sim.Tick();
        }

        Assert.Equal(1, sim.Buildings.ToArray().Count(b => b.DefIndex == Market));
    }
}
