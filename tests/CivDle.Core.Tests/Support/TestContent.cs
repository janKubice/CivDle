using CivDle.Core.Content;
using CivDle.Core.Sim;

namespace CivDle.Core.Tests.Support;

/// <summary>
/// Stavba syntetického herního obsahu v kódu — pro testy, které potřebují
/// přesně řízené definice (fallback biom, stall výroby…) místo skutečných dat.
/// </summary>
internal static class TestContent
{
    public static readonly NoiseSpec Noise = new(Frequency: 1.5f, Octaves: 4, Persistence: 0.5f, Lacunarity: 2f);

    /// <summary>Vodní biom pokrývající všechny hloubky.</summary>
    public static Biome WaterBiome(string id = "water") => new(
        id, new RgbColor(0, 0, 128), 0f, IsWater: true,
        DepthRange: ValueRange.Full, ElevationRange: ValueRange.Full, MoistureRange: ValueRange.Full);

    /// <summary>Pevninský biom s daným pokrytím výšek.</summary>
    public static Biome LandBiome(string id, ValueRange? elevation = null) => new(
        id, new RgbColor(0, 128, 0), 0f, IsWater: false,
        DepthRange: ValueRange.Full,
        ElevationRange: elevation ?? ValueRange.Full,
        MoistureRange: ValueRange.Full);

    /// <summary>Složí kompletní <see cref="GameContent"/> z dodaných či výchozích částí.</summary>
    public static GameContent Build(
        IReadOnlyList<Biome>? biomes = null,
        int fallbackBiomeIndex = 1,
        IReadOnlyList<Resource>? resources = null,
        IReadOnlyList<BuildingDef>? buildings = null,
        GameplayConfig? gameplay = null,
        IReadOnlyList<TechDef>? techs = null,
        PrestigeConfig? prestige = null,
        IReadOnlyList<PrestigeUpgradeDef>? prestigeUpgrades = null,
        IReadOnlyList<QuestDef>? quests = null,
        DynamicQuestConfig? questsDynamic = null,
        IReadOnlyList<AchievementDef>? achievements = null)
    {
        biomes ??= new[] { WaterBiome(), LandBiome("grass") };
        resources ??= new[] { new Resource("wood", new RgbColor(140, 90, 40), StartAmount: 10, BaseStorage: 1000) };
        buildings ??= new[] { SimpleBuilding("hut", biomes.Count) };
        gameplay ??= DefaultGameplay;
        techs ??= Array.Empty<TechDef>();
        prestige ??= DefaultPrestige;
        prestigeUpgrades ??= Array.Empty<PrestigeUpgradeDef>();
        quests ??= Array.Empty<QuestDef>();
        questsDynamic ??= DefaultDynamicQuest;
        achievements ??= Array.Empty<AchievementDef>();

        var preset = new TerrainPreset("test", SeaLevel: 0.5f, fallbackBiomeIndex, Noise, Noise);
        var catalog = new WorldGenCatalog(
            new[] { new WorldSize("s", 64, 64) }, new[] { preset }, 0, 0);
        var language = new LanguageDef("test", "Test", new Dictionary<string, string> { ["_"] = "_" });

        return new GameContent(
            new BiomeRegistry(biomes),
            new DefRegistry<Resource>(resources, r => r.Id, "surovina"),
            new DefRegistry<BuildingDef>(buildings, b => b.Id, "budova"),
            new DefRegistry<TechDef>(techs, t => t.Id, "technologie", allowEmpty: true),
            prestige,
            new DefRegistry<PrestigeUpgradeDef>(prestigeUpgrades, u => u.Id, "upgrade Vzestupu", allowEmpty: true),
            new DefRegistry<QuestDef>(quests, q => q.Id, "úkol", allowEmpty: true),
            questsDynamic,
            new DefRegistry<AchievementDef>(achievements, a => a.Id, "achievement", allowEmpty: true),
            catalog,
            gameplay,
            new DefRegistry<LanguageDef>(new[] { language }, l => l.Id, "jazyk"),
            new[] { "Testov", "Zkouškovice" },
            Array.Empty<DecorationDef>(),
            Array.Empty<FaunaDef>(),
            Array.Empty<DevlogEntry>());
    }

    /// <summary>Výchozí prestige config testů (Vzestup od 50 obyvatel, body = populace ÷ 15).</summary>
    public static PrestigeConfig DefaultPrestige => new(
        new GoalCondition(MetricKind.Population, -1, 50), MetricKind.Population, -1, 15);

    /// <summary>Výchozí dynamický úkol testů (populace, práh roste ×1.5; bez odměny).</summary>
    public static DynamicQuestConfig DefaultDynamicQuest => new(
        new GoalCondition(MetricKind.Population, -1, 20), 1.5, Array.Empty<ResourceAmount>(), 1.5);

    /// <summary>Výchozí gameplay config testů (auto-stavba na dlouhém intervalu, ať do testů nezasahuje).</summary>
    public static GameplayConfig DefaultGameplay => new(
        StartingPopulation: 5, BaseHousingCapacity: 6,
        PopulationGrowthPerSecond: 0.12, FoodPerPersonPerSecond: 0.04, FoodResourceIndex: 0,
        AutoBuild: new AutoBuildConfig(IntervalTicks: 100_000, SearchRadius: 6, PopulationHeadroom: 2),
        Roads: new RoadConfig(new RgbColor(150, 145, 130), MaxSearchDistance: 60),
        Settlements: new SettlementConfig(MinBuildings: 3, ClusterDistance: 3, UpdateIntervalTicks: 50),
        DayNight: new DayNightConfig(
            DayLengthSeconds: 240, StartTimeOfDay: 0.32,
            NightColor: new RgbColor(10, 20, 48), DuskColor: new RgbColor(232, 134, 47),
            NightAlpha: 0.45, DuskAlpha: 0.18),
        // Krit vypnutý (critChance 0), ať testy sběru zůstanou deterministické.
        Boost: new BoostConfig(30, 120, 2.0),
        Harvest: new HarvestConfig(0.0, 5.0),
        DailyReward: new DailyRewardConfig(new[] { new ResourceAmount(0, 10) }, StreakCap: 7));

    /// <summary>Levná budova bez výroby povolená na všech biomech.</summary>
    public static BuildingDef SimpleBuilding(
        string id, int biomeCount, int housing = 0, bool buildable = true,
        int upgradesToIndex = -1, IReadOnlyList<ResourceAmount>? upgradeCost = null) => new(
        id, "test", new RgbColor(200, 100, 50), 1, 1,
        WorkerSlots: 0, HousingCapacity: housing,
        BuildCost: new[] { new ResourceAmount(0, 1) },
        Recipe: null,
        AllowedBiomes: Enumerable.Repeat(true, biomeCount).ToArray(),
        StorageBonus: Array.Empty<ResourceAmount>(),
        AutoBuild: false,
        Buildable: buildable,
        UpgradesToIndex: upgradesToIndex,
        UpgradeCost: upgradeCost ?? Array.Empty<ResourceAmount>());
}
