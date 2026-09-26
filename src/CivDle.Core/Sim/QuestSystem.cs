using CivDle.Core.Content;

namespace CivDle.Core.Sim;

/// <summary>
/// Vyhodnocuje úkoly na nízké frekvenci (ne každý tik): splněné pevné úkoly
/// označí, udělí odměnu a vyrobí oznámení; dynamický úkol se po splnění posune
/// na vyšší práh i odměnu (nekonečně). Stav (splněno, tier) drží simulace kvůli
/// savu — systém jen řídí „jak".
/// </summary>
internal sealed class QuestSystem
{
    private const int CheckIntervalTicks = 10; // ~1× za sekundu (10 Hz sim)

    private readonly GameContent _content;

    // Rytmus kontrol se odvozuje z čísla tiku, ne z pole „příště v tiku X":
    // to se neukládalo, takže načtená hra kontrolovala jinak než ta, která běžela
    // dál, a odměny přicházely o pár tiků jindy (rozchod po načtení). A Vzestup
    // nuluje tiky, pole ne — nový běh by čekal, než tiky dohoní starou hodnotu.

    public QuestSystem(GameContent content)
    {
        _content = content;
    }

    public void Tick(Simulation sim)
    {
        if (sim.TickCount % CheckIntervalTicks != 0)
        {
            return;
        }

        var quests = _content.Quests;
        var completed = sim.QuestsCompleted;
        for (int i = 0; i < quests.Count; i++)
        {
            if (completed[i])
            {
                continue;
            }

            var condition = quests[i].Condition;
            if (sim.EvaluateMetric(condition.Kind, condition.Param) >= condition.Target)
            {
                completed[i] = true;
                GrantReward(sim, quests[i].Reward, 1.0);
                sim.EnqueueNotification(new GameNotification(NotificationKind.QuestCompleted, "toast.quest", quests[i].NameKey));
            }
        }

        var dynamic = _content.QuestsDynamic;
        if (sim.EvaluateMetric(dynamic.BaseCondition.Kind, dynamic.BaseCondition.Param) >= sim.DynamicQuestTarget)
        {
            long completedTarget = sim.DynamicQuestTarget;
            GrantReward(sim, dynamic.BaseReward, Math.Pow(dynamic.RewardGrowth, sim.DynamicQuestTier));
            sim.DynamicQuestTier++;
            // Cíl jde do zprávy: jméno dynamického úkolu ho obsahuje, jinak by
            // se v toastu ukázalo doslova „{0}".
            sim.EnqueueNotification(new GameNotification(
                NotificationKind.QuestCompleted, "toast.quest", "quest.dynamic", completedTarget));
        }
    }

    private static void GrantReward(Simulation sim, IReadOnlyList<ResourceAmount> reward, double multiplier)
    {
        for (int i = 0; i < reward.Count; i++)
        {
            sim.AddResource(reward[i].ResourceIndex, reward[i].Amount * multiplier);
        }
    }
}
