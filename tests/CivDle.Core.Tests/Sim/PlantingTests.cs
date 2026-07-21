using CivDle.Core.Save;
using CivDle.Core.Sim;
using CivDle.Core.Tests.Support;
using CivDle.Core.World;
using Xunit;

namespace CivDle.Core.Tests.Sim;

/// <summary>
/// Sázení obnovitelných zdrojů: zaplatí se, vznikne uzel, který jde těžit jako
/// přírodní, dvakrát na totéž místo nejde, a přežije to save (v8).
/// </summary>
public class PlantingTests
{
    [Fact]
    public void Plant_CostsResources_CreatesHarvestableNode()
    {
        var sim = new Simulation(TestContent.Build(), new UniformTerrain((byte)1));
        double woodBefore = sim.GetResource(0);

        Assert.Equal(PlacementResult.Ok, sim.CanPlant(2, 2));
        Assert.Equal(PlacementResult.Ok, sim.TryPlant(2, 2));
        Assert.Equal(woodBefore - 5, sim.GetResource(0)); // cena 5

        Assert.True(sim.TryGetPlantedNode(2, 2, out int resource));
        Assert.Equal(0, resource);

        Assert.True(sim.TryHarvest(2, 2, out int harvested, out int amount));
        Assert.Equal(0, harvested);
        Assert.Equal(2, amount); // výnos zasazeného háje

        Assert.Equal(PlacementResult.Occupied, sim.CanPlant(2, 2)); // uzel už tam je
    }

    [Fact]
    public void SaveRoundtrip_KeepsPlantedNode()
    {
        var content = TestContent.Build();
        var sim = new Simulation(content, new UniformTerrain((byte)1));
        sim.TryPlant(3, 3);

        var metadata = new SaveMetadata(1, "s", "test", System.DateTime.UtcNow);
        var stream = new MemoryStream();
        new SaveGameSerializer().Write(stream, sim, metadata);
        stream.Position = 0;
        var (loaded, _) = new SaveGameSerializer().Read(stream, content);

        Assert.True(loaded.TryGetPlantedNode(3, 3, out _));
    }
}
