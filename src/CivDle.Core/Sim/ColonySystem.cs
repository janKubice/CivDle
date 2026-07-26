using CivDle.Core.Content;
using CivDle.Core.WorldGen;

namespace CivDle.Core.Sim;

/// <summary>
/// Guvernér (automatizace, stupeň 5 dle living-city.md): když je doma plno,
/// sám založí novou kolonii opodál — zasadí „semínko" (auto-stavitelnou budovu)
/// dost daleko od stávající zástavby, aby vzniklo samostatné sídlo, které pak
/// doroste běžnou auto-stavbou. Tím se civilizace rozlézá po nekonečné mapě,
/// aniž by hráč sáhl na myš (satelitní sídla, living-city §8.6).
///
/// Zakládá se za normální cenu (soft pressure zůstává) a jen s politikou
/// „auto_expand". Běží na velmi nízké frekvenci — kolonie má být vzácná událost,
/// ne příval. „Náhoda" (směr expanze) je bezstavový hash (seed, tik).
/// </summary>
internal sealed class ColonySystem
{
    /// <summary>Kolonie se zakládá řádově vzácněji než běžná auto-stavba.</summary>
    private const int IntervalMultiplier = 8;

    /// <summary>Kolik prstenců kolem cílového místa se prohledá, než to systém vzdá.</summary>
    private const int SearchRings = 6;

    private readonly GameContent _content;
    private readonly long _seed;

    public ColonySystem(GameContent content, long seed)
    {
        _content = content;
        _seed = seed;
    }

    public void Tick(Simulation sim)
    {
        if (!sim.AutoExpandColonies)
        {
            return;
        }

        var config = _content.Gameplay.AutoBuild;
        long interval = (long)config.IntervalTicks * IntervalMultiplier;
        if (sim.TickCount % interval != 0)
        {
            return;
        }

        // Bez domoviny není odkud expandovat — první budovu klade hráč.
        if (sim.Buildings.Length == 0)
        {
            return;
        }

        // Kolonie řeší nedostatek místa doma: zakládá se až pod tlakem bydlení.
        if (sim.Population < sim.HousingCapacity - config.PopulationHeadroom)
        {
            return;
        }

        int seedDefIndex = FindColonySeedBuilding();
        if (seedDefIndex < 0 || !sim.CanAfford(seedDefIndex))
        {
            return;
        }

        TryFoundColony(sim, seedDefIndex);
    }

    /// <summary>Semínkem kolonie je první auto-stavitelná budova (bydlení) — stejná volba jako u auto-stavby.</summary>
    private int FindColonySeedBuilding()
    {
        for (int i = 0; i < _content.Buildings.Count; i++)
        {
            if (_content.Buildings[i].AutoBuild)
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>Vybere směr od těžiště zástavby a v cílové oblasti hledá místo dost daleko od všeho.</summary>
    private bool TryFoundColony(Simulation sim, int defIndex)
    {
        var buildings = sim.Buildings;
        long sumX = 0, sumY = 0;
        for (int i = 0; i < buildings.Length; i++)
        {
            sumX += buildings[i].X;
            sumY += buildings[i].Y;
        }

        int centerX = (int)(sumX / buildings.Length);
        int centerY = (int)(sumY / buildings.Length);

        // Deterministický směr: hash (seed, tik) → úhel; vzdálenost dle politiky.
        var rng = new SplitMix64(unchecked((ulong)_seed ^ ((ulong)sim.TickCount * 0xD1B54A32D192ED03UL)));
        double angle = (rng.Next() % 3600) / 3600.0 * Math.Tau;
        int distance = sim.ColonyDistance;

        int targetX = centerX + (int)Math.Round(Math.Cos(angle) * distance);
        int targetY = centerY + (int)Math.Round(Math.Sin(angle) * distance);

        // Prstence kolem cíle — první místo, které je volné, vhodné a dost daleko od zástavby.
        for (int ring = 0; ring < SearchRings; ring++)
        {
            for (int dy = -ring; dy <= ring; dy++)
            {
                for (int dx = -ring; dx <= ring; dx++)
                {
                    if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != ring)
                    {
                        continue; // jen okraj prstence
                    }

                    int x = targetX + dx;
                    int y = targetY + dy;
                    if (!IsFarFromExisting(sim, x, y, distance))
                    {
                        continue;
                    }

                    if (sim.CanPlace(defIndex, x, y) == PlacementResult.Ok
                        && sim.TryPlaceBuilding(defIndex, x, y) == PlacementResult.Ok)
                    {
                        sim.EnqueueColonyFounded();
                        return true;
                    }
                }
            }
        }

        return false;
    }

    /// <summary>Kolonie musí stát samostatně — jinak by jen splynula se stávajícím městem.</summary>
    private static bool IsFarFromExisting(Simulation sim, int x, int y, int distance)
    {
        // Půlka expanzní vzdálenosti stačí: dost daleko na samostatné sídlo,
        // ne tak daleko, aby se místo nikdy nenašlo.
        int minDistance = Math.Max(2, distance / 2);
        int minSquared = minDistance * minDistance;

        var buildings = sim.Buildings;
        for (int i = 0; i < buildings.Length; i++)
        {
            int dx = buildings[i].X - x;
            int dy = buildings[i].Y - y;
            if (dx * dx + dy * dy < minSquared)
            {
                return false;
            }
        }

        return true;
    }
}
