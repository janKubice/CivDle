using CivDle.Core.Content;
using CivDle.Core.Save;
using CivDle.Core.Sim;
using CivDle.Core.Tests.Support;
using CivDle.Core.World;
using Xunit;

namespace CivDle.Core.Tests.Sim;

/// <summary>
/// Zvláštní pravidla výzev (<see cref="ScenarioRule"/>) a jejich odměny.
///
/// <para>Každé pravidlo má dvě strany a obě se hlídají: ve výzvě musí dělat,
/// co slibuje, a <b>mimo ni nesmí dělat nic</b> — pravidlo, které prosákne do
/// volné hry, by hráči tiše rozbilo hlavní město. Pravidla, která mění svět
/// (zatopení, jeden biom, rychlejší vlny), navíc musí přežít save: jinak by se
/// po načtení změnila mapa pod rozestavěným městem.</para>
/// </summary>
public class ChallengeRuleTests
{
    // ----- silnice -----

    [Fact]
    public void NoRoadsForbidsRoadsForThePlayer()
    {
        var free = FreeGame(RuleContent(ScenarioRule.NoRoads));
        var challenge = Challenge(RuleContent(ScenarioRule.NoRoads));

        Assert.True(free.RoadsAllowed);
        Assert.Equal(PlacementResult.Ok, free.CanBuildRoad(3, 3));

        Assert.False(challenge.RoadsAllowed);
        Assert.Equal(PlacementResult.NotUnlocked, challenge.CanBuildRoad(3, 3));
        Assert.Equal(PlacementResult.NotUnlocked, challenge.TryBuildRoad(3, 3));
        Assert.Empty(challenge.RoadTiles);
    }

    [Fact]
    public void NoRoadsMakesEveryWorkshopRunAsDisconnected()
    {
        // Bez pravidla a bez silnic platí všechno za napojené (první chalupa
        // se nemá k čemu připojit). S pravidlem svoz trpí všude.
        var gameplay = QuietGameplay with
        {
            Roads = TestContent.DefaultGameplay.Roads with { DisconnectedProductionMult = 0.5 },
        };
        var content = RuleContent(ScenarioRule.NoRoads, gameplay: gameplay, producer: true);

        double free = Produced(FreeGame(content), ticks: 3000);
        double challenge = Produced(Challenge(content), ticks: 3000);

        Assert.True(free > 0);
        Assert.InRange(challenge / free, 0.47, 0.53);
    }

    [Fact]
    public void AutoRoadsConnectNothingInACityWithoutRoads()
    {
        // Tatáž dvojice domů v témž světě: ve volné hře je auto-silnice spojí,
        // ve výzvě ne.
        var content = TestData.LoadRealContent();
        int index = content.Scenarios.IndexOf("no_roads");
        var free = new Simulation(content, ScenarioWorld.TerrainFor(content, content.Scenarios[index]), seed: 1);
        var challenge = ScenarioWorld.Create(content, index);

        foreach (var sim in new[] { free, challenge })
        {
            var (x, y) = StartSiteFinder.Find(sim);
            int house = content.Buildings.IndexOf("house");
            sim.AddResource(content.Resources.IndexOf("wood"), 200);
            sim.AddResource(content.Resources.IndexOf("planks"), 200);
            Assert.True(PlaceNear(sim, house, x, y));
            Assert.True(PlaceNear(sim, house, x + 7, y));
            Assert.True(PlaceNear(sim, house, x, y + 7));
            for (int i = 0; i < 200; i++)
            {
                sim.Tick();
            }
        }

        Assert.NotEmpty(free.RoadTiles);
        Assert.Empty(challenge.RoadTiles);
    }

    // ----- výzkum -----

    [Fact]
    public void NoResearchLocksEveryTechOnlyInsideTheChallenge()
    {
        var content = TestData.LoadRealContent();
        int tech = FirstRootTech(content);

        var free = new Simulation(content, new UniformTerrain(1), seed: 1);
        var challenge = ScenarioWorld.Create(content, content.Scenarios.IndexOf("no_books"));

        Assert.NotEqual(PlacementResult.NotUnlocked, free.CanResearch(tech));
        Assert.False(challenge.ResearchAllowed);
        Assert.Equal(PlacementResult.NotUnlocked, challenge.CanResearch(tech));
        Assert.Equal(PlacementResult.NotUnlocked, challenge.TryResearch(tech));
    }

