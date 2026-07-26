using CivDle.Core.Content;

namespace CivDle.Core.Sim;

/// <summary>
/// Posouvá průvodce prvními kroky. Aktivní je vždy právě jeden krok; jakmile
/// jeho podmínka platí, systém přejde na další a vyrobí oznámení.
///
/// <para>Kroky se vyhodnocují popořadě a jen ten aktuální — hráč, který si
/// odbude pozdější krok dřív (postaví pilu před farmou), o něj nepřijde,
/// protože při přechodu se přeskočí všechny už splněné kroky najednou.</para>
///
/// <para>Stav (index kroku) drží simulace kvůli savu; systém řídí jen „jak".</para>
/// </summary>
internal sealed class TutorialSystem
{
    private const int CheckIntervalTicks = 10; // ~1× za sekundu (10 Hz sim)

    private readonly GameContent _content;
    private long _nextCheckTick;

    public TutorialSystem(GameContent content)
    {
        _content = content;
    }

    public void Tick(Simulation sim)
    {
        if (sim.TickCount < _nextCheckTick || sim.IsTutorialFinished)
        {
            return;
        }

        _nextCheckTick = sim.TickCount + CheckIntervalTicks;

        var steps = _content.Tutorial;
        int step = sim.TutorialStep;
        bool advanced = false;
        while (step < steps.Count && IsSatisfied(sim, steps[step].Condition))
        {
            step++;
            advanced = true;
        }

        if (!advanced)
        {
            return;
        }

        sim.TutorialStep = step;
        sim.EnqueueNotification(new GameNotification(
            NotificationKind.Milestone,
            "toast.tutorial",
            step < steps.Count ? steps[step].NameKey : "tutorial.done"));
    }

    private static bool IsSatisfied(Simulation sim, GoalCondition condition) =>
        sim.EvaluateMetric(condition.Kind, condition.Param) >= condition.Target;
}
