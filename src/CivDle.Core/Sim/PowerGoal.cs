using CivDle.Core.Content;

namespace CivDle.Core.Sim;

/// <summary>
/// Proud: když budova, která ho potřebuje, jede na půl plynu, přibude
/// elektrárna — u ní, protože proud má dosah.
///
/// <para><b>Proč to chybělo:</b> elektrárny byly kategorie, na kterou guvernér
/// nesměl sáhnout. Továrna, kterou dostavěl pro řetězec, pak stála potmě
/// a hráč musel elektrárnu stavět vždycky sám.</para>
///
/// <para>Naléhavost je vysoko (hned pod vyschlým vstupem): bez proudu stojí
/// výroba stejně jako bez suroviny.</para>
/// </summary>
internal sealed class PowerGoal : IGovernorGoal
{
    /// <summary>Hned pod vyschlým vstupem — obojí zastaví výrobu.</summary>
    public const int Urgency = 80;

    /// <summary>O kolik se špinavá elektrárna odsouvá za čistou.</summary>
    private const int PollutionPenalty = 40;

    private readonly GameContent _content;
    private readonly PowerGoalConfig _config;
    private readonly GovernorRoles _roles;

    public PowerGoal(GameContent content, GovernorRoles roles)
    {
        _content = content;
        _config = content.Gameplay.Governor.Power;
        _roles = roles;
    }

    public CityNeed Need => CityNeed.Power;

    public GoalAssessment Assess(Simulation sim)
    {
        if (!_config.IsEnabled || !CanGenerate(sim))
        {
            return GoalAssessment.Idle(Need);
        }

        var buildings = sim.Buildings;
        int worst = -1;
        double worstCoverage = _config.MinCoverage;
        for (int i = 0; i < buildings.Length; i++)
        {
            if (!buildings[i].IsComplete || !_content.Buildings[buildings[i].DefIndex].NeedsPower)
            {
                continue;
            }

            double coverage = sim.PowerAt(buildings[i].X, buildings[i].Y);
            if (coverage < worstCoverage)
            {
                worstCoverage = coverage;
                worst = i;
            }
        }

        return worst >= 0 ? new GoalAssessment(Need, Urgency, -1, worst) : GoalAssessment.Idle(Need);
    }

    /// <summary>Víc výkonu je lepší, čistá elektrárna před špinavou.</summary>
    public int Score(Simulation sim, int defIndex, in GoalAssessment goal)
    {
        var def = _content.Buildings[defIndex];
        if (def.PowerSupply <= 0)
        {
            return 0;
        }

        int score = 100 + Math.Min(200, def.PowerSupply);
        return def.Pollution.IsNeutral ? score : Math.Max(1, score - PollutionPenalty);
    }

    /// <summary>Umí guvernér postavit něco, co proud vyrábí?</summary>
    private bool CanGenerate(Simulation sim)
    {
        for (int d = 0; d < _content.Buildings.Count; d++)
        {
            if (_content.Buildings[d].PowerSupply > 0 && _roles.MayBuild(sim, d))
            {
                return true;
            }
        }

        return false;
    }
}
