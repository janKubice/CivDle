using CivDle.Core.Content;
using CivDle.Core.Sim;
using CivDle.Core.Tests.Support;
using CivDle.Core.World;
using Xunit;

namespace CivDle.Core.Tests.Sim;

/// <summary>
/// Tempo automatické výstavby a to, jak hezky se staví.
///
/// <para>Existuje kvůli dvěma konkrétním vadám. Za prvé: bonus
/// <c>autobuild_speed</c> se počítal do <see cref="PrestigeBonuses"/>, ale nikdo
/// ho nečetl — hráč si kupoval zrychlení, které nic nedělalo. Za druhé:
/// auto-stavba brala první volné políčko od kotvy, takže město rostlo jako
/// beztvará skvrna místo jako civilizace.</para>
/// </summary>
public class AutoBuildPaceTests
{
    private static PrestigeUpgradeDef Faster(double magnitude, int maxLevel = 20) =>
        new("master_builders", "autobuild_speed", magnitude, 1, Array.Empty<int>(), maxLevel, 1.0);

    /// <summary>Vzestup hned a štědře, ať je v testu za co kupovat úrovně.</summary>
    private static PrestigeConfig EarlyAscension =>
        new(new GoalCondition(MetricKind.Population, -1, 5), MetricKind.Population, -1, 1);

    /// <summary>
    /// Obsah, ve kterém město opravdu roste: krátký interval a chalupa, kterou
    /// guvernér smí stavět. Výchozí testovací obsah má auto-stavbu schválně
    /// na dlouhém intervalu, aby do ostatních testů nezasahovala.
    /// </summary>
    private static GameContent GrowingContent()
    {
        var biomes = new[] { TestContent.WaterBiome(), TestContent.LandBiome("grass") };
        var resources = new[] { new Resource("wood", new RgbColor(140, 90, 40), StartAmount: 5000, BaseStorage: 20000) };

        // Chalupa s bydlením a značkou autoBuild: lidí je od začátku víc než
        // střech, takže guvernér má důvod stavět a test má co měřit.
        var hut = new BuildingDef(
            "hut", "housing", new RgbColor(200, 180, 120), 1, 1,
            WorkerSlots: 0, HousingCapacity: 2,
            BuildCost: new[] { new ResourceAmount(0, 1) },
            Recipe: null,
            AllowedBiomes: new[] { false, true },
            StorageBonus: Array.Empty<ResourceAmount>(),
            AutoBuild: true, Buildable: true, UpgradesToIndex: -1,
            UpgradeCost: Array.Empty<ResourceAmount>(),
            PowerSupply: 0, PowerDemand: 0);
        var gameplay = TestContent.DefaultGameplay with
        {
            AutoBuild = new AutoBuildConfig(IntervalTicks: 2, SearchRadius: 6, PopulationHeadroom: 2),
            PopulationGrowthPerSecond = 0,
            FoodPerPersonPerSecond = 0,
            StartingPopulation = 40,
            BaseHousingCapacity = 4,
        };

        return TestContent.Build(biomes, 1, resources, new[] { hut }, gameplay);
    }

    /// <summary>Obsah s realistickým intervalem (jako ostrá data) — na měření tempa.</summary>
    private static GameContent PaceContent(params PrestigeUpgradeDef[] upgrades)
    {
        var gameplay = TestContent.DefaultGameplay with
        {
            AutoBuild = new AutoBuildConfig(IntervalTicks: 60, SearchRadius: 6, PopulationHeadroom: 2),
        };

        return TestContent.Build(gameplay: gameplay, prestige: EarlyAscension, prestigeUpgrades: upgrades);
    }

    private static Simulation NewSim(GameContent content) => new(content, new UniformTerrain(1));

    [Fact]
    public void WithoutUpgrades_ThePaceIsExactlyWhatTheDataSay()
    {
        var content = TestContent.Build();
        var sim = NewSim(content);

        Assert.Equal(content.Gameplay.AutoBuild.IntervalTicks, sim.AutoBuildInterval);
        Assert.Equal(sim.BuildsPerInterval, sim.AutoBuildBudget);
    }

