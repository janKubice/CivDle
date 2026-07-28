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
    public void PlacingManyBuildingsOverARoadNetwork_DoesNotCrash()
    {
        // Regrese: cache napojení na silnice se zneplatňovala až PO tom, co se jí
        // RoadBuilder zeptal na právě položenou budovu. Jakmile pole budov mezitím
        // narostlo, sáhlo se za jeho konec a hra při stavbě spadla. Chytilo to až
        // měření balancu na delším běhu — proto se tady staví hodně a daleko od sebe,
        // ať pole víckrát zdvojnásobí kapacitu a síť silnic mezitím vznikne.
        var sim = GrassSim(out var content);
        int house = content.Buildings.IndexOf("house");

        // Jedna výrobna na začátku: jen ta se každý tik ptá, jestli je napojená
        // na silnici, a právě tím prohlásí cache za čerstvou. Bez ní by skulina,
        // ve které se chybovalo, vůbec nevznikla.
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuildingFree(content.Buildings.IndexOf("farm"), 1, 1));

        // Placenou cestou, ne zdarma: silnice staví jen TryPlaceBuilding, a bez
        // silnic by test neprošel zrovna tam, kde chyba byla. Sklad se doplňuje
        // před každou stavbou — jde o síť, ne o ekonomiku.
        for (int i = 0; i < 200; i++)
        {
            for (int r = 0; r < content.Resources.Count; r++)
            {
                sim.AddResource(r, sim.GetStorageCap(r));
            }

            int x = i % 20 * 3;
            int y = i / 20 * 3;
            Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuilding(house, x, y));

            // Tik mezi stavbami je to podstatné: výroba se ptá na napojení, čímž
            // cache prohlásí za čerstvou. Další stavba pak pole budov zvětší —
            // a přesně v té skulině se dřív sahalo za jeho konec.
            sim.Tick();
        }

        Assert.Equal(201, sim.Buildings.Length);
        Assert.NotEmpty(sim.RoadTiles); // síť opravdu vznikla, test tedy testoval, co měl

        // A odpověď na „je napojená?" musí dávat smysl pro každou budovu, ne padat.
        for (int i = 0; i < sim.Buildings.Length; i++)
        {
            sim.IsBuildingConnected(i);
        }
    }

    [Fact]
    public void RoadNetwork_NeverFormsPavedPatches()
    {
        // Dlažební placka 2×2 dělá z ulice parkoviště. Auto-silnice se jí vyhýbá.
        var sim = GrassSim(out var content);
        int house = content.Buildings.IndexOf("house");

        // Rozházená zástavba: hodně napojování, hodně příležitostí udělat placku.
        for (int i = 0; i < 60; i++)
        {
            for (int r = 0; r < content.Resources.Count; r++)
            {
                sim.AddResource(r, sim.GetStorageCap(r));
            }

            int x = (i * 7) % 23;
            int y = (i * 11) % 19;
            sim.TryPlaceBuilding(house, x, y);
        }

        Assert.NotEmpty(sim.RoadTiles);
        Assert.Empty(PavedPatches(sim));
    }

    /// <summary>Levé horní rohy všech souvislých ploch silnice 2×2.</summary>
    private static List<(int X, int Y)> PavedPatches(Simulation sim)
    {
        var found = new List<(int X, int Y)>();
        foreach (var tile in sim.RoadTiles)
        {
            if (sim.IsRoad(tile.X + 1, tile.Y)
                && sim.IsRoad(tile.X, tile.Y + 1)
                && sim.IsRoad(tile.X + 1, tile.Y + 1))
            {
                found.Add((tile.X, tile.Y));
            }
        }

        return found;
    }

    [Fact]
    public void ConnectingATown_PrefersStraightStreets()
    {
        // Stejně dlouhá cesta jde vést jako schodiště nebo jako ulice se zatáčkou.
        // Chceme to druhé — proto se počítají zatáčky, ne délka.
        var sim = GrassSim(out var content);
        int house = content.Buildings.IndexOf("house");

        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuildingFree(house, 0, 0));
        for (int r = 0; r < content.Resources.Count; r++)
        {
            sim.AddResource(r, sim.GetStorageCap(r));
        }

        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuilding(house, 9, 7));

        int turns = CountTurns(sim);
        Assert.True(turns <= 3, $"cesta má být ulice s pár zatáčkami, ne schodiště ({turns} zatáček)");
    }

    /// <summary>Kolikrát síť mění směr — schodiště jich má tolik co dlaždic.</summary>
    private static int CountTurns(Simulation sim)
    {
        int turns = 0;
        foreach (var tile in sim.RoadTiles)
        {
            bool horizontal = sim.IsRoad(tile.X - 1, tile.Y) || sim.IsRoad(tile.X + 1, tile.Y);
            bool vertical = sim.IsRoad(tile.X, tile.Y - 1) || sim.IsRoad(tile.X, tile.Y + 1);
            if (horizontal && vertical)
            {
                turns++;
            }
        }

        return turns;
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
