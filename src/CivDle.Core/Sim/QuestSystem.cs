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
    private long _nextCheckTick;

    public QuestSystem(GameContent content)
    {
        _content = content;
    }

    public void Tick(Simulation sim)
    {
        if (sim.TickCount < _nextCheckTick)
        {
            return;
        }

        _nextCheckTick = sim.TickCount + CheckIntervalTicks;

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
            GrantReward(sim, dynamic.BaseReward, Math.Pow(dynamic.RewardGrowth, sim.DynamicQuestTier));
            sim.DynamicQuestTier++;
            sim.EnqueueNotification(new GameNotification(NotificationKind.QuestCompleted, "toast.quest", "quest.dynamic"));
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