    // ----- údržba -----

    [Fact]
    public void HighUpkeepMultipliesWhatServicesCost()
    {
        var gameplay = QuietGameplay with
        {
            BaseHousingCapacity = 100,
            HappinessOrNull = new HappinessConfig(
                IntervalTicks: 1, BaseHappiness: 0.5, ServiceWeight: 0.5, OvercrowdingPenalty: 0.25,
                PeoplePerServicePoint: 10, GrowthFloor: 0.2, FreePopulation: 0),
        };
        var content = RuleContent(ScenarioRule.HighUpkeep, gameplay: gameplay, service: true);

        var free = FreeGame(content);
        var challenge = Challenge(content);
        Assert.Equal(1.0, free.UpkeepMult);
        Assert.Equal(ChallengeRulesConfig.Default.HighUpkeepMult, challenge.UpkeepMult);

        double freeUpkeep = UpkeepPaid(free);
        double challengeUpkeep = UpkeepPaid(challenge);

        Assert.True(freeUpkeep > 0);
        Assert.Equal(ChallengeRulesConfig.Default.HighUpkeepMult, challengeUpkeep / freeUpkeep, precision: 3);
    }

    [Fact]
    public void FrugalPolicyIsLockedUntilTheChallengeIsWon()
    {
        var content = RuleContent(ScenarioRule.HighUpkeep, frugalPolicy: true);
        var sim = Challenge(content);

        Assert.False(sim.IsPolicyAvailable(0));
        Assert.False(sim.TogglePolicy(0));
        Assert.False(sim.IsPolicyActive(0));

        sim.SetProfileUnlocks(new[] { ChallengeRewards.KeyOf(TestScenarioId) });

        Assert.True(sim.IsPolicyAvailable(0));
        Assert.True(sim.TogglePolicy(0));
        Assert.Equal(ChallengeRulesConfig.Default.HighUpkeepMult * 0.5, sim.UpkeepMult, precision: 6);

        // Vypnout jde vždycky — i kdyby profil odměnu mezitím ztratil.
        sim.SetProfileUnlocks(Array.Empty<string>());
        Assert.False(sim.TogglePolicy(0));
        Assert.Equal(ChallengeRulesConfig.Default.HighUpkeepMult, sim.UpkeepMult, precision: 6);
    }

    // ----- noc -----

    [Fact]
    public void EternalNightStopsTheClockAtNight()
    {
        var content = RuleContent(ScenarioRule.NightWorld);
        var free = FreeGame(content);
        var challenge = Challenge(content);
        double freeStart = free.TimeOfDay01;

        for (int i = 0; i < 700; i++)
        {
            free.Tick();
            challenge.Tick();
        }

        Assert.NotEqual(freeStart, free.TimeOfDay01);
        Assert.True(challenge.IsEternalNight);
        Assert.Equal(ChallengeRulesConfig.Default.NightTimeOfDay, challenge.TimeOfDay01);
    }

    [Fact]
    public void EternalNightCutsFoodProduction()
    {
        // V testovacím obsahu je jídlo surovina 0 — stejná, kterou vyrábí výrobna.
        var content = RuleContent(ScenarioRule.NightWorld, gameplay: QuietGameplay, producer: true);

        double free = Produced(FreeGame(content), ticks: 3000);
        double challenge = Produced(Challenge(content), ticks: 3000);

        double expected = ChallengeRulesConfig.Default.NightFoodMult;
        Assert.InRange(challenge / free, expected - 0.03, expected + 0.03);
    }

    // ----- svět -----

