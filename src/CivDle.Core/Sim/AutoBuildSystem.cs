using CivDle.Core.Content;
using CivDle.Core.WorldGen;

namespace CivDle.Core.Sim;

/// <summary>
/// Guvernér: automatický růst města (fáze 2 roadmapy: „domy se staví samy dle
/// poptávky"), dnes už i jeho zásobování. Staví za normální cenu, takže růst
/// táhne poptávku po surovinách (dřevo → prkna).
///
/// <para>Jedno kolo = zjistit, co město nejvíc potřebuje, a zkusit to pokrýt:</para>
/// <list type="bullet">
/// <item><see cref="GovernorNeeds"/> — co chybí (jídlo, vstupy, služby, bydlení, práce).</item>
/// <item><see cref="GovernorChains"/> — dá se surovina vůbec sehnat? Nezačne řetěz,
/// který nedotáhne (pekárna bez obilí).</item>
/// <item><see cref="GovernorSites"/> — kam budovu dát (dřevorubce k lesu, služby
/// k neobslouženým domům) a kde lidé nasbírají surovinu ručně.</item>
/// <item><see cref="ConstructionClaim"/> — na co se šetří; výroba to nechá být.</item>
/// <item><see cref="GovernorStatus"/> — co dělá a proč případně stojí (pro UI).</item>
/// </list>
///
/// <para>Chybějící materiál se shání po řetězu (dům → prkna → pila → dřevo →
/// dřevorubec); nová výrobna přibude, jen když ji má kdo obsadit a čím krmit.
/// Ze zámku „na dřevorubce je potřeba dřevo" pošle lidi bez práce sbírat ručně.</para>
///
/// <para>Běží na nízké frekvenci (intervalTicks), ne každý tik (CLAUDE.md, výkon).
/// „Náhoda" je bezstavový hash (seed, tik) — deterministická a přežívá save/load
/// bez ukládání stavu RNG. Rozhoduje jen podle uloženého stavu (evidence toků,
/// rezerva), takže načtená hra pokračuje stejně jako ta, která běžela dál.</para>
/// </summary>
internal sealed class AutoBuildSystem
{
    private readonly GameContent _content;
    private readonly long _seed;
    private readonly GovernorNeeds _needs;
    private readonly BuildingCapability[] _capabilities;
    private readonly GovernorSites _sites;
    private readonly GovernorChains _chains;

    /// <summary>Stavební materiály a jídlo (viz <see cref="Materials"/>) — předpočítané.</summary>
    private readonly int[] _materials;

    /// <summary>Ofsety kandidátních míst kolem kotvy, od nejbližších — domy se lepí k sobě (organická vesnice).</summary>
    private readonly (int X, int Y)[] _searchOffsets;

    public AutoBuildSystem(GameContent content, long seed)
    {
        _content = content;
        _seed = seed;
        _needs = new GovernorNeeds(content);
        _capabilities = GovernorNeeds.Capabilities(content);
        _sites = new GovernorSites(content);
        _chains = new GovernorChains(content, _capabilities);
        _materials = Materials(content);

        int radius = content.Gameplay.AutoBuild.SearchRadius;
        var offsets = new List<(int X, int Y)>();
        for (int y = -radius; y <= radius; y++)
        {
            for (int x = -radius; x <= radius; x++)
            {
                if (x * x + y * y <= radius * radius && (x != 0 || y != 0))
                {
                    offsets.Add((x, y));
                }
            }
        }

        offsets.Sort((a, b) =>
        {
            int distance = (a.X * a.X + a.Y * a.Y).CompareTo(b.X * b.X + b.Y * b.Y);
            return distance != 0 ? distance : (a.Y, a.X).CompareTo((b.Y, b.X));
        });
        _searchOffsets = offsets.ToArray();
    }

    public void Tick(Simulation sim)
    {
        // Interval čte ze simulace, ne z dat: bonus autobuild_speed ho zkracuje,
        // takže po Vzestupu je zrychlení růstu vidět přímo na mapě.
        if (sim.TickCount % sim.AutoBuildInterval != 0)
        {
            return;
        }

        // Bez zástavby není kde růst — první budovu musí položit hráč.
        if (sim.Buildings.Length == 0)
        {
            return;
        }

        // Guvernér: slučování bloků má vlastní přepínač i technologii. Mění
        // půdorys města, takže se nemá zapnout nepozorovaně s vylepšováním.
        if (sim.AutoMerge)
        {
            TryAutoMerge(sim);
        }

        // Guvernér: vylepšování běží NEZÁVISLE na tlaku bydlení — hráč si nastavil,
        // že se o modernizaci města stará sám. Stupeň říká CO se smí vylepšovat,
        // tempo Vzestupu KOLIK toho za interval stihne.
        int upgradeBudget = sim.AutoUpgradeBudget;
        for (int i = 0; i < upgradeBudget; i++)
        {
            if (!TryAutoUpgrade(sim, i))
            {
                break; // není co (nebo na co) vylepšit
            }
        }

        // Politika „build_pace" i bonus autobuild_speed zvyšují počet akcí za
        // interval (jinak 1 — pozvolný růst).
        int budget = sim.AutoBuildBudget;

        // Nad jednu stavbu se dláždí až po dávce, stejně jako u hromadné stavby
        // hráče: jinak by první ulice sebrala místo domu, který měl stát vedle,
        // a čtvrť by z toho vyšla děravá.
        bool batched = budget > 1;
        if (batched)
        {
            sim.BeginBatchPlacement();
        }

        try
        {
            Grow(sim, budget);
        }
        finally
        {
            if (batched)
            {
                sim.EndBatchPlacement();
            }
        }
    }

    /// <summary>Jak dopadl pokus pokrýt jednu potřebu.</summary>
    private enum Outcome
    {
        /// <summary>Něco se postavilo nebo přestěhovalo.</summary>
        Built,

        /// <summary>Nic, ale guvernér ví, na co šetří, a surovina k tomu teče.</summary>
        Saving,

        /// <summary>Tuhle potřebu teď pokrýt nejde.</summary>
        Impossible,
    }

    /// <summary>
    /// Jak hluboko se guvernér ptá „a z čeho se staví tohle?" — dům → prkna →
    /// pila → dřevo → dřevorubec je hloubka tři. Dál už to v datech nevede
    /// a hlubší hledání by jen stálo čas.
    /// </summary>
    private const int MaxSupplyDepth = 4;

    /// <summary>Pod tímhle přítokem (za sekundu) se surovina nebere jako tekoucí.</summary>
    private const double FlowEpsilon = 0.005;

    /// <summary>
    /// Jak dlouho je guvernér ochotný šetřit, než přidá další výrobnu (s).
    /// Jedna pila stačí na první domy, ne na čtvrť — kdo čeká minutu na čtyři
    /// prkna, potřebuje druhou pilu, ne víc trpělivosti.
    /// </summary>
    private const double PatienceSeconds = 45;

