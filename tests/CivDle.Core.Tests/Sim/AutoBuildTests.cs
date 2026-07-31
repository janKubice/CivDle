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

        // Guvernér dnes řeší i obživu, takže budov přibude víc než jen dům.
        // Podstatné je, že dům vznikl a že se stavělo U ZÁSTAVBY, ne kdekoli.
        Assert.True(sim.Buildings.Length > 1, "Auto-stavba měla něco postavit.");
        int radius = content.Gameplay.AutoBuild.SearchRadius;
        bool builtHouse = false;
        for (int i = 1; i < sim.Buildings.Length; i++)
        {
            var placed = sim.Buildings[i];
            builtHouse |= placed.DefIndex == house;
            Assert.InRange(Math.Abs(placed.X - 8), 0, radius);
            Assert.InRange(Math.Abs(placed.Y - 8), 0, radius);
        }

        Assert.True(builtHouse, "Při tlaku na bydlení měl vzniknout dům.");
    }

    [Fact]
    public void LeftAloneTheCityFeedsItself()
    {
        // Tenhle test dřív tvrdil opak: že město vyhladoví (jídlo přesně 0)
        // a přestane růst. To ale nebyl záměr, to byla vada — guvernér uměl
        // postavit jedinou budovu, chalupu, takže rostl počet lidí, které nemá
        // kdo nakrmit. Teď si město obživu postaví samo.
        var content = TestData.LoadRealContent();
        var sim = new Simulation(content, UniformMap(16, (byte)content.Biomes.IndexOf("grassland")), seed: 7);
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuilding(content.Buildings.IndexOf("house"), 8, 8));

        RunTicks(sim, 1600);

        int food = content.Resources.IndexOf("food");
        Assert.True(sim.GetResource(food) > 0, "Město se mělo umět nakrmit, ne vyhladovět.");
        Assert.True(sim.Population > content.Gameplay.StartingPopulation, "A díky tomu i vyrůst.");
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

        // Dům na les nesmí. Jiné auto-budovy (lovecká chata, tábor) tam patří,
        // takže se hlídá právě ta zakázaná — ne že se nepostaví vůbec nic.
        RunTicks(sim, 600);

        int house = content.Buildings.IndexOf("house");
        foreach (var building in sim.Buildings)
        {
            Assert.NotEqual(house, building.DefIndex);
        }
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
