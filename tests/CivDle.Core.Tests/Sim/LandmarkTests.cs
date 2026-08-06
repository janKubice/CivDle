using CivDle.Core.Content;
using CivDle.Core.Sim;
using CivDle.Core.Tests.Support;
using CivDle.Core.World;
using Xunit;

namespace CivDle.Core.Tests.Sim;

/// <summary>
/// Landmarky (living-map.md §4): vzácné body zájmu proti monotónnosti. Část z nich
/// je surovinový uzel (§3 — „jelen v lese = maso"), takže dá při kliknutí víc než
/// okolní biom. Výskyt je čistá funkce pozice a seedu → nic se neukládá.
/// </summary>
public class LandmarkTests
{
    private static ITerrain Grass() => new UniformTerrain(1);

    private static LandmarkDef Def(string id, int rarity, ClickYield? yield) =>
        new(id, new[] { true, true }, new RgbColor(200, 100, 50), Size: 8, Rarity: rarity,
            ClickYield: yield, SpriteId: null, Footprint: 1);

    [Fact]
    public void Landmarks_AppearButStayRare()
    {
        // rarity 50 → řádově každá padesátá dlaždice; musí být vidět, ale ne všude.
        var content = TestContent.Build(landmarks: new[] { Def("herd", 50, new ClickYield(0, 9)) });
        var sim = new Simulation(content, Grass(), seed: 5);

        int found = 0, total = 0;
        for (int y = 0; y < 100; y++)
        {
            for (int x = 0; x < 100; x++)
            {
                total++;
                if (sim.LandmarkAt(x, y) >= 0)
                {
                    found++;
                }
            }
        }

        Assert.True(found > 0, "landmarky musí být na mapě k nalezení");
        Assert.True(found < total / 5, $"landmarky mají být vzácné, je jich {found}/{total}");
    }

    [Fact]
    public void Landmark_IsPureFunctionOfPositionAndSeed()
    {
        var content = TestContent.Build(landmarks: new[] { Def("herd", 20, null) });
        var a = new Simulation(content, Grass(), seed: 11);
        var b = new Simulation(content, Grass(), seed: 11);

        for (int i = 0; i < 300; i++)
        {
            int x = i * 3 - 150, y = i * 5 - 200;
            Assert.Equal(a.LandmarkAt(x, y), b.LandmarkAt(x, y));
        }
    }

    [Fact]
    public void HarvestableLandmark_GivesMoreThanPlainBiome()
    {
        // Biom dává 2, stádo 9 → sběr na stádu musí vynést víc.
        var biomes = new[]
        {
            TestContent.WaterBiome(),
            TestContent.LandBiome("grass") with { ClickYield = new ClickYield(0, 2) },
        };
        var resources = new[] { new Resource("food", new RgbColor(1, 1, 1), StartAmount: 0, BaseStorage: 1000) };
        var content = TestContent.Build(
            biomes, 1, resources,
            landmarks: new[] { Def("herd", 7, new ClickYield(0, 9)) });
        var sim = new Simulation(content, Grass(), seed: 3);

        // Najdi dlaždici se stádem a jednu bez něj.
        (int X, int Y)? withLandmark = null, without = null;
        for (int i = 0; i < 400 && (withLandmark is null || without is null); i++)
        {
            int x = i % 20, y = i / 20;
            if (sim.LandmarkAt(x, y) >= 0)
            {
                withLandmark ??= (x, y);
            }
            else
            {
                without ??= (x, y);
            }
        }

        Assert.NotNull(withLandmark);
        Assert.NotNull(without);

        Assert.True(sim.TryHarvest(withLandmark!.Value.X, withLandmark.Value.Y, out _, out int rich));
        Assert.True(sim.TryHarvest(without!.Value.X, without.Value.Y, out _, out int plain));
        Assert.True(rich > plain, $"stádo má dát víc než holý biom ({rich} vs {plain})");
    }

    [Fact]
    public void OccupiedTile_HidesLandmark()
    {
        var content = TestContent.Build(landmarks: new[] { Def("herd", 3, null) });
        var sim = new Simulation(content, Grass(), seed: 2);

        // Najdi dlaždici s landmarkem a postav na ni — zástavba ho překryje.
        for (int i = 0; i < 400; i++)
        {
            int x = i % 20, y = i / 20;
            if (sim.LandmarkAt(x, y) < 0)
            {
                continue;
            }

            Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuilding(0, x, y));
            Assert.Equal(-1, sim.LandmarkAt(x, y));
            return;
        }

        Assert.Fail("test nenašel žádný landmark k zastavění");
    }

    [Fact]
    public void RealContent_HasDecorativeAndHarvestableLandmarks()
    {
        var content = TestData.LoadRealContent();

        Assert.True(content.Landmarks.Count >= 6, $"landmarků má být víc druhů, je {content.Landmarks.Count}");
        Assert.Contains(content.Landmarks.All, l => l.IsHarvestable);   // stáda, háje, žíly
        Assert.Contains(content.Landmarks.All, l => !l.IsHarvestable);  // gejzír, kaňon — čistá ozdoba
    }
}
