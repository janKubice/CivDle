using CivDle.Core.Content;
using CivDle.Core.Sim;
using CivDle.Core.Tests.Support;
using CivDle.Core.World;
using Xunit;

namespace CivDle.Core.Tests.Sim;

/// <summary>
/// Ulice jako mřížka bloků.
///
/// <para>Hráč to viděl na screenshotu: pruhy pro ulice zůstávaly poloprázdné
/// a mezi bloky trčely útržky dlažby ve tvaru „H". Mřížka totiž byla jen
/// <b>zákaz stavět</b>, ne skutečná síť — dláždilo se výhradně tam, kudy zrovna
/// vedla nejkratší cesta k síti.</para>
///
/// <para>Teď se dláždí celý obvod bloku naráz a sousední bloky pruh sdílejí,
/// takže z města je klasická šachovnice.</para>
/// </summary>
public class StreetGridTests
{
    private static readonly Resource[] Planks =
    {
        new("planks", new RgbColor(200, 170, 110), StartAmount: 1_000_000, BaseStorage: 10_000_000),
    };

    private static GameContent Content()
    {
        var house = new BuildingDef(
            "house", "housing", new RgbColor(180, 100, 60), 1, 1,
            WorkerSlots: 0, HousingCapacity: 4,
            BuildCost: new[] { new ResourceAmount(0, 1) },
            Recipe: null,
            AllowedBiomes: new[] { false, true },
            StorageBonus: Array.Empty<ResourceAmount>(),
            AutoBuild: false, Buildable: true,
            UpgradesToIndex: -1, UpgradeCost: Array.Empty<ResourceAmount>(),
            PowerSupply: 0, PowerDemand: 0);

        return TestContent.Build(
            biomes: new[] { TestContent.WaterBiome(), TestContent.LandBiome("grass") },
            resources: Planks,
            buildings: new[] { house });
    }

    private static Simulation World(GameContent content) =>
        new(content, new UniformTerrain((byte)1));

    /// <summary>Postaví tažením obdélník od (fromX,fromY) do (toX,toY).</summary>
    private static Simulation Drag(GameContent content, int fromX, int fromY, int toX, int toY)
    {
        var sim = World(content);
        var bulk = new BulkBuilder(sim, content);
        var plan = new List<BulkSlot>();
        bulk.PlanArea(0, fromX, fromY, toX, toY, plan);
        bulk.Build(0, plan);
        return sim;
    }

    /// <summary>Je celý pruh mezi dvěma křižovatkami vydlážděný?</summary>
    private static bool WholeLane(Simulation sim, int fixedX, int fromY, int toY)
    {
        for (int y = fromY; y <= toY; y++)
        {
            if (!sim.IsRoad(fixedX, y))
            {
                return false;
            }
        }

        return true;
    }

    [Fact]
    public void ABuiltBlockGetsItsWholePerimeterPaved()
    {
        // Jádro chyby: dřív se z obvodu vydláždilo jen pár dlaždic.
        var content = Content();
        var sim = Drag(content, 1, 1, 5, 5); // celý blok mezi pruhy 0 a 6

        Assert.True(WholeLane(sim, 0, 0, 6), "levý pruh není vydlážděný celý");
        Assert.True(WholeLane(sim, 6, 0, 6), "pravý pruh není vydlážděný celý");
        for (int x = 0; x <= 6; x++)
        {
            Assert.True(sim.IsRoad(x, 0), $"horní pruh chybí na {x},0");
            Assert.True(sim.IsRoad(x, 6), $"dolní pruh chybí na {x},6");
        }
    }

    [Fact]
    public void NeighbouringBlocksShareTheLaneBetweenThem()
    {
        // Dva bloky vedle sebe = jedna ulice mezi nimi, ne dvě vedle sebe.
        var content = Content();
        var sim = Drag(content, 1, 1, 11, 11);

        Assert.True(WholeLane(sim, 6, 0, 6), "sdílený pruh mezi bloky není celý");
        Assert.True(WholeLane(sim, 12, 0, 6), "vnější pruh druhého bloku není celý");
    }

    [Fact]
    public void TheGridHasNoHolesInsideACity()
    {
        // Vlastní příznak ze screenshotu: díry v pruzích. Projdeme všechny
        // pruhy uvnitř zastavěné plochy a čekáme souvislou dlažbu.
        var content = Content();
        var sim = Drag(content, 1, 1, 17, 17);

        for (int lane = 0; lane <= 18; lane += 6)
        {
            for (int along = 0; along <= 18; along++)
            {
                Assert.True(sim.IsRoad(lane, along), $"díra ve svislém pruhu {lane} na {lane},{along}");
                Assert.True(sim.IsRoad(along, lane), $"díra ve vodorovném pruhu {lane} na {along},{lane}");
            }
        }
    }

    [Fact]
    public void ALoneHutInTheWildernessGetsNoRingOfStreets()
    {
        // Okruh ulic kolem jediné chalupy by vypadal směšně.
        var content = Content();
        var sim = World(content);

        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuilding(0, 2, 2));

        Assert.Equal(0, sim.RoadTiles.Count);
    }

    [Fact]
    public void EveryBuildingInACityEndsUpConnected()
    {
        // Hezká mřížka je k ničemu, když k ní budovy nevedou.
        var content = Content();
        var sim = Drag(content, 1, 1, 17, 17);

        for (int i = 0; i < sim.Buildings.Length; i++)
        {
            Assert.True(sim.IsBuildingConnected(i),
                $"budova {i} na {sim.Buildings[i].X},{sim.Buildings[i].Y} zůstala bez napojení");
        }
    }

    [Fact]
    public void RoadsNeverRunThroughBuildings()
    {
        var content = Content();
        var sim = Drag(content, 1, 1, 17, 17);

        foreach (var road in sim.RoadTiles)
        {
            Assert.False(sim.IsOccupied(road.X, road.Y), $"cesta na {road.X},{road.Y} vede skrz budovu");
        }
    }
}
