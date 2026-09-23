using CivDle.Core.Content;

namespace CivDle.Core.Sim;

/// <summary>
/// Spokojenost města (0–1) — jediná vrstva ve hře, kde stavění NENÍ zadarmo.
/// Bez ní bylo všechno monotónně dobré: víc budov = líp, a hráč nikdy nestál
/// před volbou, u které se dá udělat chyba.
///
/// <para>Skládá se ze dvou tlaků:</para>
/// <list type="bullet">
/// <item><b>Služby.</b> Obyvatelé potřebují obsloužit (trh, sýpka, lázně…). Budova
/// službu dodává, jen když má zaplacenou <b>údržbu</b> — služby tedy něco stojí
/// každou chvíli, ne jen jednou při stavbě.</item>
/// <item><b>Přelidnění.</b> Když se populace tlačí ke stropu bydlení, spokojenost
/// klesá — donutí to stavět dřív, než je pozdě.</item>
/// </list>
///
/// <para>Důsledek je měkký: nízká spokojenost brzdí RŮST, nikdy nikoho nezabíjí
/// ani neboří budovy (soft pressure dle mvp-roadmap.md). Volba zní „další výrobna,
/// nebo služby pro lidi?" — a obojí stojí zdroje.</para>
///
/// <para>Běží na nízké frekvenci (ne každý tik) — je to pomalý městský systém,
/// ne hot path.</para>
/// </summary>
internal sealed class HappinessSystem
{
    private readonly GameContent _content;

    // Pracovní pole pro rozdělení obsluhy podle dosahu. Drží se mezi voláními
    // (běží jednou za interval nad celým městem) a rostou jen s městem.
    private double[] _unserved = Array.Empty<double>();
    private double[] _unreached = Array.Empty<double>();
    private readonly List<int> _nearby = new();

    /// <summary>Poslední spočítaný rozpad (to, podle čeho se hra doopravdy řídí).</summary>
    private HappinessBreakdown _last;
    private bool _hasLast;

    public HappinessSystem(GameContent content)
    {
        _content = content;
    }

    /// <summary>Přepočítá spokojenost a strhne údržbu za obsluhující budovy.</summary>
    public void Tick(Simulation sim)
    {
        var config = _content.Gameplay.Happiness;
        if (!config.IsEnabled)
        {
            sim.Happiness = 1.0;
            return;
        }

        if (sim.TickCount % config.IntervalTicks != 0)
        {
            return;
        }

        _last = Evaluate(sim, config, payUpkeep: true);
        _hasLast = true;
        sim.Happiness = _last.Total;
    }

    /// <summary>
    /// Rozpad spokojenosti pro UI a guvernéra. Vrací ten z posledního přepočtu —
    /// ne nový. Dřív se počítal znovu v okamžiku dotazu, kdy už byla údržba
    /// strhnutá a sklad prázdný, a rozpad pak tvrdil „služby 0 %" vedle čísla,
    /// které počítalo se službami plnými. Guvernér podle toho stavěl knihovnu za
    /// knihovnou. Před prvním přepočtem se spočítá bez placení údržby.
    /// </summary>
    public HappinessBreakdown Current(Simulation sim, HappinessConfig config) =>
        _hasLast ? _last : Evaluate(sim, config, payUpkeep: false);

    /// <summary>Zapomene poslední rozpad (nový běh po Vzestupu, načtená hra).</summary>
    public void Invalidate() => _hasLast = false;

    private long _freshTick = -1;
    private HappinessBreakdown _fresh;

    /// <summary>
    /// Rozpad pro guvernéra: spočítaný teď, bez placení údržby, jednou za tik.
    ///
    /// <para><b>Proč ne ten z posledního přepočtu jako UI:</b> guvernér se podle
    /// něj rozhoduje, a rozhodnutí musí být čistou funkcí uloženého stavu — jinak
    /// by se načtená hra rozešla s tou, která běžela dál (načtená by do dalšího
    /// přepočtu žádný rozpad neměla). Spam knihoven, kvůli kterému se rozpad začal
    /// cachovat, tady nehrozí: guvernér rozlišuje dosah a zaplacenou obsluhu.</para>
    /// </summary>
    public HappinessBreakdown FreshForGovernor(Simulation sim, HappinessConfig config)
    {
        if (_freshTick != sim.TickCount)
        {
            _fresh = Evaluate(sim, config, payUpkeep: false);
            _freshTick = sim.TickCount;
        }

        return _fresh;
    }

