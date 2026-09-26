using CivDle.Core.Content;
using CivDle.Core.Sim;
using CivDle.Core.Tests.Support;
using CivDle.Core.World;
using Xunit;

namespace CivDle.Core.Tests.Sim;

/// <summary>
/// Politiky růstu (automatizace, stupeň 4): globální pravidla, která hráč zapíná
/// a která modulují auto-stavbu i plnění zón. „build_pace" zrychlí tempo,
/// „housing_density" preferuje povýšení bydlení před rozpínáním do šířky.
/// </summary>
public class PolicyTests
{
    private static ITerrain Grass() => new UniformTerrain(1);

    private static void RunTicks(Simulation sim, int ticks)
    {
        for (int i = 0; i < ticks; i++)
        {
            sim.Tick();
        }
    }

    [Fact]
    public void TogglePolicy_BuildPace_RaisesBuildsPerInterval()
    {
        var content = TestContent.Build(policies: new[] { new GrowthPolicyDef("rapid", "build_pace", 3) });
        var sim = new Simulation(content, Grass());

        Assert.Equal(1, sim.BuildsPerInterval);
        Assert.True(sim.TogglePolicy(0));
        Assert.Equal(3, sim.BuildsPerInterval);
        Assert.False(sim.TogglePolicy(0));
        Assert.Equal(1, sim.BuildsPerInterval);
    }

    /// <summary>Obsah: 1×1 „hut" plněná zónou; auto-stavba na interval 1; politika rychlého růstu.</summary>
    private static GameContent ZonePolicyContent()
    {
        var biomes = new[] { TestContent.WaterBiome(), TestContent.LandBiome("grass") };
        var resources = new[] { new Resource("wood", new RgbColor(140, 90, 40), StartAmount: 1000, BaseStorage: 5000) };
        var hut = TestContent.SimpleBuilding("hut", biomes.Length);
        var zoneTypes = new[] { new ZoneTypeDef("res", new RgbColor(0, 0, 200), new[] { 0 }) };
        var policies = new[] { new GrowthPolicyDef("rapid", "build_pace", 3) };
        var gameplay = TestContent.DefaultGameplay with
        {
            AutoBuild = new AutoBuildConfig(IntervalTicks: 1, SearchRadius: 6, PopulationHeadroom: 2),
            PopulationGrowthPerSecond = 0,
            FoodPerPersonPerSecond = 0,
        };
        return TestContent.Build(biomes, 1, resources, new[] { hut }, gameplay, zoneTypes: zoneTypes, policies: policies);
    }

    [Fact]
    public void RapidGrowth_PlacesMorePerInterval()
    {
        var slow = new Simulation(ZonePolicyContent(), Grass());
        Assert.True(slow.AddZone(0, 2, 2, 5, 5)); // 25 dlaždic, dost místa
        slow.Tick(); // jeden interval

        var fast = new Simulation(ZonePolicyContent(), Grass());
        Assert.True(fast.AddZone(0, 2, 2, 5, 5));
        Assert.True(fast.TogglePolicy(0)); // rychlý růst (build_pace 3)
        fast.Tick();

        Assert.Equal(1, slow.Buildings.Length);
        Assert.Equal(3, fast.Buildings.Length);
    }

    /// <summary>Obsah: „house" (bydlení 2, auto-stavba, vylepšení na „manor") + „manor" (bydlení 10).</summary>
    private static GameContent DensityContent()
    {
        var biomes = new[] { TestContent.WaterBiome(), TestContent.LandBiome("grass") };
        var resources = new[] { new Resource("wood", new RgbColor(140, 90, 40), StartAmount: 1000, BaseStorage: 5000) };
        var mask = new[] { false, true };
        var house = new BuildingDef(
            "house", "housing", new RgbColor(200, 180, 120), 1, 1,
            WorkerSlots: 0, HousingCapacity: 2,
            BuildCost: new[] { new ResourceAmount(0, 1) },
            Recipe: null, AllowedBiomes: mask, StorageBonus: System.Array.Empty<ResourceAmount>(),
            AutoBuild: true, Buildable: true, UpgradesToIndex: 1, UpgradeCost: new[] { new ResourceAmount(0, 1) },
            PowerSupply: 0, PowerDemand: 0);
        var manor = new BuildingDef(
            "manor", "housing", new RgbColor(180, 150, 100), 1, 1,
            WorkerSlots: 0, HousingCapacity: 10,
            BuildCost: new[] { new ResourceAmount(0, 1) },
            Recipe: null, AllowedBiomes: mask, StorageBonus: System.Array.Empty<ResourceAmount>(),
            AutoBuild: false, Buildable: false, UpgradesToIndex: -1, UpgradeCost: System.Array.Empty<ResourceAmount>(),
            PowerSupply: 0, PowerDemand: 0);

        var policies = new[] { new GrowthPolicyDef("dense", "housing_density", 0) };
        var gameplay = TestContent.DefaultGameplay with
        {
            AutoBuild = new AutoBuildConfig(IntervalTicks: 1, SearchRadius: 6, PopulationHeadroom: 2),
            PopulationGrowthPerSecond = 0,
            FoodPerPersonPerSecond = 0,
            StartingPopulation = 5,
            BaseHousingCapacity = 4,
        };
        return TestContent.Build(biomes, 1, resources, new[] { house, manor }, gameplay, policies: policies);
    }

