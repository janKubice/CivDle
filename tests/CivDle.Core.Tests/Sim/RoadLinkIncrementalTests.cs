using CivDle.Core.Content;
using CivDle.Core.Save;
using CivDle.Core.Sim;
using CivDle.Core.Tests.Support;
using CivDle.Core.World;
using Xunit;

namespace CivDle.Core.Tests.Sim;

/// <summary>
/// Napojení na silnice se po stavbě a nové dlaždici cesty jen dopočítává
/// od místa změny — dřív se po každé z nich počítalo celé město znovu.
/// Dopočítaný stav musí být <b>přesně</b> ten, který dá plný přepočet;
/// ten udělá načtení savu, takže se porovná rozehraná hra s načtenou.
/// </summary>
public class RoadLinkIncrementalTests
{
    [Theory]
    [InlineData(3)]
    [InlineData(17)]
    public void LinksAfterBuildingAreTheSameAsAfterLoading(long seed)
    {
        var content = TestData.LoadRealContent();
        var preset = content.WorldGen.Presets.Single(p => p.Id == "continents");
        var sim = new Simulation(content, new ProceduralTerrain(content.Biomes, preset, seed), seed);
        var (sx, sy) = StartSiteFinder.Find(sim);
        foreach (var id in new[] { "house", "farm", "lumber_camp", "quarry" })
        {
            PlaceNear(sim, content.Buildings.IndexOf(id), sx, sy);
        }

        // Guvernér staví po dávkách a dláždí až po nich — přesně ta cesta,
        // kvůli které se napojení dopočítává.
        sim.DebugGrantEveryResource(20_000);
        sim.DebugBoostAutoBuild(8, 1e9);
        for (int t = 0; t < 4_000; t++)
        {
            sim.Tick();
        }

        Assert.True(sim.RoadTiles.Count > 20, $"silnic {sim.RoadTiles.Count}");

        // Guvernér napojí všechno. Aby bylo co porovnávat, přibudou budovy
        // bez dláždění (u bloků i daleko od nich) a kusy cest napříč —
        // dopočítávání tak projde napojení přímé, přes sousedy i žádné.
        var rng = new Random((int)seed);
        int house = content.Buildings.IndexOf("house");
        for (int i = 0; i < 400; i++)
        {
            int x = sx + rng.Next(-60, 60), y = sy + rng.Next(-60, 60);
            if (rng.Next(3) == 0)
            {
                if (!sim.IsOccupied(x, y))
                {
                    sim.AddRoadTileForTest(x, y); // ve hře silnice na budovu nevede
                }
            }
            else
            {
                sim.TryPlaceBuildingFree(house, x, y);
            }

            if (i % 50 == 0)
            {
                Connections(sim); // dotazy mezi změnami — cache se musí držet i tak
            }
        }

        bool[] running = Connections(sim);

        using var stream = new MemoryStream();
        new SaveGameSerializer().Write(stream, sim, new SaveMetadata(seed, "medium", "continents", DateTime.UtcNow));
        stream.Position = 0;
        var (loaded, _) = new SaveGameSerializer().Read(stream, content);

        Assert.Equal(Connections(loaded), running);
        Assert.Contains(false, running); // test má smysl, jen když něco napojené není
        Assert.Contains(true, running);
    }

    [Fact]
    public void ANewRoadLinksTheWholeRow_TwoHousesDeep()
    {
        // Řada čtyř domů bez ulice. Silnice u prvního napojí jeho a přes něj
        // dva další; čtvrtý je už moc daleko (blok, ne celé město).
        var sim = Row();
        sim.AddRoadTileForTest(-1, 0);

        Assert.Equal(new[] { true, true, true, false }, Connections(sim));
    }

    [Fact]
    public void ANewHouseBridgesTheGap_AndLinksTheHouseBehindIt()
    {
        // Dům u ulice, mezera, osamělý dům. Dům do mezery napojí sebe i toho za ním.
        var sim = World();
        sim.AddRoadTileForTest(-1, 0);
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuildingFree(0, 0, 0));
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuildingFree(0, 2, 0));
        Assert.Equal(new[] { true, false }, Connections(sim));

        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuildingFree(0, 1, 0));

        Assert.Equal(new[] { true, true, true }, Connections(sim));
    }

    [Fact]
    public void AShorterWayThroughALinkedHouse_ReachesFurther()
    {
        // Třetí dům je napojený až přes dva sousedy (nejdál, jak to jde), takže
        // čtvrtý ne. Ulice přímo u třetího zkrátí jeho cestu — a teprve tím
        // dosáhne na čtvrtý. Vlna se tedy musí šířit i přes dům, který už
        // napojený byl.
        var sim = Row();
        sim.AddRoadTileForTest(-1, 0);
        Assert.False(sim.IsBuildingConnected(3));

        sim.AddRoadTileForTest(2, 1);

        Assert.Equal(new[] { true, true, true, true }, Connections(sim));
    }

    /// <summary>Čtyři domy v řadě (0,0)–(3,0) a vzdálená silnice, ať napojení vůbec platí.</summary>
    private static Simulation Row()
    {
        var sim = World();
        sim.AddRoadTileForTest(40, 40);
        for (int x = 0; x < 4; x++)
        {
            Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuildingFree(0, x, 0));
        }

        Assert.Equal(new[] { false, false, false, false }, Connections(sim));
        return sim;
    }

    private static Simulation World() => new(
        TestContent.Build(
            resources: new[] { new Resource("wood", new RgbColor(140, 90, 40), 0, 100000) },
            buildings: new[] { TestContent.SimpleBuilding("house", 2, housing: 2) with { BuildCost = Array.Empty<ResourceAmount>() } }),
        new UniformTerrain(1));

    private static bool[] Connections(Simulation sim)
    {
        var result = new bool[sim.Buildings.Length];
        for (int i = 0; i < result.Length; i++)
        {
            result[i] = sim.IsBuildingConnected(i);
        }

        return result;
    }

    private static void PlaceNear(Simulation sim, int defIndex, int cx, int cy)
    {
        for (int r = 1; r < 30; r++)
        {
            for (int y = -r; y <= r; y++)
            {
                for (int x = -r; x <= r; x++)
                {
                    if (Math.Max(Math.Abs(x), Math.Abs(y)) == r
                        && sim.TryPlaceBuilding(defIndex, cx + x, cy + y) == PlacementResult.Ok)
                    {
                        return;
                    }
                }
            }
        }
    }
}
