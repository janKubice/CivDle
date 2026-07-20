using CivDle.Core.Content;
using CivDle.Core.Save;
using CivDle.Core.Sim;
using CivDle.Core.Tests.Support;
using CivDle.Core.World;
using CivDle.Core.WorldGen;
using Xunit;

namespace CivDle.Core.Tests.Save;

public class SaveGameSerializerTests
{
    private static readonly SaveMetadata Metadata = new(
        Seed: 42, SizeId: "medium", PresetId: "continents",
        SavedAtUtc: new DateTime(2026, 7, 20, 12, 0, 0, DateTimeKind.Utc));

    /// <summary>Rozehraná hra nad skutečným obsahem: mapa z generátoru, budovy, pár tiků.</summary>
    private static (GameContent Content, Simulation Sim) PlayedGame()
    {
        var content = TestData.LoadRealContent();
        var preset = content.WorldGen.Presets.Single(p => p.Id == "continents");
        var map = new MapGenerator().Generate(content, new WorldGenRequest(42, 64, 64, preset));

        // Ruční biomy na okraji, ať jdou jistě postavit budovy.
        map.BiomeIndices[map.Index(0, 0)] = (byte)content.Biomes.IndexOf("grassland");
        map.BiomeIndices[map.Index(1, 0)] = (byte)content.Biomes.IndexOf("forest");

        var sim = new Simulation(content, map);
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuilding(content.Buildings.IndexOf("house"), 0, 0));
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuilding(content.Buildings.IndexOf("lumber_camp"), 1, 0));
        for (int i = 0; i < 25; i++)
        {
            sim.Tick();
        }

        return (content, sim);
    }

    private static MemoryStream Saved(Simulation sim, SaveMetadata metadata)
    {
        var stream = new MemoryStream();
        new SaveGameSerializer().Write(stream, sim, metadata);
        stream.Position = 0;
        return stream;
    }

    [Fact]
    public void RoundTrip_PreservesWholeGameState()
    {
        var (content, original) = PlayedGame();

        using var stream = Saved(original, Metadata);
        var (loaded, metadata) = new SaveGameSerializer().Read(stream, content);

        Assert.Equal(Metadata, metadata);
        Assert.Equal(original.TickCount, loaded.TickCount);
        Assert.Equal(original.Population, loaded.Population);
        Assert.Equal(original.HousingCapacity, loaded.HousingCapacity);
        Assert.Equal(original.TotalWorkerSlots, loaded.TotalWorkerSlots);

        for (int i = 0; i < content.Resources.Count; i++)
        {
            Assert.Equal(original.GetResource(i), loaded.GetResource(i));
        }

        Assert.Equal(original.Map.BiomeIndices, loaded.Map.BiomeIndices);
        Assert.Equal(original.Map.Elevation, loaded.Map.Elevation);
        Assert.Equal(original.Map.Moisture, loaded.Map.Moisture);

        Assert.Equal(original.Buildings.Length, loaded.Buildings.Length);
        for (int i = 0; i < original.Buildings.Length; i++)
        {
            Assert.Equal(original.Buildings[i].DefIndex, loaded.Buildings[i].DefIndex);
            Assert.Equal(original.Buildings[i].X, loaded.Buildings[i].X);
            Assert.Equal(original.Buildings[i].Y, loaded.Buildings[i].Y);
            Assert.Equal(original.Buildings[i].Progress, loaded.Buildings[i].Progress);
        }
    }

    [Fact]
    public void RoundTrip_LoadedGameKeepsSimulating()
    {
        var (content, original) = PlayedGame();
        using var stream = Saved(original, Metadata);
        var (loaded, _) = new SaveGameSerializer().Read(stream, content);

        // Determinismus: originál i načtená kopie musí po dalších ticích souhlasit.
        for (int i = 0; i < 100; i++)
        {
            original.Tick();
            loaded.Tick();
        }

        Assert.Equal(original.Population, loaded.Population);
        for (int i = 0; i < content.Resources.Count; i++)
        {
            Assert.Equal(original.GetResource(i), loaded.GetResource(i));
        }
    }

    [Fact]
    public void Load_RemapsIdsWhenContentIsReordered()
    {
        // Obsah A: [wood, planks]; obsah B má stejná ID v opačném pořadí.
        var biomes = new[] { TestContent.WaterBiome(), TestContent.LandBiome("grass") };
        var woodFirst = new[]
        {
            new Resource("wood", new RgbColor(1, 1, 1), 5, 1000),
            new Resource("planks", new RgbColor(2, 2, 2), 0, 1000),
        };
        var planksFirst = new[] { woodFirst[1], woodFirst[0] };
        var building = TestContent.SimpleBuilding("hut", biomes.Length);

        var contentA = TestContent.Build(biomes, 1, woodFirst, new[] { building });
        var contentB = TestContent.Build(biomes, 1, planksFirst, new[] { building });

        var map = new WorldMap(4, 4);
        Array.Fill(map.BiomeIndices, (byte)1);
        var sim = new Simulation(contentA, map);
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuilding(0, 2, 2)); // wood: 5 − 1 = 4

        using var stream = Saved(sim, Metadata);
        var (loaded, _) = new SaveGameSerializer().Read(stream, contentB);

        // V obsahu B je wood na indexu 1 — množství ho musí následovat.
        Assert.Equal(4, loaded.GetResource(contentB.Resources.IndexOf("wood")));
        Assert.Equal(0, loaded.GetResource(contentB.Resources.IndexOf("planks")));
        Assert.Equal(1, loaded.Buildings.Length);
    }

    [Fact]
    public void Load_UnknownBuildingId_FailsWithClearError()
    {
        var biomes = new[] { TestContent.WaterBiome(), TestContent.LandBiome("grass") };
        var building = TestContent.SimpleBuilding("stara_bouda", biomes.Length);
        var contentA = TestContent.Build(biomes, 1, buildings: new[] { building });
        var contentB = TestContent.Build(biomes, 1, buildings: new[] { TestContent.SimpleBuilding("jina", biomes.Length) });

        var map = new WorldMap(4, 4);
        Array.Fill(map.BiomeIndices, (byte)1);
        var sim = new Simulation(contentA, map);
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuilding(0, 1, 1));

        using var stream = Saved(sim, Metadata);
        var ex = Assert.Throws<SaveLoadException>(() => new SaveGameSerializer().Read(stream, contentB));

        Assert.Contains("stara_bouda", ex.Message);
    }

    [Fact]
    public void Load_GarbageData_FailsWithSaveLoadException()
    {
        var content = TestData.LoadRealContent();
        using var garbage = new MemoryStream(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9 });

        Assert.Throws<SaveLoadException>(() => new SaveGameSerializer().Read(garbage, content));
    }

    [Fact]
    public void Load_WrongVersion_FailsWithVersionInMessage()
    {
        var (content, sim) = PlayedGame();
        using var stream = Saved(sim, Metadata);
        var bytes = stream.ToArray();
        bytes[4] = 99; // přepsat verzi v hlavičce (little-endian int za magic "CIVD")

        using var tampered = new MemoryStream(bytes);
        var ex = Assert.Throws<SaveLoadException>(() => new SaveGameSerializer().Read(tampered, content));

        Assert.Contains("99", ex.Message);
    }

    [Fact]
    public void Load_TruncatedFile_FailsGracefully()
    {
        var (content, sim) = PlayedGame();
        using var stream = Saved(sim, Metadata);
        var bytes = stream.ToArray();

        using var truncated = new MemoryStream(bytes, 0, bytes.Length / 3);

        Assert.Throws<SaveLoadException>(() => new SaveGameSerializer().Read(truncated, content));
    }
}
