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
    /// <summary>
    /// Obsah pro scénář: přebitá herní čísla a u <see cref="ScenarioRule.DefenceFromStart"/>
    /// rychlejší rozvrh vln. Původní obsah zůstane nedotčený.
    /// </summary>
    public static GameContent ContentFor(GameContent content, ScenarioDef scenario)
    {
        var result = scenario.Gameplay.IsEmpty
            ? content
            : content.WithGameplay(scenario.Gameplay.Apply(content.Gameplay));

        if (scenario.Has(ScenarioRule.DefenceFromStart) && result.Frontier.IsAvailable)
        {
            var rules = result.Gameplay.ChallengeRules;
            var frontier = result.Frontier;
            result = result.WithFrontier(frontier with
            {
                FirstWaveTick = Math.Min(frontier.FirstWaveTick, rules.DefenceFirstWaveTick),
                WaveIntervalTicks = Math.Max(1, (int)Math.Round(frontier.WaveIntervalTicks * rules.DefenceWaveIntervalMult)),
            });
        }

        return result;
    }

    /// <summary>
    /// Terénní preset scénáře; u <see cref="ScenarioRule.FloodedWorld"/> se
    /// zvednutou hladinou. ID presetu zůstává — save ho ukládá jen jako vodítko,
    /// skutečný svět se skládá znovu odsud.
    /// </summary>
    public static TerrainPreset PresetFor(GameContent content, ScenarioDef scenario)
    {
        var presets = content.WorldGen.Presets;
        var preset = presets[scenario.PresetIndex >= 0 ? scenario.PresetIndex : content.WorldGen.DefaultPresetIndex];
        if (scenario.Has(ScenarioRule.FloodedWorld))
        {
            // Strop pod 1: moře nad nejvyšší horou by nenechalo souš vůbec.
            float rise = (float)content.Gameplay.ChallengeRules.FloodSeaLevelRise;
            preset = preset with { SeaLevel = Math.Min(0.9f, preset.SeaLevel + rise) };
        }

        return preset;
    }

    /// <summary>Terén scénáře; u <see cref="ScenarioRule.SingleBiome"/> jeden biom s oázami.</summary>
    public static ITerrain TerrainFor(GameContent content, ScenarioDef scenario)
    {
        ITerrain terrain = new ProceduralTerrain(content.Biomes, PresetFor(content, scenario), scenario.Seed);
        if (scenario.Has(ScenarioRule.SingleBiome) && scenario.BiomeIndex >= 0)
        {
            var rules = content.Gameplay.ChallengeRules;
            terrain = new SingleBiomeTerrain(
                terrain, content.Biomes, scenario.BiomeIndex, rules.OasisChunkTiles, rules.OasisShare, scenario.Seed);
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
}
