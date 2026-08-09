using CivDle.Core.Content;
using CivDle.Core.Save;
using CivDle.Core.Sim;
using CivDle.Core.Tests.Support;
using CivDle.Core.World;
using Xunit;

namespace CivDle.Core.Tests.Sim;

/// <summary>
/// Slučování bloků 2×2: čtyři stejné budovy ve čtverci se spojí v jednu velkou.
/// Hlídá se, že se blok najde z kteréhokoli rohu, že se nesloučí pomíchaná
/// čtveřice ani nedostavěný čtverec, a že se bonusy přepočítají správně
/// (jinak by po sloučení „zmizelo" bydlení nebo pracovní místa).
/// </summary>
public class MergeTests
{
    private static readonly Resource[] Planks =
    {
        new("planks", new RgbColor(200, 170, 110), StartAmount: 500, BaseStorage: 10_000),
    };

    /// <summary>Malý dům (1×1, bydlení 4) a jeho sloučená podoba (2×2, bydlení 20).</summary>
    private static GameContent MergeContent(bool unlockTarget = true)
    {
        var biomes = new[] { TestContent.WaterBiome(), TestContent.LandBiome("grass") };
        var small = new BuildingDef(
            "hut", "housing", new RgbColor(180, 100, 60), 1, 1,
            WorkerSlots: 0, HousingCapacity: 4,
            BuildCost: new[] { new ResourceAmount(0, 1) },
            Recipe: null,
            AllowedBiomes: new[] { false, true },
            StorageBonus: Array.Empty<ResourceAmount>(),
            AutoBuild: false, Buildable: true,
            UpgradesToIndex: -1, UpgradeCost: Array.Empty<ResourceAmount>(),
            PowerSupply: 0, PowerDemand: 0,
            MergesToIndex: 1,
            MergeCostOrNull: new[] { new ResourceAmount(0, 10) });
        var big = new BuildingDef(
            "manor", "housing", new RgbColor(200, 130, 80), 2, 2,
            WorkerSlots: 0, HousingCapacity: 20,
            BuildCost: new[] { new ResourceAmount(0, 1) },
            Recipe: null,
            AllowedBiomes: new[] { false, true },
            StorageBonus: Array.Empty<ResourceAmount>(),
            AutoBuild: false,
            Buildable: false, // jen přes sloučení
            UpgradesToIndex: -1, UpgradeCost: Array.Empty<ResourceAmount>(),
            PowerSupply: 0, PowerDemand: 0);

        // Cíl se odemyká technologií; bez ní se slučovat nesmí.
        var techs = unlockTarget
            ? new[] { new TechDef("planning", new[] { new ResourceAmount(0, 1) }, Array.Empty<int>(), new[] { 1 }, string.Empty, 0) }
            : Array.Empty<TechDef>();

        return TestContent.Build(
            biomes: biomes, resources: Planks, buildings: new[] { small, big }, techs: techs);
    }

    private static Simulation WithBlock(GameContent content, bool research = true)
    {
        var sim = new Simulation(content, new UniformTerrain((byte)1));
        if (research && content.Techs.Count > 0)
        {
            sim.TryResearch(0);
        }

        foreach (var (x, y) in new[] { (4, 4), (5, 4), (4, 5), (5, 5) })
        {
            Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuilding(0, x, y));
        }

