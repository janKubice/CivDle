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

    // Rytmus kontrol se odvozuje z čísla tiku, ne z pole „příště v tiku X":
    // to se neukládalo, takže načtená hra kontrolovala jinak než ta, která běžela
    // dál, a odměny přicházely o pár tiků jindy (rozchod po načtení). A Vzestup
    // nuluje tiky, pole ne — nový běh by čekal, než tiky dohoní starou hodnotu.

    public AchievementSystem(GameContent content)
    {
        _content = content;
    }

    public void Tick(Simulation sim)
    {
        // V pískovišti se neodemyká nic. Kontroluje se to tady, na jediném
        // místě, kde achievement vzniká — kdyby se to řešilo až při zápisu do
        // profilu, hráči by v pískovišti vyskakovaly toasty za něco, co se mu
        // nikam nezapíše, a to je horší než mlčet.
        if (sim.Sandbox || sim.TickCount % CheckIntervalTicks != 0)
        {
            return;
        }

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
