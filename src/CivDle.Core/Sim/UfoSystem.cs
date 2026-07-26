using CivDle.Core.Content;
using CivDle.Core.WorldGen;

namespace CivDle.Core.Sim;

/// <summary>
/// Návštěvy UFO: mapa má občas udělat něco sama od sebe, bez hráčova kliknutí.
/// V pravidelném okně padne los; při úspěchu UFO přiletí nad město, chvíli visí
/// a pak provede JEDEN zásah — unese pár lidí, sestřelí barák, udělá kruh v obilí,
/// terraformuje kus krajiny, nebo něco nechá jako dárek.
///
/// <para><b>Kde je UFO a co udělá</b> je čistá funkce (seed, číslo okna) — stejný
/// trik jako u počasí, takže se pozice ani akce neukládají a přežijí save/load
/// zadarmo. Ukládá se jen jediné číslo: poslední okno, jehož zásah už proběhl.
/// Bez něj by se po načtení savu tentýž zásah provedl znovu.</para>
///
/// <para>Zásah se aplikuje na konci návštěvy — hráč UFO nejdřív uvidí, teprve pak
/// se něco stane (anticipace → payoff).</para>
/// </summary>
internal sealed class UfoSystem
{
    private readonly GameContent _content;
    private readonly long _seed;

    public UfoSystem(GameContent content, long seed)
    {
        _content = content;
        _seed = seed;
    }

    /// <summary>Číslo aktuálního okna losu (roste s časem).</summary>
    public long WindowAt(long tickCount) =>
        (long)(tickCount / (Simulation.TicksPerSecond * Math.Max(1.0, _content.Ufo.WindowSeconds)));

    /// <summary>Přiletí v tomhle okně UFO? Deterministické z seedu a čísla okna.</summary>
    public bool VisitsIn(long window)
    {
        if (!_content.Ufo.IsEnabled)
        {
            return false;
        }

        return Roll(window, 0x51) < _content.Ufo.Chance;
    }

    /// <summary>Je UFO právě teď vidět nad mapou (viz <see cref="UfoConfig.VisitSeconds"/>)?</summary>
    public bool IsVisible(long tickCount)
    {
        long window = WindowAt(tickCount);
        if (!VisitsIn(window))
        {
            return false;
        }

        double secondsInWindow = tickCount / Simulation.TicksPerSecond % Math.Max(1.0, _content.Ufo.WindowSeconds);
        return secondsInWindow < _content.Ufo.VisitSeconds;
    }

    /// <summary>
    /// Kam UFO v daném okně zamíří — dlaždice v okruhu kolem zadaného středu města.
    /// Střed dodává simulace (těžiště zástavby), aby systém nemusel znát mapu.
    /// </summary>
    public (int X, int Y) TargetTile(long window, int centerX, int centerY)
    {
        int radius = Math.Max(1, _content.Ufo.Radius);
        int dx = (int)(Roll(window, 0xA1) * (radius * 2 + 1)) - radius;
        int dy = (int)(Roll(window, 0xB2) * (radius * 2 + 1)) - radius;
        return (centerX + dx, centerY + dy);
    }

    /// <summary>Index akce pro dané okno (vážený los), nebo −1 když UFO nepřiletí.</summary>
    public int ActionIn(long window)
    {
        var actions = _content.Ufo.Actions;
        if (!VisitsIn(window))
        {
            return -1;
        }

        double total = 0;
        for (int i = 0; i < actions.Count; i++)
        {
            total += actions[i].Weight;
        }

        if (total <= 0)
        {
            return -1;
        }

        double roll = Roll(window, 0xC3) * total;
        for (int i = 0; i < actions.Count; i++)
        {
            roll -= actions[i].Weight;
            if (roll <= 0)
            {
                return i;
            }
        }

        return actions.Count - 1;
    }

    /// <summary>Deterministické „hození kostkou" pro dané okno a účel, výsledek v [0, 1).</summary>
    private double Roll(long window, ulong salt)
    {
        var rng = new SplitMix64(unchecked((ulong)_seed ^ ((ulong)window * 0x9E3779B97F4A7C15UL) ^ (salt * 0xD1B54A32D192ED03UL)));
        return rng.Next() / (double)ulong.MaxValue;
    }
}