    [Fact]
    public void FloodedWorldRaisesTheSeaAndLeavesLessLand()
    {
        var content = TestData.LoadRealContent();
        var scenario = content.Scenarios[content.Scenarios.IndexOf("great_flood")];
        var basePreset = content.WorldGen.Presets[scenario.PresetIndex];
        var flooded = ScenarioWorld.PresetFor(content, scenario);

        Assert.Equal(
            basePreset.SeaLevel + (float)content.Gameplay.ChallengeRules.FloodSeaLevelRise, flooded.SeaLevel, precision: 4);

        int baseLand = LandTiles(content, new ProceduralTerrain(content.Biomes, basePreset, scenario.Seed));
        int floodedLand = LandTiles(content, ScenarioWorld.TerrainFor(content, scenario));
        Assert.True(floodedLand < baseLand * 0.8, $"souše mělo ubýt: {floodedLand} vs {baseLand}");
        Assert.True(floodedLand > 0, "trocha souše musí zůstat, jinak není kde stavět");
    }

    [Fact]
    public void SingleBiomeFillsLandButKeepsWaterAndOases()
    {
        var biomes = new BiomeRegistry(new[]
        {
            TestContent.WaterBiome(), TestContent.LandBiome("grass"), TestContent.LandBiome("desert"),
        });
        var land = new SingleBiomeTerrain(new UniformTerrain(1), biomes, 2, chunkTiles: 8, oasisShare: 0.2, seed: 42);
        var sea = new SingleBiomeTerrain(new UniformTerrain(0), biomes, 2, chunkTiles: 8, oasisShare: 0.2, seed: 42);

        int oasis = 0, total = 0;
        for (int y = -200; y < 200; y += 3)
        {
            for (int x = -200; x < 200; x += 3)
            {
                total++;
                byte biome = land.BiomeAt(x, y);
                Assert.True(biome is 1 or 2);
                Assert.Equal(land.IsOasis(x, y), biome == 1);
                if (biome == 1)
                {
                    oasis++;
                }

                Assert.Equal(0, sea.BiomeAt(x, y)); // voda zůstává vodou
            }
        }

        Assert.InRange(oasis / (double)total, 0.12, 0.28);
    }

    [Fact]
    public void OasesAreWholeChunksAndDependOnlyOnTheSeed()
    {
        var biomes = new BiomeRegistry(new[]
        {
            TestContent.WaterBiome(), TestContent.LandBiome("grass"), TestContent.LandBiome("desert"),
        });
        var a = new SingleBiomeTerrain(new UniformTerrain(1), biomes, 2, 8, 0.3, seed: 7);
        var b = new SingleBiomeTerrain(new UniformTerrain(1), biomes, 2, 8, 0.3, seed: 7);
        var other = new SingleBiomeTerrain(new UniformTerrain(1), biomes, 2, 8, 0.3, seed: 8);

        bool differs = false;
        for (int chunkY = -10; chunkY < 10; chunkY++)
        {
            for (int chunkX = -10; chunkX < 10; chunkX++)
            {
                bool oasis = a.IsOasis(chunkX * 8, chunkY * 8);
                Assert.Equal(oasis, a.IsOasis(chunkX * 8 + 7, chunkY * 8 + 5)); // celý čtverec stejně
                Assert.Equal(oasis, b.IsOasis(chunkX * 8 + 3, chunkY * 8 + 3)); // stejný seed, stejný svět
                differs |= oasis != other.IsOasis(chunkX * 8, chunkY * 8);
            }
        }

        Assert.True(differs, "jiný seed má mít oázy jinde");
    }

    [Fact]
    public void DefenceFromStartTurnsDefenceOnWithFasterWaves()
    {
        var content = TestData.LoadRealContent();
        var scenario = content.Scenarios[content.Scenarios.IndexOf("on_the_walls")];
        var rules = content.Gameplay.ChallengeRules;

        var scenarioContent = ScenarioWorld.ContentFor(content, scenario);
        var sim = ScenarioWorld.Create(content, content.Scenarios.IndexOf("on_the_walls"));

        Assert.True(sim.FrontierDefense);
        Assert.Equal(Math.Min(content.Frontier.FirstWaveTick, rules.DefenceFirstWaveTick), sim.Frontier.NextWaveTick);
        Assert.Equal(
            (int)Math.Round(content.Frontier.WaveIntervalTicks * rules.DefenceWaveIntervalMult),
            scenarioContent.Frontier.WaveIntervalTicks);

        // Volná hra se nezmění: obrana vypnutá, rozvrh z dat.
        var free = new Simulation(content, new UniformTerrain(1), seed: 1);
        Assert.False(free.FrontierDefense);
    }

