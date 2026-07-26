using CivDle.Core.Content;
using CivDle.Core.WorldGen;

namespace CivDle.Core.Sim;

/// <summary>
/// Počasí nad městem (living-map.md §2): jev je vázaný na biom, ve kterém město
/// stojí — jinak prší na louce, jinak sněží v horách. Extrémní jevy (tornádo,
/// vánice, písečná bouře) dočasně sníží výrobu, ale NIKDY nic nezničí.
///
/// <para>Stav se neukládá: aktuální jev je čistá funkce (seed, číslo okna) —
/// stejný trik jako u auto-stavby. Přežije save/load i offline dohon zadarmo
/// a nikdy se nerozejde s uloženou hrou.</para>
/// </summary>
internal sealed class WeatherSystem
{
    /// <summary>Jak dlouho drží jedno „okno" počasí, než se losuje znovu (sekundy).</summary>
    private const double WindowSeconds = 120.0;

    private readonly GameContent _content;
    private readonly long _seed;

    public WeatherSystem(GameContent content, long seed)
    {
        _content = content;
        _seed = seed;
    }

    /// <summary>
    /// Aktuální jev pro daný biom a tik. Vrací −1, když počasí není definované
    /// (prázdná data) nebo pro biom žádný jev nesedí.
    /// </summary>
    public int CurrentWeather(int biomeIndex, long tickCount)
    {
        var weather = _content.Weather;
        if (weather.Count == 0)
        {
            return -1;
        }

        long window = (long)(tickCount / (Simulation.TicksPerSecond * WindowSeconds));

        // Vážený los z jevů, které v tomhle biomu dávají smysl.
        double total = 0;
        for (int i = 0; i < weather.Count; i++)
        {
            if (weather[i].AppliesTo(biomeIndex))
            {
                total += weather[i].Weight;
            }
        }

        if (total <= 0)
        {
            return -1;
        }

        var rng = new SplitMix64(unchecked((ulong)_seed ^ ((ulong)window * 0x94D049BB133111EBUL)));
        double roll = rng.Next() / (double)ulong.MaxValue * total;
        for (int i = 0; i < weather.Count; i++)
        {
            if (!weather[i].AppliesTo(biomeIndex))
            {
                continue;
            }

            roll -= weather[i].Weight;
            if (roll <= 0)
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>
    /// Kolik sekund zbývá do konce jevu. Extrémní jev je kratší než okno — po jeho
    /// odeznění je do dalšího losu klid (jinak by katastrofa trvala pořád).
    /// </summary>
    public double SecondsRemaining(int weatherIndex, long tickCount)
    {
        if (weatherIndex < 0)
        {
            return 0;
        }

        double elapsedInWindow = tickCount / (double)Simulation.TicksPerSecond % WindowSeconds;
        double duration = _content.Weather[weatherIndex].DurationSeconds;
        return Math.Max(0, duration - elapsedInWindow);
    }

    /// <summary>Je jev právě aktivní (ještě neodezněl v rámci svého okna)?</summary>
    public bool IsActive(int weatherIndex, long tickCount) =>
        weatherIndex >= 0 && SecondsRemaining(weatherIndex, tickCount) > 0;
}
