using CivDle.Core.Content;
using CivDle.Core.Sim;
using CivDle.Core.Tests.Support;
using CivDle.Core.World;
using Xunit;

namespace CivDle.Core.Tests.Sim;

/// <summary>
/// Auto-silnice (fáze 4): nová budova se sama napojí cestou na síť, cesty vedou
/// jen po suché zemi, blokují stavbu a jsou deterministické. Nekonečná mapa —
/// silnice jsou souřadnice (<see cref="RoadTile"/>), ne indexy.
/// </summary>
public class RoadTests
{
    private static Simulation GrassSim(out GameContent content)
    {
        content = TestData.LoadRealContent();
        return new Simulation(content, new UniformTerrain(content.Biomes.IndexOf("grassland")), seed: 7);
    }

    [Fact]
    public void FirstBuilding_HasNoRoad()
    {
        var sim = GrassSim(out var content);

        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuilding(content.Buildings.IndexOf("house"), 4, 4));

        Assert.Empty(sim.RoadTiles);
    }

    [Fact]
    public void SecondBuilding_GetsConnectedByRoad()
    {
        var sim = GrassSim(out var content);
        int house = content.Buildings.IndexOf("house");

        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuilding(house, 2, 2));
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuilding(house, 10, 2));

        Assert.NotEmpty(sim.RoadTiles);
        // Cesta mezi budovami ve stejné řadě: rozumná délka, ne bloudění.
        Assert.InRange(sim.RoadTiles.Count, 1, 12);
        Assert.Contains(sim.RoadTiles, t => IsAdjacentTo(t, 2, 2));
        Assert.Contains(sim.RoadTiles, t => IsAdjacentTo(t, 10, 2));
    }

    [Fact]
    public void ThirdBuilding_ReusesExistingNetwork()
    {
        var sim = GrassSim(out var content);
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
        var map = new WorldMap(16, 16);
        Array.Fill(map.BiomeIndices, (byte)content.Biomes.IndexOf("grassland"));
        byte ocean = (byte)content.Biomes.IndexOf("ocean");

        // Svislý vodní pás s jediným suchým průchodem dole.
        for (int y = 0; y < 15; y++)
        {
            map.BiomeIndices[map.Index(6, y)] = ocean;
            map.BiomeIndices[map.Index(7, y)] = ocean;
        }

        var sim = new Simulation(content, new GridTerrain(map), seed: 7);
        int house = content.Buildings.IndexOf("house");
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuilding(house, 2, 2));
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuilding(house, 11, 2));

        Assert.NotEmpty(sim.RoadTiles);
        foreach (var t in sim.RoadTiles)
        {
            Assert.False(content.Biomes[sim.BiomeAt(t.X, t.Y)].IsWater, "Silnice nesmí vést po vodě.");
        }

        // Objížďka průchodem u y=15 → cesta je výrazně delší než vzdušná čára.
        Assert.True(sim.RoadTiles.Count > 12, $"Čekám objížďku, cesta má jen {sim.RoadTiles.Count} dlaždic.");
    }

    [Fact]
    public void RoadTile_BlocksBuildingPlacement()
    {
        var sim = GrassSim(out var content);
        int house = content.Buildings.IndexOf("house");
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuilding(house, 2, 2));
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuilding(house, 10, 2));

        var road = sim.RoadTiles[0];
        Assert.Equal(PlacementResult.Occupied, sim.CanPlace(house, road.X, road.Y));
    }

    [Fact]
    public void TooDistantBuilding_StaysUnconnected()
    {
        var sim = GrassSim(out var content);
        int house = content.Buildings.IndexOf("house");

        // Vzdálenost ~113 > maxSearchDistance (60) → žádná cesta, žádný pád.
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuilding(house, 2, 2));
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuilding(house, 2, 115));

        Assert.Empty(sim.RoadTiles);
    }

    [Fact]
    public void SameOperations_SameRoads()
    {
        var simA = GrassSim(out var content);
        var simB = new Simulation(content, new UniformTerrain(content.Biomes.IndexOf("grassland")), seed: 7);
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

    private static bool IsAdjacentTo(RoadTile t, int buildingX, int buildingY) =>
        Math.Abs(t.X - buildingX) + Math.Abs(t.Y - buildingY) == 1;
}
