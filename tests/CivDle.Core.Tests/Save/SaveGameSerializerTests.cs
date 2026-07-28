using CivDle.Core.Content;
using CivDle.Core.Save;
using CivDle.Core.Sim;
using CivDle.Core.Tests.Support;
using CivDle.Core.World;
using Xunit;

namespace CivDle.Core.Tests.Save;

public class SaveGameSerializerTests
{
    private static readonly SaveMetadata Metadata = new(
        Seed: 42, SizeId: "medium", PresetId: "continents",
        SavedAtUtc: new DateTime(2026, 7, 20, 12, 0, 0, DateTimeKind.Utc));

    /// <summary>
    /// Rozehraná hra nad skutečným procedurálním terénem (stejným, jaký si save
    /// zrekonstruuje z presetu + seedu). Budovy se kladou na první vhodné dlaždice.
    /// </summary>
    private static (GameContent Content, Simulation Sim) PlayedGame()
    {
        var content = TestData.LoadRealContent();
        var preset = content.WorldGen.Presets.Single(p => p.Id == "continents");
        var terrain = new ProceduralTerrain(content.Biomes, preset, 42);
        var sim = new Simulation(content, terrain, 42);

        // Dva domy (na road) + pila — na první nalezené vhodné dlaždice u počátku.
        PlaceSome(sim, content.Buildings.IndexOf("house"), 2);
        PlaceSome(sim, content.Buildings.IndexOf("lumber_camp"), 1);
        for (int i = 0; i < 25; i++)
        {
            sim.Tick();
        }

        return (content, sim);
    }

    /// <summary>Postaví N budov na první volné vhodné dlaždice ve spirále od počátku.</summary>
    private static void PlaceSome(Simulation sim, int defIndex, int count)
    {
        int placed = 0;
        for (int radius = 0; radius < 80 && placed < count; radius++)
        {
            for (int y = -radius; y <= radius && placed < count; y++)
            {
                for (int x = -radius; x <= radius && placed < count; x++)
                {
                    if (Math.Max(Math.Abs(x), Math.Abs(y)) != radius)
                    {
                        continue; // jen okraj prstence, ať se místa neopakují
                    }

                    if (sim.TryPlaceBuilding(defIndex, x, y) == PlacementResult.Ok)
                    {
                        placed++;
                    }
                }
            }
        }

        Assert.Equal(count, placed);
    }

    private static MemoryStream Saved(Simulation sim, SaveMetadata metadata)
    {
        var stream = new MemoryStream();
        new SaveGameSerializer().Write(stream, sim, metadata);
        stream.Position = 0;
        return stream;
    }

    [Fact]
    public void RoundTrip_PreservesGameState()
    {
        var (content, original) = PlayedGame();

        using var stream = Saved(original, Metadata);
        var (loaded, metadata) = new SaveGameSerializer().Read(stream, content);

        Assert.Equal(Metadata, metadata);
        Assert.Equal(original.TickCount, loaded.TickCount);
        Assert.Equal(original.Population, loaded.Population);
        Assert.Equal(original.HousingCapacity, loaded.HousingCapacity);
        Assert.Equal(original.TotalWorkerSlots, loaded.TotalWorkerSlots);
        Assert.Equal(original.Seed, loaded.Seed);

        for (int i = 0; i < content.Resources.Count; i++)
        {
            Assert.Equal(original.GetResource(i), loaded.GetResource(i));
        }

        Assert.Equal(original.Buildings.Length, loaded.Buildings.Length);
        for (int i = 0; i < original.Buildings.Length; i++)
        {
            Assert.Equal(original.Buildings[i].DefIndex, loaded.Buildings[i].DefIndex);
            Assert.Equal(original.Buildings[i].X, loaded.Buildings[i].X);
            Assert.Equal(original.Buildings[i].Y, loaded.Buildings[i].Y);
            Assert.Equal(original.Buildings[i].Progress, loaded.Buildings[i].Progress);
        }

        // Silnice (save v3) se musí zachovat včetně pořadí.
        Assert.NotEmpty(original.RoadTiles);
        Assert.Equal(original.RoadTiles, loaded.RoadTiles);

        // Terén se rekonstruuje z presetu + seedu — musí být identický.
        Assert.Equal(original.BiomeAt(0, 0), loaded.BiomeAt(0, 0));
        Assert.Equal(original.BiomeAt(37, -12), loaded.BiomeAt(37, -12));
    }

