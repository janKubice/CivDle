using CivDle.Core.Content;
using CivDle.Core.Sim;

namespace CivDle.Balance;

/// <summary>Jak dopadla jedna výzva v běhu bez hráče.</summary>
/// <param name="Id">ID výzvy.</param>
/// <param name="Outcome">Výhra, prohra, nebo běží (bez limitu a bez konce).</param>
/// <param name="Minutes">V kolikáté minutě to skončilo.</param>
/// <param name="Population">Populace na konci.</param>
/// <param name="Buildings">Budov na konci.</param>
/// <param name="LandPercent">Kolik procent okolí startu je souš (kontrola světa).</param>
public readonly record struct ChallengeResult(
    string Id, ScenarioOutcome Outcome, double Minutes, double Population, int Buildings, int LandPercent);

/// <summary>
/// Projede výzvy (scénáře) s náhradním hráčem a řekne, jestli jdou vyhrát.
///
/// <para>Cíl výzvy se nemá odhadovat: „1 000 lidí za 55 minut" je buď
/// dosažitelné, nebo past. Náhradní hráč prvních deset minut kliká na zdroje
/// a staví úvodní budovy (jako nový hráč), pak nechá město guvernérovi
/// a jen jednou za půl minuty něco vyzkoumá. Kde vyhraje on, vyhraje i
/// člověk; kde těsně prohraje, je výzva pro aktivního hráče.</para>
/// </summary>
public sealed class ChallengeRun
{
    private static readonly string[] Opening = { "house", "farm", "lumber_camp", "sawmill", "quarry" };

    private readonly GameContent _content;

    public ChallengeRun(GameContent content) => _content = content;

    /// <summary>Odehraje jednu výzvu; bez limitu nejvýš 90 minut.</summary>
    public ChallengeResult Run(int scenarioIndex)
    {
        var scenario = _content.Scenarios[scenarioIndex];
        var content = ScenarioWorld.ContentFor(_content, scenario);
        var sim = ScenarioWorld.Create(_content, scenarioIndex);
        var (startX, startY) = StartSiteFinder.Find(sim);
        var nodes = NodesAround(sim, content, startX, startY);
        bool byHand = scenario.Has(ScenarioRule.NoAutoBuild);
        double limit = scenario.HasTimeLimit ? scenario.TimeLimitSeconds : 90 * 60;
        int ticksPerSecond = (int)Simulation.TicksPerSecond;

        int second = 0;
        for (; second <= limit + 5 && sim.ScenarioResult == ScenarioOutcome.Running; second++)
        {
            if (second < 600 || byHand)
            {
                Click(sim, nodes);
                if (second % 5 == 0)
                {
                    BuildOpening(sim, content, startX, startY, byHand);
                }
            }

            if (second % 30 == 0)
            {
                ResearchCheapest(sim, content);
            }

            for (int t = 0; t < ticksPerSecond; t++)
            {
                sim.Tick();
            }
        }

        return new ChallengeResult(
            scenario.Id, sim.ScenarioResult, second / 60.0, sim.Population, sim.Buildings.Length,
            LandPercent(sim, content, startX, startY));
    }

    private static List<(int X, int Y, int Resource)> NodesAround(Simulation sim, GameContent content, int x, int y)
    {
        var nodes = new List<(int X, int Y, int Resource, int Distance)>();
        for (int dy = -25; dy <= 25; dy++)
        {
            for (int dx = -25; dx <= 25; dx++)
            {
                if (content.Biomes[sim.BiomeAt(x + dx, y + dy)].ClickYield is { } yield)
                {
                    nodes.Add((x + dx, y + dy, yield.ResourceIndex, dx * dx + dy * dy));
                }
            }
        }

        return nodes.OrderBy(n => n.Distance).Select(n => (n.X, n.Y, n.Resource)).ToList();
    }

    /// <summary>Tři kliky za sekundu na surovinu, které je poměrně nejméně.</summary>
    private static void Click(Simulation sim, List<(int X, int Y, int Resource)> nodes)
    {
        for (int click = 0; click < 3; click++)
        {
            var wanted = nodes.Select(n => n.Resource).Distinct()
                .OrderBy(r => sim.GetResource(r) / Math.Max(1, sim.GetStorageCap(r)));
            foreach (int resource in wanted)
            {
                if (sim.GetResource(resource) >= sim.GetStorageCap(resource) - 1)
                {
                    continue;
                }

                int node = nodes.FindIndex(n => n.Resource == resource && sim.NodeChargesLeft(n.X, n.Y) > 0);
                if (node >= 0 && sim.TryHarvest(nodes[node].X, nodes[node].Y, out _, out _))
                {
                    break;
                }
            }
        }
    }

    private static void BuildOpening(Simulation sim, GameContent content, int x, int y, bool byHand)
    {
        foreach (string id in Opening)
        {
            int def = content.Buildings.IndexOf(id);
            if (def >= 0 && !sim.Buildings.ToArray().Any(b => b.DefIndex == def)
                && sim.IsBuildingBuildable(def) && sim.CanAfford(def))
            {
                PlaceNear(sim, def, x, y);
                return;
            }
        }

        if (!byHand)
        {
            return;
        }

        // Ve výzvě bez guvernéra staví náhradní hráč sám: dům, když je plno,
        // jinak první dostupnou levnou výrobu.
        int house = content.Buildings.IndexOf("house");
        if (sim.Population >= sim.HousingCapacity - 1 && sim.CanAfford(house))
        {
            PlaceNear(sim, house, x, y);
            return;
        }

        foreach (string id in new[] { "farm", "lumber_camp", "house", "sawmill", "quarry" })
        {
            int def = content.Buildings.IndexOf(id);
            if (def >= 0 && sim.IsBuildingBuildable(def) && sim.CanAfford(def))
            {
                PlaceNear(sim, def, x, y);
                return;
            }
        }
    }

    private static void PlaceNear(Simulation sim, int def, int x, int y)
    {
        for (int r = 1; r < 24; r++)
        {
            for (int dy = -r; dy <= r; dy++)
            {
                for (int dx = -r; dx <= r; dx++)
                {
                    if (Math.Max(Math.Abs(dx), Math.Abs(dy)) == r
                        && sim.CanPlace(def, x + dx, y + dy) == PlacementResult.Ok
                        && sim.TryPlaceBuilding(def, x + dx, y + dy) == PlacementResult.Ok)
                    {
                        return;
                    }
                }
            }
        }
    }

    private static void ResearchCheapest(Simulation sim, GameContent content)
    {
        int best = -1;
        double bestCost = double.MaxValue;
        for (int t = 0; t < content.Techs.Count; t++)
        {
            if (sim.CanResearch(t) == PlacementResult.Ok && sim.TotalResearchCost(t) < bestCost)
            {
                bestCost = sim.TotalResearchCost(t);
                best = t;
            }
        }

        if (best >= 0)
        {
            sim.TryResearch(best);
        }
    }

    private static int LandPercent(Simulation sim, GameContent content, int x, int y)
    {
        int land = 0, total = 0;
        for (int dy = -60; dy <= 60; dy += 2)
        {
            for (int dx = -60; dx <= 60; dx += 2)
            {
                total++;
                if (!content.Biomes[sim.BiomeAt(x + dx, y + dy)].IsWater)
                {
                    land++;
                }
            }
        }

        return land * 100 / total;
    }
}
