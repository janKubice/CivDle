using CivDle.Core.Content;
using CivDle.Core.Sim;
using CivDle.Core.Tests.Support;
using CivDle.Core.World;
using Xunit;

namespace CivDle.Core.Tests.Sim;

/// <summary>
/// Uliční mřížka u hromadné stavby.
///
/// <para>Hráč hlásil, že „při tažení a u ×25 se cesty staví divně na okraji
/// a ne uvnitř". Příčina: tažení stavělo úplně souvislý slitek, ve kterém
/// nezbyla jediná volná dlaždice — auto-silnice pak neměla kudy dovnitř
/// a mohla čtvrť jen obkroužit zvenčí.</para>
///
/// <para>Velký tah proto nechává volné pruhy pro ulice. Malý ne: kdo táhne řadu
/// tří chalup, chce tři chalupy, ne dvě a díru.</para>
/// </summary>
public class BulkStreetTests
{
    private static readonly Resource[] Planks =
    {
        new("planks", new RgbColor(200, 170, 110), StartAmount: 1_000_000, BaseStorage: 10_000_000),
    };

    private static GameContent Content(int width = 1, int height = 1)
    {
        var house = new BuildingDef(
            "house", "housing", new RgbColor(180, 100, 60), width, height,
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

    private static (Simulation Sim, BulkBuilder Bulk) World(GameContent content)
    {
        var sim = new Simulation(content, new UniformTerrain((byte)1));
        return (sim, new BulkBuilder(sim, content));
    }

    [Fact]
    public void ASmallDragBuildsEveryTileThePlayerDrew()
    {
        // Tři chalupy v řadě: hráč nakreslil řadu, ne řadu s dírou.
        var content = Content();
        var (_, bulk) = World(content);
        var plan = new List<BulkSlot>();

        bulk.PlanArea(0, 1, 1, 3, 1, plan);

        Assert.Equal(3, plan.Count);
    }

    [Fact]
    public void ABigDragLeavesLanesForStreets()
    {
        // Deset na deset: tady už ulice smysl mají, jinak z toho je slitek.
        var content = Content();
        var (_, bulk) = World(content);
        var plan = new List<BulkSlot>();

        bulk.PlanArea(0, 1, 1, 10, 10, plan);

        Assert.True(plan.Count < 100, $"velký tah nenechal místo na ulice ({plan.Count} ze 100)");
        Assert.All(plan, slot => Assert.False(
            CityLayout.IsReservedForStreet(slot.X, slot.Y),
            $"budova na {slot.X},{slot.Y} stojí v pruhu pro ulici"));
    }

    [Fact]
    public void ABigDragStillBuildsMostOfTheArea()
    {
        // Pruhy nesmí sežrat čtvrť: bloky 5×5 s jednou mezerou = většina plochy.
        var content = Content();
        var (_, bulk) = World(content);
        var plan = new List<BulkSlot>();

        bulk.PlanArea(0, 1, 1, 12, 12, plan);

        Assert.True(plan.Count > 144 / 2, $"z 144 dlaždic zbylo jen {plan.Count}");
    }

    [Fact]
    public void AfterABigDragTheRoadsReachInside()
    {
        // Vlastní příznak chyby: cesty jen kolem dokola a uvnitř žádná.
        var content = Content();
        var (sim, bulk) = World(content);
        var plan = new List<BulkSlot>();
        bulk.PlanArea(0, 1, 1, 12, 12, plan);
        bulk.Build(0, plan);

        int inside = 0;
        foreach (var road in sim.RoadTiles)
        {
            if (road.X > 1 && road.X < 12 && road.Y > 1 && road.Y < 12)
            {
                inside++;
            }
        }

        Assert.True(inside > 0, "uvnitř postavené čtvrti nevede ani jedna cesta");
    }

    [Fact]
    public void ABigBuildingNeverStandsAcrossALane()
    {
        // U větších půdorysů nestačí kontrolovat roh — budova by ulici přeťala.
        var content = Content(width: 2, height: 2);
        var (_, bulk) = World(content);
        var plan = new List<BulkSlot>();

        bulk.PlanArea(0, 1, 1, 20, 20, plan);

        Assert.All(plan, slot =>
        {
            for (int y = slot.Y; y < slot.Y + 2; y++)
            {
                for (int x = slot.X; x < slot.X + 2; x++)
                {
                    Assert.False(CityLayout.IsReservedForStreet(x, y),
                        $"půdorys z {slot.X},{slot.Y} sahá na ulici {x},{y}");
                }
            }
        });
    }
}
