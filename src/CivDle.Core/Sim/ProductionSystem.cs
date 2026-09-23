using CivDle.Core.Content;

namespace CivDle.Core.Sim;

/// <summary>
/// Výrobní tik budov: nejdřív se mezi budovy rozdělí dělníci, pak každá posune
/// svůj cyklus úměrně tomu, jak je obsazená; po dokončení cyklu se spotřebují
/// vstupy a přičtou výstupy.
/// Vyschlý vstup výrobu pozastaví (stall), nikdy nic neničí — soft pressure.
/// Bez alokací v tikové smyčce: pomocné pole se drží mezi tiky a roste jen
/// s městem, recepty se prochází indexy, ne enumerátory.
/// </summary>
internal sealed class ProductionSystem
{
    private readonly GameContent _content;

    /// <summary>
    /// Definice budov jako pole. V tikové smyčce se sahá na definici u každé
    /// budovy; přes registr to znamená indexer navíc, přes pole je to jeden
    /// přístup do paměti. Obsah je neměnný, takže se dá vytáhnout jednou.
    /// </summary>
    private readonly BuildingDef[] _defs;

    /// <summary>
    /// Vyrábí definice na daném indexu něco nedostatkového? Přepočítá se jednou
    /// za tik (desítky definic) místo u každé budovy zvlášť (statisíce) — na
    /// velkém městě je to rozdíl mezi procházením receptů 250 000× a 53×.
    /// </summary>
    private readonly bool[] _defScarce;

    /// <summary>
    /// Kolik dělníků dostala budova na daném indexu (drží se mezi tiky, aby se
    /// v hot path nealokovalo). Roste jen když přibude budov.
    /// </summary>
    private int[] _assigned = Array.Empty<int>();

    /// <summary>
    /// Proč budova zrovna nemůže dokončit cyklus, i kdyby měla lidi
    /// (<see cref="BuildingStall.None"/> = může). Počítá se na začátku tiku
    /// před rozdělením dělníků; drží se mezi tiky kvůli hot path.
    /// </summary>
    private BuildingStall[] _blocked = Array.Empty<BuildingStall>();

    public ProductionSystem(GameContent content)
    {
        _content = content;
        _defs = content.Buildings.All.ToArray();
        _defScarce = new bool[_defs.Length];
    }

    /// <summary>
    /// Změnil se od minulého tiku stav některé elektrárny?
    ///
    /// <para>Simulace si podle toho přepočítá rozvod. Bez toho by síť o vyhaslé
    /// elektrárně nevěděla: přepočítává se jen při změně zástavby, a „došlo
    /// palivo" žádná změna zástavby není.</para>
    /// </summary>
    public bool PowerPlantsChanged { get; private set; }

    /// <summary>Simulace si příznak vyzvedne a zahodí.</summary>
    public bool TakePowerPlantsChanged()
    {
        bool changed = PowerPlantsChanged;
        PowerPlantsChanged = false;
        return changed;
    }

    /// <summary>
    /// Zapíše stav budovy — a když jde o elektrárnu, upozorní na to síť.
    ///
    /// <para>Kontrola stojí jedno porovnání s nulou a je nepravdivá skoro
    /// u každé budovy ve městě, takže tikovou smyčku nezdrží.</para>
    /// </summary>
    private void SetStall(ref BuildingInstance building, BuildingDef def, BuildingStall stall)
    {
        if (def.PowerSupply > 0 && building.Stall != stall)
        {
            PowerPlantsChanged = true;
        }

        building.Stall = stall;
    }

