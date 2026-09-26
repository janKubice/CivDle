using CivDle.Core.Content;

namespace CivDle.Core.Sim;

/// <summary>
/// Sklady: postaví je, když se o plný sklad zastavuje výroba, nebo když se do
/// skladu nevejde cena věci, kterou město chce (stavba, výzkum).
///
/// <para><b>Proč to chybělo:</b> guvernér sklady nestavěl nikdy. Horší ale
/// bylo, že budovu, jejíž cena se do skladu nevešla, tiše přeskočil — a město
/// zůstalo zaseknuté na věci, na kterou se nedá našetřit, aniž by kdokoli
/// řekl proč. Teď je „nevejde se" signál pro stavbu skladu.</para>
///
/// <para>Dva stupně naléhavosti: <b>blokující</b> (cena se nevejde — bez
/// skladu to nepůjde nikdy) je nad bydlením, <b>přetékající</b> (výroba stojí
/// o plný sklad) pod ním — zásoby už jsou, jen by jich mohlo být víc.</para>
///
/// <para><b>Přetékání má strop.</b> Město na stropu měřítka vyrábí přebytky
/// donekonečna, takže „sklad je plný" platí pořád znovu — a první verze
/// postavila za hodinu pět set skladů. Sklad navíc má smysl jen do výše toho,
/// na co se surovina opravdu utrácí: do <see cref="Headroom"/>násobku
/// nejdražší ceny (stavba, vylepšení, další výzkum). Nad tím je přebytek
/// přebytkem a další sklad by jen zabral místo.</para>
///
/// <para>Přednost mají čisté sklady: přístav nebo trh sice něco pojmou, ale
/// chtějí lidi a údržbu — sklad postavený kvůli dřevu nemá z města dělat
/// přístavní čtvrť.</para>
/// </summary>
internal sealed class StorageGoal : IGovernorGoal
{
    /// <summary>Cena se do skladu nevejde — bez skladu to nepůjde nikdy.</summary>
    public const int BlockingUrgency = 75;

    /// <summary>Výroba se zastavuje o plný sklad.</summary>
    public const int OverflowUrgency = 45;

    /// <summary>Kolikrát víc, než stojí nejdražší věc, má smysl skladovat.</summary>
    private const double Headroom = 2.0;

    /// <summary>O kolik je čistý sklad (kategorie storage) lepší než budova, která skladuje mimochodem.</summary>
    private const int PureStorageWeight = 3;

    private readonly GameContent _content;
    private readonly StorageGoalConfig _config;
    private readonly GovernorRoles _roles;
    private readonly GovernorMemory _memory;

    /// <summary>Které definice zvětšují sklad které suroviny (předpočítané).</summary>
    private readonly List<int>[] _storesResource;

    /// <summary>Kolik výroben které suroviny stojí o plný sklad — pomocné pole, drží se mezi koly.</summary>
    private readonly int[] _blocked;

    public StorageGoal(GameContent content, GovernorRoles roles, GovernorMemory memory)
    {
        _content = content;
        _config = content.Gameplay.Governor.Storage;
        _roles = roles;
        _memory = memory;
        _blocked = new int[content.Resources.Count];
        _storesResource = new List<int>[content.Resources.Count];
        for (int r = 0; r < _storesResource.Length; r++)
        {
            _storesResource[r] = new List<int>();
        }

        for (int d = 0; d < content.Buildings.Count; d++)
        {
            foreach (var bonus in content.Buildings[d].StorageBonus)
            {
                if (bonus.Amount > 0)
                {
                    _storesResource[bonus.ResourceIndex].Add(d);
                }
            }
        }
    }

    public CityNeed Need => CityNeed.Storage;

    public GoalAssessment Assess(Simulation sim)
    {
        if (!_config.IsEnabled)
        {
            return GoalAssessment.Idle(Need);
        }

        // Nejdřív to, co blokuje navždy: cena, která se nevejde. Guvernér si
        // ji zapamatoval v minulém kole; když ne, zkusí se další výzkum.
        int beyond = _memory.BeyondStorage >= 0 ? _memory.BeyondStorage : ResearchBeyondStorage(sim);
        if (beyond >= 0 && CanStore(sim, beyond))
        {
            return new GoalAssessment(Need, BlockingUrgency, beyond, AnchorFor(sim, beyond));
        }

        int overflowing = Overflowing(sim);
        return overflowing >= 0
            ? new GoalAssessment(Need, OverflowUrgency, overflowing, AnchorFor(sim, overflowing))
            : GoalAssessment.Idle(Need);
    }

    /// <summary>
    /// Čím víc místa pro danou surovinu, tím líp — čistý sklad před budovou,
    /// která skladuje mimochodem a chce k tomu lidi a údržbu.
    /// </summary>
    public int Score(Simulation sim, int defIndex, in GoalAssessment goal)
    {
        if (goal.Resource < 0)
        {
            return 0;
        }

        var def = _content.Buildings[defIndex];
        foreach (var bonus in def.StorageBonus)
        {
            if (bonus.ResourceIndex != goal.Resource || bonus.Amount <= 0)
            {
                continue;
            }

            double score = bonus.Amount * (def.Category == "storage" ? PureStorageWeight : 1);
            score /= 1 + def.WorkerSlots;
            if (def.Upkeep.Count > 0)
            {
                score /= 2;
            }

            return Math.Max(1, (int)Math.Min(10_000, score));
        }

        return 0;
    }

