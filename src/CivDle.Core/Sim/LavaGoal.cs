using CivDle.Core.Content;
using CivDle.Core.World;

namespace CivDle.Core.Sim;

/// <summary>
/// Hráz proti lávě (Výheň, svety-design.md 4.4): když předpovězená dráha
/// příští lávy vede přes budovu, postaví se hráz na dlaždici těsně před ní.
///
/// <para><b>Proč předem a ne až po škodě</b> (jako větrolam): kudy láva
/// poteče, je na rozdíl od bouře spočitatelné — teče z kopce z průduchu
/// nejblíž městu. Guvernér tedy vidí totéž co hráč se seismickou stanicí.
/// Po postavení hráze se dráha přepočítá; kudy láva obteče, tam přibude
/// další hráz, dokud nezůstane v prohlubni.</para>
///
/// <para>Naléhavost je vysoko (pod proudem): erupce vyřadí celou ulici.</para>
/// </summary>
internal sealed class LavaGoal : IGovernorGoal
{
    /// <summary>Pod proudem, nad sklady — láva přijde podle rozvrhu jistě.</summary>
    public const int Urgency = 70;

    private readonly GameContent _content;
    private readonly GovernorRoles _roles;

    public LavaGoal(GameContent content, GovernorRoles roles)
    {
        _content = content;
        _roles = roles;
    }

    public CityNeed Need => CityNeed.LavaDam;

    public GoalAssessment Assess(Simulation sim)
    {
        if (_content.Hazards.EruptionIndex < 0)
        {
            return GoalAssessment.Idle(Need); // svět nevybuchuje
        }

        for (int d = 0; d < _content.Buildings.Count; d++)
        {
            if (_content.Buildings[d].LavaRole == LavaRole.Wall && _roles.MayBuild(sim, d)
                && TryFindDamSite(sim, d, out _, out _))
            {
                return new GoalAssessment(Need, Urgency, -1, -1);
            }
        }

        return GoalAssessment.Idle(Need);
    }

    /// <summary>Hráz je hráz — kanál ani nic jiného lávu nezastaví.</summary>
    public int Score(Simulation sim, int defIndex, in GoalAssessment goal) =>
        _content.Buildings[defIndex].LavaRole == LavaRole.Wall ? 100 : 0;

    /// <summary>
    /// Kam dát hráz: na poslední volnou dlaždici dráhy před první budovou,
    /// kterou by láva zalila. <c>false</c> = dráha nikoho neohrožuje, nebo
    /// před ohroženou budovou není kam hráz postavit.
    /// </summary>
    public static bool TryFindDamSite(Simulation sim, int wallDef, out int x, out int y)
    {
        var path = sim.PredictedLavaPath;
        var defs = sim.Content.Buildings;
        for (int i = 1; i < path.Count; i++)
        {
            if (!sim.TryGetBuildingAt(TileKey.X(path[i]), TileKey.Y(path[i]), out int index)
                || defs[sim.Buildings[index].DefIndex].LavaRole != LavaRole.None)
            {
                continue;
            }

            for (int j = i - 1; j >= 1; j--)
            {
                x = TileKey.X(path[j]);
                y = TileKey.Y(path[j]);
                var result = sim.CanPlace(wallDef, x, y);
                if (result is PlacementResult.Ok or PlacementResult.NotEnoughResources)
                {
                    return true;
                }
            }

            break; // první ohroženou budovu nejde zahradit — další hledat nemá smysl
        }

        x = 0;
        y = 0;
        return false;
    }
}
