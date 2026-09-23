using CivDle.Core.Content;

namespace CivDle.Core.Sim;

/// <summary>
/// Hlídá milníky postupu a hlásí je jako oznámení. Každý se spustí jen jednou
/// za hru; co už padlo, drží simulace kvůli savu.
/// </summary>
internal sealed class MilestoneSystem
{
    private const int CheckIntervalTicks = 10; // ~1x za sekundu (10 Hz sim)

    private readonly GameContent _content;

    // Rytmus kontrol se odvozuje z čísla tiku, ne z pole „příště v tiku X":
    // to se neukládalo, takže načtená hra kontrolovala jinak než ta, která běžela
    // dál, a odměny přicházely o pár tiků jindy (rozchod po načtení). A Vzestup
    // nuluje tiky, pole ne — nový běh by čekal, než tiky dohoní starou hodnotu.

    public MilestoneSystem(GameContent content)
    {
        _content = content;
    }

    public void Tick(Simulation sim)
    {
        if (sim.TickCount % CheckIntervalTicks != 0)
        {
            return;
        }

        var milestones = _content.Milestones;
        for (int i = 0; i < milestones.Count; i++)
        {
            if (sim.IsMilestoneReached(i))
            {
                continue;
            }

            var condition = milestones[i].Condition;
            if (sim.EvaluateMetric(condition.Kind, condition.Param) < condition.Target)
            {
                continue;
            }

            sim.MarkMilestoneReached(i);
            sim.EnqueueNotification(new GameNotification(
                NotificationKind.Milestone, "toast.milestone", milestones[i].NameKey));

            // Na milníku se může narodit významná osobnost. Řeší to simulace,
            // ne tenhle systém — bonusy a sochy nejsou věc milníků.
            sim.OnMilestoneReached(i);
        }
    }
}