    /// <summary>
    /// Spočítá spokojenost rozepsanou na položky. <paramref name="payUpkeep"/> =
    /// false umožní se jen podívat, aniž by se tím strhly suroviny.
    /// </summary>
    public HappinessBreakdown Evaluate(Simulation sim, HappinessConfig config, bool payUpkeep)
    {
        var (coverage, reach) = config.HasServiceReach
            ? CoverageByReach(sim, config, payUpkeep)
            : CoverageCitywide(sim, config, payUpkeep);

        // Přelidnění: čím blíž je populace stropu bydlení, tím hůř se žije —
        // ale až nad prahem (viz HappinessConfig.CrowdingPenalty).
        double occupancy = sim.HousingCapacity <= 0
            ? 1.0
            : Math.Clamp(sim.Population / sim.HousingCapacity, 0.0, 1.0);

        // Kouř se počítá tam, kde lidé bydlí — nad těžištěm města, ne jako průměr
        // přes celou mapu. Díky tomu je „postav hutě za kopcem" skutečné rozhodnutí
        // a ne kosmetika: vzdálená továrna zamoří svoje okolí, ne obývák.
        double smog = -_content.Gameplay.Pollution.HappinessDrop(sim.AirPollutionOverCity);

        // Zvolený program města se přičítá ke spokojenosti — reformátoři a slavnosti
        // nedělají nic jiného, než že lidem zlepší náladu.
        return new HappinessBreakdown(
            Base: config.BaseHappiness,
            Services: coverage * config.ServiceWeight,
            Crowding: -config.CrowdingPenalty(occupancy),
            Government: sim.ElectionHappinessBonus,
            ServiceCoverage: coverage,
            Pollution: smog,
            ServiceReach: reach);
    }

    /// <summary>
    /// Kde bydlí nejvíc lidí, ke kterým žádná služba nedosáhne — tam má guvernér
    /// postavit trh. Platí pro poslední přepočet; bez dosahu služeb nic nevrací.
    /// </summary>
    public bool TryFindUnservedHome(Simulation sim, out int x, out int y)
    {
        x = y = 0;
        var config = _content.Gameplay.Happiness;
        if (!config.IsEnabled || !config.HasServiceReach)
        {
            return false;
        }

        // Pole „kdo je bez služby" musí patřit tomuhle okamžiku, ne poslednímu
        // přepočtu — kvůli determinismu po načtení (viz FreshForGovernor).
        _freshTick = -1;
        FreshForGovernor(sim, config);

        var buildings = sim.Buildings;
        int best = -1;
        double most = 0.5; // pod půl člověka to za nový trh nestojí
        for (int i = 0; i < buildings.Length && i < _unreached.Length; i++)
        {
            if (_unreached[i] > most)
            {
                most = _unreached[i];
                best = i;
            }
        }

        if (best < 0)
        {
            return false;
        }

        x = buildings[best].X;
        y = buildings[best].Y;
        return true;
    }

    /// <summary>
    /// Starý model: služby obslouží celé město bez ohledu na vzdálenost.
    /// Zůstává pro obsah bez <c>serviceReachTiles</c>.
    /// </summary>
    private (double Coverage, double Reach) CoverageCitywide(Simulation sim, HappinessConfig config, bool payUpkeep)
    {
        // Poptávku po službách tvoří až lidé NAD prahem soběstačnosti — malá
        // vesnice si vystačí sama a hra ji netrestá za chybějící trh, který se
        // stejně odemyká později.
        double demand = sim.Population - config.FreePopulation;
        if (demand <= 0)
        {
            return (1.0, 1.0);
        }

        var (served, potential) = ServedPeople(sim, config, payUpkeep);
        return (Math.Clamp(served / demand, 0.0, 1.0), Math.Clamp(potential / demand, 0.0, 1.0));
    }