    public void Tick(Simulation sim)
    {
        var buildings = sim.BuildingsMutable;
        if (_assigned.Length < buildings.Length)
        {
            Array.Resize(ref _assigned, Math.Max(buildings.Length, _assigned.Length * 2 + 16));
            Array.Resize(ref _blocked, _assigned.Length);
        }

        sim.IdleBuildings = AssignWorkers(sim, buildings);

        var resources = sim.Resources;
        var storageCaps = sim.StorageCaps;
        // Prostorový rozvod: proud se ptá u budovy, ne u říše. Globální číslo
        // se použije jen tehdy, když obsah dosah nedefinuje.
        bool spatialPower = _content.Gameplay.Power.IsEnabled;
        float powerFactor = (float)sim.PowerFactor;

        // Počasí i bonusy jsou pro celý tik konstantní — spočítej jednou, ne u každé
        // budovy (CurrentWeatherIndex je hash, v tikové smyčce by se zbytečně opakoval).
        double productionMult = sim.Bonuses.ProductionMult * sim.BoostMultiplier * sim.WeatherProductionMult
            * sim.ElectionProductionMult * sim.ToolProductionMult;

        // Roční období sahá jen na jídlo — zima podvazuje pole, ne hutě. Index
        // jídla i násobič se čtou jednou za tik, ne u každé budovy.
        int foodIndex = _content.Gameplay.FoodResourceIndex;
        double seasonFoodMult = sim.SeasonFoodMult * sim.RainFoodMult; // déšť z modlitby jde na pole
        double disconnectedMult = _content.Gameplay.Roads.DisconnectedProductionMult;
        for (int i = 0; i < buildings.Length; i++)
        {
            ref var building = ref buildings[i];
            var def = _defs[building.DefIndex];

            // Poškození se hlásí PŘED receptem: vyřazený je i dům a sklad,
            // které nic nevyrábějí, a inspektor to má ukázat u obojího.
            // Mimo režim obrany je ten údaj navždy nula, takže běžnou hru
            // to nestojí nic.
            if (building.DisabledTicks > 0)
            {
                SetStall(ref building, def, BuildingStall.Damaged);
                continue;
            }

            // Rozestavěnost se hlásí PŘED receptem. Div světa ani bašta recept
            // nemají, takže se dřív ohlásily jako „v pořádku" už ve chvíli, kdy
            // z nich stálo lešení — a kdo se ptal „stojí už to?", dostal ano.
            if (!building.IsComplete)
            {
                SetStall(ref building, def, BuildingStall.UnderConstruction);
                continue; // staveniště nevyrábí, dokud nestojí
            }

            // A obsazenost taky. Elektrárna, přístav nebo bašta recept nemají,
            // ale bez lidí nefungují — a dokud to nikdo neřekl, tvářily se, že
            // jedou. Jaderná elektrárna tak sypala do sítě plný výkon s prázdnou
            // směnou i prázdným zásobníkem paliva.
            float staffing = def.WorkerSlots > 0 ? _assigned[i] / (float)def.WorkerSlots : 1f;
            if (staffing <= 0f)
            {
                // Nejčastější tichá příčina „proč se nic neděje": budovu nemá kdo
                // obsluhovat. Bez tohohle příznaku nedostala ani červený roh,
                // protože se nikdy nedopracovala na konec cyklu.
                //
                // Jenže budova, která stojí na vstupu nebo plném skladu, dostává
                // lidi až nakonec — a hlásit u ní „chybí lidé" by hráče poslalo
                // stavět domy místo dřevorubce. Hlásí se skutečná příčina.
                SetStall(ref building, def, _blocked[i] != BuildingStall.None ? _blocked[i] : BuildingStall.NoWorkers);
                continue;
            }

            var recipe = def.Recipe;
            if (recipe is null)
            {
                SetStall(ref building, def, BuildingStall.None); // budova bez receptu nemá co stát
                continue;
            }

            // Budovy závislé na proudu zpomalí při nedostatečném pokrytí sítě
            // (spotřebují vstupy pomaleji — žádný tvrdý trest, jen míň výkonu).
            float pace = staffing;
            if (def.NeedsPower)
            {
                pace *= spatialPower ? (float)sim.PowerAt(building.X, building.Y) : powerFactor;
            }

            // Bez napojení na silnici se zboží odváží hůř. Silnice tím přestávají
            // být dekorací a auto-stavba sítě dostává smysl.
            //
            // Podmořské budovy z toho ven: k dómu na dně žádná silnice nevede
            // a nikdy nepovede — zásobuje ho přístav loděmi. Bez téhle výjimky
            // by celá vrstva jela natrvalo na šedesát procent za něco, s čím
            // hráč nemůže nic udělat.
            if (disconnectedMult < 1.0 && !def.IsSubsea && !sim.IsBuildingConnected(i))
            {
                pace *= (float)disconnectedMult;
            }

            building.Progress += pace;
            if (building.Progress < recipe.TimeTicks)
            {
                continue;
            }

            // Plný sklad zastaví jen výrobnu, která něco SPOTŘEBOVÁVÁ. Těžba
            // z ničeho smí dál propadat (idle konvence, motivace stavět sklady),
            // ale pila, která pálí dřevo na prkna do plného skladu, jen ničí
            // surovinu, kterou město potřebuje jinde. Změřeno: knihovny žraly
            // prkna na vědu, která padala do koše, pily žraly dřevo na prkna,
            // která padala taky — a guvernér hodinu nesehnal pět dřev na dům.
            if (IsBlockedByFullStorage(def, recipe, resources, storageCaps))
            {
                building.Progress = recipe.TimeTicks;
                SetStall(ref building, def, BuildingStall.OutputFull);
                continue;
            }

            if (!HasInputs(resources, sim.Claim.Amounts, recipe))
            {
                // Stall: cyklus je „hotový", ale čeká na vstupy — dokončí se hned,
                // jak suroviny dotečou.
                building.Progress = recipe.TimeTicks;
                SetStall(ref building, def, BuildingStall.MissingInput);
                continue;
            }

            // Těžba z krajiny: pila si musí vzít strom. Když v dosahu žádný není,
            // stojí — a to je ten tlak, kvůli kterému má smysl expandovat, sázet
            // háje nebo výrobnu přesunout. Roste automaticky s městem: víc lidí
            // znamená víc obsazených výroben a tím rychlejší úbytek okolí.
            if (def.HarvestsTerrain && !sim.TryConsumeTerrain(ref building, def))
            {
                building.Progress = recipe.TimeTicks;
                SetStall(ref building, def, BuildingStall.NoTerrain);
                continue;
            }

            SetStall(ref building, def, BuildingStall.None);

            for (int j = 0; j < recipe.Inputs.Count; j++)
            {
                resources[recipe.Inputs[j].ResourceIndex] -= recipe.Inputs[j].Amount;
                sim.Ledger.RecordConsumed(recipe.Inputs[j].ResourceIndex, recipe.Inputs[j].Amount);
            }

            for (int j = 0; j < recipe.Outputs.Count; j++)
            {
                int index = recipe.Outputs[j].ResourceIndex;
                sim.MarkResourceKnown(index); // první vyrobený kus surovinu odhalí v UI
                // Plný sklad výrobu nezastaví, přebytek propadá (idle konvence) —
                // motivace stavět sklady, žádný trest. Trvalý bonus Vzestupu zvedá výstup.
                double yield = recipe.Outputs[j].Amount * productionMult
                    * building.BiomeMult * building.AdjacencyMult * building.HaulMult
                    * building.PollutionMult * building.DistrictMult * building.MilestoneMult;
                if (index == foodIndex)
                {
                    yield *= seasonFoodMult;
                }

                // Technologie cílené na jednu surovinu („+5 % dřeva") — drobnosti,
                // kterými je strom plný, se musí projevit i ve výrobě.
                yield *= sim.ResourceProductionMult(index);

                // Dozvuk volby z události (ignorovaná povodeň, stávka…) — pole
                // o velikosti počtu surovin, takže jen sáhnutí do paměti.
                yield *= sim.EventEffects.ProductionMult(index);

                // Účtuje se zvlášť, co se do skladu VEŠLO a co propadlo. Plný
                // sklad výrobu nezastaví, přebytek mizí — je to záměr, ale bez
                // téhle dvojice čísel hráč nemá jak zjistit, že o něj přichází.
                double before = resources[index];
                resources[index] = Math.Min(before + yield, storageCaps[index]);
                double stored = resources[index] - before;
                sim.Ledger.RecordProduced(index, stored);
                sim.Ledger.RecordWasted(index, yield - stored);
            }

            // Ohlas dokončený cyklus renderu — bez tohohle je město opticky mrtvé,
            // i když ve skutečnosti pracuje. Fronta má pevnou kapacitu, takže při
            // statisících budov se ohlásí jen vzorek; to je záměr, ne chyba.
            if (recipe.Outputs.Count > 0)
            {
                sim.ReportVisual(VisualEventKind.Produced, building.X, building.Y, recipe.Outputs[0].ResourceIndex);
            }

            building.Progress -= recipe.TimeTicks;
        }
    }

