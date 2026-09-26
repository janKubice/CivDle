using CivDle.Core.Content;

namespace CivDle.Core.Sim;

/// <summary>
/// Vyhodnocuje denní výzvy na nízké frekvenci: splněnou označí, udělí odměnu
/// a vyrobí oznámení. Stav (den, výchozí hodnoty metrik, co je splněné) drží
/// simulace kvůli savu — systém řídí jen „jak".
///
/// <para>Simulace si sama nesahá na hodiny (musí zůstat deterministická), takže
/// aktuální den do ní vkládá aplikační vrstva přes <see cref="Simulation.SetChallengeDay"/>.
/// Stejný vstup dá vždy stejný výstup.</para>
/// </summary>
internal sealed class ChallengeSystem
{
    private const int CheckIntervalTicks = 10; // ~1× za sekundu (10 Hz sim)

    private readonly GameContent _content;

    // Rytmus kontrol se odvozuje z čísla tiku, ne z pole „příště v tiku X":
    // to se neukládalo, takže načtená hra kontrolovala jinak než ta, která běžela
    // dál, a odměny přicházely o pár tiků jindy (rozchod po načtení). A Vzestup
    // nuluje tiky, pole ne — nový běh by čekal, než tiky dohoní starou hodnotu.

    public ChallengeSystem(GameContent content)
    {
        _content = content;
    }

    public void Tick(Simulation sim)
    {
        if (sim.TickCount % CheckIntervalTicks != 0 || !_content.Challenges.IsEnabled)
        {
            return;
        }

        var active = sim.ActiveChallenges;
        for (int slot = 0; slot < active.Count; slot++)
        {
            if (sim.IsChallengeDone(slot))
            {
                continue;
            }

            var challenge = _content.Challenges.Challenges[active[slot]];
            if (sim.ChallengeProgress(slot) < challenge.Condition.Target)
            {
                continue;
            }

            sim.MarkChallengeDone(slot);
            for (int i = 0; i < challenge.Reward.Count; i++)
            {
                sim.AddResource(challenge.Reward[i].ResourceIndex, challenge.Reward[i].Amount);
            }

            sim.EnqueueNotification(new GameNotification(
                NotificationKind.QuestCompleted, "toast.challenge", challenge.NameKey));
        }
    }
}
