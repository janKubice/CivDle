using CivDle.Core.Content;
using CivDle.Core.Sim;
using CivDle.Core.Tests.Support;
using CivDle.Core.World;
using Xunit;

namespace CivDle.Core.Tests.Sim;

/// <summary>
/// Dosah napojení na silnici.
///
/// <para>Tady byla chyba, kvůli které hráč třikrát po sobě hlásil „silnice se
/// nestaví". Vlna „napojeno" se šířila zástavbou bez omezení, takže v souvislém
/// městě stačila <b>jedna</b> dlaždice cesty a všech osm set budov se tvářilo
/// jako napojených. Automat pak neměl co spravovat a nepoložil ani metr —
/// u hromadné stavby, tažení i guvernéra.</para>
/// </summary>
public class RoadLinkSpreadTests
{
    private static Simulation Grass(out GameContent content)
    {
        content = TestData.LoadRealContent();
        return new Simulation(content, new UniformTerrain(content.Biomes.IndexOf("grassland")));
    }

    private static void TopUp(Simulation sim, GameContent content)
    {
        for (int i = 0; i < content.Resources.Count; i++)
        {
            sim.AddResource(i, sim.GetStorageCap(i));
        }
    }

    /// <summary>Zajistí dlaždici cesty; automat ji tam mohl položit sám.</summary>
    private static void EnsureRoad(Simulation sim, int x, int y)
    {
        if (!sim.HasRoadAt(x, y))
        {
            Assert.Equal(PlacementResult.Ok, sim.TryBuildRoad(x, y));
        }
    }

    /// <summary>
    /// Postaví řadu domů od (x,y) doprava. Místa, kam se dům nevejde, se
    /// přeskočí — automat mezitím mohl položit ulici, a to je správně.
    /// </summary>
    private static void Row(Simulation sim, GameContent content, int x, int y, int count)
    {
        int house = content.Buildings.IndexOf("house");
        for (int i = 0; i < count; i++)
        {
            TopUp(sim, content);
            sim.TryPlaceBuilding(house, x + i, y);
        }
    }

    [Fact]
    public void OneRoadTileDoesNotConnectAWholeStreet()
    {
        // Jádro chyby: dlaždice u prvního domu nesmí napojit dům o dvacet dál.
        var sim = Grass(out var content);
        Row(sim, content, 0, 0, 20);

        // Automat mohl mezitím něco položit; test se ptá na dosah, ne na počet.
        for (int i = 0; i < 20; i++)
        {
            sim.TryRemoveRoad(i, -1);
            sim.TryRemoveRoad(i, 1);
        }

        EnsureRoad(sim, 0, -1); // ulice u prvního domu

        Assert.True(sim.TryGetBuildingAt(0, 0, out int first));
        Assert.True(sim.TryGetBuildingAt(19, 0, out int last));

        Assert.True(sim.IsBuildingConnected(first), "dům u ulice napojený je");
        Assert.False(sim.IsBuildingConnected(last),
            "dům o dvacet dál nesmí být napojený jednou dlaždicí na druhém konci");
    }

    [Fact]
    public void ABlockOfRowHousesSharesTheStreet()
    {
        // Druhá strana téže mince: dlažba mezi každými dvěma domy je nesmysl,
        // z města by byla šachovnice. Pár domů od ulice napojených být musí.
        var sim = Grass(out var content);
        Row(sim, content, 0, 0, 3);
        for (int i = 0; i < 3; i++)
        {
            sim.TryRemoveRoad(i, -1);
            sim.TryRemoveRoad(i, 1);
        }

        EnsureRoad(sim, 0, -1);

        Assert.True(sim.TryGetBuildingAt(2, 0, out int third));
        Assert.True(sim.IsBuildingConnected(third), "třetí dům v bloku se k ulici dostane přes sousedy");
    }

    [Fact]
    public void WithoutAnyRoadEverybodyCounts()
    {
        // První chalupa nemá k čemu se připojit; trestat ji by hráče jen mátlo.
        var sim = Grass(out var content);
        Row(sim, content, 0, 0, 3);
        foreach (var tile in sim.RoadTiles.ToList())
        {
            sim.TryRemoveRoad(tile.X, tile.Y);
        }

        Assert.True(sim.TryGetBuildingAt(2, 0, out int third));
        Assert.True(sim.IsBuildingConnected(third));
    }

    [Fact]
    public void ABigBlockGetsRoadsInsteadOfOneToken()
    {
        // Integrační pojistka: velká souvislá čtvrť má mít víc než jednu ulici.
        var sim = Grass(out var content);
        int house = content.Buildings.IndexOf("house");

        for (int y = 0; y < 12; y++)
        {
            for (int x = 0; x < 12; x++)
            {
                TopUp(sim, content);
                sim.TryPlaceBuilding(house, x, y);
            }
        }

        Assert.True(sim.RoadTiles.Count >= 8,
            $"čtvrť o {sim.Buildings.Length} domech má jen {sim.RoadTiles.Count} dlaždic cesty");
    }
}
