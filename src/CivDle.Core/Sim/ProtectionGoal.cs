using CivDle.Core.Content;

namespace CivDle.Core.Sim;

/// <summary>
/// Ochrana před přírodním jevem (svety-design.md 7.10): když bouře něco
/// zasypala, přibude u toho ochrana — větrolam na Duně, pluhovna na Mrazu.
///
/// <para><b>Proč až po škodě:</b> guvernér neví, kudy půjde příští bouře
/// (rozvrh zná jen jádro a hráč z předpovědi). Kde ale bouře jednou zasypala,
/// tam je město vystavené — a větrolam postavený hned vedle zasypané budovy
/// kryje i její sousedy. Po pár bouřích je okraj města obehnaný.</para>
///
/// <para>Naléhavost je střední: zasypaná budova se sama vyhrabe, jídlo
/// a domy jsou důležitější.</para>
/// </summary>
internal sealed class ProtectionGoal : IGovernorGoal
{
    /// <summary>Pod službami a domy — zasypání je dočasné.</summary>
    public const int Urgency = 45;

    private readonly GameContent _content;
    private readonly GovernorRoles _roles;

    public ProtectionGoal(GameContent content, GovernorRoles roles)
    {
        _content = content;
        _roles = roles;
    }

    public CityNeed Need => CityNeed.Protection;

    public GoalAssessment Assess(Simulation sim)
    {
        if (_content.Hazards.Count == 0)
        {
            return GoalAssessment.Idle(Need); // Domovina: žádné jevy
        }

        var buildings = sim.Buildings;
        for (int i = 0; i < buildings.Length; i++)
        {
            if (buildings[i].DisabledTicks > 0 && buildings[i].DisabledCause == DisableCause.Burial
                && CanShelter(sim))
            {
                return new GoalAssessment(Need, Urgency, -1, i);
            }
        }

        return GoalAssessment.Idle(Need);
    }

    /// <summary>Širší ochrana je lepší — kryje víc sousedů.</summary>
    public int Score(Simulation sim, int defIndex, in GoalAssessment goal)
    {
        var shelters = _content.Buildings[defIndex].Shelters;
        int best = 0;
        for (int s = 0; s < shelters.Count; s++)
        {
            best = Math.Max(best, shelters[s].Radius);
        }

        return best > 0 ? 100 + best : 0;
    }

    private bool CanShelter(Simulation sim)
    {
        for (int d = 0; d < _content.Buildings.Count; d++)
        {
            if (_content.Buildings[d].Shelters.Count > 0 && _roles.MayBuild(sim, d))
            {
                return true;
            }
        }

        return false;
    }
}
