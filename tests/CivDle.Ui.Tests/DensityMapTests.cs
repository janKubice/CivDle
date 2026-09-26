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

    // ----- město z výšky (endgame.md, B4) -----

    [Fact]
    public void WithoutExtrasTheBakeIsExactlyWhatItWas()
    {
        // Regrese: základní pečení se rozšířením nezměnilo ani o pixel.
        var (sim, house) = Scene();
        for (int x = 0; x < 5; x++)
        {
            sim.TryPlaceBuildingFree(house, x, 1);
        }

        var (counts, day, night, scratch) = Buffers();
        Assert.True(DensityMap.BakeInto(sim, 0, 0, counts, day, night, scratch));

        for (int i = 0; i < counts.Length; i++)
        {
            Assert.Equal(DensityMap.DayColorFor(counts[i]), day[i]);
            Assert.Equal(DensityMap.NightColorFor(counts[i]), night[i]);
        }
    }

    [Fact]
    public void ADistrictCellWearsItsDistrictColour_OrItsStyle()
    {
        var (sim, content) = ResidentialDistrict();
        var extras = new BakeExtras(content.Districts);
        var (counts, day, night, scratch) = Buffers();

        Assert.True(DensityMap.BakeInto(sim, 0, 0, counts, day, night, scratch, extras));
        int cell = BusiestCell(counts);
        Assert.NotEqual(DensityMap.DayColorFor(counts[cell]), day[cell]);
        var plain = day[cell];

        int residential = content.Districts.Types.IndexOf("residential");
        int brick = IndexOfStyle(content, "brick");
        Assert.True(sim.SetDistrictStyle(residential, brick));
        DensityMap.BakeInto(sim, 0, 0, counts, day, night, scratch, extras);

        Assert.NotEqual(plain, day[cell]);
    }

    [Fact]
    public void DenseCellsCastShadowOnTheirLeeSide()
    {
        var (sim, house) = Scene();
        // Plná buňka (1,1); buňka (2,2) za ní ve směru od slunce je prázdná.
        for (int y = 6; y < 12; y++)
        {
            for (int x = 6; x < 12; x++)
            {
                sim.TryPlaceBuildingFree(house, x, y);
            }
        }

        var extras = new BakeExtras(CivDle.Core.Content.DistrictCatalog.Empty);
        var (counts, day, night, scratch) = Buffers();
        DensityMap.BakeInto(sim, 0, 0, counts, day, night, scratch, extras);

        int dense = 1 * DensityMap.ChunkCells + 1;
        int lee = 2 * DensityMap.ChunkCells + 2;
        Assert.True(extras.Relief[dense].A > 0, "hustá buňka má mít světlou hranu");
        Assert.True(extras.Relief[lee].A > 0 && extras.Relief[lee].R == 0, "za ní má ležet stín");
    }

    [Fact]
    public void RoadsOutsideTheCityDrawTheArterials()
    {
        var (sim, house) = Scene();
        sim.TryPlaceBuildingFree(house, 0, 0);
        for (int x = 30; x < 60; x++)
        {
            sim.AddRoadTileForTest(x, 3);
        }

        var extras = new BakeExtras(CivDle.Core.Content.DistrictCatalog.Empty);
        foreach (var tile in sim.RoadTiles)
        {
            long key = CivDle.Core.World.TileKey.Pack(DensityMap.FloorDiv(tile.X, DensityMap.CellTiles), DensityMap.FloorDiv(tile.Y, DensityMap.CellTiles));
            extras.RoadCells[key] = extras.RoadCells.GetValueOrDefault(key) + 1;
        }

        var (counts, day, night, scratch) = Buffers();
        DensityMap.BakeInto(sim, 0, 0, counts, day, night, scratch, extras);

        Assert.Equal(0, counts[6]);
        Assert.NotEqual(Color.Transparent, day[6]); // buňka x = 36…41 s cestou mimo zástavbu
    }

    [Fact]
    public void SettlementCentresGlowAtNight()
    {
        var (sim, content) = ResidentialDistrict();
        Assert.NotEmpty(sim.Settlements);
        var (counts, day, night, scratch) = Buffers();
        DensityMap.BakeInto(sim, 0, 0, counts, day, night, scratch);
        var plain = (Color[])night.Clone();

        DensityMap.BakeInto(sim, 0, 0, counts, day, night, scratch, new BakeExtras(content.Districts));

        int brighter = 0;
        for (int i = 0; i < night.Length; i++)
        {
            Assert.True(night[i].A >= plain[i].A);
            if (night[i].A > plain[i].A)
            {
                brighter++;
            }
        }

        Assert.True(brighter > 0, "střed sídla má v noci zářit víc");
    }

    /// <summary>Obytná čtvrť: blok domů, odtikáno, až ji systém čtvrtí i sídel najde.</summary>
    private static (Simulation Sim, CivDle.Core.Content.GameContent Content) ResidentialDistrict()
    {
        var content = new ContentLoader().LoadFrom(Path.Combine(AppContext.BaseDirectory, "data"));
        var sim = new Simulation(content, new UniformTerrain(content.Biomes.IndexOf("grassland")));
        sim.SkipTutorial();
        int house = content.Buildings.IndexOf("house");
        for (int y = 1; y < 6; y++)
        {
            for (int x = 1; x < 6; x++)
            {
                sim.TryPlaceBuildingFree(house, x, y);
            }
        }

        for (int i = 0; i < 200; i++)
        {
            sim.Tick();
        }

        Assert.NotEmpty(sim.Districts);
        return (sim, content);
    }

    private static int BusiestCell(int[] counts)
    {
        int best = 0;
        for (int i = 1; i < counts.Length; i++)
        {
            if (counts[i] > counts[best])
            {
                best = i;
            }
        }

        return best;
    }

    private static int IndexOfStyle(CivDle.Core.Content.GameContent content, string id)
    {
        for (int i = 0; i < content.Districts.Styles.Count; i++)
        {
            if (content.Districts.Styles[i].Id == id)
            {
                return i;
            }
        }

        return -1;
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