    [Fact]
    public void RulesDoNothingOutsideAChallenge()
    {
        var content = TestData.LoadRealContent();
        var free = new Simulation(content, new UniformTerrain(1), seed: 1);

        Assert.True(free.RoadsAllowed);
        Assert.True(free.ResearchAllowed);
        Assert.False(free.IsEternalNight);
        Assert.Equal(1.0, free.NightFoodMult);
        Assert.Equal(1.0, free.UpkeepMult);
    }

    // ----- save -----

    [Fact]
    public void AChallengeWorldIsRebuiltTheSameAfterLoading()
    {
        var content = TestData.LoadRealContent();
        var serializer = new SaveGameSerializer();

        foreach (string id in new[] { "great_flood", "only_sand", "on_the_walls" })
        {
            int index = content.Scenarios.IndexOf(id);
            var scenario = content.Scenarios[index];
            var original = ScenarioWorld.Create(content, index);
            for (int i = 0; i < 20; i++)
            {
                original.Tick();
            }

            var preset = ScenarioWorld.PresetFor(content, scenario);
            using var stream = new MemoryStream();
            serializer.Write(stream, original, new SaveMetadata(scenario.Seed, "default", preset.Id, DateTime.UtcNow));
            stream.Position = 0;
            var (loaded, _) = serializer.Read(stream, content);

            Assert.Equal(id, loaded.Scenario?.Id);
            Assert.Equal(original.Frontier.NextWaveTick, loaded.Frontier.NextWaveTick);
            Assert.Equal(original.FrontierDefense, loaded.FrontierDefense);
            for (int y = -90; y <= 90; y += 9)
            {
                for (int x = -90; x <= 90; x += 9)
                {
                    Assert.Equal(original.BiomeAt(x, y), loaded.BiomeAt(x, y));
                }
            }
        }
    }

    // ----- odměny -----

    [Fact]
    public void WonChallengesBecomeUnlockKeysAndAllOfThemUnlockTheMaster()
    {
        var content = TestData.LoadRealContent();
        var catalog = content.Scenarios;

        var some = ChallengeRewards.UnlockKeys(catalog, new[] { "sprint", "no_longer_in_data" });
        Assert.Equal(new[] { "challenge:sprint" }, some);

        var all = ChallengeRewards.UnlockKeys(catalog, catalog.Scenarios.Select(s => s.Id));
        Assert.Contains(ChallengeRewards.KeyOf(ChallengeRewards.AllChallengesId), all);
        Assert.Equal(catalog.Count, ChallengeRewards.WonCount(catalog, catalog.Scenarios.Select(s => s.Id)));
    }

    [Fact]
    public void ARewardBuildingIsBuildableOnlyWithTheWonChallengeInTheProfile()
    {
        var content = TestData.LoadRealContent();
        int stilt = content.Buildings.IndexOf("stilt_house");
        var sim = new Simulation(content, new UniformTerrain(1), seed: 1);

        Assert.False(sim.IsBuildingBuildable(stilt));

        sim.SetProfileUnlocks(ChallengeRewards.UnlockKeys(content.Scenarios, new[] { "great_flood" }));
        Assert.True(sim.IsBuildingBuildable(stilt));
    }

    [Fact]
    public void EveryChallengeInTheDataHasAReward()
    {
        var content = TestData.LoadRealContent();
        var keys = content.Buildings.All.Select(b => b.UnlockedBy)
            .Concat(content.Policies.All.Select(p => p.UnlockedBy))
            .Where(k => k is not null)
            .ToHashSet();

        foreach (var scenario in content.Scenarios.Scenarios)
        {
            Assert.True(keys.Contains(ChallengeRewards.KeyOf(scenario.Id)), $"výzva '{scenario.Id}' nic neodemyká");
        }

        Assert.Contains(ChallengeRewards.KeyOf(ChallengeRewards.AllChallengesId), keys);
    }

    // ----- pomocníci -----

    private const string TestScenarioId = "rule_test";

