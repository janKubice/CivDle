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

    public ProductionSystem(GameContent content)
    {
        _content = content;
        _defs = content.Buildings.All.ToArray();
        _defScarce = new bool[_defs.Length];
    }

    public void Tick(Simulation sim)
    {
        var buildings = sim.BuildingsMutable;
        if (_assigned.Length < buildings.Length)
        {
            Array.Resize(ref _assigned, Math.Max(buildings.Length, _assigned.Length * 2 + 16));
        }

        sim.IdleBuildings = AssignWorkers(sim, buildings);

        var resources = sim.Resources;
        var storageCaps = sim.StorageCaps;
        float powerFactor = (float)sim.PowerFactor;

        // Počasí i bonusy jsou pro celý tik konstantní — spočítej jednou, ne u každé
        // budovy (CurrentWeatherIndex je hash, v tikové smyčce by se zbytečně opakoval).
        double productionMult = sim.Bonuses.ProductionMult * sim.BoostMultiplier * sim.WeatherProductionMult
            * sim.ElectionProductionMult * sim.ToolProductionMult;

        // Roční období sahá jen na jídlo — zima podvazuje pole, ne hutě. Index
        // jídla i násobič se čtou jednou za tik, ne u každé budovy.
        int foodIndex = _content.Gameplay.FoodResourceIndex;
        double seasonFoodMult = sim.SeasonFoodMult;
        double disconnectedMult = _content.Gameplay.Roads.DisconnectedProductionMult;
        for (int i = 0; i < buildings.Length; i++)
        {
            ref var building = ref buildings[i];
            var def = _defs[building.DefIndex];
            var recipe = def.Recipe;
            if (recipe is null || !building.IsComplete)
            {
                continue; // staveniště nevyrábí, dokud nestojí
            }

            float staffing = def.WorkerSlots > 0 ? _assigned[i] / (float)def.WorkerSlots : 1f;
            if (staffing <= 0f)
            {
                continue;
            }

            // Budovy závislé na proudu zpomalí při nedostatečném pokrytí sítě
            // (spotřebují vstupy pomaleji — žádný tvrdý trest, jen míň výkonu).
            float pace = def.NeedsPower ? staffing * powerFactor : staffing;

            // Bez napojení na silnici se zboží odváží hůř. Silnice tím přestávají
            // být dekorací a auto-stavba sítě dostává smysl.
            if (disconnectedMult < 1.0 && !sim.IsBuildingConnected(i))
            {
                pace *= (float)disconnectedMult;
            }

            building.Progress += pace;
            if (building.Progress < recipe.TimeTicks)
            {
                continue;
            }

            if (!HasInputs(resources, recipe))
            {
                // Stall: cyklus je „hotový", ale čeká na vstupy — dokončí se hned,
                // jak suroviny dotečou.
                building.Progress = recipe.TimeTicks;
                continue;
            }

            // Těžba z krajiny: pila si musí vzít strom. Když v dosahu žádný není,
            // stojí — a to je ten tlak, kvůli kterému má smysl expandovat, sázet
            // háje nebo výrobnu přesunout. Roste automaticky s městem: víc lidí
            // znamená víc obsazených výroben a tím rychlejší úbytek okolí.
            if (def.HarvestsTerrain && !sim.TryConsumeTerrain(ref building, def))
            {
                building.Progress = recipe.TimeTicks;
                continue;
            }

            for (int j = 0; j < recipe.Inputs.Count; j++)
            {
                resources[recipe.Inputs[j].ResourceIndex] -= recipe.Inputs[j].Amount;
            }

            for (int j = 0; j < recipe.Outputs.Count; j++)
            {
                int index = recipe.Outputs[j].ResourceIndex;
                sim.MarkResourceKnown(index); // první vyrobený kus surovinu odhalí v UI
                // Plný sklad výrobu nezastaví, přebytek propadá (idle konvence) —
                // motivace stavět sklady, žádný trest. Trvalý bonus Vzestupu zvedá výstup.
                double yield = recipe.Outputs[j].Amount * productionMult
                    * building.BiomeMult * building.AdjacencyMult * building.HaulMult
                    * building.PollutionMult * building.DistrictMult;
                if (index == foodIndex)
                {
                    yield *= seasonFoodMult;
                }

                resources[index] = Math.Min(resources[index] + yield, storageCaps[index]);
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
    /// <para>Dvě kola: nejdřív budovy, jejichž surovina dochází (sklad pod prahem
    /// <see cref="StaffingConfig.ScarcityThreshold"/>), pak zbytek v pořadí stavby.</para>
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

        Array.Clear(_assigned, 0, buildings.Length);
        long workforce = (long)Math.Floor(sim.Population);
        long workersLeft = AssignPass(buildings, workforce, scarceOnly: true);
        workersLeft = AssignPass(buildings, workersLeft, scarceOnly: false);

        // Kolik lidí opravdu pracuje — podle toho se opotřebovávají nástroje.
        // Tady je to zadarmo, jinde by se to muselo počítat znovu.
        sim.EmployedWorkers = workforce - workersLeft;

        int idle = 0;
        for (int i = 0; i < buildings.Length; i++)
        {
            if (_defs[buildings[i].DefIndex].WorkerSlots > 0 && _assigned[i] == 0)
            {
                idle++;
            }
        }

        return idle;
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
    /// Jedno kolo přidělování. <paramref name="scarceOnly"/> = ber jen budovy
    /// vyrábějící nedostatkovou surovinu; druhé kolo pak dosype zbytek.
    /// </summary>
    private long AssignPass(Span<BuildingInstance> buildings, long workersLeft, bool scarceOnly)
    {
        for (int i = 0; i < buildings.Length && workersLeft > 0; i++)
        {
            int defIndex = buildings[i].DefIndex;
            if (scarceOnly && !_defScarce[defIndex])
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

    private static bool HasInputs(double[] resources, Recipe recipe)
    {
        for (int j = 0; j < recipe.Inputs.Count; j++)
        {
            if (resources[recipe.Inputs[j].ResourceIndex] < recipe.Inputs[j].Amount)
            {
                return false;
            }
        }

        return true;
    }
}
