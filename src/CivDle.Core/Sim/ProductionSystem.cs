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
    /// Kolik dělníků dostala budova na daném indexu (drží se mezi tiky, aby se
    /// v hot path nealokovalo). Roste jen když přibude budov.
    /// </summary>
    private int[] _assigned = Array.Empty<int>();

    public ProductionSystem(GameContent content)
    {
        _content = content;
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
        double productionMult = sim.Bonuses.ProductionMult * sim.BoostMultiplier * sim.WeatherProductionMult;
        double disconnectedMult = _content.Gameplay.Roads.DisconnectedProductionMult;
        for (int i = 0; i < buildings.Length; i++)
        {
            ref var building = ref buildings[i];
            var def = _content.Buildings[building.DefIndex];
            var recipe = def.Recipe;
            if (recipe is null)
            {
                continue;
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
                resources[index] = Math.Min(resources[index] + recipe.Outputs[j].Amount * productionMult * building.BiomeMult, storageCaps[index]);
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
        double threshold = _content.Gameplay.Staffing.ScarcityThreshold;

        Array.Clear(_assigned, 0, buildings.Length);
        long workersLeft = AssignPass(sim, buildings, (long)Math.Floor(sim.Population), threshold, scarceOnly: true);
        AssignPass(sim, buildings, workersLeft, threshold, scarceOnly: false);

        int idle = 0;
        for (int i = 0; i < buildings.Length; i++)
        {
            if (_content.Buildings[buildings[i].DefIndex].WorkerSlots > 0 && _assigned[i] == 0)
            {
                idle++;
            }
        }

        return idle;
    }

    /// <summary>
    /// Jedno kolo přidělování. <paramref name="scarceOnly"/> = ber jen budovy
    /// vyrábějící nedostatkovou surovinu; druhé kolo pak dosype zbytek.
    /// </summary>
    private long AssignPass(
        Simulation sim, Span<BuildingInstance> buildings, long workersLeft, double threshold, bool scarceOnly)
    {
        for (int i = 0; i < buildings.Length && workersLeft > 0; i++)
        {
            var def = _content.Buildings[buildings[i].DefIndex];
            int free = def.WorkerSlots - _assigned[i];
            if (free <= 0)
            {
                continue;
            }

            if (scarceOnly && !ProducesSomethingScarce(sim, def, threshold))
            {
                continue;
            }

            int taken = (int)Math.Min(free, workersLeft);
            _assigned[i] += taken;
            workersLeft -= taken;
        }

        return workersLeft;
    }

    /// <summary>Vyrábí budova něco, čeho má město míň než <paramref name="threshold"/> skladu?</summary>
    private static bool ProducesSomethingScarce(Simulation sim, BuildingDef def, double threshold)
    {
        if (def.Recipe is not { } recipe)
        {
            return false;
        }

        var resources = sim.Resources;
        var caps = sim.StorageCaps;
        for (int i = 0; i < recipe.Outputs.Count; i++)
        {
            int index = recipe.Outputs[i].ResourceIndex;
            double cap = caps[index];
            if (cap <= 0 || resources[index] < cap * threshold)
            {
                return true;
            }
        }

        return false;
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
