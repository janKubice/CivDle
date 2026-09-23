using CivDle.Core.Content;
using CivDle.Core.Sim;
using CivDle.Core.Tests.Support;
using CivDle.Core.World;
using Xunit;

namespace CivDle.Core.Tests.Sim;

/// <summary>
/// Plný sklad zastaví výrobnu, která něco spotřebovává. Dřív pila dál pálila
/// dřevo na prkna, která rovnou propadla — a město pak nemělo dřevo na dům.
/// </summary>
public class OutputFullTests
{
    private const int Wood = 0;
    private const int Planks = 1;
    private const int Science = 2;

    private static Resource[] Resources(double planksCap = 50) => new[]
    {
        new Resource("wood", new RgbColor(140, 90, 40), StartAmount: 0, BaseStorage: 1000),
        new Resource("planks", new RgbColor(200, 160, 90), StartAmount: 0, BaseStorage: planksCap),
        new Resource("science", new RgbColor(90, 120, 220), StartAmount: 0, BaseStorage: 50),
    };

    /// <summary>
    /// Bez růstu i bez jídla: v testovacím obsahu je „jídlem" surovina 0 (tady
    /// dřevo), takže by lidé jedli přesně to, co pila nemá spotřebovat.
    /// </summary>
    private static GameplayConfig NoGrowth => TestContent.DefaultGameplay with
    {
        PopulationGrowthPerSecond = 0.0,
        FoodPerPersonPerSecond = 0.0,
    };

    private static Simulation Build(params BuildingDef[] buildings)
    {
        var content = TestContent.Build(resources: Resources(), buildings: buildings, gameplay: NoGrowth);
        var sim = new Simulation(content, new UniformTerrain((byte)1));
        for (int i = 0; i < buildings.Length; i++)
        {
            Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuilding(i, i * 2, 0));
        }

        return sim;
    }

    private static void Run(Simulation sim, int ticks)
    {
        for (int i = 0; i < ticks; i++)
        {
            sim.Tick();
        }
    }

    [Fact]
    public void Converter_WithFullOutput_KeepsItsInput()
    {
        var sawmill = TestContent.Converter("sawmill", Wood, 3, Planks, 1, timeTicks: 10);
        var sim = Build(sawmill);
        sim.DebugSetResource(Wood, 300);
        sim.DebugSetResource(Planks, 50); // plno

        Run(sim, 200);

        Assert.Equal(300, sim.GetResource(Wood), 6);
        Assert.Equal(BuildingStall.OutputFull, sim.Buildings[0].Stall);
    }

    [Fact]
    public void Converter_ResumesAsSoonAsThereIsRoom()
    {
        var sawmill = TestContent.Converter("sawmill", Wood, 3, Planks, 1, timeTicks: 10);
        var sim = Build(sawmill);
        sim.DebugSetResource(Wood, 300);
        sim.DebugSetResource(Planks, 50);
        Run(sim, 50);

        sim.DebugSetResource(Planks, 40); // někdo prkna spotřeboval
        Run(sim, 50);

        Assert.True(sim.GetResource(Wood) < 300, "pila se po uvolnění skladu nerozjela");
        Assert.NotEqual(BuildingStall.OutputFull, sim.Buildings[0].Stall);
    }

    [Fact]
    public void RawProducer_KeepsRunningIntoFullStorage()
    {
        // Těžba z ničeho nic nespotřebovává — přebytek smí propadat (idle konvence).
        var camp = TestContent.Producer("camp", Planks, 2, timeTicks: 10);
        var sim = Build(camp);
        sim.DebugSetResource(Planks, 50);

        Run(sim, 100);

        Assert.Equal(BuildingStall.None, sim.Buildings[0].Stall);
        Assert.True(sim.Ledger.WastedPerSecond(Planks) > 0, "propad plného skladu se přestal evidovat");
    }

    [Fact]
    public void MultiOutput_RunsWhileAnyOutputFits()
    {
        var monastery = new BuildingDef(
            "monastery", "test", new RgbColor(1, 2, 3), 1, 1,
            WorkerSlots: 1, HousingCapacity: 0, BuildCost: Array.Empty<ResourceAmount>(),
            Recipe: new Recipe(
                new[] { new ResourceAmount(Wood, 1) },
                new[] { new ResourceAmount(Planks, 1), new ResourceAmount(Science, 1) },
                10),
            AllowedBiomes: new[] { true, true }, StorageBonus: Array.Empty<ResourceAmount>(),
            AutoBuild: false, Buildable: true, UpgradesToIndex: -1,
            UpgradeCost: Array.Empty<ResourceAmount>(), PowerSupply: 0, PowerDemand: 0);
        var sim = Build(monastery);
        sim.DebugSetResource(Wood, 100);
        sim.DebugSetResource(Planks, 50); // prkna plná, věda ne

        Run(sim, 100);

        Assert.True(sim.GetResource(Science) > 0, "výstup, který se vejde, se přestal vyrábět");
    }

    [Fact]
    public void PowerPlant_KeepsBurningFuelWithFullByproduct()
    {
        // Jaderná elektrárna: uran → věda + proud. Plná věda nesmí zhasnout síť.
        var plant = TestContent.Converter("reactor", Wood, 1, Science, 1, timeTicks: 10, powerSupply: 50);
        var sim = Build(plant);
        sim.DebugSetResource(Wood, 100);
        sim.DebugSetResource(Science, 50);

        Run(sim, 100);

        Assert.True(sim.GetResource(Wood) < 100, "elektrárna přestala pálit palivo kvůli plnému skladu vedlejšího produktu");
        Assert.NotEqual(BuildingStall.OutputFull, sim.Buildings[0].Stall);
    }
}
