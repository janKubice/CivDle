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

    // Rytmus kontrol se odvozuje z čísla tiku, ne z pole „příště v tiku X":
    // to se neukládalo, takže načtená hra kontrolovala jinak než ta, která běžela
    // dál. A Vzestup nuluje tiky, pole ne.

    public ElectionSystem(GameContent content)
    {
        _content = content;
    }

    public void Tick(Simulation sim)
    {
        if (sim.TickCount % CheckIntervalTicks != 0 || !_content.Elections.IsEnabled)
        {
            return;
        }

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
