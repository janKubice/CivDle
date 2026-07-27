using System.Diagnostics;
using CivDle.Core.Content;
using CivDle.Core.Sim;
using CivDle.Core.World;

namespace CivDle.Balance;

/// <summary>Výsledek zátěžového měření jedné velikosti města.</summary>
/// <param name="Buildings">Kolik budov ve městě stálo.</param>
/// <param name="MicrosecondsPerTick">Průměrná doba jednoho tiku simulace.</param>
/// <param name="RealtimeBudgetPercent">Kolik procent reálného času tiky sežerou (10 Hz = 100 ms na tik).</param>
public sealed record StressSample(int Buildings, double Population, double MicrosecondsPerTick, double RealtimeBudgetPercent);

/// <summary>
/// Změří, jak dlouho trvá tik simulace v závislosti na velikosti města.
///
/// <para>Slib „miliony obyvatel na nekonečné mapě" nikdo neověřoval; balanční
/// běh se do velkých čísel nedostane, protože náhradní hráč staví pomalu.
/// Tenhle režim město naskládá rovnou a měří — díky tomu je optimalizace
/// podložená číslem, ne dojmem.</para>
///
/// <para>Není to profiler: měří celý tik, ne jednotlivé systémy. Na odpověď
/// „vejdeme se do rozpočtu?" to stačí a dá se to spustit kdykoli znovu.</para>
/// </summary>
public sealed class StressRun
{
    private const int WarmupTicks = 50;
    private const int MeasuredTicks = 200;

    private readonly GameContent _content;

    public StressRun(GameContent content)
    {
        _content = content;
    }

    /// <summary>
    /// Změří tik pro každou zadanou velikost města.
    /// </summary>
    /// <param name="fullyStaffed">
    /// Nasadit tolik lidí, aby všechny budovy vyráběly? Reálná hra se k plně
    /// obsazenému městu o statisících budov nedostane (populace roste lineárně),
    /// ale jako horní odhad zátěže to je to jediné poctivé číslo — jinak se
    /// výrobní smyčka přeskočí a měření lže.
    /// </param>
    public IReadOnlyList<StressSample> Run(IReadOnlyList<int> buildingCounts, bool fullyStaffed)
    {
        var samples = new List<StressSample>(buildingCounts.Count);
        foreach (int target in buildingCounts)
        {
            samples.Add(Measure(target, fullyStaffed));
        }

        return samples;
    }

    private StressSample Measure(int targetBuildings, bool fullyStaffed)
    {
        // Jednolitá louka: měří se tik, ne štěstí na terén.
        var (producer, biome) = PickProducer();
        var terrain = new UniformTerrain((byte)biome);

        // Plné obsazení se nedá „dorůst" — populace roste lineárně, na statisíce
        // dělníků by to trvalo dny. Nasadí se proto rovnou startovní populací
        // nad kopií obsahu; ostrá data zůstávají netknutá.
        var content = _content;
        if (fullyStaffed)
        {
            long workers = (long)targetBuildings * Math.Max(1, _content.Buildings[producer].WorkerSlots);
            content = _content.WithGameplay(_content.Gameplay with
            {
                StartingPopulation = workers,
                BaseHousingCapacity = (int)Math.Min(int.MaxValue, workers * 2),
            });
        }

        var sim = new Simulation(content, terrain, seed: 1);

        int placed = 0;
        for (int i = 0; placed < targetBuildings && i < targetBuildings * 4; i++)
        {
            // Mřížka s mezerou, ať se stavby nepřekrývají.
            int x = i % 400 * 2;
            int y = i / 400 * 2;
            if (sim.TryPlaceBuildingFree(producer, x, y) == PlacementResult.Ok)
            {
                placed++;
            }
        }

        for (int i = 0; i < WarmupTicks; i++)
        {
            sim.Tick();
        }

        var watch = Stopwatch.StartNew();
        for (int i = 0; i < MeasuredTicks; i++)
        {
            sim.Tick();
        }

        watch.Stop();

        double microseconds = watch.Elapsed.TotalMilliseconds * 1000.0 / MeasuredTicks;
        double budget = microseconds / (1_000_000.0 / Simulation.TicksPerSecond) * 100.0;
        return new StressSample(placed, sim.Population, microseconds, budget);
    }

    /// <summary>
    /// Najde dvojici (výrobna, biom), která spolu jde postavit. Hledá se dvojice,
    /// ne budova k prvnímu souš biomu — každá výrobna má vlastní seznam biomů
    /// a první souš biom v datech nemusí být ten, na kterém se dá stavět.
    /// </summary>
    private (int Building, int Biome) PickProducer()
    {
        for (int b = 0; b < _content.Buildings.Count; b++)
        {
            var def = _content.Buildings[b];
            if (def.Recipe is null || def.WorkerSlots == 0 || def.RequiresAdjacentWater
                || def.FootprintWidth != 1 || def.FootprintHeight != 1)
            {
                continue;
            }

            for (int i = 0; i < _content.Biomes.Count; i++)
            {
                if (!_content.Biomes[i].IsWater && def.IsBiomeAllowed(i))
                {
                    return (b, i);
                }
            }
        }

        throw new InvalidOperationException("Data nemají výrobnu stavitelnou na souši.");
    }
}
