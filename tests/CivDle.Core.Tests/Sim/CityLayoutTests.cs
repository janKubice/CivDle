using CivDle.Core.Content;
using CivDle.Core.Sim;
using CivDle.Core.Tests.Support;
using CivDle.Core.World;
using Xunit;

namespace CivDle.Core.Tests.Sim;

/// <summary>
/// Vkus automatu: jak vypadá čtvrť, kterou zastavěl guvernér.
///
/// <para>Testuje se výsledek, ne pravidlo: ve velké zóně nesmí vzniknout jeden
/// slitek domů bez jediné ulice. Dřív přesně takový vznikal — tři sousedi vážou
/// stejně jako jedna cesta, takže se vždycky vyplatilo přilepit se doprostřed,
/// a auto-silnice u budovy s vlastním sousedem rovnou rezignovala.</para>
/// </summary>
public class CityLayoutTests
{
    /// <summary>Obsah: 1×1 budova a typ zóny, který se jí plní. Auto-stavba každý tik.</summary>
    private static GameContent ZoneContent()
    {
        var biomes = new[] { TestContent.WaterBiome(), TestContent.LandBiome("grass") };
        var resources = new[]
        {
            new Resource("wood", new RgbColor(140, 90, 40), StartAmount: 100_000, BaseStorage: 1_000_000),
        };
        var hut = TestContent.SimpleBuilding("hut", biomes.Length);
        var zoneTypes = new[] { new ZoneTypeDef("res", new RgbColor(0, 0, 200), new[] { 0 }) };

        var gameplay = TestContent.DefaultGameplay with
        {
            AutoBuild = new AutoBuildConfig(IntervalTicks: 1, SearchRadius: 6, PopulationHeadroom: 2),
            PopulationGrowthPerSecond = 0,
            FoodPerPersonPerSecond = 0,
        };

        return TestContent.Build(biomes, 1, resources, new[] { hut }, gameplay, zoneTypes: zoneTypes);
    }

    /// <summary>Zaplněná zóna 12×12 — dost velká na to, aby se slitek poznal.</summary>
    private static Simulation FilledZone()
    {
        var sim = new Simulation(ZoneContent(), new UniformTerrain(1));
        Assert.True(sim.AddZone(0, 10, 10, 12, 12));

        for (int i = 0; i < 600; i++)
        {
            sim.Tick();
        }

        Assert.True(sim.Buildings.Length > 20, "zóna se má stihnout zaplnit");
        return sim;
    }

    [Fact]
    public void StreetsRunThroughTheDistrictNotJustAroundIt()
    {
        // Tohle je ten „obří blok bez cest": dřív vznikl slitek, kolem kterého se
        // nanejvýš objevila jedna cesta zvenčí. Uvnitř čtvrti musí být ulice.
        var sim = FilledZone();

        int inside = 0;
        for (int y = 11; y < 21; y++)
        {
            for (int x = 11; x < 21; x++)
            {
                if (sim.HasRoadAt(x, y))
                {
                    inside++;
                }
            }
        }

        Assert.True(inside >= 4, $"uvnitř zóny je jen {inside} dlaždic ulice — to je pořád slitek");
    }

    [Fact]
    public void AFilledZoneGetsStreets()
    {
        // Slitek bez cest byl dvojí chyba: rozvržení i auto-silnic. Tohle hlídá
        // tu druhou půlku — čtvrť musí být napojená, ne odříznutá.
        var sim = FilledZone();

        Assert.NotEmpty(sim.RoadTiles);
        for (int i = 0; i < sim.Buildings.Length; i++)
        {
            Assert.True(sim.IsBuildingConnected(i), $"budova {i} v zóně zůstala bez napojení");
        }
    }
}
