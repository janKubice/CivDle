using CivDle.Core.Content;
using CivDle.Core.Sim;
using CivDle.Core.Tests.Support;
using CivDle.Core.World;
using Xunit;

namespace CivDle.Core.Tests.Sim;

/// <summary>
/// Jak se plní namalované zóny.
///
/// <para>Zóna je jediné místo, kde hráč automatice říká „tady a takhle" —
/// takže musí dělat, co slíbila. Tři konkrétní vady, které tenhle test hlídá:
/// zóna se vylévala jako slitek od levého horního rohu, stavěla pořád tu
/// nejskromnější budovu ze seznamu (pokrok se do čtvrti nikdy nepromítl)
/// a nerespektovala hráčovu rezervu.</para>
/// </summary>
public class ZoneFillQualityTests
{
    private const int Wood = 0;

    private static UniformTerrain Grass() => new(1);

    /// <summary>
    /// Obsah se zónou „obytná", která zná chalupu i vilu. V datech jde pořadí
    /// od nejskromnější po nejlepší — plnění musí sáhnout po té poslední.
    /// </summary>
    private static GameContent ZoneContent(int startWood = 1000, bool villaBuildable = true, bool withReserveTech = false)
    {
        var biomes = new[] { TestContent.WaterBiome(), TestContent.LandBiome("grass") };
        var resources = new[] { new Resource("wood", new RgbColor(140, 90, 40), StartAmount: startWood, BaseStorage: 5000) };
        var mask = new[] { false, true };

        var hut = new BuildingDef(
            "hut", "housing", new RgbColor(200, 180, 120), 1, 1,
            WorkerSlots: 0, HousingCapacity: 2,
            BuildCost: new[] { new ResourceAmount(Wood, 1) },
            Recipe: null, AllowedBiomes: mask, StorageBonus: Array.Empty<ResourceAmount>(),
            AutoBuild: false, Buildable: true, UpgradesToIndex: -1,
            UpgradeCost: Array.Empty<ResourceAmount>(), PowerSupply: 0, PowerDemand: 0);

        var villa = new BuildingDef(
            "villa", "housing", new RgbColor(180, 150, 100), 1, 1,
            WorkerSlots: 0, HousingCapacity: 8,
            BuildCost: new[] { new ResourceAmount(Wood, 4) },
            Recipe: null, AllowedBiomes: mask, StorageBonus: Array.Empty<ResourceAmount>(),
            AutoBuild: false, Buildable: villaBuildable, UpgradesToIndex: -1,
            UpgradeCost: Array.Empty<ResourceAmount>(), PowerSupply: 0, PowerDemand: 0);

        var zoneTypes = new[] { new ZoneTypeDef("res", new RgbColor(0, 0, 200), new[] { 0, 1 }) };
        var gameplay = TestContent.DefaultGameplay with
        {
            AutoBuild = new AutoBuildConfig(IntervalTicks: 1, SearchRadius: 6, PopulationHeadroom: 2),
            PopulationGrowthPerSecond = 0,
            FoodPerPersonPerSecond = 0,
        };

        // Rezerva je za výzkumem — bez něj je vždycky nulová a test by měřil nic.
        var techs = withReserveTech
            ? new[]
            {
                new TechDef(Simulation.GovernorTechId, Array.Empty<ResourceAmount>(), Array.Empty<int>(), Array.Empty<int>()),
                new TechDef(Simulation.GovernorReserveTechId, Array.Empty<ResourceAmount>(), Array.Empty<int>(), Array.Empty<int>()),
            }
            : Array.Empty<TechDef>();

        return TestContent.Build(
            biomes, 1, resources, new[] { hut, villa }, gameplay, techs: techs, zoneTypes: zoneTypes);
    }

    [Fact]
    public void ZoneBuildsTheBestOptionItCanAfford()
    {
        // Dřív se bralo první v pořadí, takže čtvrť plná paneláků v datech dál
        // stavěla chalupy a pokrok se do ní nikdy nepromítl.
        var sim = new Simulation(ZoneContent(), Grass());
        Assert.True(sim.AddZone(0, 2, 2, 4, 4));

        sim.Tick();

        Assert.Equal(1, sim.Buildings.Length);
        Assert.Equal(1, sim.Buildings[0].DefIndex); // villa, ne hut
    }

    [Fact]
    public void ZoneFallsBackToTheModestOptionWhenTheBestIsLocked()
    {
        var sim = new Simulation(ZoneContent(villaBuildable: false), Grass());
        Assert.True(sim.AddZone(0, 2, 2, 4, 4));

        sim.Tick();

        Assert.Equal(1, sim.Buildings.Length);
        Assert.Equal(0, sim.Buildings[0].DefIndex); // hut
    }

    [Fact]
    public void ZoneFillRespectsTheGovernorReserve()
    {
        // Rezerva je hráčův slib „tohle mi nechte". Zóna ho dřív ignorovala,
        // takže přepínač v panelu guvernéra platil jen napůl.
        var sim = new Simulation(ZoneContent(withReserveTech: true), Grass());
        Assert.Equal(PlacementResult.Ok, sim.TryResearch(0)); // guvernér
        Assert.Equal(PlacementResult.Ok, sim.TryResearch(1)); // rezerva
        Assert.True(sim.AddZone(0, 2, 2, 4, 4));
        sim.SetGovernorReserve(0.9); // maximum, které jde nastavit

        sim.Tick();

        Assert.Equal(0, sim.Buildings.Length);
    }

    [Fact]
    public void ZoneGrowsAlongRoadsInsteadOfFillingFromTheCorner()
    {
        // Slitek od levého horního rohu je přesně to, co na zónách vypadalo
        // špatně. Cesta uvnitř zóny musí zástavbu přitáhnout k sobě.
        var sim = new Simulation(ZoneContent(), Grass());
        Assert.True(sim.AddZone(0, 2, 2, 6, 6));

        // Cesta na opačném konci zóny, než kde by začínalo plnění od rohu.
        for (int x = 3; x <= 6; x++)
        {
            Assert.Equal(PlacementResult.Ok, sim.TryBuildRoad(x, 6));
        }

        sim.Tick();

        Assert.Equal(1, sim.Buildings.Length);
        var placed = sim.Buildings[0];
        bool nextToRoad = sim.HasRoadAt(placed.X - 1, placed.Y) || sim.HasRoadAt(placed.X + 1, placed.Y)
            || sim.HasRoadAt(placed.X, placed.Y - 1) || sim.HasRoadAt(placed.X, placed.Y + 1);

        Assert.True(nextToRoad, $"budova na ({placed.X}, {placed.Y}) nestojí u cesty — plnilo se od rohu");
    }
}
