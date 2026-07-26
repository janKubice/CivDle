using CivDle.Core.Sim;
using CivDle.Core.Tests.Support;
using CivDle.Core.World;
using Xunit;

namespace CivDle.Core.Tests.Sim;

/// <summary>
/// Auto-stavba (fáze 2: domy se staví samy dle poptávky): staví se poblíž
/// existující zástavby, za normální cenu, deterministicky ze seedu.
/// Používá skutečná data (interval 60 tiků, headroom 2, dům 5 dřeva + 4 prkna).
/// </summary>
public class AutoBuildTests
{
    private static ITerrain UniformMap(int size, byte biomeIndex) => new UniformTerrain(biomeIndex);

    private static void RunTicks(Simulation sim, int ticks)
    {
        for (int i = 0; i < ticks; i++)
        {
            sim.Tick();
        }
    }

    [Fact]
    public void BuildsHouseNearExistingBuildings_WhenHousingDemand()
    {
        var content = TestData.LoadRealContent();
        var sim = new Simulation(content, UniformMap(16, (byte)content.Biomes.IndexOf("grassland")), seed: 7);
        int house = content.Buildings.IndexOf("house");
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuilding(house, 8, 8));

        // Populace dorůstá kapacitu (10) kolem tiku 250 → poptávka; interval 60.
        RunTicks(sim, 360);

        Assert.Equal(2, sim.Buildings.Length);
        var autoHouse = sim.Buildings[1];
        Assert.Equal(house, autoHouse.DefIndex);
        int radius = content.Gameplay.AutoBuild.SearchRadius;
        Assert.InRange(Math.Abs(autoHouse.X - 8), 0, radius);
        Assert.InRange(Math.Abs(autoHouse.Y - 8), 0, radius);
    }

    [Fact]
    public void StopsWhenResourcesRunOut()
    {
        var content = TestData.LoadRealContent();
        var sim = new Simulation(content, UniformMap(16, (byte)content.Biomes.IndexOf("grassland")), seed: 7);
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuilding(content.Buildings.IndexOf("house"), 8, 8));

        // Bez výroby se řetěz přetrhne: dojde jídlo → populace zamrzne pod stropem
        // bydlení → není poptávka po domech → auto-stavba se zastaví. Odměny za
        // úkoly to chvíli oddálí, ale jsou jednorázové.
        RunTicks(sim, 1000);
        int stabilized = sim.Buildings.Length;
        RunTicks(sim, 600);

        Assert.Equal(stabilized, sim.Buildings.Length); // růst se zastavil

        // A ověř i PROČ: město nemá z čeho růst.
        int food = content.Resources.IndexOf("food");
        Assert.Equal(0, sim.GetResource(food), precision: 6);
        Assert.True(sim.Population < sim.HousingCapacity,
            "Populace měla zamrznout pod stropem bydlení, ne o něj zavadit.");
    }

    [Fact]
    public void WithoutAnyBuilding_NothingGrows()
    {
        var content = TestData.LoadRealContent();
        var sim = new Simulation(content, UniformMap(16, (byte)content.Biomes.IndexOf("grassland")), seed: 7);

        // První budovu musí položit hráč — prázdný svět sám od sebe neroste.
        RunTicks(sim, 600);

        Assert.Equal(0, sim.Buildings.Length);
    }

    [Fact]
    public void RespectsAllowedBiomes()
    {
        var content = TestData.LoadRealContent();
        var sim = new Simulation(content, UniformMap(16, (byte)content.Biomes.IndexOf("forest")), seed: 7);
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuilding(content.Buildings.IndexOf("lumber_camp"), 8, 8));

        // Dům na les nesmí — v čistém lese se žádný auto-dům nepostaví.
        RunTicks(sim, 600);

        Assert.Equal(1, sim.Buildings.Length);
    }

    [Fact]
    public void SameSeed_SamePlacements()
    {
        var content = TestData.LoadRealContent();
        byte grass = (byte)content.Biomes.IndexOf("grassland");
        var simA = new Simulation(content, UniformMap(16, grass), seed: 123);
        var simB = new Simulation(content, UniformMap(16, grass), seed: 123);
        int house = content.Buildings.IndexOf("house");

        simA.TryPlaceBuilding(house, 8, 8);
        simB.TryPlaceBuilding(house, 8, 8);
        RunTicks(simA, 400);
        RunTicks(simB, 400);

        Assert.Equal(simA.Buildings.Length, simB.Buildings.Length);
        for (int i = 0; i < simA.Buildings.Length; i++)
        {
            Assert.Equal(simA.Buildings[i].X, simB.Buildings[i].X);
            Assert.Equal(simA.Buildings[i].Y, simB.Buildings[i].Y);
        }
    }
}
