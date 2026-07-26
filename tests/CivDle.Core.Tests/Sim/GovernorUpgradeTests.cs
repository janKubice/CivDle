using CivDle.Core.Content;
using CivDle.Core.Sim;
using CivDle.Core.Tests.Support;
using CivDle.Core.World;
using Xunit;

namespace CivDle.Core.Tests.Sim;

/// <summary>
/// Guvernérova správa vylepšení: hráč ji nejdřív ODEMKNE technologií a pak si
/// nastaví, jak moc si guvernér vylepšuje budovy sám. Automatizace se odemyká,
/// není výchozí (living-city.md §4).
/// </summary>
public class GovernorUpgradeTests
{
    private static ITerrain Grass() => new UniformTerrain(1);

    /// <summary>Obsah: dům → sídlo (bydlení), dílna → manufaktura (výroba), + odemykací tech.</summary>
    private static GameContent GovernorContent()
    {
        var biomes = new[] { TestContent.WaterBiome(), TestContent.LandBiome("grass") };
        var resources = new[] { new Resource("wood", new RgbColor(1, 1, 1), StartAmount: 500, BaseStorage: 5000) };
        var mask = new[] { false, true };
        var cost = new[] { new ResourceAmount(0, 1) };

        BuildingDef Make(string id, string category, int upgradesTo, int housing = 0) => new(
            id, category, new RgbColor(120, 120, 120), 1, 1,
            WorkerSlots: 0, HousingCapacity: housing,
            BuildCost: cost, Recipe: null, AllowedBiomes: mask,
            StorageBonus: System.Array.Empty<ResourceAmount>(),
            AutoBuild: false, Buildable: upgradesTo >= 0, UpgradesToIndex: upgradesTo,
            UpgradeCost: upgradesTo >= 0 ? cost : System.Array.Empty<ResourceAmount>(),
            PowerSupply: 0, PowerDemand: 0);

        var buildings = new[]
        {
            Make("house", "housing", 1, housing: 2),     // 0 → 1
            Make("manor", "housing", -1, housing: 8),    // 1 (konec řetězu)
            Make("workshop", "production", 3),           // 2 → 3
            Make("manufactory", "production", -1),       // 3
        };

        var tech = new TechDef(
            Simulation.GovernorTechId, cost,
            System.Array.Empty<int>(), System.Array.Empty<int>());

        var gameplay = TestContent.DefaultGameplay with
        {
            AutoBuild = new AutoBuildConfig(IntervalTicks: 1, SearchRadius: 4, PopulationHeadroom: 2),
            FoodPerPersonPerSecond = 0,
            PopulationGrowthPerSecond = 0,
        };
        return TestContent.Build(biomes, 1, resources, buildings, gameplay, techs: new[] { tech });
    }

    private static Simulation WithBuildings(GameContent content)
    {
        var sim = new Simulation(content, Grass());
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuilding(0, 2, 2)); // dům
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuilding(2, 5, 5)); // dílna
        return sim;
    }

    private static void RunTicks(Simulation sim, int ticks)
    {
        for (int i = 0; i < ticks; i++)
        {
            sim.Tick();
        }
    }

    [Fact]
    public void Governor_IsLockedUntilResearched()
    {
        var sim = WithBuildings(GovernorContent());

        Assert.False(sim.IsGovernorUnlocked);

        // Nastavení jde „zadat", ale bez odemčení se neprojeví.
        sim.SetAutoUpgradeLevel(3);
        Assert.Equal(0, sim.AutoUpgradeLevel);

        RunTicks(sim, 50);
        Assert.Equal(0, sim.Buildings[0].DefIndex); // dům zůstal domem
    }

    [Fact]
    public void Level1_UpgradesOnlyHousing()
    {
        var sim = WithBuildings(GovernorContent());
        Assert.Equal(PlacementResult.Ok, sim.TryResearch(0));
        sim.SetAutoUpgradeLevel(1);

        RunTicks(sim, 50);

        Assert.Equal(1, sim.Buildings[0].DefIndex); // bydlení povýšeno
        Assert.Equal(2, sim.Buildings[1].DefIndex); // výroba beze změny
    }

    [Fact]
    public void Level2_UpgradesProductionToo()
    {
        var sim = WithBuildings(GovernorContent());
        Assert.Equal(PlacementResult.Ok, sim.TryResearch(0));
        sim.SetAutoUpgradeLevel(2);

        RunTicks(sim, 50);

        Assert.Equal(1, sim.Buildings[0].DefIndex);
        Assert.Equal(3, sim.Buildings[1].DefIndex); // i výroba povýšena
    }

    [Fact]
    public void LevelZero_LeavesEverythingAlone()
    {
        var sim = WithBuildings(GovernorContent());
        Assert.Equal(PlacementResult.Ok, sim.TryResearch(0));
        sim.SetAutoUpgradeLevel(0);

        RunTicks(sim, 50);

        Assert.Equal(0, sim.Buildings[0].DefIndex);
        Assert.Equal(2, sim.Buildings[1].DefIndex);
    }

    [Fact]
    public void Level_IsClampedToValidRange()
    {
        var sim = WithBuildings(GovernorContent());
        Assert.Equal(PlacementResult.Ok, sim.TryResearch(0));

        sim.SetAutoUpgradeLevel(99);
        Assert.Equal(Simulation.MaxAutoUpgradeLevel, sim.AutoUpgradeLevel);

        sim.SetAutoUpgradeLevel(-5);
        Assert.Equal(0, sim.AutoUpgradeLevel);
    }

    [Fact]
    public void RealContent_HasGovernorUnlockTech()
    {
        var content = TestData.LoadRealContent();
        Assert.True(content.Techs.TryIndexOf(Simulation.GovernorTechId, out _),
            $"chybí technologie '{Simulation.GovernorTechId}', která guvernéra odemyká");
    }
}
