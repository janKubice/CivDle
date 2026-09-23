using CivDle.Core.Content;
using CivDle.Core.Sim;
using CivDle.Core.Tests.Support;
using CivDle.Core.World;
using Xunit;

namespace CivDle.Core.Tests.Sim;

/// <summary>
/// Výzkum dražší, než se vejde do skladu. Cena roste s každou hotovou
/// technologií, takže dřív nebo později přeroste sklad — a „nemáš na to"
/// hráče nechávalo čekat na něco, co nikdy nepřijde.
/// </summary>
public class ResearchStorageTests
{
    private const int Wood = 0;

    private static Simulation World(double woodCap, int techCost, bool withWarehouse = false)
    {
        var resources = new[] { new Resource("wood", new RgbColor(1, 1, 1), StartAmount: 0, BaseStorage: woodCap) };
        var warehouse = TestContent.SimpleBuilding("warehouse", 2) with
        {
            BuildCost = Array.Empty<ResourceAmount>(),
            StorageBonus = new[] { new ResourceAmount(Wood, 1000) },
        };
        var tech = new TechDef("big_idea", new[] { new ResourceAmount(Wood, techCost) },
            Array.Empty<int>(), Array.Empty<int>());
        var content = TestContent.Build(resources: resources, buildings: new[] { warehouse }, techs: new[] { tech });
        var sim = new Simulation(content, new UniformTerrain(1));
        if (withWarehouse)
        {
            Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuildingFree(0, 0, 0));
        }

        return sim;
    }

    [Fact]
    public void ACostAboveStorageSaysSo_NotJustTooExpensive()
    {
        var sim = World(woodCap: 100, techCost: 500);
        int cost = sim.ScaledResearchCost(0)[0].Amount;
        Assert.True(cost > 100, "test předpokládá cenu nad kapacitou skladu");

        Assert.Equal(PlacementResult.ExceedsStorage, sim.CanResearch(0));
        Assert.Equal(Wood, sim.ResourceBeyondStorage(sim.ScaledResearchCost(0)));
    }

    [Fact]
    public void AWarehouseTurnsItBackIntoAMatterOfSaving()
    {
        var sim = World(woodCap: 100, techCost: 500, withWarehouse: true);

        Assert.Equal(PlacementResult.NotEnoughResources, sim.CanResearch(0));
    }

    [Fact]
    public void ACheapTechIsJustTooExpensiveForNow()
    {
        var sim = World(woodCap: 1000, techCost: 10);

        Assert.Equal(PlacementResult.NotEnoughResources, sim.CanResearch(0));
        Assert.Equal(-1, sim.ResourceBeyondStorage(sim.ScaledResearchCost(0)));
    }
}
