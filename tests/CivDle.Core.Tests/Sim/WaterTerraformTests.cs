using CivDle.Core.Content;
using CivDle.Core.Save;
using CivDle.Core.Sim;
using CivDle.Core.Tests.Support;
using CivDle.Core.World;
using Xunit;

namespace CivDle.Core.Tests.Sim;

/// <summary>
/// Teraformace vody: hráč má mít nad světem plnou kontrolu — udělat jezero,
/// koryto řeky i moře, a stejně tak vodu vzít zpátky.
///
/// <para>Loader to dřív zakazoval („cílem nesmí být vodní biom"), takže se dala
/// krajina jen vysoušet. Zákaz padl a město místo něj hlídá simulace: pod
/// budovou ani cestou se nekope, takže si hráč město nezatopí omylem.</para>
///
/// <para>Mechanika se testuje na syntetickém obsahu (systém v izolaci),
/// skutečná data pak zvlášť — že žebřík od souše do hlubiny a zpátky opravdu
/// navazuje.</para>
/// </summary>
public class WaterTerraformTests
{
    private const int Water = 0;
    private const int Land = 1;

    private static readonly Resource[] Stone =
    {
        new("stone", new RgbColor(140, 140, 140), StartAmount: 1000, BaseStorage: 10_000),
    };

    /// <summary>Dvojice zásahů: zatopit souš a vysušit ji zpátky. Bez odemykání.</summary>
    private static GameContent WaterTools()
    {
        var hut = new BuildingDef(
            "hut", "housing", new RgbColor(180, 100, 60), 1, 1,
            WorkerSlots: 0, HousingCapacity: 4,
            BuildCost: new[] { new ResourceAmount(0, 1) },
            Recipe: null,
            AllowedBiomes: new[] { false, true },
            StorageBonus: Array.Empty<ResourceAmount>(),
            AutoBuild: false, Buildable: true,
            UpgradesToIndex: -1, UpgradeCost: Array.Empty<ResourceAmount>(),
            PowerSupply: 0, PowerDemand: 0);

        var terraform = new[]
        {
            new TerraformDef("flood", Water, new[] { Land }, new[] { new ResourceAmount(0, 10) }, -1),
            new TerraformDef("reclaim", Land, new[] { Water }, new[] { new ResourceAmount(0, 20) }, -1),
        };

        // Souš s vytěžitelným hájem — ať je vidět, že zatopení uzel spolkne.
        var grove = TestContent.LandBiome("grass") with
        {
            ClickYield = new ClickYield(0, Amount: 1, Charges: 5, RegrowSeconds: 600),
        };

        return TestContent.Build(
            biomes: new[] { TestContent.WaterBiome(), grove },
            resources: Stone,
            buildings: new[] { hut },
            terraform: terraform);
    }

    private static Simulation OnLand(GameContent content) =>
        new(content, new UniformTerrain((byte)Land));

    [Fact]
    public void Flood_TurnsDryLandIntoWater()
    {
        var content = WaterTools();
        var sim = OnLand(content);

        Assert.False(sim.IsWaterAt(10, 10));
        Assert.Equal(PlacementResult.Ok, sim.TryTerraform(content.Terraform.IndexOf("flood"), 10, 10));

        Assert.True(sim.IsWaterAt(10, 10));
    }

    [Fact]
    public void Flood_ChargesAndCounts()
    {
        var content = WaterTools();
        var sim = OnLand(content);
        double before = sim.GetResource(0);
        long terraformed = sim.TerraformedTiles;

        sim.TryTerraform(content.Terraform.IndexOf("flood"), 10, 10);

        Assert.Equal(before - 10, sim.GetResource(0));
        Assert.Equal(terraformed + 1, sim.TerraformedTiles);
    }

    [Fact]
    public void Flood_IsRefusedUnderABuilding()
    {
        // Tohle je ta pojistka, kvůli které smí být cílem voda: město se
        // nezatopí omylem, protože se pod budovou nekope.
        var content = WaterTools();
        var sim = OnLand(content);
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuilding(0, 10, 10));

