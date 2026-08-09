using CivDle.Core.Config;
using CivDle.Core.Content;
using CivDle.Core.Sim;
using CivDle.Core.Tests.Support;
using CivDle.Core.World;
using Xunit;

namespace CivDle.Core.Tests.Sim;

/// <summary>
/// Šablony zástavby (bod 44): hráč si uloží kus města a postaví ho znovu.
///
/// <para>Není to automatika — hráč pořád vybírá místo a platí plnou cenu.
/// Ušetří se klikání, ne suroviny.</para>
/// </summary>
public class TemplateTests
{
    private static Simulation Grass(out GameContent content)
    {
        content = TestData.LoadRealContent();
        return new Simulation(content, new UniformTerrain(content.Biomes.IndexOf("grassland")));
    }

    /// <summary>
    /// Zajistí silnici na dlaždici. Stavba budovy si sama táhne napojení, takže
    /// tam cesta často už je — a druhý pokus by vrátil <c>Occupied</c>.
    /// </summary>
    private static void Road(Simulation sim, int x, int y)
    {
        if (!sim.HasRoadAt(x, y))
        {
            Assert.Equal(PlacementResult.Ok, sim.TryBuildRoad(x, y));
        }
    }

    private static void TopUp(Simulation sim, GameContent content)
    {
        for (int i = 0; i < content.Resources.Count; i++)
        {
            sim.AddResource(i, sim.GetStorageCap(i));
        }
    }

    [Fact]
    public void CaptureTakesBuildingsAndRoadsFromTheRectangle()
    {
        var sim = Grass(out var content);
        int house = content.Buildings.IndexOf("house");
        TopUp(sim, content);

        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuilding(house, 10, 10));
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuilding(house, 12, 10));
        Road(sim, 11, 10);

        var template = TemplateTool.Capture(sim, content, "blok", 10, 10, 12, 12);

        Assert.Equal("blok", template.Name);
        Assert.Equal(2, template.Buildings.Count);

        // Souřadnice jsou RELATIVNÍ k rohu výběru, jinak by šablona šla položit
        // jedině zpátky na původní místo.
        Assert.Contains(template.Buildings, p => p is { Dx: 0, Dy: 0 });
        Assert.Contains(template.Buildings, p => p is { Dx: 2, Dy: 0 });
        Assert.Contains((1, 0), template.Roads);
    }

    [Fact]
    public void CaptureIgnoresWhatLiesOutsideTheRectangle()
    {
        var sim = Grass(out var content);
        int house = content.Buildings.IndexOf("house");
        TopUp(sim, content);
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuilding(house, 10, 10));
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuilding(house, 40, 40));

        var template = TemplateTool.Capture(sim, content, "roh", 9, 9, 12, 12);

        Assert.Single(template.Buildings);
    }

    [Fact]
    public void PlacingRebuildsTheSameShapeElsewhere()
    {
        var sim = Grass(out var content);
        int house = content.Buildings.IndexOf("house");
        TopUp(sim, content);
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuilding(house, 10, 10));
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuilding(house, 12, 10));
        Road(sim, 11, 10);

        var template = TemplateTool.Capture(sim, content, "blok", 10, 10, 12, 12);
        TopUp(sim, content);

        Assert.Equal(2, TemplateTool.Place(sim, content, template, 30, 30));

        Assert.True(sim.TryGetBuildingAt(30, 30, out _));
        Assert.True(sim.TryGetBuildingAt(32, 30, out _));
        Assert.True(sim.HasRoadAt(31, 30));
    }

    [Fact]
    public void PlacingCostsTheFullPrice()
    {
        // Šablona šetří klikání, ne suroviny. Kdyby stavěla zadarmo, byla by to
        // cheat a ne pohodlí.
        var sim = Grass(out var content);
        int house = content.Buildings.IndexOf("house");
        int planks = content.Resources.IndexOf("planks");
        TopUp(sim, content);
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuilding(house, 10, 10));

        var template = TemplateTool.Capture(sim, content, "dům", 10, 10, 11, 11);
        TopUp(sim, content);
        double before = sim.GetResource(planks);

        Assert.Equal(1, TemplateTool.Place(sim, content, template, 30, 30));

        Assert.True(sim.GetResource(planks) < before, "za postavený dům se má zaplatit");
    }

    [Fact]
    public void OccupiedGroundSkipsJustThatPiece()
    {
        var sim = Grass(out var content);
        int house = content.Buildings.IndexOf("house");
        TopUp(sim, content);
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuilding(house, 10, 10));
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuilding(house, 12, 10));

        var template = TemplateTool.Capture(sim, content, "blok", 10, 10, 12, 12);
        TopUp(sim, content);

        // Na cílovém místě už jeden dům stojí — druhý se má postavit stejně.
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuilding(house, 30, 30));
        TopUp(sim, content);

        Assert.Equal(1, TemplateTool.Place(sim, content, template, 30, 30));
        Assert.True(sim.TryGetBuildingAt(32, 30, out _));
    }

    [Fact]
    public void PreviewCountsWhatWouldFit()
    {
        var sim = Grass(out var content);
        int house = content.Buildings.IndexOf("house");
        TopUp(sim, content);
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuilding(house, 10, 10));
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuilding(house, 12, 10));
        var template = TemplateTool.Capture(sim, content, "blok", 10, 10, 12, 12);

        Assert.Equal(2, TemplateTool.CountPlaceable(sim, content, template, 30, 30));
        Assert.Equal(1, TemplateTool.CountPlaceable(sim, content, template, 8, 10)); // překryv s originálem
    }

    [Fact]
    public void ATemplateSurvivesTheTripThroughTheProfile()
    {
        // Šablony se ukládají do profilu hráče, takže musí přežít serializaci
        // — a hlavně Vzestup a novou hru.
        var sim = Grass(out var content);
        int house = content.Buildings.IndexOf("house");
        TopUp(sim, content);
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuilding(house, 10, 10));
        Road(sim, 11, 10);

        var template = TemplateTool.Capture(sim, content, "blok", 10, 10, 12, 12);

        string path = Path.Combine(AppContext.BaseDirectory, "tmp-profile", Guid.NewGuid().ToString("N"), "profile.json");
        try
        {
            var store = new ProfileStore(path);
            var profile = new PlayerProfile();
            profile.Templates.Add(template.ToSaved());
            store.Save(profile);

            var loaded = store.Load();
            Assert.Single(loaded.Templates);

            var roundTripped = loaded.Templates[0].ToTemplate();
            Assert.Equal(template.Name, roundTripped.Name);
            Assert.Equal(template.Buildings.Count, roundTripped.Buildings.Count);
            Assert.Equal(template.Roads.Count, roundTripped.Roads.Count);
            Assert.Equal(template.Buildings[0].BuildingId, roundTripped.Buildings[0].BuildingId);
        }
        finally
        {
            string? directory = Path.GetDirectoryName(path);
            if (directory is not null && Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public void AVanishedBuildingIsSkipped_NotACrash()
    {
        // Šablona přežije i vypnutý mod nebo přejmenovanou budovu v datech.
        var sim = Grass(out var content);
        var template = new BuildTemplate(
            "duch",
            new[] { new TemplatePart("neexistujici_budova", 0, 0) },
            Array.Empty<(int, int)>());
        TopUp(sim, content);

        Assert.Equal(0, TemplateTool.Place(sim, content, template, 20, 20));
        Assert.Equal(0, TemplateTool.CountPlaceable(sim, content, template, 20, 20));
    }
}