    /// <summary>
    /// Rozdělí lidi mezi budovy a vrátí, kolik budov zůstalo úplně bez dělníka.
    ///
    /// <para>Tři kola: nejdřív budovy, které můžou pracovat a jejichž surovina
    /// dochází (sklad pod prahem <see cref="StaffingConfig.ScarcityThreshold"/>),
    /// pak ostatní, které můžou pracovat, a nakonec ty, které by stejně stály
    /// (bez vstupu, s plným skladem, rozestavěné).</para>
    ///
    /// <para>Proč takhle: dřív se obsazenost počítala globálně jako populace ÷
    /// všechna pracovní místa, takže každá další výrobna zpomalila i všechny
    /// předchozí — stavět se hráči vyloženě nevyplácelo (nález balančního
    /// nástroje). Samotné „nejstarší mají přednost" ale zase umí umořit celou
    /// větev: staré farmy si drží všechny lidi, nová pila nedostane nikoho
    /// a dřevo přestane téct. Přednost pro nedostatkové suroviny řeší obojí —
    /// město se samo přeskupí tam, kde chybí, což je přesně to, co by hráč dělal
    /// ručně, a v idle hře to ručně dělat nechce.</para>
    /// </summary>
    private int AssignWorkers(Simulation sim, Span<BuildingInstance> buildings)
    {
        RefreshScarcity(sim);
        ClassifyBlocked(sim, buildings);

        Array.Clear(_assigned, 0, buildings.Length);
        long workforce = (long)Math.Floor(sim.Population);
        long workersLeft = AssignPass(buildings, workforce, StaffingPass.ScarceReady);
        workersLeft = AssignPass(buildings, workersLeft, StaffingPass.Ready);
        sim.ProductiveWorkers = workforce - workersLeft; // do stojících budov jde až zbytek
        workersLeft = AssignPass(buildings, workersLeft, StaffingPass.Blocked);

        // Kolik lidí opravdu pracuje — podle toho se opotřebovávají nástroje.
        // Tady je to zadarmo, jinde by se to muselo počítat znovu.
        sim.EmployedWorkers = workforce - workersLeft;

        // „Prázdná" je jen budova, která by pracovala, kdyby měla lidi. Pila bez
        // dřeva není prázdná, je hladová — a guvernér podle tohohle čísla
        // usuzuje, jestli smí stavět další výrobny. Kdyby se sem počítala,
        // přestal by stavět právě toho dřevorubce, který by ji nakrmil.
        int idle = 0;
        for (int i = 0; i < buildings.Length; i++)
        {
            if (_defs[buildings[i].DefIndex].WorkerSlots > 0 && _assigned[i] == 0
                && _blocked[i] == BuildingStall.None)
            {
                idle++;
            }
        }

        return idle;
    }

