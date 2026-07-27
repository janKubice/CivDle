using CivDle.Core.Content;
using CivDle.Core.Sim;
using CivDle.Core.Tests.Support;
using CivDle.Core.World;
using Xunit;

namespace CivDle.Core.Tests.Sim;

/// <summary>
/// Tvar silniční sítě. Auto-silnice dřív dláždily po každé stavbě, takže mezi
/// dvěma sousedními domy vždycky vyrostl kus cesty — město vypadalo jako
/// šachovnice a nešel postavit ani blok 2×2. Tyhle testy hlídají, že se dláždí
/// jen tam, kde napojení opravdu chybí.
/// </summary>
public class RoadShapeTests
{
    private static Simulation GrassSim(out GameContent content)
    {
        content = TestData.LoadRealContent();
        return new Simulation(content, new UniformTerrain((byte)content.Biomes.IndexOf("grassland")));
    }

    [Fact]
    public void AdjacentBuildings_GetNoRoadBetweenThem()
    {
        var sim = GrassSim(out var content);
        int house = content.Buildings.IndexOf("house");

        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuilding(house, 4, 4));
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuilding(house, 5, 4));

        Assert.Empty(sim.RoadTiles);
    }

    [Fact]
    public void FullBlockOfFour_FitsWithoutRoadsInTheWay()
    {
        // Přesně to, co dřív nešlo: čtvrtý roh byl zabraný auto-silnicí.
        var sim = GrassSim(out var content);
        int house = content.Buildings.IndexOf("house");

        // Zdarma: měří se tvar sítě, ne ekonomika — startovní suroviny na čtyři domy nestačí.
        foreach (var (x, y) in new[] { (4, 4), (5, 4), (4, 5), (5, 5) })
        {
            Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuildingFree(house, x, y));
        }

        Assert.Equal(4, sim.Buildings.Length);
    }

    [Fact]
    public void DistantBuilding_StillGetsARoad()
    {
        // Zrušit dláždění úplně by rozbilo smysl silnic — vzdálená budova ho pořád potřebuje.
        var sim = GrassSim(out var content);
        int house = content.Buildings.IndexOf("house");

        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuilding(house, 4, 4));
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuilding(house, 14, 4));

        Assert.NotEmpty(sim.RoadTiles);
        Assert.True(sim.IsBuildingConnected(1));
    }

    [Fact]
    public void WholeBlock_CountsAsConnected_WhenOneHouseTouchesTheRoad()
    {
        var sim = GrassSim(out var content);
        int house = content.Buildings.IndexOf("house");

        sim.AddRoadTileForTest(3, 4);
        foreach (var (x, y) in new[] { (4, 4), (5, 4), (4, 5), (5, 5) })
        {
            Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuildingFree(house, x, y));
        }

        // Silnice se dotýká jen domu na (4,4); zbytek bloku ji má „přes souseda".
        for (int i = 0; i < sim.Buildings.Length; i++)
        {
            Assert.True(sim.IsBuildingConnected(i), $"budova {i} má být napojená přes svůj blok");
        }
    }

    [Fact]
    public void PlayerCanLayAndTearUpRoads()
    {
        var sim = GrassSim(out _);

        Assert.Equal(PlacementResult.Ok, sim.TryBuildRoad(7, 7));
        Assert.True(sim.IsRoad(7, 7));
        Assert.Equal(PlacementResult.Occupied, sim.TryBuildRoad(7, 7)); // podruhé už tam je

        Assert.True(sim.TryRemoveRoad(7, 7));
        Assert.False(sim.IsRoad(7, 7));
        Assert.False(sim.TryRemoveRoad(7, 7));
        Assert.Empty(sim.RoadTiles);
    }

    [Fact]
    public void RoadCannotReplaceABuilding()
    {
        var sim = GrassSim(out var content);
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuilding(content.Buildings.IndexOf("house"), 4, 4));

        Assert.Equal(PlacementResult.Occupied, sim.TryBuildRoad(4, 4));
    }
}