    /// <summary>
    /// Služby obslouží jen domy ve svém dosahu, a každá jen tolik lidí, kolik
    /// unese. Trh na druhém konci města tvým ulicím nepomůže — to dělá z polohy
    /// služby rozhodnutí a ze čtvrtí smysl.
    ///
    /// <para>Lidé se rozpočítají do domů podle kapacity. Každá služba si pak
    /// vezme domy ve čtverci kolem sebe (přes prostorový index, ne přes celé
    /// město) a obslouží z nich, kolik unese. Dvakrát: jednou jen se službami,
    /// které mají zaplacenou údržbu (to je skutečnost), jednou se všemi (to je
    /// dosah — rozdíl říká „chybí suroviny na provoz", ne „chybí služby").</para>
    /// </summary>
    private (double Coverage, double Reach) CoverageByReach(Simulation sim, HappinessConfig config, bool payUpkeep)
    {
        if (sim.Population <= config.FreePopulation)
        {
            Array.Clear(_unreached);
            return (1.0, 1.0);
        }

        var buildings = sim.Buildings;
        if (_unserved.Length < buildings.Length)
        {
            _unserved = new double[Math.Max(buildings.Length, _unserved.Length * 2 + 16)];
            _unreached = new double[_unserved.Length];
        }

        double totalWeight = 0;
        for (int i = 0; i < buildings.Length; i++)
        {
            var def = _content.Buildings[buildings[i].DefIndex];
            if (buildings[i].IsComplete && def.HousingCapacity > 0)
            {
                totalWeight += def.HousingCapacity;
            }
        }

        // Bez domů bydlí všichni v táboře — ten dosah nemá, obslouží ho cokoli.
        if (totalWeight <= 0)
        {
            return CoverageCitywide(sim, config, payUpkeep);
        }

        double perUnit = sim.Population / totalWeight;
        double housed = 0;
        for (int i = 0; i < buildings.Length; i++)
        {
            var def = _content.Buildings[buildings[i].DefIndex];
            double people = buildings[i].IsComplete && def.HousingCapacity > 0 ? def.HousingCapacity * perUnit : 0;
            _unserved[i] = people;
            _unreached[i] = people;
            housed += people;
        }

        var resources = sim.Resources;
        int reachTiles = config.ServiceReachTiles;
        double served = 0, reached = 0;
        for (int i = 0; i < buildings.Length; i++)
        {
            var def = _content.Buildings[buildings[i].DefIndex];
            if (def.ServiceValue <= 0 || !buildings[i].IsComplete)
            {
                continue;
            }

            bool paid = PayUpkeep(sim, resources, def, payUpkeep);
            double capacity = def.ServiceValue * config.PeoplePerServicePoint;

            _nearby.Clear();
            sim.BuildingsIn(
                buildings[i].X - reachTiles, buildings[i].Y - reachTiles,
                buildings[i].X + reachTiles, buildings[i].Y + reachTiles, _nearby);

            reached += Serve(_unreached, capacity, buildings, buildings[i].X, buildings[i].Y, reachTiles);
            if (paid)
            {
                served += Serve(_unserved, capacity, buildings, buildings[i].X, buildings[i].Y, reachTiles);
            }
        }

        // Prvních pár lidí se obslouží samo (malá vesnice), dál rozhoduje dosah.
        double free = Math.Min(config.FreePopulation, housed);
        return (
            Math.Clamp((served + free) / housed, 0.0, 1.0),
            Math.Clamp((reached + free) / housed, 0.0, 1.0));
    }

    /// <summary>
    /// Obslouží z domů v okolí, kolik služba unese. Index vrací celé bloky
    /// 32×32, takže se tu ještě ověří skutečná vzdálenost.
    /// </summary>
    private double Serve(
        double[] remaining, double capacity, ReadOnlySpan<BuildingInstance> buildings, int x, int y, int reach)
    {
        double taken = 0;
        for (int n = 0; n < _nearby.Count && capacity > 0; n++)
        {
            int index = _nearby[n];
            if (index >= remaining.Length || remaining[index] <= 0
                || Math.Abs(buildings[index].X - x) > reach || Math.Abs(buildings[index].Y - y) > reach)
            {
                continue;
            }

            double take = Math.Min(remaining[index], capacity);
            remaining[index] -= take;
            capacity -= take;
            taken += take;
        }

        return taken;
    }

    /// <summary>
    /// Kolik lidí obslouží postavené budovy (bez dosahu) — skutečně a potenciálně.
    /// Budova se počítá jen tehdy, když se za ni zaplatí údržba — to je ta
    /// opakovaná cena, která dělá ze služeb rozhodnutí, a ne samozřejmost.
    /// </summary>
    private (double Served, double Potential) ServedPeople(Simulation sim, HappinessConfig config, bool payUpkeep)
    {
        var buildings = sim.Buildings;
        var resources = sim.Resources;
        double served = 0, potential = 0;

        for (int i = 0; i < buildings.Length; i++)
        {
            var def = _content.Buildings[buildings[i].DefIndex];
            if (def.ServiceValue <= 0)
            {
                continue;
            }

            double people = def.ServiceValue * config.PeoplePerServicePoint;
            potential += people;
            if (PayUpkeep(sim, resources, def, payUpkeep))
            {
                served += people;
            }
        }

        return (served, potential);
    }

    /// <summary>
    /// Zaplatí údržbu služby (když se platí), nebo jen ověří, že by šla zaplatit.
    /// Údržba se platí jen z toho, co je nad rezervou guvernéra (viz
    /// ConstructionClaim): když se šetří na farmu, trh chvíli počká.
    /// </summary>
    private static bool PayUpkeep(Simulation sim, double[] resources, BuildingDef def, bool pay)
    {
        if (def.Upkeep.Count == 0)
        {
            return true;
        }

        if (!CanPay(resources, sim.Claim.Amounts, def.Upkeep))
        {
            return false; // nezaplacená údržba = budova neslouží
        }

        if (pay)
        {
            for (int u = 0; u < def.Upkeep.Count; u++)
            {
                resources[def.Upkeep[u].ResourceIndex] -= def.Upkeep[u].Amount;
                sim.Ledger.RecordConsumed(def.Upkeep[u].ResourceIndex, def.Upkeep[u].Amount, ConsumptionKind.Upkeep);
            }
        }

        return true;
    }

    private static bool CanPay(double[] resources, double[] claimed, IReadOnlyList<ResourceAmount> upkeep)
    {
        for (int i = 0; i < upkeep.Count; i++)
        {
            int index = upkeep[i].ResourceIndex;
            if (resources[index] - claimed[index] < upkeep[i].Amount)
            {
                return false;
            }
        }

        return true;
    }
}
