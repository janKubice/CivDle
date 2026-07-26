using CivDle.Core.Content;
using CivDle.Core.Save;
using CivDle.Core.Sim;
using CivDle.Core.Tests.Support;
using CivDle.Core.World;
using Xunit;

namespace CivDle.Core.Tests.Sim;

/// <summary>
/// Návštěvy UFO: mapa dělá věci sama od sebe. Testuje se, že zásah opravdu nastane,
/// že proběhne nejvýš JEDNOU za okno (i přes save/load) a že žádné chování neshodí
/// simulaci do nesmyslného stavu (záporná populace, budova ve vodě).
/// </summary>
public sealed class UfoTests
{
    /// <summary>UFO přiletí v každém okně (chance 1) a hned zasáhne — krátká okna, ať se dá odtikat.</summary>
    private static UfoConfig Always(string behavior, double magnitude) => new(
        WindowSeconds: 2.0,
        Chance: 1.0,
        VisitSeconds: 1.0,
        Radius: 2,
        Actions: new[] { new UfoActionDef(behavior, behavior, 1.0, magnitude) });

    [Fact]
    public void Abduction_TakesPeopleButNeverBelowZero()
    {
        var sim = NewSim(Always("abduct", magnitude: 1_000_000));
        double before = sim.Population;

        TickSeconds(sim, 5);

        Assert.True(sim.Population < before, "Únos nikoho neunesl.");
        Assert.True(sim.Population >= 0, "Populace nesmí spadnout pod nulu.");
    }

    [Fact]
    public void BeamHouse_RemovesABuilding()
    {
        var sim = NewSim(Always("demolish", magnitude: 1));
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuilding(0, 0, 0));
        int before = sim.Buildings.Length;

        TickSeconds(sim, 5);

        Assert.True(sim.Buildings.Length < before, "Paprsek žádnou budovu nesestřelil.");
    }

    [Fact]
    public void CropCircle_LeavesSomethingToHarvest()
    {
        var sim = NewSim(Always("plant", magnitude: 5));

        TickSeconds(sim, 5);

        bool planted = false;
        for (int y = -4; y <= 4 && !planted; y++)
        {
            for (int x = -4; x <= 4; x++)
            {
                if (sim.TryGetPlantedNode(x, y, out _))
                {
                    planted = true;
                    break;
                }
            }
        }

        Assert.True(planted, "Kruh v obilí nic nezasadil.");
    }

    [Fact]
    public void Terraform_ChangesTheGroundButKeepsLandAsLand()
    {
        // Dva pevninské biomy — terraformace přemaluje souš na jinou souš, ne na vodu.
        var content = UfoContent(Always("terraform", magnitude: 5), new[]
        {
            TestContent.WaterBiome(),
            TestContent.LandBiome("grass"),
            TestContent.LandBiome("sand"),
        });
        var sim = new Simulation(content, new UniformTerrain(1));
        byte before = sim.BiomeAt(0, 0);

        TickSeconds(sim, 5);

        bool changed = false;
        for (int y = -4; y <= 4 && !changed; y++)
        {
            for (int x = -4; x <= 4; x++)
            {
                if (sim.BiomeAt(x, y) != before)
                {
                    changed = true;
                    Assert.False(content.Biomes[sim.BiomeAt(x, y)].IsWater,
                        "Terraformace nesmí ze souše udělat vodu — utopila by město.");
                    break;
                }
            }
        }

        Assert.True(changed, "Terraformace nic nezměnila.");
    }

    [Fact]
    public void Gift_AddsResources()
    {
        var sim = NewSim(Always("gift", magnitude: 100));
        double before = sim.GetResource(0);

        TickSeconds(sim, 5);

        Assert.True(sim.GetResource(0) > before, "Dárek od UFO nic nepřinesl.");
    }

    /// <summary>Zásah smí proběhnout jen jednou za okno, jinak by UFO město vysálo.</summary>
    [Fact]
    public void OneVisit_AppliesItsEffectExactlyOnce()
    {
        // Dlouhé okno, krátká návštěva — po zásahu zbývá spousta času na to,
        // aby se případný druhý zásah v tomtéž okně projevil.
        var sim = NewSim(new UfoConfig(WindowSeconds: 30, Chance: 1.0, VisitSeconds: 1.0, Radius: 2,
            Actions: new[] { new UfoActionDef("abduct", "abduct", 1.0, 1) }));

        TickSeconds(sim, 3); // doletí a zasáhne
        double afterFirst = sim.Population;

        TickSeconds(sim, 10); // pořád stejné okno
        Assert.Equal(afterFirst, sim.Population, 6);
    }

    /// <summary>Po načtení savu se zásah z už vyřízeného okna nesmí provést podruhé.</summary>
    [Fact]
    public void SaveRoundtrip_DoesNotReplayTheSameVisit()
    {
        var content = UfoContent(new UfoConfig(WindowSeconds: 30, Chance: 1.0, VisitSeconds: 1.0, Radius: 2,
            Actions: new[] { new UfoActionDef("abduct", "abduct", 1.0, 1) }));
        var sim = new Simulation(content, new UniformTerrain(1));
        TickSeconds(sim, 3);
        double afterVisit = sim.Population;

        var stream = new MemoryStream();
        new SaveGameSerializer().Write(stream, sim, new SaveMetadata(7, "s", "test", DateTime.UtcNow));
        stream.Position = 0;
        var (loaded, _) = new SaveGameSerializer().Read(stream, content);

        Assert.Equal(afterVisit, loaded.Population, 6);
        loaded.Tick();
        Assert.Equal(afterVisit, loaded.Population, 6);
    }

    [Fact]
    public void DisabledUfo_NeverShowsUp()
    {
        var sim = NewSim(UfoConfig.Disabled);
        double before = sim.Population;

        TickSeconds(sim, 30);

        Assert.False(sim.IsUfoVisible);
        Assert.Equal(before, sim.Population, 6);
    }

    private static void TickSeconds(Simulation sim, int seconds)
    {
        int ticks = (int)(Simulation.TicksPerSecond * seconds);
        for (int i = 0; i < ticks; i++)
        {
            sim.Tick();
        }
    }

    private static Simulation NewSim(UfoConfig ufo) => new(UfoContent(ufo), new UniformTerrain(1));

    /// <summary>Obsah bez růstu populace a bez hladu — do výsledku pak mluví jen UFO.</summary>
    private static GameContent UfoContent(UfoConfig ufo, IReadOnlyList<Biome>? biomes = null) => TestContent.Build(
        biomes: biomes,
        gameplay: TestContent.DefaultGameplay with
        {
            PopulationGrowthPerSecond = 0,
            FoodPerPersonPerSecond = 0,
        },
        ufo: ufo);
}
