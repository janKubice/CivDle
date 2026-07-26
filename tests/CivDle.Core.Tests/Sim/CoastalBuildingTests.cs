using CivDle.Core.Content;
using CivDle.Core.Sim;
using CivDle.Core.Tests.Support;
using CivDle.Core.World;
using Xunit;

namespace CivDle.Core.Tests.Sim;

/// <summary>
/// Pobřežní budovy (přístav, rybolov): musí sousedit s vodou. Tím dostává pobřeží
/// ekonomickou identitu — u moře či řeky se dá dělat něco, co ve vnitrozemí ne.
/// </summary>
public class CoastalBuildingTests
{
    /// <summary>Souš všude, kromě svislého vodního pásu na x = 0.</summary>
    private sealed class ShoreTerrain : ITerrain
    {
        public byte BiomeAt(int x, int y) => x == 0 ? (byte)0 : (byte)1;
    }

    private static GameContent CoastContent()
    {
        var biomes = new[] { TestContent.WaterBiome(), TestContent.LandBiome("grass") };
        var harbor = new BuildingDef(
            "harbor", "civic", new RgbColor(60, 120, 170), 1, 1,
            WorkerSlots: 0, HousingCapacity: 0,
            BuildCost: new[] { new ResourceAmount(0, 1) },
            Recipe: null, AllowedBiomes: new[] { false, true },
            StorageBonus: System.Array.Empty<ResourceAmount>(),
            AutoBuild: false, Buildable: true, UpgradesToIndex: -1,
            UpgradeCost: System.Array.Empty<ResourceAmount>(),
            PowerSupply: 0, PowerDemand: 0, RequiresAdjacentWater: true);
        var shed = TestContent.SimpleBuilding("shed", biomes.Length); // bez nároku na vodu
        return TestContent.Build(biomes, 1, buildings: new[] { harbor, shed });
    }

    [Fact]
    public void Harbor_NeedsWaterNextToIt()
    {
        var sim = new Simulation(CoastContent(), new ShoreTerrain());

        // x = 1 sousedí s vodou na x = 0 → OK.
        Assert.Equal(PlacementResult.Ok, sim.CanPlace(0, 1, 5));

        // Hluboko ve vnitrozemí voda není → jasná chyba, ne tiché selhání.
        Assert.Equal(PlacementResult.NeedsWaterAccess, sim.CanPlace(0, 8, 5));
    }

    [Fact]
    public void OrdinaryBuilding_DoesNotNeedWater()
    {
        var sim = new Simulation(CoastContent(), new ShoreTerrain());
        Assert.Equal(PlacementResult.Ok, sim.CanPlace(1, 8, 5));
    }

    [Fact]
    public void RealContent_HasCoastalBuildings()
    {
        var content = TestData.LoadRealContent();
        foreach (var id in new[] { "harbor", "fishery", "deep_sea_port" })
        {
            Assert.True(content.Buildings.TryIndexOf(id, out int index), $"chybí pobřežní budova '{id}'");
            Assert.True(content.Buildings[index].NeedsWaterAccess, $"'{id}' má vyžadovat vodu");
        }
    }
}
