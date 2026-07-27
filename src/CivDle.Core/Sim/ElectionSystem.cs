using CivDle.Core.Content;

namespace CivDle.Core.Sim;

/// <summary>
/// Volby na pozadí: každých pár herních dní se sestaví kandidátka a hráč vybere
/// program, který městu po celé období dává bonus.
///
/// <para>Období nikdy nezačíná bez vlády: hned na začátku nastoupí první program
/// z kandidátky a hráč ho může kdykoli během období vyměnit za jiný. Idle hra
/// nesmí čekat na klik — kdo nechce volit, prostě nechá věci běžet.</para>
///
/// <para>Kandidátka je odvozená z čísla období a seedu světa, ne z náhody za
/// běhu — po načtení savu vyjde stejná.</para>
/// </summary>
internal sealed class ElectionSystem
{
    private const int CheckIntervalTicks = 10; // ~1× za sekundu (10 Hz sim)

    private readonly GameContent _content;
    private long _nextCheckTick;

    public ElectionSystem(GameContent content)
    {
        _content = content;
    }

    public void Tick(Simulation sim)
    {
        if (sim.TickCount < _nextCheckTick || !_content.Elections.IsEnabled)
        {
            return;
        }

        _nextCheckTick = sim.TickCount + CheckIntervalTicks;

        long term = CurrentTerm(sim);
        if (term == sim.ElectionTerm)
        {
            return;
        }

        sim.BeginElectionTerm(term);
        sim.EnqueueNotification(new GameNotification(
            NotificationKind.WorldEvent, "toast.election", "election.title"));
    }

    /// <summary>Kolikáté volební období právě běží (podle herního dne).</summary>
    private long CurrentTerm(Simulation sim) => sim.DayNumber / _content.Elections.TermDays;
}