    [Fact]
    public void RoundTrip_LoadedGameKeepsSimulating()
    {
        var (content, original) = PlayedGame();
        using var stream = Saved(original, Metadata);
        var (loaded, _) = new SaveGameSerializer().Read(stream, content);

        // Determinismus: originál i načtená kopie (stejný terén, seed) musí po
        // dalších ticích souhlasit.
        for (int i = 0; i < 100; i++)
        {
            original.Tick();
            loaded.Tick();
        }

        Assert.Equal(original.Population, loaded.Population);
        Assert.Equal(original.Buildings.Length, loaded.Buildings.Length);
        for (int i = 0; i < content.Resources.Count; i++)
        {
            Assert.Equal(original.GetResource(i), loaded.GetResource(i));
        }
    }

    [Fact]
    public void RoundTrip_PreservesZones()
    {
        // Zóna (save v9) se ukládá přes stabilní ID typu a musí se vrátit beze změny.
        var (content, original) = PlayedGame();
        int residential = content.ZoneTypes.IndexOf("residential");
        Assert.True(original.AddZone(residential, 3, 4, 5, 6));

        using var stream = Saved(original, Metadata);
        var (loaded, _) = new SaveGameSerializer().Read(stream, content);

        var zone = Assert.Single(loaded.Zones);
        Assert.Equal(residential, zone.TypeIndex);
        Assert.Equal(3, zone.X);
        Assert.Equal(4, zone.Y);
        Assert.Equal(5, zone.Width);
        Assert.Equal(6, zone.Height);
    }

    [Fact]
    public void RoundTrip_PreservesPolicies()
    {
        // Politika (save v10) se ukládá přes stabilní ID a její efekt se po načtení
        // přepočítá ve FinalizeLoad (proto kontrolujeme i odvozený BuildsPerInterval).
        var (content, original) = PlayedGame();
        int rapid = content.Policies.IndexOf("rapid_growth");
        Assert.True(original.TogglePolicy(rapid));
        Assert.Equal(3, original.BuildsPerInterval);

        using var stream = Saved(original, Metadata);
        var (loaded, _) = new SaveGameSerializer().Read(stream, content);

        Assert.True(loaded.IsPolicyActive(rapid));
        Assert.Equal(3, loaded.BuildsPerInterval);
    }

    [Fact]
    public void RoundTrip_PreservesPollution()
    {
        // Zamoření je stav světa, ne odvozená veličina — kdyby se neukládalo,
        // stačilo by hru vypnout a zapnout, aby byl smog pryč.
        var (content, original) = PlayedGame();
        original.PollutionMap.Emit(12, 34, PollutionKind.Air, 7.5);
        original.PollutionMap.Emit(12, 34, PollutionKind.Soil, 2.25);

        using var stream = Saved(original, Metadata);
        var (loaded, _) = new SaveGameSerializer().Read(stream, content);

        Assert.Equal(7.5, loaded.PollutionMap.At(12, 34, PollutionKind.Air), 6);
        Assert.Equal(2.25, loaded.PollutionMap.At(12, 34, PollutionKind.Soil), 6);
        Assert.Equal(0, loaded.PollutionMap.At(12, 34, PollutionKind.Water));
    }

    [Fact]
    public void Load_RemapsResourceIdsWhenContentIsReordered()
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

        var sim = new Simulation(contentA, new UniformTerrain(1));
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuilding(0, 2, 2)); // wood: 5 − 1 = 4

        using var stream = Saved(sim, Metadata with { PresetId = "test" });
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
        var contentA = TestContent.Build(biomes, 1, buildings: new[] { TestContent.SimpleBuilding("stara_bouda", biomes.Length) });
        var contentB = TestContent.Build(biomes, 1, buildings: new[] { TestContent.SimpleBuilding("jina", biomes.Length) });

        var sim = new Simulation(contentA, new UniformTerrain(1));
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuilding(0, 1, 1));

        using var stream = Saved(sim, Metadata with { PresetId = "test" });
        var ex = Assert.Throws<SaveLoadException>(() => new SaveGameSerializer().Read(stream, contentB));

        Assert.Contains("stara_bouda", ex.Message);
    }

    [Fact]
    public void Load_UnknownPreset_FailsWithClearError()
    {
        var (content, sim) = PlayedGame();
        using var stream = Saved(sim, Metadata with { PresetId = "neexistuje" });

        var ex = Assert.Throws<SaveLoadException>(() => new SaveGameSerializer().Read(stream, content));

        Assert.Contains("neexistuje", ex.Message);
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
