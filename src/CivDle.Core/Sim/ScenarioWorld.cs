using CivDle.Core.Content;
using CivDle.Core.World;

namespace CivDle.Core.Sim;

/// <summary>
/// Svět výzvy (scénáře): obsah, terén a simulace podle dat scénáře.
///
/// <para><b>Proč jedno místo.</b> Svět výzvy se skládá dvakrát — při startu
/// a při načtení savu. Dřív ho skládala jen obrazovka výběru, takže po načtení
/// savu běžel scénář s čísly volné hry (přebití <c>gameplay</c> se ztratilo).
/// Pravidla, která mění svět (zatopení, jeden biom, rychlejší vlny), by tu
/// chybu jen znásobila: po načtení by se změnila mapa pod rozestavěným
/// městem.</para>
///
/// <para>Vrstva: jádro, čistá funkce dat scénáře — žádný stav, nic neukládá.</para>
/// </summary>
public static class ScenarioWorld
{
    /// <summary>Pravidla světa výzvy.</summary>
    public static WorldRules RulesOf(ScenarioDef scenario) => new(scenario.Rules, scenario.BiomeIndex);

    /// <summary>
    /// Obsah pro scénář: přebitá herní čísla a pravidla světa (viz
    /// <see cref="ContentFor(GameContent, WorldRules)"/>). Původní obsah
    /// zůstane nedotčený.
    /// </summary>
    public static GameContent ContentFor(GameContent content, ScenarioDef scenario)
    {
        var result = scenario.Gameplay.IsEmpty
            ? content
            : content.WithGameplay(scenario.Gameplay.Apply(content.Gameplay));
        return ContentFor(result, RulesOf(scenario));
    }

    /// <summary>
    /// Obsah pro pravidla světa: u <see cref="ScenarioRule.DefenceFromStart"/>
    /// rychlejší rozvrh vln.
    /// </summary>
    public static GameContent ContentFor(GameContent content, WorldRules rules)
    {
        if (!rules.Has(ScenarioRule.DefenceFromStart) || !content.Frontier.IsAvailable)
        {
            return content;
        }

        var numbers = content.Gameplay.ChallengeRules;
        var frontier = content.Frontier;
        return content.WithFrontier(frontier with
        {
            FirstWaveTick = Math.Min(frontier.FirstWaveTick, numbers.DefenceFirstWaveTick),
            WaveIntervalTicks = Math.Max(1, (int)Math.Round(frontier.WaveIntervalTicks * numbers.DefenceWaveIntervalMult)),
        });
    }

    /// <summary>Terénní preset scénáře (viz <see cref="PresetFor(GameContent, int, WorldRules)"/>).</summary>
    public static TerrainPreset PresetFor(GameContent content, ScenarioDef scenario) =>
        PresetFor(content, scenario.PresetIndex, RulesOf(scenario));

    /// <summary>
    /// Terénní preset; u <see cref="ScenarioRule.FloodedWorld"/> se zvednutou
    /// hladinou. ID presetu zůstává — save ho ukládá jen jako vodítko, skutečný
    /// svět se skládá znovu odsud.
    /// </summary>
    public static TerrainPreset PresetFor(GameContent content, int presetIndex, WorldRules rules)
    {
        var presets = content.WorldGen.Presets;
        var preset = presets[presetIndex >= 0 && presetIndex < presets.Count ? presetIndex : content.WorldGen.DefaultPresetIndex];
        if (rules.Has(ScenarioRule.FloodedWorld))
        {
            // Strop pod 1: moře nad nejvyšší horou by nenechalo souš vůbec.
            float rise = (float)content.Gameplay.ChallengeRules.FloodSeaLevelRise;
            preset = preset with { SeaLevel = Math.Min(0.9f, preset.SeaLevel + rise) };
        }

        return preset;
    }

    /// <summary>Terén scénáře.</summary>
    public static ITerrain TerrainFor(GameContent content, ScenarioDef scenario) =>
        TerrainFor(content, scenario.PresetIndex, RulesOf(scenario), scenario.Seed);

    /// <summary>Terén podle pravidel; u <see cref="ScenarioRule.SingleBiome"/> jeden biom s oázami.</summary>
    public static ITerrain TerrainFor(GameContent content, int presetIndex, WorldRules rules, long seed)
    {
        ITerrain terrain = new ProceduralTerrain(content.Biomes, PresetFor(content, presetIndex, rules), seed);
        if (rules.Has(ScenarioRule.SingleBiome) && rules.BiomeIndex >= 0)
        {
            var numbers = content.Gameplay.ChallengeRules;
            terrain = new SingleBiomeTerrain(
                terrain, content.Biomes, rules.BiomeIndex, numbers.OasisChunkTiles, numbers.OasisShare, seed);
        }

        return terrain;
    }

    /// <summary>
    /// Založí nový svět scénáře: obsah, terén, simulace a startovní zásoby.
    /// </summary>
    public static Simulation Create(GameContent content, int scenarioIndex)
    {
        var scenario = content.Scenarios[scenarioIndex];
        var scenarioContent = ContentFor(content, scenario);
        var simulation = new Simulation(scenarioContent, TerrainFor(scenarioContent, scenario), scenario.Seed);
        simulation.StartScenario(scenarioIndex);
        return simulation;
    }

    /// <summary>
    /// Založí volnou hru s pravidly světa (Nová hra+). Na rozdíl od výzvy nemá
    /// cíl ani limit — pravidlo jen mění, jak se hraje.
    /// </summary>
    public static Simulation CreateFree(GameContent content, long seed, int presetIndex, WorldRules rules)
    {
        var ruledContent = ContentFor(content, rules);
        var simulation = new Simulation(ruledContent, TerrainFor(ruledContent, presetIndex, rules, seed), seed);
        simulation.StartWithWorldRules(rules);
        return simulation;
    }
}
