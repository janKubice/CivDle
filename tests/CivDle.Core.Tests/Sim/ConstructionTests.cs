using CivDle.Core.Content;
using CivDle.Core.Save;
using CivDle.Core.Sim;
using CivDle.Core.Tests.Support;
using CivDle.Core.World;
using Xunit;

namespace CivDle.Core.Tests.Sim;

/// <summary>
/// Divy světa se staví v čase: megastruktura, která vyroste jedním kliknutím,
/// je jen drahá budova. S odpočtem je z ní událost.
///
/// <para>Klíčové pravidlo, které testy hlídají: rozestavěná budova NIC nedává.
/// Kdyby bonusy platily hned, byl by odpočet jen kosmetika.</para>
/// </summary>
public class ConstructionTests
{
    private const int Wood = 0;

    /// <summary>Doba stavby v ticích; násobek intervalu stavebního systému.</summary>
    private const int BuildTicks = 100;

    private static GameContent Content()
    {
        var biomes = new[] { TestContent.WaterBiome(), TestContent.LandBiome("grass") };
        var resources = new[] { new Resource("wood", new RgbColor(1, 1, 1), StartAmount: 1000, BaseStorage: 1_000_000) };

        var wonder = new BuildingDef(
            "wonder", "megastructure", new RgbColor(200, 160, 90), 2, 2,
            WorkerSlots: 1, HousingCapacity: 50,
            BuildCost: new[] { new ResourceAmount(Wood, 10) },
            Recipe: new Recipe(
                Inputs: Array.Empty<ResourceAmount>(),
                Outputs: new[] { new ResourceAmount(Wood, 10) },
                TimeTicks: 1),
            AllowedBiomes: new[] { false, true },
            StorageBonus: new[] { new ResourceAmount(Wood, 5000) },
            AutoBuild: false, Buildable: true, UpgradesToIndex: -1,
            UpgradeCost: Array.Empty<ResourceAmount>(), PowerSupply: 0, PowerDemand: 0,
            BuildTicks: BuildTicks);

        var hut = wonder with
        {
            Id = "hut",
            HousingCapacity = 5,
            StorageBonus = Array.Empty<ResourceAmount>(),
            BuildTicks = 0,
        };

        var gameplay = TestContent.DefaultGameplay with
        {
            FoodPerPersonPerSecond = 0,
            PopulationGrowthPerSecond = 0,
        };
        return TestContent.Build(biomes, 1, resources, new[] { wonder, hut }, gameplay);
    }

    private static Simulation NewSim() => new(Content(), new UniformTerrain(1));

    [Fact]
    public void WonderStartsAsAConstructionSite()
    {
        var sim = NewSim();
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuilding(0, 0, 0));

