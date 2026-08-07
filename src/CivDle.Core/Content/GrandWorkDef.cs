namespace CivDle.Core.Content;

/// <summary>
/// Jeden stupeň Velkého díla: co stojí a co za to trvale dá.
/// </summary>
/// <param name="Cost">Cena stupně. Násobí se <see cref="GrandWorkConfig.CostGrowth"/> na mocninu stupně.</param>
/// <param name="Effect">Behavior-ID bonusu — tytéž efekty jako u Vzestupu.</param>
/// <param name="Magnitude">Síla bonusu za tenhle stupeň.</param>
public sealed record GrandWorkStage(
    IReadOnlyList<ResourceAmount> Cost,
    string Effect,
    double Magnitude);

/// <summary>
/// Velké dílo — <b>bezedný odběr přebytků</b>.
///
/// <para>Proč to ve hře chybělo: v pozdní fázi teče hráči deset milionů dřeva za
/// vteřinu a není kam to dát. Sklady jsou plné, budovy postavené, výzkum hotový
/// — a produkce, na které stojí celá hra, přestane mít smysl. Velké dílo je
/// místo, kam se dá sypat donekonečna: každý další stupeň je násobně dražší
/// a dá trvalý bonus.</para>
///
/// <para>Stupně se v datech <b>necyklí ručně</b>: definuje se pár vzorových
/// stupňů a ty se dokola opakují s rostoucí cenou. Bez toho by data musela mít
/// tisíc řádků, aby stačila na dlouhou hru.</para>
/// </summary>
/// <param name="Stages">Vzor stupňů, který se dokola opakuje.</param>
/// <param name="CostGrowth">Kolikrát je každý další stupeň dražší.</param>
/// <param name="UnlockAscensionLevel">Od kolikátého Vzestupu je dílo k dispozici.</param>
public sealed record GrandWorkConfig(
    IReadOnlyList<GrandWorkStage> Stages,
    double CostGrowth,
    int UnlockAscensionLevel)
{
    /// <summary>Má hra Velké dílo vůbec zapnuté? (Prázdná data = ne.)</summary>
    public bool IsEnabled => Stages.Count > 0;

    /// <summary>Vzor pro daný stupeň (cyklí se dokola).</summary>
    public GrandWorkStage StageAt(int stage) => Stages[stage % Stages.Count];

    /// <summary>
    /// Cena stupně: základ ze vzoru × růst na mocninu stupně. Roste geometricky,
    /// takže i „bezedné" dílo má u každého dalšího stupně poctivou cenu.
    /// </summary>
    public double CostOf(int stage, int resourceIndex)
    {
        var pattern = StageAt(stage);
        for (int i = 0; i < pattern.Cost.Count; i++)
        {
            if (pattern.Cost[i].ResourceIndex == resourceIndex)
            {
                return pattern.Cost[i].Amount * Math.Pow(CostGrowth, stage);
            }
        }

        return 0;
    }
}
