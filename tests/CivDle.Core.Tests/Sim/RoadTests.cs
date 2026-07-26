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

    /// <summary>Mapa 16×16 louky se svislým vodním pásem dané šířky (suchý průchod dole).</summary>
    private static Simulation WaterStripWorld(GameContent content, int stripWidth)
    {
        var map = new WorldMap(16, 16);
        Array.Fill(map.BiomeIndices, (byte)content.Biomes.IndexOf("grassland"));
        byte ocean = (byte)content.Biomes.IndexOf("ocean");

        for (int y = 0; y < 15; y++)
        {
            for (int x = 6; x < 6 + stripWidth; x++)
            {
                map.BiomeIndices[map.Index(x, y)] = ocean;
            }
        }

        return new Simulation(content, new GridTerrain(map), seed: 7);
    }

    [Fact]
    public void Roads_BridgeNarrowWater()
    {
        // Úzký tok se přemostí (jinak by řeky trvale odřízly části města).
        var content = TestData.LoadRealContent();
        var sim = WaterStripWorld(content, stripWidth: 2);
        int house = content.Buildings.IndexOf("house");
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuilding(house, 2, 2));
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuilding(house, 11, 2));

        Assert.NotEmpty(sim.RoadTiles);
        Assert.Contains(sim.RoadTiles, t => sim.IsBridge(t.X, t.Y));
    }

    [Fact]
    public void Roads_NeverBuildBridgeLongerThanSpan()
    {
        // Skutečný invariant mostů: souvislý úsek cesty po vodě nesmí být delší než
        // maxBridgeSpan. (Že most vůbec nevznikne, platí jen když je voda široká na
        // VŠECHNY strany — to ověřuje BridgeTests; tady jde o dodržení stropu.)
        var content = TestData.LoadRealContent();
        int span = content.Gameplay.Roads.MaxBridgeSpan;
        var sim = WaterStripWorld(content, stripWidth: span + 2);
        int house = content.Buildings.IndexOf("house");
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuilding(house, 2, 2));
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuilding(house, 6 + span + 3, 2));

        Assert.NotEmpty(sim.RoadTiles);

        var bridgeTiles = sim.RoadTiles.Where(t => sim.IsBridge(t.X, t.Y))
            .Select(t => (t.X, t.Y)).ToHashSet();
        foreach (var start in bridgeTiles)
        {
            Assert.True(ComponentSize(bridgeTiles, start) <= span,
                $"Most u {start} je delší než povolené rozpětí {span}.");
        }
    }

    /// <summary>Velikost souvislé (4-sousední) skupiny mostních dlaždic obsahující daný bod.</summary>
    private static int ComponentSize(HashSet<(int X, int Y)> tiles, (int X, int Y) start)
    {
        var seen = new HashSet<(int X, int Y)> { start };
        var stack = new Stack<(int X, int Y)>();
        stack.Push(start);
        while (stack.Count > 0)
        {
            var (x, y) = stack.Pop();
            foreach (var next in new[] { (x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1) })
            {
                if (tiles.Contains(next) && seen.Add(next))
                {
                    stack.Push(next);
                }
            }
        }

        return seen.Count;
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
