using CivDle.Core.Content;

namespace CivDle.Core.Sim;

/// <summary>
/// Obnova krajiny: když dřevorubec nebo lom vytěží okolí, přibude vedle něj
/// budova, která krajinu vrací (lesní školka).
///
/// <para><b>Proč to chybělo:</b> guvernér na vytěžený les znal jedinou
/// odpověď — přestěhovat dřevorubce dál. Město tak těžbu vytlačovalo pořád
/// dál od sebe, až nebylo kam, a lesní školku, která existuje přesně proto,
/// nepostavil nikdy. Stěhování zůstává (je zadarmo a pomůže hned); školka je
/// dlouhodobá odpověď, aby se dalo zůstat.</para>
///
/// <para>Hlídá se <b>dřív, než okolí dojde úplně</b> (pod
/// <see cref="LandscapeGoalConfig.MinNodes"/> uzlů v dosahu): až těžba stojí,
/// je pozdě — školka les vrací po jedné dlaždici.</para>
///
/// <para>Výkon: počítání uzlů kolem každé těžby není zadarmo, a guvernér se
/// v jednom kole ptá tolikrát, kolik staví. Odpověď se proto drží, dokud se
/// nezmění tik ani počet budov (<see cref="_cachedTick"/>, <see cref="_cachedBuildings"/>),
/// a počítání končí, jakmile je uzlů dost. Na počtu budov záleží: bez něj si
/// guvernér nevšiml školky, kterou v tomtéž kole právě postavil, a stavěl
/// k jednomu dřevorubci jednu za druhou.</para>
/// </summary>
internal sealed class LandscapeGoal : IGovernorGoal
{
    /// <summary>Pod bydlením: les ubývá pomalu a stěhování mezitím pomůže.</summary>
    public const int Urgency = 45;

    private readonly GameContent _content;
    private readonly LandscapeGoalConfig _config;
    private readonly GovernorRoles _roles;
    private readonly List<(int X, int Y, int Radius)> _restorers = new();
    private long _cachedTick = -1;
    private int _cachedBuildings = -1;
    private GoalAssessment _cached;

    public LandscapeGoal(GameContent content, GovernorRoles roles)
    {
        _content = content;
        _config = content.Gameplay.Governor.Landscape;
        _roles = roles;
        _cached = GoalAssessment.Idle(CityNeed.Landscape);
    }

    public CityNeed Need => CityNeed.Landscape;

    public GoalAssessment Assess(Simulation sim)
    {
        if (!_config.IsEnabled || !CanRestore(sim))
        {
            return GoalAssessment.Idle(Need);
        }

        if (_cachedTick != sim.TickCount || _cachedBuildings != sim.Buildings.Length)
        {
            _cached = Find(sim);
            _cachedTick = sim.TickCount;
            _cachedBuildings = sim.Buildings.Length;
        }

        return _cached;
    }

    /// <summary>Budova, která krajinu vrací; větší dosah je lepší.</summary>
    public int Score(Simulation sim, int defIndex, in GoalAssessment goal)
    {
        var def = _content.Buildings[defIndex];
        return def.Reforests ? 100 + def.ReforestRadius : 0;
    }

    private GoalAssessment Find(Simulation sim)
    {
        var buildings = sim.Buildings;
        CollectRestorers(sim);

        int worst = -1;
        int worstNodes = _config.MinNodes;
        for (int i = 0; i < buildings.Length; i++)
        {
            var def = _content.Buildings[buildings[i].DefIndex];
            if (!def.HarvestsTerrain || !buildings[i].IsComplete || IsRestored(buildings[i].X, buildings[i].Y))
            {
                continue;
            }

            int nodes = CountNodes(sim, buildings[i].X, buildings[i].Y, def.TerrainHarvestRadius, worstNodes);
            if (nodes < worstNodes)
            {
                worstNodes = nodes;
                worst = i;
            }
        }

        if (worst < 0)
        {
            return GoalAssessment.Idle(Need);
        }

        var recipe = _content.Buildings[buildings[worst].DefIndex].Recipe;
        int output = recipe is { Outputs.Count: > 0 } ? recipe.Outputs[0].ResourceIndex : -1;
        return new GoalAssessment(Need, Urgency, output, worst);
    }

    /// <summary>Umí guvernér postavit něco, co krajinu vrací?</summary>
    private bool CanRestore(Simulation sim)
    {
        for (int d = 0; d < _content.Buildings.Count; d++)
        {
            if (_content.Buildings[d].Reforests && _roles.MayBuild(sim, d))
            {
                return true;
            }
        }

        return false;
    }

    private void CollectRestorers(Simulation sim)
    {
        _restorers.Clear();
        var buildings = sim.Buildings;
        for (int i = 0; i < buildings.Length; i++)
        {
            var def = _content.Buildings[buildings[i].DefIndex];
            if (def.Reforests)
            {
                _restorers.Add((buildings[i].X, buildings[i].Y, def.ReforestRadius));
            }
        }
    }

    /// <summary>Stojí už v dosahu téhle těžby budova, která krajinu vrací?</summary>
    private bool IsRestored(int x, int y)
    {
        foreach (var (rx, ry, radius) in _restorers)
        {
            if (Math.Abs(rx - x) <= radius && Math.Abs(ry - y) <= radius)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Uzly v dosahu; počítání skončí, jakmile jich je <paramref name="enough"/>.</summary>
    private static int CountNodes(Simulation sim, int x, int y, int radius, int enough)
    {
        int count = 0;
        for (int dy = -radius; dy <= radius; dy++)
        {
            for (int dx = -radius; dx <= radius; dx++)
            {
                if (sim.TryPeekNode(x + dx, y + dy, out _) && ++count >= enough)
                {
                    return count;
                }
            }
        }

        return count;
    }
}