    /// <summary>Kolo přidělování dělníků.</summary>
    private enum StaffingPass
    {
        /// <summary>Budovy, které můžou pracovat a vyrábějí nedostatkovou surovinu.</summary>
        ScarceReady,

        /// <summary>Ostatní budovy, které můžou pracovat.</summary>
        Ready,

        /// <summary>Budovy, které by stejně stály (bez vstupu, plný sklad, staveniště) — jen zbytek.</summary>
        Blocked,
    }

    /// <summary>
    /// Jak často dostane budova, které došlo okolí, šanci zkusit to znovu.
    /// Les dorůstá — kdyby se na ni už nikdy nedostali lidé, nikdy by to nezjistila.
    /// </summary>
    private const int TerrainRetryTicks = 50;

    /// <summary>
    /// Zjistí u každé budovy, jestli by s lidmi vůbec mohla dokončit cyklus.
    ///
    /// <para>Proč: dřív šli dělníci podle pořadí stavby a nedostatkovosti — a pila
    /// bez dřeva (prkna jsou „nedostatková") dostala lidi přednostně, zatímco
    /// dřevorubec, který by ji nakrmil, stál prázdný. Při málo lidech to byl
    /// zámek navždy. Teď jdou lidé nejdřív tam, kde opravdu něco vznikne.</para>
    /// </summary>
    private void ClassifyBlocked(Simulation sim, Span<BuildingInstance> buildings)
    {
        var resources = sim.Resources;
        var caps = sim.StorageCaps;
        long tick = sim.TickCount;

        for (int i = 0; i < buildings.Length; i++)
        {
            ref readonly var building = ref buildings[i];
            var def = _defs[building.DefIndex];

            if (building.DisabledTicks > 0)
            {
                _blocked[i] = BuildingStall.Damaged;
                continue;
            }

            if (!building.IsComplete)
            {
                _blocked[i] = BuildingStall.UnderConstruction;
                continue;
            }

            if (def.Recipe is not { } recipe)
            {
                _blocked[i] = BuildingStall.None;
                continue;
            }

            if (IsBlockedByFullStorage(def, recipe, resources, caps))
            {
                _blocked[i] = BuildingStall.OutputFull;
            }
            else if (!HasInputs(resources, sim.Claim.Amounts, recipe))
            {
                _blocked[i] = BuildingStall.MissingInput;
            }
            else if (def.HarvestsTerrain && building.OutOfResources && (tick + i) % TerrainRetryTicks != 0)
            {
                _blocked[i] = BuildingStall.NoTerrain;
            }
            else
            {
                _blocked[i] = BuildingStall.None;
            }
        }
    }