    /// <summary>Po jak dlouhém šetření na totéž se guvernér ohlásí (≈ 3 minuty).</summary>
    private const long SlowSavingTicks = (long)(Simulation.TicksPerSecond * 180);

    /// <summary>Jak často smí přijít stejné hlášení (≈ 5 minut) — jinak by to byl spam.</summary>
    private const long ReportCooldownTicks = (long)(Simulation.TicksPerSecond * 300);

    // Stav jednoho kola. Přepisuje se každý interval a neukládá se: hlášení
    // a stav pro UI hru neovlivňují, jen ji vysvětlují.
    private GovernorStatus _roundStatus = GovernorStatus.Idle;
    private bool _claimedThisRound;
    private bool _gatheredThisRound;
    private readonly Dictionary<long, long> _lastReportTick = new();

    /// <summary>Samotné kolo růstu: až <paramref name="budget"/> staveb podle potřeb města.</summary>
    private void Grow(Simulation sim, int budget)
    {
        // Mimo cyklus: stackalloc uvnitř by při vyšším rozpočtu staveb narůstal
        // po každé otáčce (CA2014).
        Span<CityNeed> needs = stackalloc CityNeed[GovernorNeeds.MaxNeeds];
        _claimedThisRound = false;
        _gatheredThisRound = false;
        _roundStatus = GovernorStatus.Idle;
        _chains.BeginRound();

        for (int b = 0; b < budget; b++)
        {
            // Co město doopravdy potřebuje. Dřív se tady stál jen dotaz na tlak
            // bydlení — a protože značku 'autoBuild' měla jediná budova (chalupa),
            // rostla populace, kterou nikdo nekrmil ani neobsluhoval.
            int needCount = _needs.AssessAll(sim, needs);
            if (needCount == 0)
            {
                break;
            }

            // Projít potřeby od nejnaléhavější. Šetření na jednu nebrání tomu
            // pokrýt nižší — jen z přebytku nad rezervou (viz ConstructionClaim).
            bool acted = false;
            for (int n = 0; n < needCount && !acted; n++)
            {
                // Politika „housing_density": u tlaku na bydlení nejdřív povýšit
                // existující domy (víc lidí na stejném místě) místo stavby dalších.
                if (needs[n] == CityNeed.Housing && sim.PreferHousingDensity && TryDensify(sim))
                {
                    acted = true;
                    break;
                }

                acted = MeetNeed(sim, b, needs[n]) == Outcome.Built;
            }

            if (!acted)
            {
                break; // ani jedna potřeba nejde teď pokrýt → konec kola
            }
        }

        // Rezerva platí jen pro to, na co se šetřilo v TOMHLE kole. Co guvernér
        // už nechce (postavil, nebo potřeba zmizela), nesmí dál blokovat výrobu.
        if (!_claimedThisRound)
        {
            sim.Claim.Clear();
        }

        sim.GovernorStatus = _roundStatus;
    }

    /// <summary>
    /// Pokusí se pokrýt jednu potřebu: nejdřív postavit nejlepší budovu, na kterou
    /// je; když není na žádnou, zjistit proč a začít to řešit (šetřit, postavit
    /// výrobnu chybějící suroviny, přestěhovat vytěženou).
    /// </summary>
    private Outcome MeetNeed(Simulation sim, int nonce, CityNeed need)
    {
        // Deterministická „náhoda": hash (seed, tik, pořadí v dávce) — žádný stav k ukládání.
        var rng = new SplitMix64(unchecked(
            (ulong)_seed ^ ((ulong)sim.TickCount * 0x9E3779B97F4A7C15UL) ^ ((ulong)nonce * 0xBF58476D1CE4E5B9UL)));

        // Vyschlý vstup je otázka „odkud vezmu surovinu", ne „jakou budovu chci".
        // Jde přes stejnou úvahu jako chybějící materiál: když výrobny té
        // suroviny už stojí a jen hladoví, řeší se jejich vstup — ne další pila
        // vedle dvaceti hladových (přesně to se dřív dělo).
        if (need == CityNeed.Inputs)
        {
            int dried = _needs.DriedUpInput(sim);
            return dried >= 0 ? SecureResource(sim, dried, forTarget: -1, depth: 0, ref rng) : Outcome.Impossible;
        }

        if (need == CityNeed.Jobs)
        {
            return MeetJobs(sim, ref rng);
        }

        // Služby: když by stávající stačily, jen nemají zaplacenou údržbu, další
        // trh nepomůže — chybí suroviny na provoz (dřív guvernér stavěl knihovnu
        // za knihovnou, které pak stály bez údržby taky).
        if (need == CityNeed.Services && _needs.ServicesLackUpkeepOnly(sim))
        {
            int upkeep = MissingUpkeepResource(sim);
            return upkeep >= 0 ? SecureResource(sim, upkeep, forTarget: -1, depth: 0, ref rng) : Outcome.Impossible;
        }

        Span<int> ranked = stackalloc int[_content.Buildings.Count];
        int count = RankCandidates(sim, need, ranked);
        if (count == 0)
        {
            return Outcome.Impossible;
        }

        for (int i = 0; i < count; i++)
        {
            if (TryPlace(sim, ranked[i], ref rng))
            {
                return Outcome.Built;
            }
        }

        // Nic z toho teď nejde postavit. O dalším postupu rozhodne nejlepší
        // kandidát, na kterého se dá vůbec kdy našetřit.
        for (int i = 0; i < count; i++)
        {
            if (FitsInStorage(sim, ranked[i]))
            {
                return Pursue(sim, ranked[i], depth: 0, ref rng);
            }
        }

        return Outcome.Impossible;
    }

    /// <summary>
    /// Chce budovu, ale nepostavil ji. Buď na ni nemá (pak se řeší surovina),
    /// nebo na ni má a nevešla se (pak to hlásí).
    /// </summary>
    private Outcome Pursue(Simulation sim, int target, int depth, ref SplitMix64 rng)
    {
        int missing = _needs.MissingBuildMaterial(sim, target);
        if (missing >= 0)
        {
            return SecureResource(sim, missing, target, depth, ref rng);
        }

        // Na stavbu je. Buď ji drží rezerva (hráčova, nebo důležitější stavba) —
        // to je v pořádku — nebo pro ni není místo, a to má hráč vědět.
        if (sim.AutomationCanSpend(_content.Buildings[target].BuildCost, target))
        {
            SetStuck(sim, GovernorBlocker.NoSite, target, -1);
        }

        return Outcome.Impossible;
    }

