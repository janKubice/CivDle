using CivDle.Core.Content;

namespace CivDle.Core.Sim;

/// <summary>
/// Opotřebení nástrojů: co se pracuje, to se ohladí.
///
/// <para>Proč to v hře je: nástroje se do téhle chvíle vyráběly, jednou dvakrát
/// utratily za stavbu a pak se hromadily do stropu skladu — celá jejich větev
/// byla slepá. S opotřebením mají trvalý odbyt, a protože se opotřebení řídí
/// počtem PRACUJÍCÍCH lidí, roste potřeba s městem: co bylo pro vesnici jedna
/// dílna, je pro velkoměsto celá výrobní větev.</para>
///
/// <para>Bonus za vybavenost čte simulace jako
/// <see cref="Simulation.ToolCoverage"/>; tenhle systém řeší jen tu jednu věc,
/// která mění stav — úbytek. Jedna třída, jedna zodpovědnost.</para>
/// </summary>
internal sealed class ToolsSystem
{
    private readonly GameContent _content;

    public ToolsSystem(GameContent content) => _content = content;

    public void Tick(Simulation sim)
    {
        var tools = _content.Gameplay.Tools;
        if (!tools.IsEnabled || tools.WearPerWorkerPerSecond <= 0 || sim.EmployedWorkers <= 0)
        {
            return;
        }

        double wear = sim.EmployedWorkers * tools.WearPerWorkerPerSecond / Simulation.TicksPerSecond;
        var resources = sim.Resources;
        int index = tools.ResourceIndex;

        // Do záporu se nikdy nejde: bez nástrojů se pracuje hůř (nulový bonus),
        // ale nikdo nedluží — soft pressure jako u jídla a paliva.
        // Opotřebí se jen nástroje nad rezervou guvernéra — ty odložené na
        // stavbu se nepoužívají (lidé pracují bez nich, jen s menším bonusem).
        double usable = Math.Max(0, resources[index] - sim.Claim.AmountOf(index));
        double worn = Math.Min(usable, wear);
        resources[index] -= worn;
        sim.Ledger.RecordConsumed(index, worn, ConsumptionKind.Tools);
    }
}
