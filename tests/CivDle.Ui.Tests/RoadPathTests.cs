using CivDle.Core.Content;
using CivDle.Core.Sim;
using CivDle.Core.World;
using CivDle.Screens;
using Xunit;

namespace CivDle.Ui.Tests;

/// <summary>
/// Trasa tažené silnice — a hlavně to, že o překážce v cestě ví <b>dřív</b>,
/// než hráč pustí tlačítko.
///
/// <para>Tohle hlásil hráč: táhl silnici přes terén, kde se stavět nedá, celá
/// trasa svítila zeleně, a po puštění se nepostavilo nic. To vypadá úplně
/// stejně jako rozbitý nástroj.</para>
/// </summary>
public class RoadPathTests
{
    [Fact]
    public void OnClearGroundEveryTileIsBuildable()
    {
        var (sim, _) = World();
        var path = new List<RoadGhostTile>();

        int usable = RoadPath.Trace(sim, erasing: false, 0, 0, 5, 0, path);

        Assert.Equal(6, path.Count);
        Assert.Equal(6, usable);
        Assert.All(path, tile => Assert.True(tile.Ok));
    }

    [Fact]
    public void ATileWithABuildingOnItComesBackRed()
    {
        var (sim, content) = World();
        int house = content.Buildings.IndexOf("house");
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuildingFree(house, 3, 0));

        var path = new List<RoadGhostTile>();
        int usable = RoadPath.Trace(sim, erasing: false, 0, 0, 5, 0, path);

        Assert.Equal(6, path.Count);
        Assert.Equal(5, usable);
        Assert.False(path.Single(tile => tile.X == 3 && tile.Y == 0).Ok);
    }

    [Fact]
    public void OverWaterNothingIsBuildable()
    {
        // Voda je ten případ, na který hráč narazil: přes zátoku se táhne
        // stejně ochotně jako přes louku a nevznikne ani dlaždice.
        var content = LoadContent();
        var sim = new Simulation(content, new UniformTerrain(WaterBiome(content)));
        var path = new List<RoadGhostTile>();

        int usable = RoadPath.Trace(sim, erasing: false, 0, 0, 4, 0, path);

        Assert.Equal(5, path.Count);
        Assert.Equal(0, usable);
        Assert.All(path, tile => Assert.False(tile.Ok));
    }

    [Fact]
    public void ErasingCountsOnlyTilesThatActuallyHaveARoad()
    {
        var (sim, _) = World();
        Assert.Equal(PlacementResult.Ok, sim.TryBuildRoad(1, 0));
        Assert.Equal(PlacementResult.Ok, sim.TryBuildRoad(2, 0));

        var path = new List<RoadGhostTile>();
        int usable = RoadPath.Trace(sim, erasing: true, 0, 0, 4, 0, path);

        Assert.Equal(5, path.Count);
        Assert.Equal(2, usable);
    }

    [Fact]
    public void ThePathBendsOnceAndCoversEveryStep()
    {
        // Lomená cesta, ne schody po úhlopříčce — a bez děr, jinak by tažení
        // nechalo v ulici mezery.
        var (sim, _) = World();
        var path = new List<RoadGhostTile>();

        RoadPath.Trace(sim, erasing: false, 0, 0, 3, 2, path);

        Assert.Equal(6, path.Count); // 4 vodorovně + 2 svisle
        for (int i = 1; i < path.Count; i++)
        {
            int step = Math.Abs(path[i].X - path[i - 1].X) + Math.Abs(path[i].Y - path[i - 1].Y);
            Assert.Equal(1, step);
        }
    }

    private static (Simulation Sim, GameContent Content) World()
    {
        var content = LoadContent();
        var sim = new Simulation(content, new UniformTerrain(content.Biomes.IndexOf("grassland")));
        sim.SkipTutorial();
        sim.DebugFillStorages();
        return (sim, content);
    }

    private static int WaterBiome(GameContent content)
    {
        for (int i = 0; i < content.Biomes.Count; i++)
        {
            if (content.Biomes[i].IsWater)
            {
                return i;
            }
        }

        Assert.Fail("obsah nemá vodní biom");
        return -1;
    }

    private static GameContent LoadContent() =>
        new ContentLoader().LoadFrom(Path.Combine(AppContext.BaseDirectory, "data"));
}