    /// <summary>
    /// Zajistí surovinu, bez které nejde postavit <paramref name="forTarget"/>.
    ///
    /// <para>Tohle je ten řetěz úvah, který guvernérovi chyběl: „na dům chybí
    /// prkna → prkna nikdo nedělá → postav pilu → na pilu chybí dřevo → dřevo
    /// teče → šetři na pilu". Dřív skončil u první otázky a čekal.</para>
    /// </summary>
    /// <param name="sim">Simulace.</param>
    /// <param name="resource">Chybějící surovina.</param>
    /// <param name="forTarget">Budova, na kterou se surovina shání (na ni se šetří); −1 = žádná (vyschlý vstup).</param>
    /// <param name="depth">Jak hluboko v řetězu úvah už guvernér je.</param>
    /// <param name="rng">Deterministická náhoda kola.</param>
    private Outcome SecureResource(Simulation sim, int resource, int forTarget, int depth, ref SplitMix64 rng)
    {
        var situation = ProducerSituation(sim, resource);

        // Surovina přitéká (nebo brzy začne — výrobna už pracuje či se staví,
        // jen evidence toků to ještě nezachytila): stačí počkat a nenechat ji
        // rozebrat výrobou. Bez „brzy začne" stavěl guvernér druhý důl dřív,
        // než první stihl vytěžit první rudu. Když ale surovina teče tak pomalu,
        // že by se čekalo dlouho, řeší se úzké hrdlo.
        if (IsFlowing(sim, resource) || situation.Coming > 0)
        {
            if (forTarget >= 0 && WouldTakeLong(sim, resource, forTarget) && TryWiden(sim, resource, situation, ref rng))
            {
                return Outcome.Built;
            }

            return Save(sim, forTarget, resource);
        }

        // Výrobny jsou, jen nemají lidi. Další by stála taky — pomůže růst.
        if (situation.Unstaffed > 0)
        {
            return Save(sim, forTarget, resource, GovernorBlocker.NeedsPeople);
        }

        // Výrobny jsou, ale nemají z čeho: skutečná překážka je o patro níž.
        // Pila bez dřeva chce dřevorubce, ne další pilu. Když dřevo teče, jen
        // ho je málo — přibude dřevorubec; když neteče vůbec, hledá se proč.
        if (situation.StarvedInput >= 0 && depth + 1 < MaxSupplyDepth)
        {
            int input = situation.StarvedInput;
            if (IsFlowing(sim, input))
            {
                // Hladová je pila i chvilku mezi dvěma dávkami dřeva — dřevorubec
                // navíc má smysl, jen když by se na stavbu jinak čekalo dlouho.
                if (forTarget >= 0 && WouldTakeLong(sim, resource, forTarget) && TryAddProducer(sim, input, ref rng))
                {
                    return Outcome.Built;
                }

                return Save(sim, forTarget, input);
            }

            return SecureResource(sim, input, forTarget, depth + 1, ref rng);
        }

        // Výrobně došlo okolí: přestěhovat ji do nového lesa je zadarmo.
        if (situation.Exhausted >= 0 && TryRelocate(sim, situation.Exhausted))
        {
            return Outcome.Built;
        }

        Span<int> producers = stackalloc int[_content.Buildings.Count];
        int count = RankProducers(sim, resource, producers);
        if (count == 0)
        {
            // Hlásí se kořen řetězu: „nemá čím vyrobit obilí" řekne hráči, co
            // vyzkoumat; „nemá čím vyrobit mouku" by ho poslalo stavět mlýn.
            SetStuck(sim, GovernorBlocker.NoProducer, forTarget, _chains.MissingRoot(sim, resource));
            return Outcome.Impossible;
        }

        for (int i = 0; i < count; i++)
        {
            if (TryPlace(sim, producers[i], ref rng, servesClaim: true))
            {
                return Outcome.Built;
            }
        }

        if (depth + 1 >= MaxSupplyDepth)
        {
            SetStuck(sim, GovernorBlocker.NoProducer, forTarget, resource);
            return Outcome.Impossible;
        }

        // Žádnou výrobnu teď nepostavil. Na tu nejlepší, na kterou se dá
        // našetřit, se šetří — o patro níž.
        int bootstrap = -1;
        for (int i = 0; i < count; i++)
        {
            if (!FitsInStorage(sim, producers[i]))
            {
                continue;
            }

            // Kruh: na dřevorubce je potřeba dřevo, které neteče. Tudy ne —
            // snad jiná výrobna nepotřebuje právě tu surovinu.
            if (_needs.MissingBuildMaterial(sim, producers[i]) == resource)
            {
                bootstrap = bootstrap < 0 ? producers[i] : bootstrap;
                continue;
            }

            return Pursue(sim, producers[i], depth + 1, ref rng);
        }

        // Všechny výrobny potřebují ke stavbě právě tu surovinu, která neteče.
        // Lidé bez práce ji jdou sbírat ručně, a co nasbírají, drží se na
        // výrobnu (nesní to pila ani topení). Hráče se guvernér dovolá, jen
        // když v dosahu není co sbírat.
        if (bootstrap >= 0)
        {
            // Sbírá se jednou za kolo, i když ke stejnému zámku dojde víc potřeb.
            if (!_gatheredThisRound && _sites.GatherByHand(sim, resource, GatherBatch(sim)) > 0)
            {
                _gatheredThisRound = true;
            }

            return _gatheredThisRound
                ? Gather(sim, bootstrap, resource)
                : Save(sim, bootstrap, resource, GovernorBlocker.Bootstrap);
        }

        SetStuck(sim, GovernorBlocker.NoProducer, forTarget, resource);
        return Outcome.Impossible;
    }

    /// <summary>Na kolik lidí bez práce připadá jedna dávka ručního sběru za kolo.</summary>
    private const int PeoplePerGather = 10;

    /// <summary>Nejvíc dávek ručního sběru za kolo — nouzové řešení, ne náhrada výroby.</summary>
    private const int MaxGathersPerRound = 3;

    /// <summary>Kolik dávek nasbírají lidé bez práce (aspoň jednu — někdo se vždycky najde).</summary>
    private static int GatherBatch(Simulation sim)
    {
        double jobless = Math.Max(0, sim.Population - sim.ProductiveWorkers);
        return Math.Clamp((int)(jobless / PeoplePerGather), 1, MaxGathersPerRound);
    }

    /// <summary>Lidé sbírají ručně na první výrobnu: šetří se jako obvykle, jen stav řekne proč.</summary>
    private Outcome Gather(Simulation sim, int defIndex, int resource)
    {
        Save(sim, defIndex, resource);
        if (_roundStatus.Activity == GovernorActivity.Saving && _roundStatus.DefIndex == defIndex)
        {
            _roundStatus = new GovernorStatus(GovernorActivity.Gathering, GovernorBlocker.None, defIndex, resource);
        }

        return Outcome.Saving;
    }

