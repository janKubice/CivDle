using CivDle.Core.Content;
using CivDle.Core.Sim;
using CivDle.Core.World;
using CivDle.Rendering;
using Xunit;

namespace CivDle.Ui.Tests;

/// <summary>
/// Inspektor úzkých hrdel — pravidlo, jakou barvu která budova dostane.
///
/// <para>Je to rozhodnutí o hře, ne o kreslení: co se počítá jako úzké hrdlo
/// a co má přednost, když jich je víc naráz. Proto se to testuje bez okna.</para>
/// </summary>
public class StallOverlayTests
{
    [Fact]
    public void AWorkingBuildingIsGreen()
    {
        var (sim, content) = World();
        var building = Instance(content, "lumber_camp", BuildingStall.None);

        var color = StallOverlayRenderer.ColorFor(sim, building, Def(content, "lumber_camp"));

        Assert.Equal(StallOverlayRenderer.Legend[0].Color, color);
    }

    [Theory]
    [InlineData(BuildingStall.MissingInput, 1)]
    [InlineData(BuildingStall.NoWorkers, 2)]
    [InlineData(BuildingStall.NoTerrain, 3)]
    [InlineData(BuildingStall.UnderConstruction, 5)]
    public void EveryReasonHasItsOwnColour(BuildingStall stall, int legendSlot)
    {
        // Kdyby dvě příčiny sdílely barvu, hráč by z mapy nepoznal, co řešit.
        var (sim, content) = World();
        var building = Instance(content, "lumber_camp", stall);

        var color = StallOverlayRenderer.ColorFor(sim, building, Def(content, "lumber_camp"));

        Assert.Equal(StallOverlayRenderer.Legend[legendSlot].Color, color);
    }

    [Fact]
    public void AllLegendColoursDiffer()
    {
        // Legenda o šesti položkách je k ničemu, když dvě vypadají stejně.
        var colors = StallOverlayRenderer.Legend.Select(entry => entry.Color).ToList();

        Assert.Equal(colors.Count, colors.Distinct().Count());
    }

    [Fact]
    public void AFullStoreIsItsOwnWarning()
    {
        // Budova nestojí, ale vyrábí naprázdno. Ze samotné simulace se to
        // nepozná — plný sklad výrobu nezastaví, jen se přebytek ztrácí.
        var (sim, content) = World();
        for (int i = 0; i < sim.ResourceCount; i++)
        {
            sim.AddResource(i, sim.GetStorageCap(i));
        }

        var building = Instance(content, "lumber_camp", BuildingStall.None);
        var color = StallOverlayRenderer.ColorFor(sim, building, Def(content, "lumber_camp"));

        Assert.Equal(StallOverlayRenderer.Legend[4].Color, color);
    }

    [Fact]
    public void AStallBeatsAFullStore()
    {
        // Budova bez vstupu není "ucpaná skladem", i kdyby byl sklad plný —
        // řešení je jiné a barva to musí říct správně.
        var (sim, content) = World();
        for (int i = 0; i < sim.ResourceCount; i++)
        {
            sim.AddResource(i, sim.GetStorageCap(i));
        }

        var building = Instance(content, "sawmill", BuildingStall.MissingInput);
        var color = StallOverlayRenderer.ColorFor(sim, building, Def(content, "sawmill"));

        Assert.Equal(StallOverlayRenderer.Legend[1].Color, color);
    }

    [Fact]
    public void ABuildingWithoutOutputNeverLooksClogged()
    {
        // Dům ani park nic nevyrábějí, takže se ucpat nemůžou. Bez téhle
        // výjimky by při plných skladech zežloutlo celé město.
        var (sim, content) = World();
        for (int i = 0; i < sim.ResourceCount; i++)
        {
            sim.AddResource(i, sim.GetStorageCap(i));
        }

        var building = Instance(content, "house", BuildingStall.None);
        var color = StallOverlayRenderer.ColorFor(sim, building, Def(content, "house"));

        Assert.Equal(StallOverlayRenderer.Legend[0].Color, color);
    }

    [Fact]
    public void LegendSlotAgreesWithTheColour()
    {
        // Počty v legendě a barvy na mapě musí plynout z jednoho pravidla.
        var (sim, content) = World();
        var building = Instance(content, "lumber_camp", BuildingStall.NoWorkers);

        int slot = StallOverlayRenderer.LegendSlot(sim, building, Def(content, "lumber_camp"));

        Assert.Equal(
            StallOverlayRenderer.ColorFor(sim, building, Def(content, "lumber_camp")),
            StallOverlayRenderer.Legend[slot].Color);
    }

    private static (Simulation Sim, GameContent Content) World()
    {
        var content = new ContentLoader().LoadFrom(Path.Combine(AppContext.BaseDirectory, "data"));
        var sim = new Simulation(content, new UniformTerrain(content.Biomes.IndexOf("grassland")));
        return (sim, content);
    }

    private static BuildingDef Def(GameContent content, string id)
    {
        Assert.True(content.Buildings.TryIndexOf(id, out int index), id);
        return content.Buildings[index];
    }

    private static BuildingInstance Instance(GameContent content, string id, BuildingStall stall)
    {
        Assert.True(content.Buildings.TryIndexOf(id, out int index), id);
        return new BuildingInstance { DefIndex = index, X = 0, Y = 0, Stall = stall };
    }
}
