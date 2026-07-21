using CivDle.Core.Sim;
using CivDle.Core.Tests.Support;
using CivDle.Core.World;
using Xunit;

namespace CivDle.Core.Tests.Sim;

/// <summary>
/// Bourání a přesun budov: uvolnění dlaždic, částečný refund, přepočet bydlení
/// a konzistentní occupancy po swap-remove z plochého pole.
/// </summary>
public class DemolishMoveTests
{
    private static (CivDle.Core.Content.GameContent Content, Simulation Sim) Grassland()
    {
        var content = TestData.LoadRealContent();
        var sim = new Simulation(content, new UniformTerrain((byte)content.Biomes.IndexOf("grassland")));
        return (content, sim);
    }

    [Fact]
    public void Demolish_FreesTiles_RefundsHalf_RemovesHousing()
    {
        var (content, sim) = Grassland();
        int house = content.Buildings.IndexOf("house");
        int wood = content.Resources.IndexOf("wood");
        int capBefore = sim.HousingCapacity;

        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuilding(house, 2, 2));
        Assert.True(sim.IsOccupied(2, 2));
        double woodAfterBuild = sim.GetResource(wood); // 30 − 5 = 25

        Assert.Equal(PlacementResult.Ok, sim.TryDemolish(0));

        Assert.Equal(0, sim.Buildings.Length);
        Assert.False(sim.IsOccupied(2, 2));
        Assert.Equal(capBefore, sim.HousingCapacity);       // bydlení zase pryč
        Assert.Equal(woodAfterBuild + 2, sim.GetResource(wood)); // refund floor(5×0.5)=2
    }

    [Fact]
    public void Move_RelocatesForFree()
    {
        var (content, sim) = Grassland();
        int house = content.Buildings.IndexOf("house");
        int wood = content.Resources.IndexOf("wood");

        sim.TryPlaceBuilding(house, 2, 2);
        double woodBefore = sim.GetResource(wood);

        Assert.Equal(PlacementResult.Ok, sim.TryMoveBuilding(0, 6, 6));

        Assert.False(sim.IsOccupied(2, 2));
        Assert.True(sim.IsOccupied(6, 6));
        Assert.Equal(6, sim.Buildings[0].X);
        Assert.Equal(woodBefore, sim.GetResource(wood)); // přesun je zdarma
    }

    [Fact]
    public void Demolish_SwapRemove_KeepsOtherBuildingResolvable()
    {
        var (content, sim) = Grassland();
        int house = content.Buildings.IndexOf("house");
        sim.TryPlaceBuilding(house, 2, 2); // index 0
        sim.TryPlaceBuilding(house, 6, 6); // index 1

        Assert.Equal(PlacementResult.Ok, sim.TryDemolish(0)); // idx 1 se přesune na idx 0

        Assert.Equal(1, sim.Buildings.Length);
        Assert.True(sim.TryGetBuildingAt(6, 6, out int idx));
        Assert.Equal(house, sim.Buildings[idx].DefIndex);
        Assert.False(sim.IsOccupied(2, 2));
    }
}
