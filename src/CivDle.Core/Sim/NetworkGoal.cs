using CivDle.Core.Content;

namespace CivDle.Core.Sim;

/// <summary>
/// Sítě mimo proud (voda na Duně, teplo na Mrazu): když budova, která síť
/// chce, na ni nedosáhne, přibude zdroj — u ní, protože síť má dosah
/// (svety-design.md 7.10, zobecněný <see cref="PowerGoal"/>).
///
/// <para><b>Proč zvlášť od proudu:</b> proud má vlastní pole budov a vlastní
/// nastavení v datech, a na Domovině se nesmí chovat jinak než dřív. Sítě
/// světů jsou jiné: zdroj bývá vázaný na terén (studna jen na zvodni) a když
/// se nevejde, přijde na řadu slabší zdroj, který stojí kdekoli (lapač rosy).</para>
///
/// <para>Hodnotí se <b>průměr dne</b>, ne okamžik: lapač rosy ve dne nedává
/// nic, a guvernér, který by koukal na poledne, by stavěl jeden za druhým.</para>
///
/// <para>Pole <see cref="GoalAssessment.Resource"/> tu nese index sítě.</para>
/// </summary>
internal sealed class NetworkGoal : IGovernorGoal
{
    /// <summary>Stejně jako proud: bez vody stojí háj jako bez suroviny.</summary>
    public const int Urgency = PowerGoal.Urgency;

    private readonly GameContent _content;
    private readonly GovernorRoles _roles;
    private readonly double _minCoverage;

    /// <summary>Které sítě umí guvernér právě zásobit (přepočítá se na začátku každého posouzení).</summary>
    private readonly bool[] _canSupply;

    public NetworkGoal(GameContent content, GovernorRoles roles)
    {
        _content = content;
        _roles = roles;
        _minCoverage = content.Gameplay.Governor.Power.MinCoverage;
        _canSupply = new bool[content.Networks.Count];
    }

    public CityNeed Need => CityNeed.Network;

    public GoalAssessment Assess(Simulation sim)
    {
        var networks = _content.Networks;
        if (networks.Count <= 1 || _minCoverage <= 0)
        {
            return GoalAssessment.Idle(Need); // Domovina: jen proud, ten má svůj cíl
        }

        bool any = false;
        for (int n = 1; n < networks.Count; n++)
        {
            _canSupply[n] = networks[n].IsEnabled && CanSupply(sim, n);
            any |= _canSupply[n];
        }

        if (!any)
        {
            return GoalAssessment.Idle(Need);
        }

        var buildings = sim.Buildings;
        int worst = -1;
        int worstNetwork = -1;
        double worstCoverage = _minCoverage;
        for (int i = 0; i < buildings.Length; i++)
        {
            if (!buildings[i].IsComplete)
            {
                continue;
            }

            var def = _content.Buildings[buildings[i].DefIndex];
            var uses = def.Networks;
            for (int u = 0; u < uses.Count; u++)
            {
                int network = uses[u].NetworkIndex;
                if (uses[u].Demand <= 0 || !_canSupply[network])
                {
                    continue;
                }

                double coverage = sim.NetworkSteadyCoverageAt(network, buildings[i].X, buildings[i].Y);
                if (coverage < worstCoverage)
                {
                    worstCoverage = coverage;
                    worst = i;
                    worstNetwork = network;
                }
            }
        }

        return worst >= 0 ? new GoalAssessment(Need, Urgency, worstNetwork, worst) : GoalAssessment.Idle(Need);
    }

    /// <summary>
    /// Víc průměrného výkonu je lepší. Zdroj, který dodává jen půl dne, se
    /// počítá napůl — studna tak vyhraje nad lapačem rosy, pokud se vejde.
    /// </summary>
    public int Score(Simulation sim, int defIndex, in GoalAssessment goal)
    {
        var def = _content.Buildings[defIndex];
        int supply = goal.Resource > 0 ? def.SupplyOf(goal.Resource) : 0;
        if (supply <= 0)
        {
            return 0;
        }

        double steady = supply * SupplyCurve.Average(def.SupplyTime);
        return 100 + (int)Math.Min(200, steady * 10);
    }

    /// <summary>Umí guvernér postavit něco, co do sítě dodává?</summary>
    private bool CanSupply(Simulation sim, int network)
    {
        for (int d = 0; d < _content.Buildings.Count; d++)
        {
            if (_content.Buildings[d].SupplyOf(network) > 0 && _roles.MayBuild(sim, d))
            {
                return true;
            }
        }

        return false;
    }
}