        Assert.False(sim.Buildings[0].IsComplete);
        Assert.Equal(1, sim.BuildingsUnderConstruction);
        Assert.Equal(0, sim.ConstructionProgress01(0), 3);
    }

    [Fact]
    public void UnfinishedWonder_GivesNothing()
    {
        // Tohle je celý smysl odpočtu: kdyby bonusy platily hned, byla by to kosmetika.
        var sim = NewSim();
        int housingBefore = sim.HousingCapacity;
        long slotsBefore = sim.TotalWorkerSlots;
        double storageBefore = sim.GetStorageCap(Wood);

        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuilding(0, 0, 0));

        Assert.Equal(housingBefore, sim.HousingCapacity);
        Assert.Equal(slotsBefore, sim.TotalWorkerSlots);
        Assert.Equal(storageBefore, sim.GetStorageCap(Wood), 3);
    }

    [Fact]
    public void UnfinishedWonder_ProducesNothing()
    {
        var sim = NewSim();
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuilding(0, 0, 0));
        double before = sim.GetResource(Wood);

        for (int i = 0; i < BuildTicks / 2; i++)
        {
            sim.Tick();
        }

        Assert.Equal(before, sim.GetResource(Wood), 3);
    }

    [Fact]
    public void FinishedWonder_TurnsOnAndPaysOff()
    {
        var sim = NewSim();
        int housingBefore = sim.HousingCapacity;
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuilding(0, 0, 0));

        RunUntilBuilt(sim);

        Assert.True(sim.Buildings[0].IsComplete);
        Assert.Equal(0, sim.BuildingsUnderConstruction);
        Assert.Equal(1.0, sim.ConstructionProgress01(0), 3);
        Assert.True(sim.HousingCapacity > housingBefore, "dostavěný div má konečně dát bydlení");
        Assert.Equal(1, sim.WondersCompleted);

        double before = sim.GetResource(Wood);
        sim.Tick();
        Assert.True(sim.GetResource(Wood) > before, "dostavěný div má vyrábět");
    }

    [Fact]
    public void ProgressGrowsWhileBuilding()
    {
        var sim = NewSim();
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuilding(0, 0, 0));

        for (int i = 0; i < BuildTicks / 2; i++)
        {
            sim.Tick();
        }

        double half = sim.ConstructionProgress01(0);
        Assert.InRange(half, 0.4, 0.6);
    }

    [Fact]
    public void FinishingAWonder_TellsThePlayer()
    {
        // Dokončení je událost, ne tichá změna čísla — jinak si ho hráč nevšimne.
        var sim = NewSim();
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuilding(0, 0, 0));
        while (sim.TryDequeueNotification(out _))
        {
        }

        RunUntilBuilt(sim);

        bool announced = false;
        while (sim.TryDequeueNotification(out var note))
        {
            announced |= note.TitleKey == "toast.wonderDone";
        }

        Assert.True(announced, "dostavěný div se má ohlásit");
    }

    [Fact]
    public void DemolishingAConstructionSite_LeavesNoGhostBehind()
    {
        // Zrušené staveniště nesmí zůstat viset v počítadle, jinak by stavební
        // systém nadarmo procházel celé město až do konce hry.
        var sim = NewSim();
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuilding(0, 0, 0));
        int housingBefore = sim.HousingCapacity;

        Assert.Equal(PlacementResult.Ok, sim.TryDemolish(0));

        Assert.Equal(0, sim.BuildingsUnderConstruction);
        Assert.Equal(housingBefore, sim.HousingCapacity); // nic se neodečetlo, nic nebylo připsáno
    }

    [Fact]
    public void OrdinaryBuildings_StillGoUpInstantly()
    {
        // Doba stavby je vlastnost divů, ne nové pravidlo pro celou hru.
        var sim = NewSim();
        int housingBefore = sim.HousingCapacity;

        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuilding(1, 5, 5));

        Assert.True(sim.Buildings[0].IsComplete);
        Assert.Equal(0, sim.BuildingsUnderConstruction);
        Assert.True(sim.HousingCapacity > housingBefore);
    }

    [Fact]
    public void HalfBuiltWonder_SurvivesSaveAndLoad()
    {
        // Rozestavěný div se po restartu nesmí tvářit jako hotový (bonusy zdarma)
        // ani se vrátit na začátek. Odpočet proto jde do savu ve vlastní sekci.
        var sim = NewSim();
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuilding(0, 0, 0));
        for (int i = 0; i < BuildTicks / 2; i++)
        {
            sim.Tick();
        }

        int remaining = sim.Buildings[0].BuildTicksRemaining;
        Assert.True(remaining > 0);

        var stream = new MemoryStream();
        new SaveGameSerializer().Write(stream, sim, new SaveMetadata(1, "s", "test", DateTime.UnixEpoch));
        stream.Position = 0;
        var (loaded, _) = new SaveGameSerializer().Read(stream, Content());

        Assert.Equal(remaining, loaded.Buildings[0].BuildTicksRemaining);
        Assert.False(loaded.Buildings[0].IsComplete);
        Assert.Equal(1, loaded.BuildingsUnderConstruction);

        // A hlavně: pořád nic nedává.
        Assert.Equal(sim.HousingCapacity, loaded.HousingCapacity);
        Assert.Equal(sim.GetStorageCap(Wood), loaded.GetStorageCap(Wood), 3);
    }

    [Fact]
    public void RealContent_MakesMegastructuresTakeTime()
    {
        var content = TestData.LoadRealContent();
        int timed = 0;
        foreach (var def in content.Buildings.All)
        {
            if (def.Category != "megastructure")
            {
                continue;
            }

            Assert.True(def.TakesTimeToBuild, $"megastruktura '{def.Id}' vyroste okamžitě");
            timed++;

            // Minuty, ne hodiny: div, na který se čeká přes celé sezení, je otrava.
            double minutes = def.BuildTicks / Simulation.TicksPerSecond / 60.0;
            Assert.InRange(minutes, 1.0, 30.0);
        }

        Assert.True(timed >= 5, $"divů světa má být víc, je jich {timed}");
    }

    private static void RunUntilBuilt(Simulation sim)
    {
        for (int i = 0; i < BuildTicks + Simulation.ConstructionIntervalTicks * 2; i++)
        {
            sim.Tick();
            if (sim.Buildings[0].IsComplete)
            {
                return;
            }
        }

        Assert.Fail("div se nedostavěl");
    }
}
