using CivDle.Core.Content;
using CivDle.Core.Sim;
using CivDle.Core.Tests.Support;
using CivDle.Core.World;
using Xunit;

namespace CivDle.Core.Tests.Sim;

/// <summary>
/// Reforestace: budova, která krajinu vrací zpátky.
///
/// <para>Proč to existuje: pily vytěží okolí rychleji, než samo doroste, a hráči
/// zůstane holina, se kterou nemůže nic dělat. Lesní školka je ta druhá páka —
/// stojí místo, dělníky i provoz, ale les vrací.</para>
/// </summary>
public class ReforestTests
{
    private const int Wood = 0;

    private static BuildingDef Nursery(int radius) => new(
        "nursery", "production", new RgbColor(1, 1, 1), 1, 1,
        WorkerSlots: 0, HousingCapacity: 0,
        BuildCost: Array.Empty<ResourceAmount>(),
        Recipe: null,
        AllowedBiomes: new[] { false, true },
        StorageBonus: Array.Empty<ResourceAmount>(),
        AutoBuild: false, Buildable: true, UpgradesToIndex: -1,
        UpgradeCost: Array.Empty<ResourceAmount>(), PowerSupply: 0, PowerDemand: 0,
        ReforestRadius: radius);

    private static Simulation NewSim(int radius)
    {
        var biomes = new[]
        {
            TestContent.WaterBiome(),
            TestContent.LandBiome("forest") with { ClickYield = new ClickYield(Wood, 2, Charges: 2, RegrowSeconds: 100_000) },
        };
        var resources = new[] { new Resource("wood", new RgbColor(1, 1, 1), StartAmount: 0, BaseStorage: 1_000_000) };
        var gameplay = TestContent.DefaultGameplay with
        {
            FoodPerPersonPerSecond = 0,
            PopulationGrowthPerSecond = 0,
        };

        var content = TestContent.Build(biomes, 1, resources, new[] { Nursery(radius) }, gameplay);
        return new Simulation(content, new UniformTerrain(1));
    }

    private static void Tick(Simulation sim, int ticks)
    {
        for (int i = 0; i < ticks; i++)
        {
            sim.Tick();
        }
    }

    [Fact]
    public void ANurseryBringsTheForestBack()
    {
        var sim = NewSim(radius: 4);

        // Vytěžit dlaždici do sucha. Dorůstání je nastavené tak pomalé, že by
        // se sama nevrátila — co se vrátí, vrátila školka.
        Assert.True(sim.TryHarvest(12, 12, out _, out _));
        Assert.True(sim.TryHarvest(12, 12, out _, out _));
        Assert.Equal(0, sim.NodeChargesLeft(12, 12));

        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuilding(0, 12, 14));
        Tick(sim, 4000);

        Assert.True(sim.NodeChargesLeft(12, 12) > 0, "školka měla les vrátit");
    }

    [Fact]
    public void WithoutANurseryTheClearingStays()
    {
        // Protějšek předchozího testu: bez školky se nic nevrátí. Kdyby se
        // vracelo samo, první test by nic nedokazoval.
        var sim = NewSim(radius: 4);

        Assert.True(sim.TryHarvest(12, 12, out _, out _));
        Assert.True(sim.TryHarvest(12, 12, out _, out _));
        Tick(sim, 4000);

        Assert.Equal(0, sim.NodeChargesLeft(12, 12));
    }

    [Fact]
    public void ANurseryDoesNotReachAcrossTheMap()
    {
        // Okruh musí platit — jinak by jedna školka udržela zelený celý svět.
        var sim = NewSim(radius: 2);

        Assert.True(sim.TryHarvest(60, 60, out _, out _));
        Assert.True(sim.TryHarvest(60, 60, out _, out _));

        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuilding(0, 12, 14));
        Tick(sim, 4000);

        Assert.Equal(0, sim.NodeChargesLeft(60, 60));
    }

    [Fact]
    public void RealContent_HasAWayToRegrowTheForest()
    {
        // Bez tohohle by zbytek testů hlídal mechaniku, kterou hra nenabízí.
        var content = TestData.LoadRealContent();

        Assert.Contains(content.Buildings.All, b => b.Reforests);
    }
}
