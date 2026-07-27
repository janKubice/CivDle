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
    private long _nextCheckTick;

    public ChallengeSystem(GameContent content)
    {
        _content = content;
    }

    public void Tick(Simulation sim)
    {
        if (sim.TickCount < _nextCheckTick || !_content.Challenges.IsEnabled)
        {
            return;
        }

        _nextCheckTick = sim.TickCount + CheckIntervalTicks;

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