    /// <summary>Surovina, kterou nejlevnější další výzkum chce víc, než se vejde; −1 = žádná.</summary>
    private static int ResearchBeyondStorage(Simulation sim)
    {
        int tech = sim.CheapestOpenTech();
        if (tech < 0)
        {
            return -1;
        }

        var cost = sim.ContentRef.Techs[tech].Cost;
        for (int i = 0; i < cost.Count; i++)
        {
            int resource = cost[i].ResourceIndex;
            if (sim.ResearchCostOf(tech, resource) > sim.GetStorageCap(resource))
            {
                return resource;
            }
        }

        return -1;
    }

    /// <summary>
    /// Surovina, jejíž sklad je plný a výroba se o něj zastavuje nebo přetéká;
    /// −1 = žádná. Ze všech ta, o kterou stojí nejvíc výroben.
    /// </summary>
    private int Overflowing(Simulation sim)
    {
        Array.Clear(_blocked);
        var buildings = sim.Buildings;
        for (int i = 0; i < buildings.Length; i++)
        {
            if (buildings[i].Stall != BuildingStall.OutputFull
                || _content.Buildings[buildings[i].DefIndex].Recipe is not { } recipe)
            {
                continue;
            }

            for (int o = 0; o < recipe.Outputs.Count; o++)
            {
                _blocked[recipe.Outputs[o].ResourceIndex]++;
            }
        }

        int best = -1;
        double bestWeight = 0;
        var ledger = sim.Ledger;
        for (int r = 0; r < _blocked.Length; r++)
        {
            double cap = sim.GetStorageCap(r);
            if (cap <= 0 || sim.GetResource(r) < cap * _config.FullShare)
            {
                continue;
            }

            // Přetékání z evidence toků se počítá taky: výrobna bez zastavení
            // (sběr, zlaté úlovky) sklad nezastaví, jen o surovinu přijde.
            double weight = _blocked[r] + (ledger.WastedPerSecond(r) > 0 ? 1 : 0);
            if (weight > bestWeight && cap < Headroom * LargestSpend(sim, r) && CanStore(sim, r))
            {
                bestWeight = weight;
                best = r;
            }
        }

        return best;
    }

    /// <summary>
    /// Nejdražší věc, za kterou se surovina teď dá utratit: stavba nebo
    /// vylepšení, které guvernér smí udělat, a další výzkum.
    /// </summary>
    private double LargestSpend(Simulation sim, int resource)
    {
        double largest = 0;
        for (int d = 0; d < _content.Buildings.Count; d++)
        {
            var def = _content.Buildings[d];
            if (_roles.MayBuild(sim, d))
            {
                largest = Math.Max(largest, AmountOf(def.BuildCost, resource));
            }

            if (def.HasUpgrade && sim.IsBuildingUnlocked(def.UpgradesToIndex))
            {
                largest = Math.Max(largest, AmountOf(def.UpgradeCost, resource));
            }
        }

        int tech = sim.CheapestOpenTech(resource);
        if (tech >= 0)
        {
            largest = Math.Max(largest, sim.ResearchCostOf(tech, resource));
        }

        return largest;
    }

    private static double AmountOf(IReadOnlyList<ResourceAmount> cost, int resource)
    {
        for (int i = 0; i < cost.Count; i++)
        {
            if (cost[i].ResourceIndex == resource)
            {
                return cost[i].Amount;
            }
        }

        return 0;
    }

    /// <summary>Umí guvernér postavit něco, co sklad téhle suroviny zvětší?</summary>
    private bool CanStore(Simulation sim, int resource)
    {
        foreach (int defIndex in _storesResource[resource])
        {
            if (_roles.MayBuild(sim, defIndex))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// U které budovy sklad postavit: u výrobny té suroviny (nejlépe té, která
    /// stojí o plný sklad) — svoz je pak krátký. −1 = kdekoli.
    /// </summary>
    private int AnchorFor(Simulation sim, int resource)
    {
        var buildings = sim.Buildings;
        int any = -1;
        for (int i = 0; i < buildings.Length; i++)
        {
            if (_content.Buildings[buildings[i].DefIndex].Recipe is not { } recipe || !Outputs(recipe, resource))
            {
                continue;
            }

            if (buildings[i].Stall == BuildingStall.OutputFull)
            {
                return i;
            }

            any = any < 0 ? i : any;
        }

        return any;
    }

    private static bool Outputs(Recipe recipe, int resource)
    {
        for (int o = 0; o < recipe.Outputs.Count; o++)
        {
            if (recipe.Outputs[o].ResourceIndex == resource)
            {
                return true;
            }
        }

        return false;
    }
}
