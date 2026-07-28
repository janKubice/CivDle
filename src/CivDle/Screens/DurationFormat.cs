namespace CivDle.Screens;

/// <summary>
/// Trvání pro hráče („3 h 20 min", „6 min", „45 s"). Jednotky h/min/s jsou
/// univerzální, takže se nepřekládají — jinak by šlo o klíč navíc v každém jazyce
/// kvůli jednomu písmenu.
///
/// <para>Vrstva UI: formátuje čísla, nic nepočítá.</para>
/// </summary>
internal static class DurationFormat
{
    /// <summary>Trvání v sekundách jako čitelný text; pod minutu se ukazují sekundy.</summary>
    public static string Human(double seconds)
    {
        long total = (long)Math.Max(0, Math.Round(seconds));
        if (total < 60)
        {
            return $"{total} s";
        }

        long minutes = total / 60;
        long hours = minutes / 60;
        minutes %= 60;
        return hours > 0 ? $"{hours} h {minutes} min" : $"{minutes} min";
    }

    /// <summary>Trvání zadané v ticích simulace.</summary>
    public static string FromTicks(double ticks) => Human(ticks / CivDle.Core.Sim.Simulation.TicksPerSecond);
}