    [Fact]
    public void BuyingSpeedShortensTheInterval()
    {
        // Tohle je ta odměna, kterou hráč po Vzestupu opravdu VIDÍ na mapě.
        var content = PaceContent(Faster(1.0));
        var sim = NewSim(content);

        int before = sim.AutoBuildInterval;
        Assert.Equal(PlacementResult.Ok, sim.TryAscend());
        Assert.Equal(PlacementResult.Ok, sim.TryBuyUpgrade(0)); // ×2

        Assert.Equal(before / 2, sim.AutoBuildInterval);
    }

    [Fact]
    public void OnceTheIntervalHitsItsFloorTheSpeedSpillsIntoTheBudget()
    {
        // Bez přetečení by se bonus nad určitou úroveň přestal projevovat
        // a další koupené úrovně by byly k ničemu.
        var content = PaceContent(Faster(1.0, maxLevel: 20));
        var sim = NewSim(content);

        Assert.Equal(PlacementResult.Ok, sim.TryAscend());
        for (int i = 0; i < 10 && sim.CanBuyUpgrade(0) == PlacementResult.Ok; i++)
        {
            sim.TryBuyUpgrade(0);
        }

        Assert.True(sim.UpgradeLevel(0) >= 5, "test potřebuje aspoň pět úrovní zrychlení");

        // Interval už nemůže níž, ale tempo musí dál růst — proto rozpočet.
        Assert.True(sim.AutoBuildBudget > 1, $"rozpočet měl přetéct nad 1, je {sim.AutoBuildBudget}");
    }

    [Fact]
    public void TheIntervalNeverDropsBelowTheFloor()
    {
        // Pod dnem by město „vybuchlo" místo aby rostlo — a růst, který není
        // vidět, není odměna.
        var content = PaceContent(Faster(3.0, maxLevel: 20));
        var sim = NewSim(content);

        Assert.Equal(PlacementResult.Ok, sim.TryAscend());
        for (int i = 0; i < 15 && sim.CanBuyUpgrade(0) == PlacementResult.Ok; i++)
        {
            sim.TryBuyUpgrade(0);
        }

        Assert.True(sim.AutoBuildInterval >= 1, "interval nesmí spadnout na nulu (dělení modulem)");
        Assert.True(
            sim.AutoBuildInterval < content.Gameplay.AutoBuild.IntervalTicks,
            "s koupeným zrychlením musí být interval kratší než základní");
    }

    [Fact]
    public void GrowthPrefersSpotsAlongRoads()
    {
        // Bez tohohle rostlo město jako skvrna. Domy u cest z toho dělají ulice.
        var content = GrowingContent();
        var sim = NewSim(content);

        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuilding(0, 20, 20));

        // Cesta vede na jednu stranu od kotvy; volno je na všech stranách stejně.
        for (int x = 21; x <= 26; x++)
        {
            sim.TryBuildRoad(x, 22);
        }

        // Odtikat dost dlouho, aby guvernér stihl několik staveb.
        for (int i = 0; i < 400; i++)
        {
            sim.Tick();
        }

        int nextToRoad = 0;
        int total = 0;
        foreach (var building in sim.Buildings)
        {
            if (building.X == 20 && building.Y == 20)
            {
                continue; // ruční kotva se nepočítá
            }

            total++;
            if (sim.HasRoadAt(building.X - 1, building.Y) || sim.HasRoadAt(building.X + 1, building.Y)
                || sim.HasRoadAt(building.X, building.Y - 1) || sim.HasRoadAt(building.X, building.Y + 1))
            {
                nextToRoad++;
            }
        }

        Assert.True(total > 0, "guvernér nepostavil nic — test by nic neměřil");
        Assert.True(nextToRoad > 0, $"ani jedna z {total} budov nestojí u cesty");
    }

    [Fact]
    public void GrowthStaysCompactInsteadOfScattering()
    {
        // Roztroušené domky po celé mapě jsou přesně ta „náhodná změť",
        // kvůli které auto-stavba vypadala jako chyba.
        var content = GrowingContent();
        var sim = NewSim(content);
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuilding(0, 20, 20));

        for (int i = 0; i < 400; i++)
        {
            sim.Tick();
        }

        Assert.True(sim.Buildings.Length > 1, "guvernér nepostavil nic — test by nic neměřil");

        foreach (var building in sim.Buildings)
        {
            int distance = Math.Max(Math.Abs(building.X - 20), Math.Abs(building.Y - 20));
            Assert.True(
                distance <= content.Gameplay.AutoBuild.SearchRadius * 3,
                $"budova na ({building.X}, {building.Y}) je od jádra moc daleko ({distance})");
        }
    }
}
