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
        DepthRange: ValueRange.Full, ElevationRange: ValueRange.Full, MoistureRange: ValueRange.Full,
        TemperatureRange: ValueRange.Full);

    /// <summary>Pevninský biom s daným pokrytím výšek.</summary>
    public static Biome LandBiome(string id, ValueRange? elevation = null) => new(
        id, new RgbColor(0, 128, 0), 0f, IsWater: false,
        DepthRange: ValueRange.Full,
        ElevationRange: elevation ?? ValueRange.Full,
        MoistureRange: ValueRange.Full,
        TemperatureRange: ValueRange.Full);

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
        IReadOnlyList<AchievementDef>? achievements = null,
        IReadOnlyList<EventDef>? events = null,
        IReadOnlyList<EraDef>? eras = null,
        IReadOnlyList<ZoneTypeDef>? zoneTypes = null,
        IReadOnlyList<GrowthPolicyDef>? policies = null,
        IReadOnlyList<AscensionTierDef>? ascensionTiers = null,
        IReadOnlyList<WeatherDef>? weather = null,
        IReadOnlyList<LandmarkDef>? landmarks = null,
        IReadOnlyList<FeatureDef>? features = null,
        UfoConfig? ufo = null,
        IReadOnlyList<TutorialStepDef>? tutorial = null,
        ChallengeCatalog? challenges = null,
        ElectionConfig? elections = null,
        IReadOnlyList<MilestoneDef>? milestones = null,
        SeasonCalendar? seasons = null,
        ContractCatalog? contracts = null,
        DistrictCatalog? districts = null,
        SettlementRankLadder? settlementRanks = null,
        CitizenCatalog? citizens = null,
        NeighbourCatalog? neighbours = null)
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
        events ??= Array.Empty<EventDef>();
        eras ??= Array.Empty<EraDef>();
        zoneTypes ??= Array.Empty<ZoneTypeDef>();
        policies ??= Array.Empty<GrowthPolicyDef>();
        ascensionTiers ??= Array.Empty<AscensionTierDef>();
        weather ??= Array.Empty<WeatherDef>();
        landmarks ??= Array.Empty<LandmarkDef>();
        features ??= Array.Empty<FeatureDef>();

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
            new DefRegistry<EventDef>(events, e => e.Id, "událost", allowEmpty: true),
            new DefRegistry<EraDef>(eras, e => e.Id, "éra", allowEmpty: true),
            catalog,
            gameplay,
            new DefRegistry<LanguageDef>(new[] { language }, l => l.Id, "jazyk"),
            new[] { "Testov", "Zkouškovice" },
            Array.Empty<DecorationDef>(),
            Array.Empty<FaunaDef>(),
            Array.Empty<DevlogEntry>(),
            new DefRegistry<ZoneTypeDef>(zoneTypes, z => z.Id, "typ zóny", allowEmpty: true),
            new DefRegistry<GrowthPolicyDef>(policies, p => p.Id, "politika", allowEmpty: true),
            new DefRegistry<AscensionTierDef>(ascensionTiers, t => t.Id, "stupeň měřítka", allowEmpty: true),
            new DefRegistry<WeatherDef>(weather, w => w.Id, "počasí", allowEmpty: true),
            new DefRegistry<LandmarkDef>(landmarks, l => l.Id, "landmark", allowEmpty: true),
            new DefRegistry<FeatureDef>(features, f => f.Id, "funkce", allowEmpty: true),
            ufo ?? UfoConfig.Disabled,
            Array.Empty<AmbienceDef>(),
            new DefRegistry<TerraformDef>(Array.Empty<TerraformDef>(), t => t.Id, "terraformace", allowEmpty: true),
            tutorial ?? Array.Empty<TutorialStepDef>(),
            challenges ?? ChallengeCatalog.Empty,
            contracts ?? ContractCatalog.Empty,
            districts ?? DistrictCatalog.Empty,
            settlementRanks ?? SettlementRankLadder.Empty,
            citizens ?? CitizenCatalog.Empty,
            neighbours ?? NeighbourCatalog.Empty,
            elections ?? ElectionConfig.Disabled,
            milestones ?? Array.Empty<MilestoneDef>(),
            seasons ?? SeasonCalendar.Disabled);
    }

    /// <summary>Výchozí prestige config testů (Vzestup od 50 obyvatel, body = populace ÷ 15).</summary>
    public static PrestigeConfig DefaultPrestige => new(
        new GoalCondition(MetricKind.Population, -1, 50), MetricKind.Population, -1, 15);

    /// <summary>Výchozí dynamický úkol testů (populace, práh roste ×1.5; bez odměny).</summary>
    public static DynamicQuestConfig DefaultDynamicQuest => new(
        new GoalCondition(MetricKind.Population, -1, 20), 1.5, Array.Empty<ResourceAmount>(), 1.5);

    /// <summary>Výchozí gameplay config testů (auto-stavba na dlouhém intervalu, ať do testů nezasahuje).</summary>
    public static GameplayConfig DefaultGameplay => new(
        // Testy začínají na prázdné mapě schválně: každý test si postaví přesně
        // to, co měří. Startovní domek se testuje zvlášť (StartingBuildingsTests).
        StartingPopulation: 5, StartingBuildingIndices: Array.Empty<int>(), BaseHousingCapacity: 6,
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
        DailyReward: new DailyRewardConfig(new[] { new ResourceAmount(0, 10) }, StreakCap: 7),
        Planting: new PlantingConfig(new[] { new ResourceAmount(0, 5) }, ResourceIndex: 0, Amount: 2));

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
        UpgradeCost: upgradeCost ?? Array.Empty<ResourceAmount>(),
        PowerSupply: 0,
        PowerDemand: 0);

    /// <summary>Budova poskytující službu za pravidelnou údržbu — pro testy spokojenosti.</summary>
    public static BuildingDef Service(
        string id, int serviceValue, int upkeepResource, int upkeepAmount, int biomeCount = 2) => new(
        id, "civic", new RgbColor(180, 160, 220), 1, 1,
        WorkerSlots: 0, HousingCapacity: 0,
        BuildCost: Array.Empty<ResourceAmount>(),
        Recipe: null,
        AllowedBiomes: Enumerable.Repeat(true, biomeCount).ToArray(),
        StorageBonus: Array.Empty<ResourceAmount>(),
        AutoBuild: false,
        Buildable: true,
        UpgradesToIndex: -1,
        UpgradeCost: Array.Empty<ResourceAmount>(),
        PowerSupply: 0,
        PowerDemand: 0,
        RequiresAdjacentWater: false,
        ServiceValue: serviceValue,
        UpkeepOrNull: new[] { new ResourceAmount(upkeepResource, upkeepAmount) });

    /// <summary>Budova, která z ničeho vyrábí zadanou surovinu — pro testy výroby.</summary>
    public static BuildingDef Producer(
        string id, int outputResource, int amount, int timeTicks, int biomeCount = 2, int workerSlots = 1) => new(
        id, "test", new RgbColor(120, 160, 90), 1, 1,
        WorkerSlots: workerSlots, HousingCapacity: 0,
        BuildCost: Array.Empty<ResourceAmount>(),
        Recipe: new Recipe(
            Array.Empty<ResourceAmount>(),
            new[] { new ResourceAmount(outputResource, amount) },
            timeTicks),
        AllowedBiomes: Enumerable.Repeat(true, biomeCount).ToArray(),
        StorageBonus: Array.Empty<ResourceAmount>(),
        AutoBuild: false,
        Buildable: true,
        UpgradesToIndex: -1,
        UpgradeCost: Array.Empty<ResourceAmount>(),
        PowerSupply: 0,
        PowerDemand: 0);
}
