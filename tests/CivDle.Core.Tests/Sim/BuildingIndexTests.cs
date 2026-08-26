using CivDle.Core.Content;
using CivDle.Core.Sim;
using CivDle.Core.Tests.Support;
using CivDle.Core.World;
using Xunit;

namespace CivDle.Core.Tests.Sim;

/// <summary>
/// Index budov pro render.
///
/// <para>Zásadní test je ten poslední: po náhodné sérii stavba/bourání/přesun
/// musí index vrátit <b>přesně totéž</b>, co hrubé projití celého pole. Přesně
/// tohle je ta chyba, které se u indexů dělá — zapomenutá aktualizace na jedné
/// z cest, která se projeví až tím, že hráči zmizí dům z obrazovky.</para>
/// </summary>
public class BuildingIndexTests
{
    [Fact]
    public void AnEmptyWorldHasAnEmptyIndex()
    {
        var (sim, _) = World();
        var found = new List<int>();

        sim.BuildingsIn(-100, -100, 100, 100, found);

        Assert.Empty(found);
    }

    [Fact]
    public void APlacedBuildingIsFoundInItsOwnChunk()
    {
        var (sim, content) = World();
        int house = content.Buildings.IndexOf("house");
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuildingFree(house, 5, 5));

        var found = new List<int>();
        sim.BuildingsIn(0, 0, 10, 10, found);

        Assert.Single(found);
        Assert.Equal(0, found[0]);
    }

    [Fact]
    public void ADistantChunkDoesNotSeeIt()
    {
        var (sim, content) = World();
        int house = content.Buildings.IndexOf("house");
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuildingFree(house, 5, 5));

        var found = new List<int>();
        sim.BuildingsIn(500, 500, 520, 520, found);

        Assert.Empty(found);
    }

    [Fact]
    public void ABuildingOnAChunkBorderIsInBothChunks()
    {
        // Velká budova může sedět na hraně. Kdyby byla jen v jednom chunku,
        // z druhé strany by při odjetí kamery zmizela.
        var (sim, content) = World();
        int port = content.Buildings.IndexOf("warehouse"); // 2×2

        int border = BuildingIndex.ChunkSize; // levý horní roh chunku
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuildingFree(port, border - 1, border - 1));

        var fromLeft = new List<int>();
        sim.BuildingsIn(border - 2, border - 2, border - 1, border - 1, fromLeft);

        var fromRight = new List<int>();
        sim.BuildingsIn(border, border, border + 1, border + 1, fromRight);

        Assert.Single(fromLeft);
        Assert.Single(fromRight);
        Assert.Equal(fromLeft[0], fromRight[0]);
    }

    [Fact]
    public void DemolishingTakesItOutOfTheIndex()
    {
        var (sim, content) = World();
        int house = content.Buildings.IndexOf("house");
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuildingFree(house, 5, 5));
        Assert.Equal(PlacementResult.Ok, sim.TryDemolish(0));

        var found = new List<int>();
        sim.BuildingsIn(0, 0, 10, 10, found);

        Assert.Empty(found);
    }

    [Fact]
    public void MovingABuildingMovesItInTheIndexToo()
    {
        var (sim, content) = World();
        int house = content.Buildings.IndexOf("house");
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuildingFree(house, 5, 5));
        Assert.Equal(PlacementResult.Ok, sim.TryMoveBuilding(0, 200, 200));

        var atOldPlace = new List<int>();
        sim.BuildingsIn(0, 0, 10, 10, atOldPlace);

        var atNewPlace = new List<int>();
        sim.BuildingsIn(195, 195, 205, 205, atNewPlace);

        Assert.Empty(atOldPlace);
        Assert.Single(atNewPlace);
    }

    [Fact]
    public void TheIndexSurvivesSaveAndLoad()
    {
        // Obnova ze savu chodí toutéž cestou jako stavba, takže index vzniká
        // sám. Kdyby ne, po načtení by se nekreslilo nic.
        var (sim, content) = World();
        int house = content.Buildings.IndexOf("house");
        for (int i = 0; i < 5; i++)
        {
            sim.TryPlaceBuildingFree(house, 4 + i * 2, 4);
        }

        var stream = new MemoryStream();
        new Core.Save.SaveGameSerializer().Write(
            stream, sim, new Core.Save.SaveMetadata(sim.Seed, "medium", "continents", DateTime.UtcNow));
        stream.Position = 0;
        var (loaded, _) = new Core.Save.SaveGameSerializer().Read(stream, content);

        var found = new List<int>();
        loaded.BuildingsIn(0, 0, 20, 20, found);

        Assert.Equal(5, found.Count);
    }

    [Fact]
    public void TheIndexAgreesWithBruteForceAfterAMessOfEdits()
    {
        // Tenhle test je celý smysl souboru: cokoli jiného může projít
        // i s rozbitým indexem.
        var (sim, content) = World();
        int house = content.Buildings.IndexOf("house");
        int granary = content.Buildings.IndexOf("granary");
        var random = new Random(4242); // pevné semínko: padne-li to, padne to znovu

        for (int step = 0; step < 400; step++)
        {
            // Váženo ve prospěch stavby: se třetinovým bouráním by na mapě
            // po čtyřech stech krocích nezbylo skoro nic a test by neověřil nic.
            switch (random.Next(5))
            {
                case 0:
                case 1:
                case 2:
                    sim.TryPlaceBuildingFree(
                        random.Next(2) == 0 ? house : granary,
                        random.Next(-40, 40), random.Next(-40, 40));
                    break;

                case 3 when sim.Buildings.Length > 0:
                    sim.TryDemolish(random.Next(sim.Buildings.Length));
                    break;

                case 4 when sim.Buildings.Length > 0:
                    sim.TryMoveBuilding(
                        random.Next(sim.Buildings.Length),
                        random.Next(-60, 60), random.Next(-60, 60));
                    break;
            }
        }

        Assert.True(sim.Buildings.Length > 20, "test by neověřil nic nad prázdnou mapou");

        var found = new List<int>();
        sim.BuildingsIn(-80, -80, 80, 80, found);

        // Index smí vrátit i budovy kousek mimo výřez (pracuje po chuncích),
        // ale nesmí žádnou vynechat a nesmí vrátit neplatný index.
        var fromIndex = new HashSet<int>(found);
        for (int i = 0; i < sim.Buildings.Length; i++)
        {
            Assert.True(fromIndex.Contains(i), $"index zapomněl budovu {i}");
        }

        foreach (int index in fromIndex)
        {
            Assert.InRange(index, 0, sim.Buildings.Length - 1);
        }

        Assert.Equal(fromIndex.Count, found.Count); // žádná budova dvakrát
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
