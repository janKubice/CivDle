using CivDle.Core.Content;
using CivDle.Core.Sim;
using CivDle.Core.Tests.Support;
using CivDle.Core.World;
using Xunit;

namespace CivDle.Core.Tests.Sim;

/// <summary>
/// Mosty: auto-silnice umí překlenout úzký tok (řeku), ale ne širokou vodu —
/// oceán zůstává přirozenou bariérou. Most se neukládá: silniční dlaždice na vodě
/// JE most, odvozeno z terénu.
/// </summary>
public class BridgeTests
{
    /// <summary>Terén se svislým vodním pruhem dané šířky od x = 3 (jinak souš).</summary>
    private sealed class RiverStrip : ITerrain
    {
        private readonly int _width;

        public RiverStrip(int width) => _width = width;

        public byte BiomeAt(int x, int y) => x >= 3 && x < 3 + _width ? (byte)0 : (byte)1;
    }

    private static GameContent BridgeContent(int maxBridgeSpan)
    {
        var biomes = new[] { TestContent.WaterBiome(), TestContent.LandBiome("grass") };
        var gameplay = TestContent.DefaultGameplay with
        {
            Roads = new RoadConfig(new RgbColor(150, 145, 130), MaxSearchDistance: 60, MaxBridgeSpan: maxBridgeSpan),
        };
        // Budova jen na souši (biom 1), ať se staví na obou březích.
        var hut = new BuildingDef(
            "hut", "test", new RgbColor(200, 100, 50), 1, 1,
            WorkerSlots: 0, HousingCapacity: 0,
            BuildCost: new[] { new ResourceAmount(0, 1) },
            Recipe: null, AllowedBiomes: new[] { false, true },
            StorageBonus: System.Array.Empty<ResourceAmount>(),
            AutoBuild: false, Buildable: true, UpgradesToIndex: -1,
            UpgradeCost: System.Array.Empty<ResourceAmount>(), PowerSupply: 0, PowerDemand: 0);
        return TestContent.Build(biomes, 1, buildings: new[] { hut }, gameplay: gameplay);
    }

    /// <summary>Postaví domy na obou březích a vrátí simulaci.</summary>
    private static Simulation ConnectAcross(int riverWidth, int maxBridgeSpan)
    {
        var sim = new Simulation(BridgeContent(maxBridgeSpan), new RiverStrip(riverWidth));
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuilding(0, 1, 5));                 // levý břeh
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuilding(0, 3 + riverWidth + 1, 5)); // pravý břeh
        return sim;
    }

    [Fact]
    public void NarrowRiver_IsBridged()
    {
        var sim = ConnectAcross(riverWidth: 2, maxBridgeSpan: 6);

        // Cesta musí vzniknout a část vést po vodě → to je most.
        Assert.NotEmpty(sim.RoadTiles);
        Assert.Contains(sim.RoadTiles, t => sim.IsBridge(t.X, t.Y));
    }

    [Fact]
    public void WideWater_IsNotBridged()
    {
        // Tok širší než povolené rozpětí zůstane bariérou — přes oceán se nestaví.
        var sim = ConnectAcross(riverWidth: 12, maxBridgeSpan: 4);

        Assert.DoesNotContain(sim.RoadTiles, t => sim.IsBridge(t.X, t.Y));
    }

    [Fact]
    public void BridgesDisabled_KeepsWaterImpassable()
    {
        var sim = ConnectAcross(riverWidth: 2, maxBridgeSpan: 0);

        Assert.DoesNotContain(sim.RoadTiles, t => sim.IsBridge(t.X, t.Y));
    }

    [Fact]
    public void RoadOnLand_IsNotABridge()
    {
        var sim = ConnectAcross(riverWidth: 2, maxBridgeSpan: 6);

        foreach (var tile in sim.RoadTiles)
        {
            bool water = tile.X >= 3 && tile.X < 5;
            Assert.Equal(water, sim.IsBridge(tile.X, tile.Y));
        }
    }

    [Fact]
    public void RealContent_AllowsBridges()
    {
        // Řeky rozdělují souš — bez mostů by za nimi zůstaly odříznuté budovy.
        var content = TestData.LoadRealContent();
        Assert.True(content.Gameplay.Roads.MaxBridgeSpan > 0, "ostrý obsah má mít mosty zapnuté");
    }
}
