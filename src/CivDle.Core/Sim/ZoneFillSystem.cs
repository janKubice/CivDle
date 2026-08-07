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
        // Stejné tempo jako u auto-stavby — zóny jsou její řízená varianta,
        // takže se musí zrychlovat spolu s ní.
        if (sim.TickCount % sim.AutoBuildInterval != 0)
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

        // Tempo se rozkládá po zónách (jedna na zónu a průchod), ať se plní
        // rovnoměrně, ne jedna zóna napřed.
        int budget = sim.AutoBuildBudget;
        while (budget > 0)
        {
            bool placedAny = false;
            for (int i = 0; i < zones.Count && budget > 0; i++)
            {
                if (TryFillOne(sim, zones[(start + i) % zones.Count]))
                {
                    budget--;
                    placedAny = true;
                }
            }

            if (!placedAny)
            {
                break; // nic dalšího nejde položit (plno / došly suroviny)
            }
        }
    }

    /// <summary>Zkusí do zóny položit jednu budovu; vrátí true, když se to povedlo.</summary>
    private bool TryFillOne(Simulation sim, Zone zone)
    {
        var buildings = _content.ZoneTypes[zone.TypeIndex].BuildingIndices;

        // Odzadu: v datech je typ zóny seřazený od nejskromnější budovy po
        // nejlepší, takže poslední dostupná je ta nejlepší, na kterou město má.
        // Dřív se bralo první v pořadí, takže obytná čtvrť plná paneláků
        // v datech dál stavěla chalupy — pokrok se do zón nikdy nepromítl.
        for (int b = buildings.Count - 1; b >= 0; b--)
        {
            int defIndex = buildings[b];

            // Rezerva guvernéra platí i pro zóny. Dřív se tu ptalo jen na
            // „mám na to", takže si automatika sáhla i na to, co si hráč
            // schoval — a přepínač v panelu guvernéra byl poloviční slib.
            if (!sim.IsBuildingUnlocked(defIndex)
                || !sim.AutomationCanSpend(_content.Buildings[defIndex].BuildCost))
            {
                continue;
            }

            if (TryPlaceBest(sim, zone, defIndex))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Najde v zóně nejhezčí volné místo pro danou budovu a postaví tam.
    ///
    /// <para>Dřív se bralo první volné políčko od levého horního rohu, takže se
    /// zóna vylila jako slitek od kraje. Se skórováním vzniká zástavba podél
    /// cest a kolem už postaveného — čtvrť místo bloku.</para>
    /// </summary>
    private static bool TryPlaceBest(Simulation sim, Zone zone, int defIndex)
    {
        int bestX = 0, bestY = 0, bestScore = int.MinValue;

        for (int ty = zone.Y; ty < zone.Y + zone.Height; ty++)
        {
            for (int tx = zone.X; tx < zone.X + zone.Width; tx++)
            {
                var result = sim.CanPlace(defIndex, tx, ty);
                if (result == PlacementResult.NotEnoughResources)
                {
                    return false; // cena nezávisí na místě
                }

                if (result != PlacementResult.Ok)
                {
                    continue;
                }

                int score = CityLayout.Score(sim, tx, ty);
                if (score > bestScore)
                {
                    bestScore = score;
                    bestX = tx;
                    bestY = ty;
                }
            }
        }

        return bestScore != int.MinValue
            && sim.TryPlaceBuilding(defIndex, bestX, bestY) == PlacementResult.Ok;
    }
}