    /// <summary>
    /// Šetří na budovu: zadrží její cenu před výrobou a ohlásí stav. Šetří se
    /// jen na jednu věc za kolo — tu nejnaléhavější.
    /// </summary>
    private Outcome Save(Simulation sim, int defIndex, int resource, GovernorBlocker blocker = GovernorBlocker.None)
    {
        // Vyschlý vstup nemá stavbu, na kterou by se šetřilo — jen se počká.
        if (defIndex < 0)
        {
            if (blocker != GovernorBlocker.None)
            {
                SetStuck(sim, blocker, -1, resource);
            }

            return Outcome.Saving;
        }

        if (!_claimedThisRound)
        {
            _claimedThisRound = true;
            sim.Claim.Set(defIndex, _content.Buildings[defIndex].BuildCost, sim.StorageCaps, sim.TickCount);

            // Šetří se neúměrně dlouho: surovina teče tak slabě, že to hráč má vědět.
            bool slow = sim.TickCount - sim.Claim.SinceTick > SlowSavingTicks;
            if (blocker != GovernorBlocker.None || slow)
            {
                SetStuck(sim, blocker != GovernorBlocker.None ? blocker : GovernorBlocker.SlowSupply, defIndex, resource);
            }
            else if (_roundStatus.Activity == GovernorActivity.Idle)
            {
                _roundStatus = new GovernorStatus(GovernorActivity.Saving, GovernorBlocker.None, defIndex, resource);
            }
        }

        return Outcome.Saving;
    }

    /// <summary>
    /// Postaví budovu tam, kam patří: těžbu k lesu či skále, ostatní ke
    /// zástavbě, a když se kolem zástavby nevejde, na nejbližší vhodné místo.
    /// </summary>
    /// <param name="sim">Simulace.</param>
    /// <param name="defIndex">Co postavit.</param>
    /// <param name="rng">Deterministická náhoda kola.</param>
    /// <param name="servesClaim">
    /// Staví se výrobna pro to, na co se šetří (dřevorubec, aby bylo dřevo na
    /// farmu)? Pak smí sáhnout do rezervy — je součástí téhož plánu. Bez toho
    /// rezerva na farmu držela dřevo, ze kterého by se postavil dřevorubec,
    /// a farma se šetřila půl hodiny.
    /// </param>
    private bool TryPlace(Simulation sim, int defIndex, ref SplitMix64 rng, bool servesClaim = false)
    {
        var def = _content.Buildings[defIndex];

        // Cena nezávisí na místě — hledat místo pro něco, na co není, je zbytečná práce.
        int spendAs = servesClaim && sim.Claim.IsActive ? sim.Claim.DefIndex : defIndex;
        if (!sim.CanAfford(def.BuildCost) || !sim.AutomationCanSpend(def.BuildCost, spendAs))
        {
            return false;
        }

        bool placed;
        if (def.HarvestsTerrain)
        {
            placed = _sites.TryFindHarvestSite(sim, defIndex, ignoreBuilding: -1, forMove: false, out int x, out int y)
                && sim.TryPlaceBuilding(defIndex, x, y) == PlacementResult.Ok;
        }
        else if (def.ServiceValue > 0 && sim.TryFindUnservedHome(out int homeX, out int homeY)
            && TryBuildNear(sim, defIndex, homeX, homeY))
        {
            // Služba patří tam, kam žádná jiná nedosáhne — trh vedle trhu nikomu nepomůže.
            placed = true;
        }
        else
        {
            placed = TryAtAnchors(sim, defIndex, ref rng)
                || (_sites.TryFindAnySite(sim, defIndex, out int x, out int y)
                    && sim.TryPlaceBuilding(defIndex, x, y) == PlacementResult.Ok);
        }

        if (placed)
        {
            if (sim.Claim.DefIndex == defIndex)
            {
                // Našetřeno a postaveno — rezerva splnila účel.
                sim.Claim.Clear();
                _claimedThisRound = false;
            }

            _roundStatus = new GovernorStatus(GovernorActivity.Building, GovernorBlocker.None, defIndex, -1);
        }

        return placed;
    }

