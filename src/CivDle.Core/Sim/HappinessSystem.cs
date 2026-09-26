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

    // Guvernérův pohled na obsluhu (viz FreshForGovernor): pro který okamžik
    // platí pole _unserved/_unreached a součty z posledního přepočtu po dosahu.
    private long _viewTick = -1;
    private long _viewLayout;
    private double _viewPopulation;
    private int _viewCount;
    private HappinessBreakdown _fresh;

    // Součty posledního přepočtu po dosahu — z nich se po přidání služby
    // skládá nové pokrytí, aniž by se počítalo celé město znovu.
    private bool _byReach;
    private double _served, _reached, _housed, _free;

    /// <summary>
    /// Rozpad pro guvernéra: bez placení údržby, jednou za tik spočítaný celý
    /// a pak už jen <b>dopočítávaný</b> o budovy, které guvernér v tomtéž tiku
    /// přidal.
    ///
    /// <para><b>Proč ne ten z posledního přepočtu jako UI:</b> guvernér se podle
    /// něj rozhoduje, a rozhodnutí musí být čistou funkcí uloženého stavu — jinak
    /// by se načtená hra rozešla s tou, která běžela dál (načtená by do dalšího
    /// přepočtu žádný rozpad neměla). Spam knihoven, kvůli kterému se rozpad začal
    /// cachovat, tady nehrozí: guvernér rozlišuje dosah a zaplacenou obsluhu.</para>
    ///
    /// <para><b>Proč dopočítávat, a ne počítat znovu:</b> guvernér se ptá po
    /// každé stavbě služby. Dřív to znamenalo celé město znovu — každá služba
    /// projde domy ve svém okolí — a v kole s rozpočtem tisíců staveb to
    /// u velkého města trvalo desítky minut; na tom zamrzalo přetočení času
    /// i dohánění offline. Přidaná budova má nejvyšší index, takže ji plný
    /// přepočet zpracuje jako poslední: stačí ji obsloužit ze zbytků, výsledek
    /// je stejný (hlídá to test).</para>
    ///
    /// <para>Dvě zjednodušení platí do konce tiku, pak je plný přepočet srovná:
    /// dům přidaný v tomtéž tiku je <b>prázdný</b> (lidé se do něj teprve
    /// nastěhují — jinak by se musela přerozdělit celá populace a s ní celé
    /// město), a údržba starších služeb se posuzuje podle stavu skladu na
    /// začátku. Vylepšení se projeví taky až v dalším tiku. Zbourání, přesun
    /// domu či služby a dostavba pohled zahodí (<see cref="Simulation.LayoutRevision"/>).</para>
    /// </summary>
    public HappinessBreakdown FreshForGovernor(Simulation sim, HappinessConfig config)
    {
        var buildings = sim.Buildings;
        if (!ViewIsCurrent(sim))
        {
            _fresh = Evaluate(sim, config, payUpkeep: false);
            _viewTick = sim.TickCount;
            _viewLayout = sim.LayoutRevision;
            _viewPopulation = sim.Population;
            _viewCount = buildings.Length;
        }
        else if (buildings.Length > _viewCount)
        {
            ExtendView(sim, config);
        }

        return _fresh;
    }

    private bool ViewIsCurrent(Simulation sim) =>
        _viewTick == sim.TickCount
        && _viewLayout == sim.LayoutRevision
        && _viewPopulation.Equals(sim.Population)
        && sim.Buildings.Length >= _viewCount;

    /// <summary>
    /// Dopočítá pohled o budovy přidané od posledního přepočtu (viz
    /// <see cref="FreshForGovernor"/>). Bez pole po domech (malé město, obsluha
    /// bez dosahu) je přepočet levný, takže se po nové službě či domě udělá celý.
    /// </summary>
    private void ExtendView(Simulation sim, HappinessConfig config)
    {
        var buildings = sim.Buildings;
        int from = _viewCount;
        _viewCount = buildings.Length;
        EnsureArrays(buildings.Length);

        bool changed = false;
        for (int i = from; i < buildings.Length; i++)
        {
            var def = _content.Buildings[buildings[i].DefIndex];
            bool counts = buildings[i].IsComplete && (def.ServiceValue > 0 || def.HousingCapacity > 0);
            if (!_byReach)
            {
                changed |= counts;
                continue;
            }

            // Nový dům je zatím prázdný (viz FreshForGovernor) — pole na jeho
            // indexu může ale pamatovat budovu, která tu stála před zbouráním.
            _unserved[i] = 0;
            _unreached[i] = 0;
            if (counts && def.ServiceValue > 0)
            {
                ServeFrom(sim, config, i, def, payUpkeep: false, ref _served, ref _reached);
                changed = true;
            }
        }

        if (!changed)
        {
            return;
        }

        if (!_byReach)
        {
            // Plný přepočet — pohled tím zanikne, tak se hned obnoví.
            _fresh = Evaluate(sim, config, payUpkeep: false);
            _viewTick = sim.TickCount;
            _viewLayout = sim.LayoutRevision;
            _viewPopulation = sim.Population;
            return;
        }

        var (coverage, reach) = Ratios();
        _fresh = Compose(sim, config, coverage, reach);
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
        return Compose(sim, config, coverage, reach);
    }

    /// <summary>Složí rozpad z pokrytí službami a ze zbytku města (přelidnění, kouř, program).</summary>
    private HappinessBreakdown Compose(Simulation sim, HappinessConfig config, double coverage, double reach)
    {
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
    /// postavit trh. Platí pro guvernérův pohled (<see cref="FreshForGovernor"/>),
    /// tedy včetně služeb, které v tomhle tiku už postavil; bez dosahu služeb
    /// nic nevrací.
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
        FreshForGovernor(sim, config);
        if (!_byReach)
        {
            return false; // malá vesnice nebo lidé v táboře: dosah nerozhoduje
        }

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
        // Pole se teď přepíšou — guvernérův pohled, pokud nějaký byl, tím končí.
        _viewTick = -1;
        _byReach = false;
        if (sim.Population <= config.FreePopulation)
        {
            Array.Clear(_unreached);
            return (1.0, 1.0);
        }

        var buildings = sim.Buildings;
        EnsureArrays(buildings.Length);

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

        double served = 0, reached = 0;
        for (int i = 0; i < buildings.Length; i++)
        {
            var def = _content.Buildings[buildings[i].DefIndex];
            if (def.ServiceValue > 0 && buildings[i].IsComplete)
            {
                ServeFrom(sim, config, i, def, payUpkeep, ref served, ref reached);
            }
        }

        // Prvních pár lidí se obslouží samo (malá vesnice), dál rozhoduje dosah.
        _served = served;
        _reached = reached;
        _housed = housed;
        _free = Math.Min(config.FreePopulation, housed);
        _byReach = true;
        return Ratios();
    }

    private (double Coverage, double Reach) Ratios() => (
        Math.Clamp((_served + _free) / _housed, 0.0, 1.0),
        Math.Clamp((_reached + _free) / _housed, 0.0, 1.0));

    /// <summary>
    /// Jedna služba obslouží domy ve svém okolí: jednou jen se zaplacenou
    /// údržbou (skutečnost), jednou bez ohledu na ni (dosah — rozdíl říká
    /// „chybí suroviny na provoz", ne „chybí služby").
    /// </summary>
    private void ServeFrom(
        Simulation sim, HappinessConfig config, int index, BuildingDef def, bool payUpkeep,
        ref double served, ref double reached)
    {
        var buildings = sim.Buildings;
        bool paid = PayUpkeep(sim, sim.Resources, def, payUpkeep);
        double capacity = def.ServiceValue * config.PeoplePerServicePoint;
        int reachTiles = config.ServiceReachTiles;
        int x = buildings[index].X, y = buildings[index].Y;

        _nearby.Clear();
        sim.BuildingsIn(x - reachTiles, y - reachTiles, x + reachTiles, y + reachTiles, _nearby);

        reached += Serve(_unreached, capacity, buildings, x, y, reachTiles);
        if (paid)
        {
            served += Serve(_unserved, capacity, buildings, x, y, reachTiles);
        }
    }

    private void EnsureArrays(int count)
    {
        if (_unserved.Length >= count)
        {
            return;
        }

        int size = Math.Max(count, _unserved.Length * 2 + 16);
        Array.Resize(ref _unserved, size);
        Array.Resize(ref _unreached, size);
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

        // Násobič údržby (výzva „Drahý provoz", politika „Úsporná správa")
        // sahá na obojí — na to, jestli na údržbu je, i na to, kolik se strhne.
        // Kdyby se zdražila jen platba, služba by „měla na to" a pak šla do mínusu.
        double mult = sim.UpkeepMult;
        if (!CanPay(resources, sim.Claim.Amounts, def.Upkeep, mult))
        {
            return false; // nezaplacená údržba = budova neslouží
        }

        if (pay)
        {
            for (int u = 0; u < def.Upkeep.Count; u++)
            {
                double amount = def.Upkeep[u].Amount * mult;
                resources[def.Upkeep[u].ResourceIndex] -= amount;
                sim.Ledger.RecordConsumed(def.Upkeep[u].ResourceIndex, amount, ConsumptionKind.Upkeep);
            }
        }

        return true;
    }

    private static bool CanPay(double[] resources, double[] claimed, IReadOnlyList<ResourceAmount> upkeep, double mult)
    {
        for (int i = 0; i < upkeep.Count; i++)
        {
            int index = upkeep[i].ResourceIndex;
            if (resources[index] - claimed[index] < upkeep[i].Amount * mult)
            {
                return false;
            }
        }

        return true;
    }
}
