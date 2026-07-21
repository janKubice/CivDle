using CivDle.Core.Content;

namespace CivDle.Core.Sim;

/// <summary>
/// Výrobní tik budov: postup cyklu roste s globální obsazeností (populace vs.
/// pracovní místa); po dokončení cyklu se spotřebují vstupy a přičtou výstupy.
/// Vyschlý vstup výrobu pozastaví (stall), nikdy nic neničí — soft pressure.
/// Bez alokací: recepty se prochází indexy, ne enumerátory.
/// </summary>
internal sealed class ProductionSystem
{
    private readonly GameContent _content;

    public ProductionSystem(GameContent content)
    {
        _content = content;
    }

    public void Tick(Simulation sim)
    {
        // Jednotná obsazenost pro všechny budovy — rozdělování dělníků po budovách
        // přijde s pozdější fází (zóny/politiky), teď stačí agregát.
        float staffing = sim.TotalWorkerSlots == 0
            ? 0f
            : (float)Math.Min(1.0, Math.Floor(sim.Population) / sim.TotalWorkerSlots);
        if (staffing <= 0f)
        {
            return;
        }

        var buildings = sim.BuildingsMutable;
        var resources = sim.Resources;
        var storageCaps = sim.StorageCaps;

        for (int i = 0; i < buildings.Length; i++)
        {
            ref var building = ref buildings[i];
            var recipe = _content.Buildings[building.DefIndex].Recipe;
            if (recipe is null)
            {
                continue;
            }

            building.Progress += staffing;
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

            double productionMult = sim.Bonuses.ProductionMult;
            for (int j = 0; j < recipe.Outputs.Count; j++)
            {
                int index = recipe.Outputs[j].ResourceIndex;
                // Plný sklad výrobu nezastaví, přebytek propadá (idle konvence) —
                // motivace stavět sklady, žádný trest. Trvalý bonus Vzestupu zvedá výstup.
                resources[index] = Math.Min(resources[index] + recipe.Outputs[j].Amount * productionMult, storageCaps[index]);
            }

            building.Progress -= recipe.TimeTicks;
        }
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
