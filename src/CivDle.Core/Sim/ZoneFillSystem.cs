using CivDle.Core.Content;
using CivDle.Core.WorldGen;

namespace CivDle.Core.Sim;

/// <summary>
/// Zaplňování zón (automatizace, stupeň 3 dle living-city.md): každých pár tiků
/// projde jednu namalovanou zónu a do jedné volné vhodné dlaždice položí budovu
/// jejího typu — za normální cenu, takže výrobní řetězce pořád rozhodují
/// (soft pressure zůstává). Max jedna budova za interval → pozvolné, rovnoměrné
/// plnění bez špičky (dávkování, CLAUDE.md výkon).
///
/// Běží na simulační vrstvě a na nízké frekvenci (intervalTicks). „Náhoda"
/// (kterou zónu vzít) je bezstavový hash (seed, tik) — deterministická a přežije
/// save/load bez ukládání stavu RNG. Terén a suroviny řeší <see cref="Simulation.CanPlace"/>.
/// </summary>
internal sealed class ZoneFillSystem
{
    private readonly GameContent _content;
    private readonly long _seed;

    public ZoneFillSystem(GameContent content, long seed)
    {
        _content = content;
        _seed = seed;
    }

    public void Tick(Simulation sim)
    {
        var config = _content.Gameplay.AutoBuild;
        if (sim.TickCount % config.IntervalTicks != 0)
        {
            return;
        }

        var zones = sim.Zones;
        if (zones.Count == 0)
        {
            return;
        }

        // Deterministický start → v čase se plní všechny zóny rovnoměrně, ne jen ta první.
        var rng = new SplitMix64(unchecked((ulong)_seed ^ ((ulong)sim.TickCount * 0x2545F4914F6CDD1DUL)));
        int start = (int)(rng.Next() % (ulong)zones.Count);
        for (int i = 0; i < zones.Count; i++)
        {
            if (TryFillOne(sim, zones[(start + i) % zones.Count]))
            {
                return; // max jedna budova za interval
            }
        }
    }

    /// <summary>Zkusí do zóny položit jednu budovu; vrátí true, když se to povedlo.</summary>
    private bool TryFillOne(Simulation sim, Zone zone)
    {
        var buildings = _content.ZoneTypes[zone.TypeIndex].BuildingIndices;
        for (int b = 0; b < buildings.Count; b++)
        {
            int defIndex = buildings[b];

            // Cena nezávisí na místě — nedostupnou budovu přeskoč hned, ať nescanujeme zbytečně.
            if (!sim.CanAfford(defIndex))
            {
                continue;
            }

            for (int ty = zone.Y; ty < zone.Y + zone.Height; ty++)
            {
                for (int tx = zone.X; tx < zone.X + zone.Width; tx++)
                {
                    if (sim.CanPlace(defIndex, tx, ty) == PlacementResult.Ok)
                    {
                        return sim.TryPlaceBuilding(defIndex, tx, ty) == PlacementResult.Ok;
                    }
                }
            }
        }

        return false;
    }
}
