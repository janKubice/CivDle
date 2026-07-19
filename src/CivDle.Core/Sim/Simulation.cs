using CivDle.Core.World;

namespace CivDle.Core.Sim;

/// <summary>
/// Stav a tik simulace. Zatím jen kostra (mapa + počítadlo tiků) — sem přibudou
/// systémy výroby, populace a růstu z dalších fází roadmapy. Simulace nezná render
/// ani obrazovku; tiká na pevné frekvenci přes <see cref="FixedStepLoop"/>.
/// </summary>
public sealed class Simulation
{
    /// <summary>Frekvence simulace dle tech-stack.md (10–20 Hz stačí, render běží vlastním tempem).</summary>
    public const double TicksPerSecond = 10.0;

    public Simulation(WorldMap map)
    {
        Map = map;
    }

    /// <summary>Mapa světa, nad kterou simulace běží.</summary>
    public WorldMap Map { get; }

    /// <summary>Počet proběhlých tiků od startu hry.</summary>
    public long TickCount { get; private set; }

    /// <summary>Jeden krok simulace. Deterministický — žádná náhoda bez seedu.</summary>
    public void Tick()
    {
        TickCount++;
    }
}