        return sim;
    }

    [Theory]
    [InlineData(4, 4)]
    [InlineData(5, 4)]
    [InlineData(4, 5)]
    [InlineData(5, 5)]
    public void BlockIsFound_FromAnyOfItsFourTiles(int x, int y)
    {
        var sim = WithBlock(MergeContent());

        Assert.True(sim.TryFindMergeGroup(x, y, out var group));
        Assert.Equal(4, group.X);
        Assert.Equal(4, group.Y);
    }

    [Fact]
    public void Merge_ReplacesFourBuildingsWithOne()
    {
        var content = MergeContent();
        var sim = WithBlock(content);

        Assert.Equal(PlacementResult.Ok, sim.TryMerge(4, 4));

        Assert.Single(sim.Buildings.ToArray());
        Assert.Equal(1, sim.Buildings[0].DefIndex);
        Assert.Equal(4, sim.Buildings[0].X);
        Assert.Equal(4, sim.Buildings[0].Y);
        Assert.Equal(1, sim.MergedBuildings);
    }

    [Fact]
    public void Merge_RecalculatesHousing()
    {
        var content = MergeContent();
        var sim = WithBlock(content);
        double baseHousing = content.Gameplay.BaseHousingCapacity;

        Assert.Equal(baseHousing + 4 * 4, sim.HousingCapacity); // čtyři chatrče
        sim.TryMerge(4, 4);

        // Sloučená budova musí nahradit bydlení čtyř, ne se k němu přičíst.
        Assert.Equal(baseHousing + 20, sim.HousingCapacity);
    }

    [Fact]
    public void Merge_ChargesTheTopUp()
    {
        var content = MergeContent();
        var sim = WithBlock(content);
        double before = sim.GetResource(0);

        sim.TryMerge(4, 4);

        Assert.Equal(before - 10, sim.GetResource(0));
    }

    [Fact]
    public void Merge_OccupiesTheWholeSquare()
    {
        var sim = WithBlock(MergeContent());
        sim.TryMerge(4, 4);

        foreach (var (x, y) in new[] { (4, 4), (5, 4), (4, 5), (5, 5) })
        {
            Assert.True(sim.TryGetBuildingAt(x, y, out int index), $"dlaždice {x},{y} má patřit sloučené budově");
            Assert.Equal(0, index);
        }
    }

    [Fact]
    public void IncompleteSquare_DoesNotMerge()
    {
        var content = MergeContent();
        var sim = new Simulation(content, new UniformTerrain((byte)1));
        sim.TryResearch(0);
        sim.TryPlaceBuilding(0, 4, 4);
        sim.TryPlaceBuilding(0, 5, 4);
        sim.TryPlaceBuilding(0, 4, 5); // čtvrtý roh chybí

        Assert.False(sim.TryFindMergeGroup(4, 4, out _));
        Assert.Equal(PlacementResult.Occupied, sim.TryMerge(4, 4));
        Assert.Equal(3, sim.Buildings.Length);
    }

    [Fact]
    public void WithoutResearch_MergeIsLocked()
    {
        // Blok je celý, ale cílová budova není odemčená — slučovat nejde.
        var sim = WithBlock(MergeContent(), research: false);

        Assert.True(sim.TryFindMergeGroup(4, 4, out _));
        Assert.Equal(PlacementResult.NotUnlocked, sim.TryMerge(4, 4));
        Assert.Equal(4, sim.Buildings.Length);
    }

    [Fact]
    public void WithoutResources_MergeIsRefused()
    {
        var content = MergeContent();
        var sim = new Simulation(content, new UniformTerrain((byte)1));
        sim.TryResearch(0);
        foreach (var (x, y) in new[] { (4, 4), (5, 4), (4, 5), (5, 5) })
        {
            sim.TryPlaceBuilding(0, x, y);
        }

        // Prkna se utratí stavěním dalších chatrčí (mimo blok), ať zbyde míň než
        // doplatek za sloučení. Simulace nemá „vezmi mi suroviny", a je to tak
        // správně — testy mají hrát hru, ne obcházet pravidla.
        //
        // Místo, kam se dům nevejde, se přeskočí: jakmile osada povyroste,
        // začne automat dláždit ulice a nějakou dlaždici si vezme. To je
        // správné chování, ne chyba — test počítá jen skutečně postavené domy.
        int spent = 0;
        for (int slot = 0; slot < 4000 && sim.GetResource(0) >= 10; slot++)
        {
            if (sim.TryPlaceBuilding(0, 20 + slot % 50, 20 + slot / 50) == PlacementResult.Ok)
            {
                spent++;
            }
        }

        Assert.Equal(PlacementResult.NotEnoughResources, sim.TryMerge(4, 4));
        Assert.Equal(4 + spent, sim.Buildings.Length);
    }

    [Fact]
    public void SaveRoundtrip_KeepsMergedBuildingAndCounter()
    {
        var content = MergeContent();
        var sim = WithBlock(content);
        sim.TryMerge(4, 4);

        var stream = new MemoryStream();
        new SaveGameSerializer().Write(stream, sim, new SaveMetadata(1, "s", "test", DateTime.UtcNow));
        stream.Position = 0;
        var (loaded, _) = new SaveGameSerializer().Read(stream, content);

        Assert.Single(loaded.Buildings.ToArray());
        Assert.Equal(1, loaded.Buildings[0].DefIndex);
        Assert.Equal(sim.MergedBuildings, loaded.MergedBuildings);
        Assert.Equal(sim.HousingCapacity, loaded.HousingCapacity);
    }

    [Fact]
    public void MergedMetric_FeedsGoals()
    {
        var sim = WithBlock(MergeContent());
        sim.TryMerge(4, 4);

        Assert.Equal(1, sim.EvaluateMetric(MetricKind.MergedBuildings, -1));
    }

    [Fact]
    public void Merge_IsNotAnnouncedAsAMilestone()
    {
        // Guvernér slučuje sám a pořád dokola. Jako milník to hráči po každém
        // intervalu přehodilo přes obrazovku oslavný pruh — proto má sloučení
        // vlastní druh, který se v UI ukáže jen jako řádek v seznamu.
        var sim = WithBlock(MergeContent());
        while (sim.TryDequeueNotification(out _))
        {
            // stavba bloku si taky něco vyrobila; zajímá nás až sloučení
        }

        Assert.Equal(PlacementResult.Ok, sim.TryMerge(4, 4));

        Assert.True(sim.TryDequeueNotification(out var note));
        Assert.Equal(NotificationKind.BuildingMerged, note.Kind);
        Assert.Equal("toast.merged", note.TitleKey);
    }
}