        Assert.Equal(PlacementResult.Occupied, sim.CanTerraform(content.Terraform.IndexOf("flood"), 10, 10));
        Assert.False(sim.IsWaterAt(10, 10));
    }

    [Fact]
    public void Flood_IsRefusedUnderARoad()
    {
        var content = WaterTools();
        var sim = OnLand(content);
        Assert.Equal(PlacementResult.Ok, sim.TryBuildRoad(10, 10));

        Assert.Equal(PlacementResult.Occupied, sim.CanTerraform(content.Terraform.IndexOf("flood"), 10, 10));
    }

    [Fact]
    public void Flood_TakesTheHarvestNodeWithIt()
    {
        // Uzel pod hladinou by dál nabízel těžbu uprostřed jezera.
        var content = WaterTools();
        var sim = OnLand(content);
        Assert.True(sim.NodeChargesLeft(12, 12) > 0, "na souši má co těžit");

        Assert.Equal(PlacementResult.Ok, sim.TryTerraform(content.Terraform.IndexOf("flood"), 12, 12));

        Assert.Equal(0, sim.NodeChargesLeft(12, 12));
    }

    [Fact]
    public void Reclaim_TakesTheWaterBack()
    {
        var content = WaterTools();
        var sim = OnLand(content);
        int flood = content.Terraform.IndexOf("flood");
        int reclaim = content.Terraform.IndexOf("reclaim");

        sim.TryTerraform(flood, 10, 10);
        Assert.Equal(PlacementResult.Ok, sim.TryTerraform(reclaim, 10, 10));

        Assert.False(sim.IsWaterAt(10, 10));
    }

    [Fact]
    public void RepeatingTheSameToolOnTheSameTileIsRefused()
    {
        var content = WaterTools();
        var sim = OnLand(content);
        int flood = content.Terraform.IndexOf("flood");
        sim.TryTerraform(flood, 10, 10);

        Assert.Equal(PlacementResult.WrongBiome, sim.CanTerraform(flood, 10, 10));
    }

    [Fact]
    public void ADugRiverSurvivesSaving()
    {
        var content = WaterTools();
        var sim = OnLand(content);
        int flood = content.Terraform.IndexOf("flood");
        for (int x = 20; x < 26; x++)
        {
            Assert.Equal(PlacementResult.Ok, sim.TryTerraform(flood, x, 40));
        }

        var stream = new MemoryStream();
        new SaveGameSerializer().Write(stream, sim, new SaveMetadata(sim.Seed, "s", "test", DateTime.UtcNow));
        stream.Position = 0;
        var (loaded, _) = new SaveGameSerializer().Read(stream, content);

        for (int x = 20; x < 26; x++)
        {
            Assert.True(loaded.IsWaterAt(x, 40), $"koryto na {x},40 se ze savu nevrátilo");
        }
    }

    // ----- skutečná data -----

    [Fact]
    public void RealContent_OffersTheWholeWaterLadder()
    {
        var content = TestData.LoadRealContent();

        foreach (string id in new[] { "flood", "dredge", "deepen", "shoal", "silt", "reclaim", "break_ice" })
        {
            Assert.True(content.Terraform.TryIndexOf(id, out _), $"chybí teraformace '{id}'");
        }
    }

    [Fact]
    public void RealContent_TheLadderLinksUpBothWays()
    {
        // Souš → mělčina → moře → hlubina a zpátky. Kdyby některý článek
        // navazoval na jiný biom, zůstal by hráč stát uprostřed žebříku.
        var content = TestData.LoadRealContent();

        void Links(string action, string from, string to)
        {
            var def = content.Terraform[content.Terraform.IndexOf(action)];
            Assert.True(def.AppliesTo(content.Biomes.IndexOf(from)),
                $"'{action}' neumí vyjít z '{from}'");
            Assert.Equal(content.Biomes.IndexOf(to), def.TargetBiomeIndex);
        }

        Links("flood", "grassland", "shallow_water");
        Links("dredge", "shallow_water", "ocean");
        Links("deepen", "ocean", "deep_ocean");
        Links("shoal", "deep_ocean", "ocean");
        Links("silt", "ocean", "shallow_water");
        Links("reclaim", "shallow_water", "grassland");
    }

    [Fact]
    public void RealContent_WaterToolsAreLockedBehindResearch()
    {
        // Přetvářet moře nesmí jít od začátku hry — je to konec žebříku, ne start.
        var content = TestData.LoadRealContent();
        var sim = new Simulation(content, new UniformTerrain(content.Biomes.IndexOf("grassland")));
        for (int i = 0; i < content.Resources.Count; i++)
        {
            sim.AddResource(i, sim.GetStorageCap(i));
        }

        foreach (string id in new[] { "flood", "dredge", "deepen", "shoal", "silt", "reclaim", "break_ice" })
        {
            var def = content.Terraform[content.Terraform.IndexOf(id)];
            Assert.True(def.UnlockTechIndex >= 0, $"'{id}' není za technologií");
            Assert.Equal(PlacementResult.NotUnlocked, sim.CanTerraform(content.Terraform.IndexOf(id), 5, 5));
        }
    }
}