    [Fact]
    public void DenseHousing_UpgradesExistingInsteadOfSprawling()
    {
        var sim = new Simulation(DensityContent(), Grass());
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuilding(0, 2, 2)); // house
        Assert.True(sim.TogglePolicy(0)); // hustota

        RunTicks(sim, 5);

        Assert.Equal(1, sim.Buildings.Length);     // žádná nová budova nepřibyla…
        Assert.Equal(1, sim.Buildings[0].DefIndex); // …dům byl povýšen na manor (index 1)
    }

    [Fact]
    public void WithoutDenseHousing_BuildsNewHousing()
    {
        var sim = new Simulation(DensityContent(), Grass());
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuilding(0, 2, 2)); // house, politika vypnutá

        RunTicks(sim, 5);

        Assert.True(sim.Buildings.Length > 1, "bez hustoty se staví nové domy do šířky");
    }

    // ----- zahušťování bez politiky (endgame.md, B2) -----

    /// <summary>
    /// Souš jen v pásu x = 1…<paramref name="length"/>, y = 2; všude jinde voda.
    /// Mimo řádky a sloupce vyhrazené ulicím (každá šestá), kam guvernér nestaví.
    /// </summary>
    private sealed class StripTerrain(int length) : ITerrain
    {
        public byte BiomeAt(int x, int y) => (byte)(y == 2 && x >= 1 && x <= length ? 1 : 0);
    }

    [Fact]
    public void WithoutThePolicy_NoRoomLeft_TheGovernorDensifies()
    {
        // Souš je zastavěná a politika vypnutá. Dřív tu guvernér stál
        // a populace se zasekla na stropu bydlení.
        var sim = new Simulation(DensityContent(), new StripTerrain(1));
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuilding(0, 1, 2));

        RunTicks(sim, 5);

        Assert.Equal(1, sim.Buildings.Length);
        Assert.Equal(1, sim.Buildings[0].DefIndex); // povýšený na manor
    }

    [Fact]
    public void WithRoomLeft_TheGovernorStillBuildsNewHousesFirst()
    {
        // Bez politiky zůstává výchozí cesta stejná: dokud je místo, staví se.
        var sim = new Simulation(DensityContent(), new StripTerrain(5));
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuilding(0, 1, 2));

        RunTicks(sim, 3);

        Assert.True(sim.Buildings.Length > 1);
        Assert.Equal(0, sim.Buildings[0].DefIndex);
    }

    [Fact]
    public void TheCityDensifiesFromItsCentreOut()
    {
        var sim = new Simulation(DensityContent(), new StripTerrain(5));
        for (int x = 1; x <= 5; x++)
        {
            Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuilding(0, x, 2));
        }

        sim.SetPopulationForTest(sim.HousingCapacity); // plno — tlak na bydlení

        // Až se povýší první dům, musí to být ten nejblíž těžišti.
        for (int t = 0; t < 50 && !sim.Buildings.ToArray().Any(b => b.DefIndex == 1); t++)
        {
            sim.Tick();
        }

        var upgraded = sim.Buildings.ToArray().Single(b => b.DefIndex == 1);
        float cx = sim.Settlements.Count > 0 ? sim.Settlements[0].CenterX : 0;
        float cy = sim.Settlements.Count > 0 ? sim.Settlements[0].CenterY : 0;
        float Distance(int x, int y) => (x - cx) * (x - cx) + (y - cy) * (y - cy);
        float nearest = sim.Buildings.ToArray().Min(b => Distance(b.X, b.Y));
        Assert.Equal(nearest, Distance(upgraded.X, upgraded.Y), 3);
    }
}
