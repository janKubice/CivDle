namespace CivDle.Core.Content;

/// <summary>
/// Zvláštní pravidla, se kterými svět vznikl — z výzvy, nebo zvolená pro
/// Novou hru+ (endgame.md, C3).
///
/// <para>Pravidla výzvy mění i to, jak svět vypadá (zatopení, jeden biom),
/// proto je to hodnota, kterou si nese save v hlavičce a ze které se svět
/// po načtení složí znovu — stejně jako ze seedu.</para>
/// </summary>
/// <param name="Rules">Pravidla (behavior-ID, <see cref="ScenarioRule"/>).</param>
/// <param name="BiomeIndex">Biom souše pro <see cref="ScenarioRule.SingleBiome"/>; −1 = žádný.</param>
public sealed record WorldRules(IReadOnlyList<ScenarioRule> Rules, int BiomeIndex = -1)
{
    /// <summary>Svět bez zvláštních pravidel.</summary>
    public static WorldRules None { get; } = new(Array.Empty<ScenarioRule>());

    /// <summary>Platí pravidlo?</summary>
    public bool Has(ScenarioRule rule) => Rules.Contains(rule);

    /// <summary>Nemá svět žádné zvláštní pravidlo?</summary>
    public bool IsEmpty => Rules.Count == 0;

    /// <summary>
    /// Pravidla, která jde zvolit pro Novou hru+: ta, co dávají smysl v nekonečné
    /// hře. <see cref="ScenarioRule.NoAscension"/> ne — volná hra bez Vzestupu
    /// by se zasekla na prvním měřítku navždy.
    /// </summary>
    public static IReadOnlyList<ScenarioRule> NewGamePlusChoices { get; } = new[]
    {
        ScenarioRule.NoRoads,
        ScenarioRule.FloodedWorld,
        ScenarioRule.SingleBiome,
        ScenarioRule.DefenceFromStart,
        ScenarioRule.HighUpkeep,
        ScenarioRule.NightWorld,
        ScenarioRule.NoAutoBuild,
    };
}