    private static GameContent RuleContent(
        ScenarioRule rule,
        GameplayConfig? gameplay = null,
        bool producer = false,
        bool service = false,
        bool frugalPolicy = false)
    {
        var biomes = new[] { TestContent.WaterBiome(), TestContent.LandBiome("grass") };
        var buildings = new List<BuildingDef> { TestContent.SimpleBuilding("hut", biomes.Length) };
        if (producer)
        {
            buildings.Add(TestContent.Producer("farm", outputResource: 0, amount: 1, timeTicks: 10, biomeCount: biomes.Length));
        }

        if (service)
        {
            buildings.Add(TestContent.Service("shrine", serviceValue: 3, upkeepResource: 0, upkeepAmount: 1, biomeCount: biomes.Length));
        }

        var policies = frugalPolicy
            ? new[] { new GrowthPolicyDef("frugal", "upkeep_discount", 50, ChallengeRewards.KeyOf(TestScenarioId)) }
            : Array.Empty<GrowthPolicyDef>();

        return TestContent.Build(
            biomes: biomes,
            buildings: buildings,
            gameplay: gameplay,
            policies: policies,
            scenarios: new ScenarioCatalog(new[]
            {
                new ScenarioDef(
                    TestScenarioId,
                    Seed: 7,
                    PresetIndex: -1,
                    GameplayOverride.None,
                    Array.Empty<ResourceAmount>(),
                    new GoalCondition(MetricKind.TotalBuildings, -1, 1000),
                    FailBelow: null,
                    TimeLimitSeconds: 0,
                    new[] { rule }),
            }));
    }

    private static Simulation FreeGame(GameContent content) => new(content, new UniformTerrain(1), seed: 1);

    private static Simulation Challenge(GameContent content)
    {
        var sim = new Simulation(content, new UniformTerrain(1), seed: 1);
        sim.StartScenario(0);
        return sim;
    }

    /// <summary>
    /// Hra, ve které se nic samo nejí ani nerodí — co přibude, vyrobila výrobna,
    /// co ubude, snědla údržba. Měření je pak prostý rozdíl skladu.
    /// </summary>
    private static GameplayConfig QuietGameplay => TestContent.DefaultGameplay with
    {
        FoodPerPersonPerSecond = 0.0,
        PopulationGrowthPerSecond = 0.0,
    };

    /// <summary>Kolik suroviny 0 výrobna vyrobila (výrobna je budova 1, stavba je zdarma a hned).</summary>
    private static double Produced(Simulation sim, int ticks)
    {
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuilding(1, 2, 2));
        double before = sim.GetResource(0);
        for (int i = 0; i < ticks; i++)
        {
            sim.Tick();
        }

        return sim.GetResource(0) - before;
    }

    /// <summary>Kolik suroviny 0 služba za 100 tiků spotřebovala na údržbu.</summary>
    private static double UpkeepPaid(Simulation sim)
    {
        Assert.Equal(PlacementResult.Ok, sim.TryPlaceBuilding(1, 2, 2));
        sim.SetPopulationForTest(50);
        sim.AddResource(0, 500);
        double before = sim.GetResource(0);
        for (int i = 0; i < 100; i++)
        {
            sim.Tick();
        }

        return before - sim.GetResource(0);
    }

    private static bool PlaceNear(Simulation sim, int defIndex, int x, int y)
    {
        for (int r = 0; r < 12; r++)
        {
            for (int dy = -r; dy <= r; dy++)
            {
                for (int dx = -r; dx <= r; dx++)
                {
                    if (Math.Max(Math.Abs(dx), Math.Abs(dy)) == r
                        && sim.TryPlaceBuilding(defIndex, x + dx, y + dy) == PlacementResult.Ok)
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    private static int FirstRootTech(GameContent content)
    {
        for (int i = 0; i < content.Techs.Count; i++)
        {
            if (content.Techs[i].PrerequisiteIndices.Count == 0)
            {
                return i;
            }
        }

        throw new InvalidOperationException("V datech není technologie bez prerekvizit.");
    }

    private static int LandTiles(GameContent content, ITerrain terrain)
    {
        int land = 0;
        for (int y = -150; y <= 150; y += 3)
        {
            for (int x = -150; x <= 150; x += 3)
            {
                if (!content.Biomes[terrain.BiomeAt(x, y)].IsWater)
                {
                    land++;
                }
            }
        }

        return land;
    }
}
