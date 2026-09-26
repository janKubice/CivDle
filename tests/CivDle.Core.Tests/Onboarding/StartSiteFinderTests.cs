using CivDle.Core.Content;
using CivDle.Core.Sim;
using CivDle.Core.Tests.Support;
using CivDle.Core.World;
using CivDle.Core.WorldGen;
using Xunit;

namespace CivDle.Core.Tests.Onboarding;

/// <summary>
/// Kde hra začne. Dřív to byla první souš od počátku — často savana nebo pláž,
/// kde klik nic neudělá a první budova nemá kam. Na první obrazovce má být
/// les na první klik, kámen a louka na dům a farmu.
/// </summary>
public class StartSiteFinderTests
{
    private const byte Water = 0;
    private const byte Grass = 1;
    private const byte Forest = 2;
    private const byte Rock = 3;

    private const int Wood = 0;
    private const int Stone = 1;

    private static GameContent Content(OnboardingConfig onboarding)
    {
        var biomes = new[]
        {
            TestContent.WaterBiome(),
            TestContent.LandBiome("grass"),
            TestContent.LandBiome("forest") with { ClickYield = new ClickYield(Wood, 2, Charges: 8) },
            TestContent.LandBiome("rock") with { ClickYield = new ClickYield(Stone, 1, Charges: 40) },
        };
        var resources = new[]
        {
            new Resource("wood", new RgbColor(1, 1, 1), StartAmount: 0, BaseStorage: 100),
            new Resource("stone", new RgbColor(1, 1, 1), StartAmount: 0, BaseStorage: 100),
        };
        bool[] grassOnly = { false, true, false, false };
        var house = TestContent.SimpleBuilding("house", 4, housing: 4) with { AllowedBiomes = grassOnly };
        var farm = TestContent.SimpleBuilding("farm", 4) with
        {
            AllowedBiomes = grassOnly,
            FootprintWidth = 2,
            FootprintHeight = 2,
        };

        return TestContent.Build(
            biomes, fallbackBiomeIndex: 1, resources, new[] { house, farm },
            TestContent.DefaultGameplay with { OnboardingOrNull = onboarding });
    }

    private static OnboardingConfig Wanted(int searchRadius = 70) => new(
        QuickStartSeeds: Array.Empty<long>(),
        StartRadius: 8,
        StartSearchRadius: searchRadius,
        StartNodes: new[] { new ResourceAmount(Wood, 6), new ResourceAmount(Stone, 3) },
        StartBuildings: new[] { 0, 1 },
        FirstDaySeconds: 0,
        FirstDayUntil: 0);

    /// <summary>
    /// Moře. Blízko počátku holý travnatý ostrov (bez lesa a kamene), dál
    /// ostrov, kde je všechno: louka, les i skála.
    /// </summary>
    private static Simulation World(OnboardingConfig onboarding)
    {
        var map = new WorldMap(90, 90);
        Array.Fill(map.BiomeIndices, Water);
        Paint(map, (5, 5, 12, 12), Grass);          // holý ostrov u počátku
        Paint(map, (40, 40, 58, 58), Grass);        // dobrý ostrov
        Paint(map, (42, 42, 45, 49), Forest);
        Paint(map, (53, 44, 55, 47), Rock);
        return new Simulation(Content(onboarding), new GridTerrain(map));
    }

    private static void Paint(WorldMap map, (int MinX, int MinY, int MaxX, int MaxY) area, byte biome)
    {
        for (int y = area.MinY; y <= area.MaxY; y++)
        {
            for (int x = area.MinX; x <= area.MaxX; x++)
            {
                map.BiomeIndices[map.Index(x, y)] = biome;
            }
        }
    }

    [Fact]
    public void ItSkipsTheBareIslandForTheOneWithEverything()
    {
        var sim = World(Wanted());

        var (x, y) = StartSiteFinder.Find(sim);

        Assert.Equal(Grass, sim.BiomeAt(x, y)); // táborák (a první dům) stojí na louce
        Assert.True(CountWithin(sim, x, y, 8, Forest) >= 6, $"na dohled od ({x},{y}) není les");
        Assert.True(CountWithin(sim, x, y, 8, Rock) >= 3, $"na dohled od ({x},{y}) není kámen");
    }

    [Fact]
    public void WithoutTheOnboardingBlockItStartsOnTheFirstLand()
    {
        // Starší data a mody bez bloku dostanou přesně to, co dřív.
        var sim = World(OnboardingConfig.Disabled);

        var (x, y) = StartSiteFinder.Find(sim);

        Assert.Equal((5, 5), (x, y));
    }

    [Fact]
    public void WhenNothingFitsItTakesTheBestAvailable()
    {
        // Svět bez skály: kámen nesplní nic, ale les a louka ano — lepší než holý ostrov.
        var map = new WorldMap(90, 90);
        Array.Fill(map.BiomeIndices, Water);
        Paint(map, (5, 5, 12, 12), Grass);
        Paint(map, (40, 40, 58, 58), Grass);
        Paint(map, (42, 42, 45, 49), Forest);
        var sim = new Simulation(Content(Wanted()), new GridTerrain(map));

        var (x, y) = StartSiteFinder.Find(sim);

        Assert.True(CountWithin(sim, x, y, 8, Forest) >= 6, $"({x},{y}) nemá ani les");
    }

    [Fact]
    public void EveryQuickStartSeedStartsWhereThereIsSomethingToDo()
    {
        // Prověřené světy z „Hrát". Kdyby změna generátoru terénu posunula les
        // mimo dohled, první klik by zase šel do prázdna — tenhle test to chytí.
        var content = TestData.LoadRealContent();
        var onboarding = content.Gameplay.Onboarding;
        Assert.NotEmpty(onboarding.QuickStartSeeds);
        var preset = content.WorldGen.Presets[content.WorldGen.DefaultPresetIndex];

        foreach (long seed in onboarding.QuickStartSeeds)
        {
            var sim = new Simulation(content, new ProceduralTerrain(content.Biomes, preset, seed), seed);

            var (x, y) = StartSiteFinder.Find(sim);

            foreach (var need in onboarding.StartNodes)
            {
                int nodes = 0;
                ForEachWithin(x, y, onboarding.StartRadius, (tx, ty) =>
                {
                    if (content.Biomes[sim.BiomeAt(tx, ty)].ClickYield?.ResourceIndex == need.ResourceIndex)
                    {
                        nodes++;
                    }
                });

                Assert.True(nodes >= need.Amount,
                    $"seed {seed}: u startu ({x},{y}) je {nodes}× {content.Resources[need.ResourceIndex].Id}, chce se {need.Amount}");
            }

            var firstBuilding = content.Buildings[onboarding.StartBuildings[0]];
            Assert.True(firstBuilding.AllowedBiomes[sim.BiomeAt(x, y)],
                $"seed {seed}: na místě startu ({x},{y}) nejde postavit {firstBuilding.Id}");
        }
    }

    private static int CountWithin(Simulation sim, int cx, int cy, int radius, byte biome)
    {
        int count = 0;
        ForEachWithin(cx, cy, radius, (x, y) =>
        {
            if (sim.BiomeAt(x, y) == biome)
            {
                count++;
            }
        });
        return count;
    }

    private static void ForEachWithin(int cx, int cy, int radius, Action<int, int> visit)
    {
        for (int y = cy - radius; y <= cy + radius; y++)
        {
            for (int x = cx - radius; x <= cx + radius; x++)
            {
                visit(x, y);
            }
        }
    }
}