    /// <summary>
    /// Přepočítá, které definice vyrábí zrovna nedostatkovou surovinu. Dělá se to
    /// nad definicemi (desítky), ne nad budovami (statisíce) — výsledek je pro
    /// všechny budovy téhož typu stejný.
    /// </summary>
    private void RefreshScarcity(Simulation sim)
    {
        double threshold = _content.Gameplay.Staffing.ScarcityThreshold;
        var resources = sim.Resources;
        var caps = sim.StorageCaps;

        for (int d = 0; d < _defs.Length; d++)
        {
            _defScarce[d] = false;
            if (_defs[d].Recipe is not { } recipe)
            {
                continue;
            }

            for (int i = 0; i < recipe.Outputs.Count; i++)
            {
                int index = recipe.Outputs[i].ResourceIndex;
                double cap = caps[index];
                if (cap <= 0 || resources[index] < cap * threshold)
                {
                    _defScarce[d] = true;
                    break;
                }
            }
        }
    }

    /// <summary>
    /// Jedno kolo přidělování. Kola jdou od budov, kde práce opravdu něco
    /// vyrobí a je to potřeba, po ty, které by stejně stály — ty dostanou
    /// jen to, co zbude.
    /// </summary>
    private long AssignPass(Span<BuildingInstance> buildings, long workersLeft, StaffingPass pass)
    {
        for (int i = 0; i < buildings.Length && workersLeft > 0; i++)
        {
            int defIndex = buildings[i].DefIndex;
            bool ready = _blocked[i] == BuildingStall.None;
            bool take = pass switch
            {
                StaffingPass.ScarceReady => ready && _defScarce[defIndex],
                StaffingPass.Ready => ready,
                _ => !ready,
            };

            if (!take)
            {
                continue;
            }

            int free = _defs[defIndex].WorkerSlots - _assigned[i];
            if (free <= 0)
            {
                continue;
            }

            int taken = (int)Math.Min(free, workersLeft);
            _assigned[i] += taken;
            workersLeft -= taken;
        }

        return workersLeft;
    }

    /// <summary>
    /// Nemá výrobna kam dát, co vyrobí? Platí jen pro recepty se vstupy
    /// a jen když je plný <b>každý</b> výstup — klášter, který dělá víru
    /// i vědu, pracuje dál, dokud se aspoň jedno z toho vejde.
    ///
    /// <para>Elektrárny výjimka: jejich skutečný výstup je proud, ne surovina
    /// v receptu. Jaderná elektrárna s plným skladem vědy musí dál svítit.</para>
    /// </summary>
    internal static bool IsBlockedByFullStorage(
        BuildingDef def, Recipe recipe, double[] resources, double[] storageCaps)
    {
        if (recipe.Inputs.Count == 0 || recipe.Outputs.Count == 0 || def.PowerSupply > 0)
        {
            return false;
        }

        for (int j = 0; j < recipe.Outputs.Count; j++)
        {
            int index = recipe.Outputs[j].ResourceIndex;
            if (resources[index] < storageCaps[index] - FullEpsilon)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Tolerance „plného" skladu — výroba ořezává na strop přesně, jen pojistka proti zaokrouhlení.</summary>
    private const double FullEpsilon = 1e-6;

    /// <summary>
    /// Má výrobna z čeho vyrábět? Počítá se jen to, co je <b>nad</b> rezervou
    /// guvernéra — materiál odložený na stavbu pila nesmí rozřezat
    /// (viz <see cref="ConstructionClaim"/>).
    /// </summary>
    private static bool HasInputs(double[] resources, double[] claimed, Recipe recipe)
    {
        for (int j = 0; j < recipe.Inputs.Count; j++)
        {
            int index = recipe.Inputs[j].ResourceIndex;
            if (resources[index] - claimed[index] < recipe.Inputs[j].Amount)
            {
                return false;
            }
        }

        return true;
    }
}
