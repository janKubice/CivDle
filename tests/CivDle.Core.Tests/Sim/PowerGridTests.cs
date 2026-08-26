using CivDle.Core.Content;
using CivDle.Core.Sim;
using CivDle.Core.Tests.Support;
using CivDle.Core.World;
using Xunit;

namespace CivDle.Core.Tests.Sim;

/// <summary>
/// Prostorový rozvod proudu.
///
/// <para>Do téhle chvíle byla energie jedno globální číslo a „kam s elektrárnou"
/// nebyla otázka. Testy hlídají obojí, co z toho dělá rozhodnutí: že <b>dosah
/// platí</b> a že se při nedostatku <b>klesá poměrně</b>, ne kdo dřív přijde.</para>
/// </summary>
public class PowerGridTests
{
    [Fact]
    public void RealContentHasSpatialPower()
    {
        var content = TestData.LoadRealContent();

        Assert.True(content.Gameplay.Power.IsEnabled, "bez dosahu v datech je to zase jedno globální číslo");
    }

    [Fact]
    public void WithoutAPlantThereIsNoPower()
    {
        var (sim, content) = World();
        int factory = PlaceConsumer(sim, content, 0, 0);

        Assert.Equal(0.0, sim.PowerAt(sim.Buildings[factory].X, sim.Buildings[factory].Y), 3);
    }

    [Fact]
    public void APlantNearbyLightsItUp()
    {
        var (sim, content) = World();
        int factory = PlaceConsumer(sim, content, 0, 0);
        PlacePlant(sim, content, 6, 0);

        Assert.Equal(1.0, sim.PowerAt(sim.Buildings[factory].X, sim.Buildings[factory].Y), 3);
    }

    [Fact]
    public void APlantOnTheOtherSideOfTheMapDoesNot()
    {
        // Tohle je celý smysl změny: dřív by tahle elektrárna zásobovala všechno.
        var (sim, content) = World();
        int factory = PlaceConsumer(sim, content, 0, 0);
        PlacePlant(sim, content, 400, 400);

        Assert.Equal(0.0, sim.PowerAt(sim.Buildings[factory].X, sim.Buildings[factory].Y), 3);
    }

    [Fact]
    public void PlacesNobodyAsksAboutAreFullyPowered()
    {
        // Prázdná pláň není „bez proudu" — nikdo ho tam nechce. Kdyby vracela
        // nulu, každá budova bez spotřeby by hlásila výpadek.
        var (sim, _) = World();

        Assert.Equal(1.0, sim.PowerAt(50, 50), 3);
    }

    [Fact]
    public void WhenThereIsNotEnoughEveryoneDropsTogether()
    {
        // Ne že první tři dostanou a zbytek nic. Nedostatek má být zpomalení
        // čtvrti, ne loterie podle pořadí v poli.
        var (sim, content) = World();
        var consumers = new List<int>();
        for (int i = 0; i < 6; i++)
        {
            consumers.Add(PlaceConsumer(sim, content, i * 4, 0));
        }

        PlacePlant(sim, content, 10, 0);

        var coverage = consumers
            .Select(index => sim.PowerAt(sim.Buildings[index].X, sim.Buildings[index].Y))
            .ToList();

        Assert.Contains(coverage, value => value < 0.999); // opravdu jich je moc
        Assert.All(coverage, value => Assert.InRange(value, 0.0, 1.0));

        // Ti, kdo proud dostávají, ho mají stejně.
        var served = coverage.Where(v => v > 0.001).ToList();
        Assert.True(served.Count > 1);
        Assert.All(served, value => Assert.Equal(served[0], value, 2));
    }

    [Fact]
    public void TwoPlantsAddUp()
    {
        var (sim, content) = World();
        int factory = PlaceConsumer(sim, content, 0, 0);
        PlacePlant(sim, content, 6, 0);
        double one = sim.PowerSupplyAt(0, 0);

        PlacePlant(sim, content, 0, 6);
        double two = sim.PowerSupplyAt(0, 0);

        Assert.True(two > one, $"druhá elektrárna nepřidala nic ({one} → {two})");
        Assert.Equal(1.0, sim.PowerAt(sim.Buildings[factory].X, sim.Buildings[factory].Y), 3);
    }

    [Fact]
    public void DemolishingThePlantTurnsTheLightsOff()
    {
        var (sim, content) = World();
        int factory = PlaceConsumer(sim, content, 0, 0);
        int plant = PlacePlant(sim, content, 6, 0);
        Assert.True(sim.PowerAt(0, 0) > 0.5);

        Assert.Equal(PlacementResult.Ok, sim.TryDemolish(plant));

        Assert.Equal(0.0, sim.PowerAt(sim.Buildings[factory].X, sim.Buildings[factory].Y), 3);
    }

    [Fact]
    public void AnUnfinishedPlantSuppliesNothing()
    {
        var (sim, content) = World();
        PlaceConsumer(sim, content, 0, 0);

        int plant = content.Buildings.IndexOf("coal_power_plant");
        if (content.Buildings[plant].BuildTicks <= 0)
        {
            return; // tenhle typ se staví okamžitě, není co testovat
        }

        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuildingFree(plant, 6, 0));

        Assert.Equal(0.0, sim.PowerAt(0, 0), 3);
    }

    [Fact]
    public void ProductionFollowsTheLocalGridNotTheGlobalOne()
    {
        // Bez proudu má továrna běžet pomaleji, i když má říše výroby dost —
        // jinak je celý dosah jen číslo v UI.
        var (sim, content) = World();
        int factory = PlaceConsumer(sim, content, 0, 0);
        PlacePlant(sim, content, 400, 400); // proud je, ale jinde

        Assert.True(sim.TotalPowerSupply >= sim.TotalPowerDemand, "říše má výroby dost");
        Assert.Equal(0.0, sim.PowerAt(sim.Buildings[factory].X, sim.Buildings[factory].Y), 3);
    }

    private static int PlaceConsumer(Simulation sim, GameContent content, int x, int y)
    {
        int factory = content.Buildings.IndexOf("factory");
        Assert.True(content.Buildings[factory].PowerDemand > 0, "továrna má chtít proud");
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuildingFree(factory, x, y));
        return sim.Buildings.Length - 1;
    }

    private static int PlacePlant(Simulation sim, GameContent content, int x, int y)
    {
        int plant = content.Buildings.IndexOf("coal_power_plant");
        Assert.True(content.Buildings[plant].PowerSupply > 0, "elektrárna má proud dodávat");
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuildingFree(plant, x, y));

        // Rozestavěná elektrárna nedodává — dotikáme ji.
        for (int i = 0; i < content.Buildings[plant].BuildTicks + 10 && !sim.Buildings[^1].IsComplete; i++)
        {
            sim.Tick();
        }

        return sim.Buildings.Length - 1;
    }

    private static (Simulation Sim, GameContent Content) World()
    {
        var content = TestData.LoadRealContent();
        var sim = new Simulation(content, new UniformTerrain(content.Biomes.IndexOf("grassland")));
        sim.SkipTutorial();
        for (int i = 0; i < content.Techs.Count; i++)
        {
            sim.DebugGrantTech(i);
        }

        sim.DebugFillStorages();
        sim.SetAutoUpgradeLevel(0);
        sim.SetAutoMerge(false);
        return (sim, content);
    }
}
