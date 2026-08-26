using CivDle.Core.Content;
using CivDle.Core.Sim;
using CivDle.Core.World;
using CivDle.Rendering;
using Microsoft.Xna.Framework;
using Xunit;

namespace CivDle.Ui.Tests;

/// <summary>
/// Hustotní mapa: zastavěnost upečená do textur místo tisíců obdélníčků.
///
/// <para>Textury bez grafického zařízení nevzniknou, takže se testuje to, co
/// je na tom rozhodnutí o hře, ne o kreslení: <b>co</b> se v kusu mapy má
/// objevit. A hlavně dělení záporných souřadnic — mapa je nekonečná oběma
/// směry a <c>/</c> u záporných čísel zaokrouhluje k nule, takže by celé
/// město za počátkem skončilo o kus vedle.</para>
/// </summary>
public class DensityMapTests
{
    [Fact]
    public void FloorDiv_RoundsDownEvenBehindTheOrigin()
    {
        Assert.Equal(0, DensityMap.FloorDiv(5, 6));
        Assert.Equal(1, DensityMap.FloorDiv(6, 6));
        Assert.Equal(-1, DensityMap.FloorDiv(-1, 6));
        Assert.Equal(-1, DensityMap.FloorDiv(-6, 6));
        Assert.Equal(-2, DensityMap.FloorDiv(-7, 6));
    }

    [Fact]
    public void AnEmptyPieceOfMapIsNotBaked()
    {
        var (sim, _) = Scene();
        var (counts, day, night, scratch) = Buffers();

        Assert.False(DensityMap.BakeInto(sim, 0, 0, counts, day, night, scratch));
    }

    [Fact]
    public void BuildingsLandInTheirOwnCell()
    {
        var (sim, house) = Scene();

        // Dvě budovy v jedné buňce (buňka = 6 dlaždic), jedna vedle.
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuildingFree(house, 0, 0));
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuildingFree(house, 1, 1));
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuildingFree(house, 6, 0));

        var (counts, day, night, scratch) = Buffers();
        Assert.True(DensityMap.BakeInto(sim, 0, 0, counts, day, night, scratch));

        Assert.Equal(2, counts[0]);
        Assert.Equal(1, counts[1]);
        Assert.Equal(0, counts[2]);
    }

    [Fact]
    public void ACityBehindTheOriginIsBakedIntoItsOwnPiece()
    {
        // Přesně ta chyba, kterou hlídá FloorDiv: budova na −1 patří do kusu
        // vlevo nahoře, ne do toho na počátku.
        var (sim, house) = Scene();
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuildingFree(house, -1, -1));

        var (counts, day, night, scratch) = Buffers();

        Assert.False(DensityMap.BakeInto(sim, 0, 0, counts, day, night, scratch));
        Assert.True(DensityMap.BakeInto(sim, -1, -1, counts, day, night, scratch));

        // Poslední buňka kusu — hned za rohem počátku.
        Assert.Equal(1, counts[^1]);
    }

    [Fact]
    public void DenserCellsAreBrighter_AndEmptyOnesAreInvisible()
    {
        Assert.Equal(Color.Transparent, DensityMap.DayColorFor(0));
        Assert.Equal(Color.Transparent, DensityMap.NightColorFor(0));

        Assert.True(DensityMap.NightColorFor(10).A > DensityMap.NightColorFor(1).A);
        Assert.True(DensityMap.DayColorFor(10).A > DensityMap.DayColorFor(1).A);
    }

    [Fact]
    public void NightBrightnessComesOnlyFromDensity()
    {
        // Jediná buňka s jedním domem musí být v noci sotva vidět. Pevná složka
        // by z okraje města udělala ostrý světlý obdélník.
        int faint = DensityMap.NightColorFor(1).A;
        int full = DensityMap.NightColorFor(10).A;

        Assert.True(faint < full / 2, $"jeden dům svítí {faint}, plná buňka {full}");
    }

    [Fact]
    public void RebuildingIsDrivenByTheBuildingRevision_NotTheBuildingCount()
    {
        // Zbourat jednu budovu a postavit jinou nechá počet stejný. Kdyby se
        // mapa řídila počtem, ukazovala by město, které už nestojí.
        var (sim, house) = Scene();
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuildingFree(house, 0, 0));

        long before = sim.BuildingRevision;
        int count = sim.Buildings.Length;

        Assert.Equal(PlacementResult.Ok, sim.TryDemolish(0));
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuildingFree(house, 20, 20));

        Assert.Equal(count, sim.Buildings.Length);
        Assert.NotEqual(before, sim.BuildingRevision);
    }

    private static (int[] Counts, Color[] Day, Color[] Night, List<int> Scratch) Buffers()
    {
        int cells = DensityMap.ChunkCells * DensityMap.ChunkCells;
        return (new int[cells], new Color[cells], new Color[cells], new List<int>());
    }

    private static (Simulation Sim, int House) Scene()
    {
        var content = new ContentLoader().LoadFrom(Path.Combine(AppContext.BaseDirectory, "data"));
        var sim = new Simulation(content, new UniformTerrain(content.Biomes.IndexOf("grassland")));
        sim.SkipTutorial();
        return (sim, content.Buildings.IndexOf("house"));
    }
}
