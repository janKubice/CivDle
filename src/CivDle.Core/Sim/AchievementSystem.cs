using CivDle.Core.Content;

namespace CivDle.Core.Sim;

/// <summary>
/// Odemyká achievementy na nízké frekvenci (ne každý tik): když metrika dosáhne
/// prahu a achievement ještě není odemčený, označí ho a vyrobí oznámení (toast).
/// Bez odměny — achievement je jen záznam. Stav (co je odemčené) drží simulace;
/// perzistenci do účet-wide profilu řeší až aplikační vrstva.
/// </summary>
internal sealed class AchievementSystem
{
    private const int CheckIntervalTicks = 10; // ~1× za sekundu

    private readonly GameContent _content;
    private long _nextCheckTick;

    public AchievementSystem(GameContent content)
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

        var achievements = _content.Achievements;
        var unlocked = sim.AchievementsUnlocked;
        for (int i = 0; i < achievements.Count; i++)
        {
            if (unlocked[i])
            {
                continue;
            }

            var condition = achievements[i].Condition;
            if (sim.EvaluateMetric(condition.Kind, condition.Param) >= condition.Target)
            {
                unlocked[i] = true;
                sim.EnqueueNotification(new GameNotification(
                    NotificationKind.AchievementUnlocked, "toast.achievement", achievements[i].NameKey));
            }
        }
    }
}
