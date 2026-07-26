namespace CivDle.Core.Content;

/// <summary>
/// Veškerý načtený a zvalidovaný herní obsah (definice typů). Vzniká jednou při startu
/// v <see cref="ContentLoader"/> a dál se jen čte — systémy ho dostávají závislostí (DI),
/// žádný globální singleton.
/// </summary>
public sealed class GameContent
{
    public GameContent(
        BiomeRegistry biomes,
        DefRegistry<Resource> resources,
        DefRegistry<BuildingDef> buildings,
        DefRegistry<TechDef> techs,
        PrestigeConfig prestige,
        DefRegistry<PrestigeUpgradeDef> prestigeUpgrades,
        DefRegistry<QuestDef> quests,
        DynamicQuestConfig questsDynamic,
        DefRegistry<AchievementDef> achievements,
        DefRegistry<EventDef> events,
        DefRegistry<EraDef> eras,
        WorldGenCatalog worldGen,
        GameplayConfig gameplay,
        DefRegistry<LanguageDef> languages,
        IReadOnlyList<string> settlementNames,
        IReadOnlyList<DecorationDef> decorations,
        IReadOnlyList<FaunaDef> fauna,
        IReadOnlyList<DevlogEntry> devlog,
        DefRegistry<ZoneTypeDef> zoneTypes,
        DefRegistry<GrowthPolicyDef> policies,
        DefRegistry<AscensionTierDef> ascensionTiers,
        DefRegistry<WeatherDef> weather,
        DefRegistry<LandmarkDef> landmarks,
        DefRegistry<FeatureDef> features,
        UfoConfig ufo)
    {
        Biomes = biomes;
        Resources = resources;
        Buildings = buildings;
        Techs = techs;
        Prestige = prestige;
        PrestigeUpgrades = prestigeUpgrades;
        Quests = quests;
        QuestsDynamic = questsDynamic;
        Achievements = achievements;
        Events = events;
        Eras = eras;
        WorldGen = worldGen;
        Gameplay = gameplay;
        Languages = languages;
        SettlementNames = settlementNames;
        Decorations = decorations;
        Fauna = fauna;
        Devlog = devlog;
        ZoneTypes = zoneTypes;
        Policies = policies;
        AscensionTiers = ascensionTiers;
        Weather = weather;
        Landmarks = landmarks;
        Features = features;
        Ufo = ufo;
    }

    /// <summary>Definice technologií z <c>data/tech.json</c> (tech tree).</summary>
    public DefRegistry<TechDef> Techs { get; }

    /// <summary>Nastavení Vzestupu z <c>data/prestige.json</c>.</summary>
    public PrestigeConfig Prestige { get; }

    /// <summary>Trvalé upgrady Vzestupu z <c>data/prestige.json</c> (smí být prázdné).</summary>
    public DefRegistry<PrestigeUpgradeDef> PrestigeUpgrades { get; }

    /// <summary>Pevné úkoly z <c>data/quests.json</c> (smí být prázdné).</summary>
    public DefRegistry<QuestDef> Quests { get; }

    /// <summary>Nastavení dynamických (opakujících se) úkolů z <c>data/quests.json</c>.</summary>
    public DynamicQuestConfig QuestsDynamic { get; }

    /// <summary>Achievementy z <c>data/achievements.json</c> (smí být prázdné). Účet-wide.</summary>
    public DefRegistry<AchievementDef> Achievements { get; }

    /// <summary>Náhodné události s volbami z <c>data/events.json</c> (smí být prázdné).</summary>
    public DefRegistry<EventDef> Events { get; }

    /// <summary>Éry civilizace z <c>data/eras.json</c> (progrese T0–T6; smí být prázdné).</summary>
    public DefRegistry<EraDef> Eras { get; }

    /// <summary>Typy zón pro automatizaci z <c>data/zones.json</c> (smí být prázdné).</summary>
    public DefRegistry<ZoneTypeDef> ZoneTypes { get; }

    /// <summary>Politiky růstu z <c>data/policies.json</c> (automatizace, stupeň 4; smí být prázdné).</summary>
    public DefRegistry<GrowthPolicyDef> Policies { get; }

    /// <summary>Stupně měřítka z <c>data/ascension-tiers.json</c> (stropy populace, megastruktury; smí být prázdné).</summary>
    public DefRegistry<AscensionTierDef> AscensionTiers { get; }

    /// <summary>Počasí z <c>data/weather.json</c> (živá mapa; smí být prázdné).</summary>
    public DefRegistry<WeatherDef> Weather { get; }

    /// <summary>Landmarky z <c>data/landmarks.json</c> (body zájmu; smí být prázdné).</summary>
    public DefRegistry<LandmarkDef> Landmarks { get; }

    /// <summary>Odemykatelné funkce z <c>data/features.json</c> (postupné odhalování UI; smí být prázdné).</summary>
    public DefRegistry<FeatureDef> Features { get; }

    /// <summary>Návštěvy UFO z <c>data/ufo.json</c> (living-map; smí být vypnuté).</summary>
    public UfoConfig Ufo { get; }

    /// <summary>Vývojový deník z <c>data/devlog.json</c> (smí být prázdný).</summary>
    public IReadOnlyList<DevlogEntry> Devlog { get; }

    /// <summary>Jména osad z <c>data/settlement-names.json</c> (vlastní jména se nepřekládají).</summary>
    public IReadOnlyList<string> SettlementNames { get; }

    /// <summary>Biomové dekorace z <c>data/decorations.json</c> (smí být prázdné).</summary>
    public IReadOnlyList<DecorationDef> Decorations { get; }

    /// <summary>Ambientní fauna z <c>data/fauna.json</c> (smí být prázdné).</summary>
    public IReadOnlyList<FaunaDef> Fauna { get; }

    /// <summary>Definice biomů z <c>data/biomes.json</c>.</summary>
    public BiomeRegistry Biomes { get; }

    /// <summary>Definice surovin z <c>data/resources.json</c>.</summary>
    public DefRegistry<Resource> Resources { get; }

    /// <summary>Definice budov z <c>data/buildings.json</c>.</summary>
    public DefRegistry<BuildingDef> Buildings { get; }

    /// <summary>Nastavení generátoru světa z <c>data/worldgen.json</c>.</summary>
    public WorldGenCatalog WorldGen { get; }

    /// <summary>Parametry herní smyčky z <c>data/gameplay.json</c>.</summary>
    public GameplayConfig Gameplay { get; }

    /// <summary>Jazyky z <c>data/lang/*.json</c>.</summary>
    public DefRegistry<LanguageDef> Languages { get; }
}
