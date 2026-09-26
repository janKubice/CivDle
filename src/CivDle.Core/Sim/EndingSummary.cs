namespace CivDle.Core.Sim;

/// <summary>
/// Souhrn první kapitoly pro závěrečnou sekvenci: statistiky a jména do
/// titulků.
///
/// <para><b>Jen čte.</b> Sekvence se dá pustit kdykoli znovu z menu, a proto
/// nesmí na stav hry sáhnout — souhrn je snímek, který si obrazovky předávají,
/// místo aby se každá ptala simulace po svém (a jedna z nich jednou omylem
/// něco „opravila").</para>
///
/// <para>Vrstva: jádro, čistá funkce stavu simulace. Obrazovka z něj skládá
/// texty; tady se nic neformátuje.</para>
/// </summary>
/// <param name="GameSeconds">Herní čas od založení světa.</param>
/// <param name="PeakPopulation">Nejvyšší populace, jakou město mělo.</param>
/// <param name="Buildings">Kolik budov stojí.</param>
/// <param name="Ascensions">Kolikrát město vystoupalo (měřítko).</param>
/// <param name="WavesRepelled">Kolik vln obrany přečkalo (0 bez obrany).</param>
/// <param name="AttackersDefeated">Kolik útočníků padlo před hradbami.</param>
/// <param name="ContractsCompleted">Kolik zakázek odevzdalo.</param>
/// <param name="WondersCompleted">Kolik divů a megastruktur dostavělo.</param>
/// <param name="SettlementNameIndices">Jména sídel (index do <c>SettlementNames</c>), od největšího.</param>
/// <param name="FigureIndices">Osobnosti, které městem prošly (žijící i vzpomínané), bez opakování.</param>
public sealed record EndingSummary(
    double GameSeconds,
    long PeakPopulation,
    int Buildings,
    int Ascensions,
    int WavesRepelled,
    int AttackersDefeated,
    long ContractsCompleted,
    long WondersCompleted,
    IReadOnlyList<int> SettlementNameIndices,
    IReadOnlyList<int> FigureIndices)
{
    /// <summary>Sestaví souhrn ze simulace. Nic nemění.</summary>
    public static EndingSummary Of(Simulation simulation)
    {
        var settlements = simulation.Settlements
            .OrderByDescending(s => s.BuildingCount)
            .Select(s => s.NameIndex)
            .Distinct()
            .ToList();

        var figures = simulation.Figures.Remembered
            .Concat(simulation.Figures.Living.Select(f => f.FigureIndex))
            .Distinct()
            .ToList();

        return new EndingSummary(
            simulation.TickCount / Simulation.TicksPerSecond,
            simulation.PeakPopulation,
            simulation.Buildings.Length,
            simulation.AscensionLevel,
            simulation.FrontierDefense ? simulation.Frontier.NextWave : 0,
            simulation.Frontier.Killed,
            simulation.ContractsCompleted,
            simulation.WondersCompleted,
            settlements,
            figures);
    }
}
