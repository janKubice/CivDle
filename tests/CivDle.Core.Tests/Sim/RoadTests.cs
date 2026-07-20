using CivDle.Core.Sim;
using CivDle.Core.Tests.Support;
using CivDle.Core.World;
using Xunit;

namespace CivDle.Core.Tests.Sim;

/// <summary>
/// Auto-silnice (fáze 4): nová budova se sama napojí cestou na síť, cesty vedou
/// jen po suché zemi, blokují stavbu a jsou deterministické.
/// </summary>
public class RoadTests
{
    private static WorldMap UniformMap(int size, byte biomeIndex)
    {
        var map = new WorldMap(size, size);
        Array.Fill(map.BiomeIndices, biomeIndex);
        return map;
    }

    private static Simulation GrassSim(int size, out CivDle.Core.Content.GameContent content)
    {
        content = TestData.LoadRealContent();
        return new Simulation(content, UniformMap(size, (byte)content.Biomes.IndexOf("grassland")), seed: 7);
    }

    [Fact]
    public void FirstBuilding_HasNoRoad()
    {
        var sim = GrassSim(16, out var content);

        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuilding(content.Buildings.IndexOf("house"), 4, 4));

        Assert.Empty(sim.RoadTiles);
    }

    [Fact]
    public void SecondBuilding_GetsConnectedByRoad()
    {
        var sim = GrassSim(16, out var content);
        int house = content.Buildings.IndexOf("house");

        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuilding(house, 2, 2));
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuilding(house, 10, 2));

        Assert.NotEmpty(sim.RoadTiles);
        // Cesta mezi budovami ve stejné řadě: rozumná délka, ne bloudění.
        Assert.InRange(sim.RoadTiles.Count, 1, 12);
        Assert.Contains(sim.RoadTiles, t => IsAdjacentTo(sim, t, 2, 2));
        Assert.Contains(sim.RoadTiles, t => IsAdjacentTo(sim, t, 10, 2));
    }

    [Fact]
    public void ThirdBuilding_ReusesExistingNetwork()
    {
        var sim = GrassSim(20, out var content);
        int house = content.Buildings.IndexOf("house");

        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuilding(house, 2, 2));
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuilding(house, 12, 2));
        int roadsAfterTwo = sim.RoadTiles.Count;

        // Třetí budova (farma — na další dům by nestačila prkna) hned vedle
        // existující cesty → napojí se krátce, síť neduplikuje.
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuilding(content.Buildings.IndexOf("farm"), 7, 4));

        Assert.InRange(sim.RoadTiles.Count - roadsAfterTwo, 0, 4);
    }

    [Fact]
    public void Roads_AvoidWater()
    {
        var content = TestData.LoadRealContent();
        var map = UniformMap(16, (byte)content.Biomes.IndexOf("grassland"));
        byte ocean = (byte)content.Biomes.IndexOf("ocean");

        // Svislý vodní pás s jediným suchým průchodem dole.
        for (int y = 0; y < 15; y++)
        {
            map.BiomeIndices[map.Index(6, y)] = ocean;
            map.BiomeIndices[map.Index(7, y)] = ocean;
        }

        var sim = new Simulation(content, map, seed: 7);
        int house = content.Buildings.IndexOf("house");
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuilding(house, 2, 2));
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuilding(house, 11, 2));

        Assert.NotEmpty(sim.RoadTiles);
        foreach (int tileIndex in sim.RoadTiles)
        {
            Assert.False(content.Biomes[sim.Map.BiomeIndices[tileIndex]].IsWater, "Silnice nesmí vést po vodě.");
        }

        // Objížďka průchodem u y=15 → cesta je výrazně delší než vzdušná čára.
        Assert.True(sim.RoadTiles.Count > 12, $"Čekám objížďku, cesta má jen {sim.RoadTiles.Count} dlaždic.");
    }

    [Fact]
    public void RoadTile_BlocksBuildingPlacement()
    {
        var sim = GrassSim(16, out var content);
        int house = content.Buildings.IndexOf("house");
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuilding(house, 2, 2));
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuilding(house, 10, 2));

        int roadTile = sim.RoadTiles[0];
        int roadX = roadTile % sim.Map.Width;
        int roadY = roadTile / sim.Map.Width;

        Assert.Equal(PlacementResult.Occupied, sim.CanPlace(house, roadX, roadY));
    }

    [Fact]
    public void TooDistantBuilding_StaysUnconnected()
    {
        var content = TestData.LoadRealContent();
        var sim = new Simulation(content, UniformMap(128, (byte)content.Biomes.IndexOf("grassland")), seed: 7);
        int house = content.Buildings.IndexOf("house");

        // Vzdálenost ~110 > maxSearchDistance (60) → žádná cesta, žádný pád.
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuilding(house, 2, 2));
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuilding(house, 2, 115));

        Assert.Empty(sim.RoadTiles);
    }

    [Fact]
    public void SameOperations_SameRoads()
    {
        var simA = GrassSim(20, out var content);
        var simB = new Simulation(content, UniformMap(20, (byte)content.Biomes.IndexOf("grassland")), seed: 7);
        int house = content.Buildings.IndexOf("house");
        int farm = content.Buildings.IndexOf("farm");

        foreach (var sim in new[] { simA, simB })
        {
            sim.TryPlaceBuilding(house, 2, 2);
            sim.TryPlaceBuilding(farm, 12, 6);
            sim.TryPlaceBuilding(house, 6, 12);
        }

        Assert.Equal(simA.RoadTiles, simB.RoadTiles);
    }

    private static bool IsAdjacentTo(Simulation sim, int tileIndex, int buildingX, int buildingY)
    {
        int x = tileIndex % sim.Map.Width;
        int y = tileIndex / sim.Map.Width;
        return Math.Abs(x - buildingX) + Math.Abs(y - buildingY) == 1;
    }
}