    /// <summary>
    /// Víc kotev, ne jedna. Dřív si guvernér vybral jednu náhodnou budovu,
    /// a když kolem ní nebylo místo (v hustém shluku skoro vždycky), vzdal
    /// celý interval — město pak stálo na stropu bydlení s plným skladem.
    /// </summary>
    private bool TryAtAnchors(Simulation sim, int defIndex, ref SplitMix64 rng)
    {
        int buildingCount = sim.Buildings.Length;
        int tries = Math.Min(AnchorAttempts, buildingCount);
        for (int attempt = 0; attempt < tries; attempt++)
        {
            var anchor = sim.Buildings[(int)(rng.Next() % (ulong)buildingCount)];
            if (TryBuildNear(sim, defIndex, anchor.X, anchor.Y))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Surovina teče, ale pomalu: rozšíří úzké hrdlo. Kde je, napoví stav
    /// výroben — hladová pila chce dřevorubce, ne další pilu; vytěžený dřevorubec
    /// chce nový les; teprve když všechny jedou naplno, přidá se další.
    /// </summary>
    private bool TryWiden(Simulation sim, int resource, Situation situation, ref SplitMix64 rng)
    {
        if (situation.StarvedInput >= 0)
        {
            return TryAddProducer(sim, situation.StarvedInput, ref rng);
        }

        if (situation.Exhausted >= 0)
        {
            return TryRelocate(sim, situation.Exhausted);
        }

        return situation.Unstaffed == 0 && TryAddProducer(sim, resource, ref rng);
    }

    /// <summary>
    /// Lidé bez práce: postaví výrobnu. Nejdřív toho, na co se zrovna šetří
    /// (tam je práce nejvíc vidět), jinak stavebního materiálu, kterého je ve
    /// skladu relativně nejméně. Jen budovy, které guvernér smí stavět sám —
    /// tohle je jeho iniciativa, ne dokrmení hráčova řetězu.
    /// </summary>
    private Outcome MeetJobs(Simulation sim, ref SplitMix64 rng)
    {
        if (sim.Claim.IsActive)
        {
            var cost = _content.Buildings[sim.Claim.DefIndex].BuildCost;
            for (int i = 0; i < cost.Count; i++)
            {
                if (sim.GetResource(cost[i].ResourceIndex) < cost[i].Amount
                    && TryAddProducer(sim, cost[i].ResourceIndex, ref rng, ownInitiative: true))
                {
                    return Outcome.Built;
                }
            }
        }

        // Materiály seřazené podle naplnění skladu (nejprázdnější první).
        Span<int> order = stackalloc int[_materials.Length];
        _materials.CopyTo(order);
        for (int i = 1; i < order.Length; i++)
        {
            int current = order[i];
            double fill = FillOf(sim, current);
            int at = i;
            while (at > 0 && FillOf(sim, order[at - 1]) > fill)
            {
                order[at] = order[at - 1];
                at--;
            }

            order[at] = current;
        }

        foreach (int resource in order)
        {
            if (FillOf(sim, resource) < JobsFillCeiling && TryAddProducer(sim, resource, ref rng, ownInitiative: true))
            {
                return Outcome.Built;
            }
        }

        return Outcome.Impossible;
    }

    /// <summary>Plný sklad nepotřebuje další výrobnu, ani když jsou lidi bez práce.</summary>
    private const double JobsFillCeiling = 0.8;

    private static double FillOf(Simulation sim, int resource)
    {
        double cap = sim.GetStorageCap(resource);
        return cap <= 0 ? 1.0 : sim.GetResource(resource) / cap;
    }

    /// <summary>
    /// Suroviny, ze kterých guvernér staví (a jídlo) — ty, o které má smysl se
    /// starat, když jsou lidi bez práce. Víra nebo věda z knihoven by jen
    /// zaplnily sklad, který nikdo nečerpá.
    /// </summary>
    private static int[] Materials(GameContent content)
    {
        var set = new SortedSet<int> { content.Gameplay.FoodResourceIndex };
        foreach (var def in content.Buildings.All)
        {
            if (!def.AutoBuild)
            {
                continue;
            }

            foreach (var cost in def.BuildCost)
            {
                set.Add(cost.ResourceIndex);
            }
        }

        return set.Where(r => r >= 0).ToArray();
    }

    /// <summary>Čekalo by se na surovinu déle než <see cref="PatienceSeconds"/>?</summary>
    private bool WouldTakeLong(Simulation sim, int resource, int forTarget)
    {
        double needed = 0;
        var cost = _content.Buildings[forTarget].BuildCost;
        for (int i = 0; i < cost.Count; i++)
        {
            if (cost[i].ResourceIndex == resource)
            {
                needed = cost[i].Amount;
            }
        }

        double missing = needed - sim.GetResource(resource);
        var ledger = sim.Ledger;
        double rate = ledger.ProducedPerSecond(resource) - ledger.UncontrolledPerSecond(resource);
        return missing > 0 && rate > 0 && missing / rate > PatienceSeconds;
    }

    /// <summary>
    /// Přidá další výrobnu suroviny, která teče, ale pomalu — pokud ji má kdo
    /// obsadit a pokud ji má čím krmit. Prázdná druhá pila by nevyrobila nic
    /// a spolkla by materiál; hladová druhá pila by jen ujídala té první.
    /// Když chybí vstup, přidá se místo ní výrobna vstupu (o patro níž).
    /// </summary>
    /// <param name="sim">Simulace.</param>
    /// <param name="resource">Čeho má být víc.</param>
    /// <param name="rng">Deterministická náhoda kola.</param>
    /// <param name="ownInitiative">Jen budovy se značkou autoBuild (guvernérova vlastní iniciativa).</param>
    /// <param name="depth">Kolikáté patro řetězu (výrobna vstupu se hledá jen jednou).</param>
    private bool TryAddProducer(
        Simulation sim, int resource, ref SplitMix64 rng, bool ownInitiative = false, int depth = 0)
    {
        Span<int> producers = stackalloc int[_content.Buildings.Count];
        int count = RankProducers(sim, resource, producers);
        for (int i = 0; i < count; i++)
        {
            if (ownInitiative && !_content.Buildings[producers[i]].AutoBuild)
            {
                continue;
            }

            // Stejná budova už stojí bez vstupu, nebo vstup nemá přebytek →
            // další by stála vedle ní. Změřeno: 47 pil a jeden dřevorubec — lidé
            // bez práce „dostali" pilu za pilou, i když žádná neměla z čeho řezat.
            // Úzké hrdlo je vstup, takže přibude jeho výrobna.
            int shortInput = ShortInputOf(sim, producers[i]);
            if (shortInput >= 0 || HasStarvedInstance(sim, producers[i]))
            {
                int feed = shortInput >= 0 ? shortInput : StarvedInputOf(sim, producers[i]);
                if (feed >= 0 && depth == 0 && TryAddProducer(sim, feed, ref rng, ownInitiative, depth + 1))
                {
                    return true;
                }

                continue;
            }

            if (!IsPointlessNow(sim, producers[i], CityNeed.Inputs) && TryPlace(sim, producers[i], ref rng, servesClaim: true))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Přestěhuje výrobnu, které došlo okolí, do nového lesa či ke skále.</summary>
    private bool TryRelocate(Simulation sim, int buildingIndex)
    {
        int defIndex = sim.Buildings[buildingIndex].DefIndex;
        if (!_sites.TryFindHarvestSite(sim, defIndex, buildingIndex, forMove: true, out int x, out int y)
            || sim.TryMoveBuilding(buildingIndex, x, y) != PlacementResult.Ok)
        {
            return false;
        }

        _roundStatus = new GovernorStatus(GovernorActivity.Building, GovernorBlocker.None, defIndex, -1);
        return true;
    }

    /// <summary>Surovina, bez které stojí údržba některé služby (−1 = žádná).</summary>
    private int MissingUpkeepResource(Simulation sim)
    {
        var buildings = sim.Buildings;
        for (int i = 0; i < buildings.Length; i++)
        {
            var upkeep = _content.Buildings[buildings[i].DefIndex].Upkeep;
            for (int u = 0; u < upkeep.Count; u++)
            {
                int index = upkeep[u].ResourceIndex;
                if (sim.GetResource(index) - sim.Claim.AmountOf(index) < upkeep[u].Amount)
                {
                    return index;
                }
            }
        }

        return -1;
    }

    /// <summary>Jakou část chuti nové výrobny musí pokrýt dnešní přebytek vstupu.</summary>
    private const double SurplusShare = 0.5;

    /// <summary>Nad takovým naplněním skladu je vstupu dost, ať teče jakkoli.</summary>
    private const double SurplusFillFloor = 0.5;

    /// <summary>
    /// Vstup, jehož přítok neuživí ještě jednu takovou budovu (−1 = všechny uživí).
    /// Ptá se na přebytek (výroba − veškerá spotřeba), ne na zásobu: pár klád na
    /// skladě pilu nakrmí na minutu, ale ne napořád.
    /// </summary>
    private int ShortInputOf(Simulation sim, int defIndex)
    {
        if (_content.Buildings[defIndex].Recipe is not { } recipe || recipe.Inputs.Count == 0)
        {
            return -1;
        }

        double cyclesPerSecond = Simulation.TicksPerSecond / Math.Max(1, recipe.TimeTicks);
        var ledger = sim.Ledger;
        for (int j = 0; j < recipe.Inputs.Count; j++)
        {
            int index = recipe.Inputs[j].ResourceIndex;
            if (FillOf(sim, index) >= SurplusFillFloor)
            {
                continue; // sklad se plní — přebytek je, ať ho evidence vidí, nebo ne
            }

            double surplus = ledger.ProducedPerSecond(index) - ledger.ConsumedPerSecond(index);
            if (surplus < recipe.Inputs[j].Amount * cyclesPerSecond * SurplusShare)
            {
                return index;
            }
        }

        return -1;
    }

    /// <summary>Stojí některá budova tohoto druhu, protože jí chybí vstup?</summary>
    private static bool HasStarvedInstance(Simulation sim, int defIndex)
    {
        var buildings = sim.Buildings;
        for (int i = 0; i < buildings.Length; i++)
        {
            if (buildings[i].DefIndex == defIndex && buildings[i].Stall == BuildingStall.MissingInput)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Stojí některá hotová výrobna té suroviny jen proto, že nemá lidi?</summary>
    private bool HasUnstaffedProducerOf(Simulation sim, int resource)
    {
        var buildings = sim.Buildings;
        for (int i = 0; i < buildings.Length; i++)
        {
            if (buildings[i].Stall == BuildingStall.NoWorkers
                && _capabilities[buildings[i].DefIndex].Outputs.Contains(resource))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Jak na tom jsou postavené výrobny jedné suroviny.</summary>
    /// <param name="Coming">Kolik jich pracuje nebo se staví — přítok je na cestě.</param>
    /// <param name="Unstaffed">Kolik by jich pracovalo, kdyby měly lidi.</param>
    /// <param name="StarvedInput">Vstup, na který čekají (−1 = žádný).</param>
    /// <param name="Exhausted">Výrobna, které došlo okolí (−1 = žádná).</param>
    private readonly record struct Situation(int Coming, int Unstaffed, int StarvedInput, int Exhausted);

    private Situation ProducerSituation(Simulation sim, int resource)
    {
        var buildings = sim.Buildings;
        int coming = 0, unstaffed = 0, starvedInput = -1, exhausted = -1;
        for (int i = 0; i < buildings.Length; i++)
        {
            if (!_capabilities[buildings[i].DefIndex].Outputs.Contains(resource))
            {
                continue;
            }

            if (!buildings[i].IsComplete)
            {
                coming++; // staví se — za chvíli bude vyrábět
                continue;
            }

            switch (buildings[i].Stall)
            {
                case BuildingStall.None:
                    coming++;
                    break;
                case BuildingStall.NoWorkers:
                    unstaffed++;
                    break;
                case BuildingStall.MissingInput when starvedInput < 0:
                    starvedInput = StarvedInputOf(sim, buildings[i].DefIndex);
                    break;
                case BuildingStall.NoTerrain when exhausted < 0:
                    exhausted = i;
                    break;
            }
        }

        return new Situation(coming, unstaffed, starvedInput, exhausted);
    }

    /// <summary>Který vstup výrobně chybí (počítá se s rezervou, jako ve výrobě).</summary>
    private int StarvedInputOf(Simulation sim, int defIndex)
    {
        if (_content.Buildings[defIndex].Recipe is not { } recipe)
        {
            return -1;
        }

        for (int j = 0; j < recipe.Inputs.Count; j++)
        {
            int index = recipe.Inputs[j].ResourceIndex;
            if (sim.GetResource(index) - sim.Claim.AmountOf(index) < recipe.Inputs[j].Amount)
            {
                return index;
            }
        }

        return -1;
    }

    /// <summary>
    /// Teče surovina? Přítok se bere z evidence toků, očištěný o to, co snědí
    /// lidé — na to rezerva nesáhne, takže kdyby snědli celý přítok, šetřilo by
    /// se donekonečna. (Výroba, topení, údržba i nástroje rezervu respektují.)
    /// </summary>
    internal static bool IsFlowing(Simulation sim, int resource)
    {
        var ledger = sim.Ledger;
        return ledger.ProducedPerSecond(resource) - ledger.UncontrolledPerSecond(resource) > FlowEpsilon;
    }

    /// <summary>Vejde se cena do skladu? Když ne, našetřit se na ni nedá nikdy.</summary>
    private static bool FitsInStorage(Simulation sim, int defIndex)
    {
        var cost = sim.ContentRef.Buildings[defIndex].BuildCost;
        for (int i = 0; i < cost.Count; i++)
        {
            if (cost[i].Amount > sim.GetStorageCap(cost[i].ResourceIndex))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Seřadí budovy, které umí vyrobit <paramref name="resource"/>, od nejvhodnější.
    ///
    /// <para>Přednost má ta, na kterou je hned, pak ta, jejíž vstupy tečou, pak
    /// čistá před špinavou. Mezi výrobny se tu počítají i ty, které guvernér sám
    /// od sebe nestaví: když ve městě stojí huť, smí jí dostavět důl, jinak by ji
    /// hráč musel krmit ručně navždy.</para>
    /// </summary>
    private int RankProducers(Simulation sim, int resource, Span<int> ranked)
    {
        Span<int> scores = stackalloc int[_content.Buildings.Count];
        int count = 0;
        for (int defIndex = 0; defIndex < _content.Buildings.Count; defIndex++)
        {
            var capability = _capabilities[defIndex];
            if (!IsAllowedForSupply(sim, defIndex)
                || !capability.Outputs.Contains(resource)
                || capability.NeedsInputs.Contains(resource)
                || !_chains.InputsObtainable(sim, defIndex)) // mlýn bez obilí by jen stál
            {
                continue;
            }

            var def = _content.Buildings[defIndex];
            int score = 100;
            if (_needs.MissingBuildMaterial(sim, defIndex) < 0)
            {
                score += 40; // na tuhle je hned
            }

            if (InputsFlow(sim, capability))
            {
                score += 25;
            }

            if (def.AutoBuild)
            {
                score += 10; // návrhář ji guvernérovi svěřil sám
            }

            if (!def.Pollution.IsNeutral)
            {
                score -= 30;
            }

            int at = count++;
            while (at > 0 && scores[at - 1] < score)
            {
                scores[at] = scores[at - 1];
                ranked[at] = ranked[at - 1];
                at--;
            }

            scores[at] = score;
            ranked[at] = defIndex;
        }

        return count;
    }

    private static bool InputsFlow(Simulation sim, BuildingCapability capability)
    {
        foreach (int input in capability.NeedsInputs)
        {
            if (!IsFlowing(sim, input) && sim.GetResource(input) <= 0)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Smí guvernér budovu postavit, aby nakrmil řetězec? (viz <see cref="GovernorChains"/>)</summary>
    private bool IsAllowedForSupply(Simulation sim, int defIndex) => _chains.IsAllowedForSupply(sim, defIndex);

    /// <summary>
    /// Zapíše, že guvernér uvízl, a jednou za čas to řekne hráči. Stav pro UI se
    /// přepíše jen poprvé v kole — nejnaléhavější potřeba má přednost.
    /// </summary>
    private void SetStuck(Simulation sim, GovernorBlocker blocker, int defIndex, int resource)
    {
        if (_roundStatus.Activity is GovernorActivity.Idle or GovernorActivity.Saving)
        {
            _roundStatus = new GovernorStatus(GovernorActivity.Stuck, blocker, defIndex, resource);
        }

        // Lidé přibudou sami — to není důvod hráče vyrušovat.
        if (blocker == GovernorBlocker.NeedsPeople)
        {
            return;
        }

        long key = ((long)blocker << 40) ^ ((long)(defIndex + 1) << 20) ^ (resource + 1);
        if (_lastReportTick.TryGetValue(key, out long last) && sim.TickCount - last < ReportCooldownTicks)
        {
            return;
        }

        _lastReportTick[key] = sim.TickCount;
        sim.EnqueueNotification(blocker switch
        {
            GovernorBlocker.Bootstrap => new GameNotification(
                NotificationKind.GovernorStuck, "toast.governor.bootstrap", _content.Resources[resource].NameKey),
            GovernorBlocker.NoSite => new GameNotification(
                NotificationKind.GovernorStuck, "toast.governor.noSite", _content.Buildings[defIndex].NameKey),
            GovernorBlocker.SlowSupply => new GameNotification(
                NotificationKind.GovernorStuck, "toast.governor.slowSupply", _content.Resources[resource].NameKey),
            _ => new GameNotification(
                NotificationKind.GovernorStuck, "toast.governor.noProducer", _content.Resources[resource].NameKey),
        });
    }

    /// <summary>
    /// Nemělo by stavět prázdnou výrobnu? Když už teď zůstávají budovy bez lidí,
    /// další jen spolkne materiál, který chybí na domy a služby — a nevyrobí nic.
    ///
    /// <para>Změřeno: 158 prázdných budov, 444 pracovních míst na 126 obyvatel,
    /// spokojenost 0.30. Guvernér stavěl výrobny místo trhů, protože mu nikdo
    /// neřekl, že je nemá kdo obsadit.</para>
    ///
    /// <para>Dvě výjimky, obě zjištěné měřením:</para>
    /// <list type="bullet">
    /// <item><b>Hlad</b> — bez jídla město neporoste nikdy, takže na pole se staví,
    /// i když je lidí málo; jinak by se z nedostatku lidí nešlo dostat.</item>
    /// <item><b>Doplnění chybějícího materiálu</b> (<see cref="SecureResource"/>) — ta
    /// stavba je cílená na konkrétní překážku. Když se zakázala i ona, město
    /// uvázlo na 22 obyvatelích: nesmělo postavit pilu, tím pádem nebyla prkna,
    /// tím pádem ani domy.</item>
    /// </list>
    /// </summary>
    private bool IsPointlessNow(Simulation sim, int defIndex, CityNeed need)
    {
        if (need == CityNeed.Food)
        {
            // Hlad se řeší i bez volných lidí — ale jen dokud všechna pole
            // pracují. Když některé stojí bez lidí, další by stálo taky: dělníci
            // jdou na nedostatkové výrobny přednostně, takže prázdné pole znamená,
            // že lidé prostě nejsou. (Změřeno: bez tohohle 495 polí na 150 lidí.)
            return HasUnstaffedProducerOf(sim, _content.Gameplay.FoodResourceIndex);
        }

        int slots = _content.Buildings[defIndex].WorkerSlots;
        if (slots <= 0)
        {
            return false; // domy, sklady a služby lidi nepotřebují
        }

        // Strop platí jen tehdy, když už teď někde stojí budova bez lidí.
        //
        // Dřív to byl tvrdý zákaz: jakmile počet pracovních míst přerostl
        // populaci × 2,5, guvernér přestal stavět cokoli s dělníky — navždycky.
        // Zbyly mu domy (nula míst) a pole (hlad tuhle kontrolu obchází), takže
        // hráč viděl město složené z chalup a polí a žádný důl, hájovnu ani
        // pilu. Přitom plná zaměstnanost není důvod nestavět: když má město
        // lidi navíc, je další výrobna přesně to, co potřebuje.
        return sim.IdleBuildings > 0
            && sim.TotalWorkerSlots + slots > sim.Population * WorkerSlotsPerPerson;
    }

    /// <summary>
    /// Kolik pracovních míst na obyvatele je ještě rozumné. Nad tím už jen přibývá
    /// prázdných budov — trochu rezervy je v pořádku, město roste.
    /// </summary>
    private const double WorkerSlotsPerPerson = 2.5;

    /// <summary>
    /// Kolik různých kotev se zkusí, než to guvernér pro tenhle interval vzdá.
    /// Nízké číslo drží náklad malý (běží jednou za interval, ne každý tik) a
    /// zároveň spolehlivě najde okraj zástavby.
    /// </summary>
    private const int AnchorAttempts = 8;

    /// <summary>
    /// Smí guvernér tuhle budovu vůbec postavit?
    ///
    /// <para>Zakázané kategorie se tady <b>neřeší</b>. Zákaz může být jiný
    /// v každém sídle („město A těžba, město B zemědělství") a tady se ještě
    /// neví, kam se bude stavět — kontrola je až u konkrétní dlaždice
    /// v <see cref="TryBuildNear"/>.</para>
    /// </summary>
    private bool IsAllowed(Simulation sim, int defIndex)
    {
        var def = _content.Buildings[defIndex];
        return def.AutoBuild && sim.IsBuildingUnlocked(defIndex);
    }

    /// <summary>
    /// Seřadí auto-stavitelné budovy podle toho, jak dobře pokrývají danou
    /// potřebu (nejlepší první). Vrací, kolik jich do výběru vůbec patří.
    ///
    /// <para>Insertion sort nad desítkami definic, jednou za interval — proti
    /// alokaci seznamu a komparátoru je to levnější a čitelnější.</para>
    /// </summary>
    private int RankCandidates(Simulation sim, CityNeed need, Span<int> ranked)
    {
        Span<int> scores = stackalloc int[_content.Buildings.Count];
        int count = 0;
        int missingInput = need == CityNeed.Inputs ? _needs.DriedUpInput(sim) : -1;

        for (int defIndex = 0; defIndex < _content.Buildings.Count; defIndex++)
        {
            // Vyschlý vstup smí guvernér dokrmit i výrobnou, kterou by sám od
            // sebe nestavěl (důl k huti, kterou postavil hráč). Ostatní potřeby
            // jsou jeho vlastní iniciativa a drží se značky autoBuild.
            bool allowed = need == CityNeed.Inputs ? IsAllowedForSupply(sim, defIndex) : IsAllowed(sim, defIndex);
            if (!allowed)
            {
                continue;
            }

            int score = ScoreFor(defIndex, need, missingInput);
            if (score <= 0 || IsPointlessNow(sim, defIndex, need))
            {
                continue; // tuhle potřebu neřeší (nebo by ji stejně neobsloužil)
            }

            // Budova, jejíž vstup nikdo nevyrobí (pekárna, když je obilné pole
            // za výzkumem), by stála od prvního dne.
            if (!_chains.InputsObtainable(sim, defIndex))
            {
                continue;
            }

            int at = count++;
            while (at > 0 && scores[at - 1] < score)
            {
                scores[at] = scores[at - 1];
                ranked[at] = ranked[at - 1];
                at--;
            }

            scores[at] = score;
            ranked[at] = defIndex;
        }

        return count;
    }

    /// <summary>
    /// Jak dobře budova pokrývá potřebu. 0 = vůbec, takže se nepostaví — díky
    /// tomu se z guvernéra nestane stavitel všeho, co je zrovna k mání.
    /// </summary>
    private int ScoreFor(int defIndex, CityNeed need, int missingInput)
    {
        var capability = _capabilities[defIndex];
        switch (need)
        {
            case CityNeed.Food:
                return capability.ProducesFood ? 100 : 0;

            case CityNeed.Inputs:
                // Budova, která chybějící surovinu vyrábí — a sama ji nepotřebuje,
                // jinak by se postavila jen další hladový krk ve stejném řetězci.
                if (missingInput < 0 || !capability.Outputs.Contains(missingInput))
                {
                    return 0;
                }

                return capability.NeedsInputs.Contains(missingInput) ? 0 : 90;

            case CityNeed.Services:
                return capability.Services;

            case CityNeed.Housing:
                return capability.Housing;

            default:
                return 0;
        }
    }

    /// <summary>
    /// Guvernérovo vylepšení: povýší první budovu, kterou nastavená míra pokrývá
    /// a na kterou jsou suroviny. Vyšší stupeň zabírá víc kategorií i víc vylepšení
    /// za interval (viz <see cref="Simulation.AutoUpgradeCovers"/>).
    /// </summary>
    /// <param name="attempt">Pořadí vylepšení v rámci intervalu (posouvá start hledání).</param>
    private bool TryAutoUpgrade(Simulation sim, int attempt)
    {
        var buildings = sim.Buildings;
        if (buildings.Length == 0)
        {
            return false;
        }

        // Hledání nezačíná od nuly. Při vysokém tempu se vylepšuje mnohokrát za
        // interval a rozhledna od začátku pole by u města o tisících budov
        // znamenala kvadratickou práci — a pořád by narážela na tutéž budovu.
        //
        // Start se odvozuje z TIKU, ne z uloženého kurzoru: tik je součástí savu,
        // takže načtená hra pokračuje úplně stejně jako ta původní. Kurzor jako
        // pole třídy by se do savu nedostal a determinismus by se rozešel.
        int start = (int)((sim.TickCount + attempt) % buildings.Length);
        for (int step = 0; step < buildings.Length; step++)
        {
            int i = (start + step) % buildings.Length;
            var def = _content.Buildings[buildings[i].DefIndex];
            // Vylepšení nesmí sáhnout na materiál, na který guvernér šetří jinde
            // (ani na hráčovu rezervu) — jinak by si chalupy snědly prkna na pilu.
            if (def.HasUpgrade && sim.AutoUpgradeCovers(def.Category) && sim.CanUpgrade(i) == PlacementResult.Ok
                && sim.AutomationCanSpend(def.UpgradeCost))
            {
                return sim.TryUpgradeBuilding(i) == PlacementResult.Ok;
            }
        }

        return false;
    }

    /// <summary>
    /// Sloučí první blok 2×2, na který guvernér narazí. Jeden za interval —
    /// slučování je vidět a je nevratné, takže má město měnit postupně, ne
    /// přestavět se hráči pod rukama během jednoho tiku.
    /// </summary>
    private static bool TryAutoMerge(Simulation sim)
    {
        var buildings = sim.Buildings;
        for (int i = 0; i < buildings.Length; i++)
        {
            if (sim.TryFindMergeGroup(buildings[i].X, buildings[i].Y, out var group)
                && sim.CanMerge(group) == PlacementResult.Ok)
            {
                return sim.TryMerge(group.X, group.Y) == PlacementResult.Ok;
            }
        }

        return false;
    }

    /// <summary>Povýší první bydlení, které má vylepšení a hráč na něj má — hustota bez záboru místa.</summary>
    private bool TryDensify(Simulation sim)
    {
        var buildings = sim.Buildings;
        for (int i = 0; i < buildings.Length; i++)
        {
            if (_content.Buildings[buildings[i].DefIndex].HousingCapacity > 0
                && sim.CanUpgrade(i) == PlacementResult.Ok)
            {
                return sim.TryUpgradeBuilding(i) == PlacementResult.Ok;
            }
        }

        return false;
    }

    /// <summary>
    /// Kolik volných míst se ohodnotí, než se z nich vybere to nejhezčí.
    /// Ofsety jsou seřazené od nejbližších, takže tahle hrstka pokryje okolí
    /// kotvy; procházet celý kruh by jen stálo čas na horších místech.
    /// </summary>
    private const int PlacementCandidates = 20;

    private bool TryBuildNear(Simulation sim, int defIndex, int anchorX, int anchorY)
    {
        // Rezerva guvernéra: co si hráč schoval, automatika neutratí — a co si
        // guvernér odložil na důležitější stavbu, taky ne. Kontrola je před
        // hledáním místa — cena na místě nezávisí.
        if (!sim.AutomationCanSpend(_content.Buildings[defIndex].BuildCost, defIndex))
        {
            return false;
        }

        int bestX = 0, bestY = 0, bestScore = int.MinValue;
        int seen = 0;

        foreach (var (offsetX, offsetY) in _searchOffsets)
        {
            int x = anchorX + offsetX;
            int y = anchorY + offsetY;
            var result = sim.CanPlace(defIndex, x, y);
            if (result == PlacementResult.NotEnoughResources)
            {
                // Cena nezávisí na místě — další hledání nemá smysl.
                return false;
            }

            if (result != PlacementResult.Ok || CityLayout.IsReservedForStreet(x, y))
            {
                continue;
            }

            // Zákaz kategorie platí podle toho, KDE se staví: plán sídla
            // nahradí říšský. Kdo si v hornickém městě vypne bydlení, nechce
            // ho tam vidět vyrůstat, i když ho jinde staví rád.
            if (!sim.PlanAt(x, y).AllowsCategory(_content.Buildings[defIndex].Category))
            {
                continue;
            }

            int score = CityLayout.Score(sim, x, y) - (offsetX * offsetX + offsetY * offsetY);
            if (score > bestScore)
            {
                bestScore = score;
                bestX = x;
                bestY = y;
            }

            if (++seen >= PlacementCandidates)
            {
                break;
            }
        }

        return bestScore != int.MinValue
            && sim.TryPlaceBuilding(defIndex, bestX, bestY) == PlacementResult.Ok;
    }

}
