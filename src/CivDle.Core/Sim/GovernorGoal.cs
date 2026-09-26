namespace CivDle.Core.Sim;

/// <summary>
/// Jeden cíl guvernéra: umí poznat, jak moc ho město potřebuje, a ohodnotit,
/// jak dobře ho která budova plní.
///
/// <para><b>Proč rozhraní:</b> dřív byly potřeby pevný seznam podmínek v jedné
/// metodě a pořadí dané kódem (hlad, vstupy, služby, bydlení, práce). Nová
/// starost — sklady, věda, krajina — znamenala zasahovat do celého rozhodování.
/// Teď je každý cíl malá třída; plánovač je seřadí podle naléhavosti a staví
/// shora (<see cref="GovernorGoals"/>).</para>
///
/// <para>Cíl je <b>čistá úvaha nad stavem simulace</b>: nic nemění, takže jde
/// testovat bez odtikávání a stejná odpověď poslouží i UI („Plán guvernéra").</para>
/// </summary>
internal interface IGovernorGoal
{
    /// <summary>Kterou potřebu cíl hlídá.</summary>
    CityNeed Need { get; }

    /// <summary>Jak moc to město teď potřebuje (0 = vůbec) a čeho se to týká.</summary>
    GoalAssessment Assess(Simulation sim);

    /// <summary>Jak dobře budova cíl plní (0 = vůbec). Volá se jen pro aktivní cíl.</summary>
    int Score(Simulation sim, int defIndex, in GoalAssessment goal);
}

/// <summary>Posouzení jednoho cíle v jednom kole.</summary>
/// <param name="Need">Potřeba.</param>
/// <param name="Urgency">
/// Naléhavost 1–100; 0 = cíl teď nic nechce. Stupnice je společná všem cílům:
/// hlad 100, vyschlý vstup 90, proud 80, … práce pro nezaměstnané 20.
/// </param>
/// <param name="Resource">Čeho se cíl týká (plný sklad dřeva, chybějící věda); −1 = ničeho konkrétního.</param>
/// <param name="Anchor">
/// Index budovy, u které se má stavět (vytěžený dřevorubec, budova bez proudu);
/// −1 = kdekoli.
/// </param>
internal readonly record struct GoalAssessment(CityNeed Need, int Urgency, int Resource, int Anchor)
{
    /// <summary>Cíl teď nic nechce.</summary>
    public static GoalAssessment Idle(CityNeed need) => new(need, 0, -1, -1);

    /// <summary>Chce cíl něco?</summary>
    public bool IsActive => Urgency > 0;
}

/// <summary>
/// Jedna položka plánu guvernéra pro UI: co město potřebuje, jak moc a čeho se
/// to týká. Plán se přepisuje každé kolo guvernéra a neukládá se — hru
/// neovlivňuje, jen ji vysvětluje.
/// </summary>
/// <param name="Need">Potřeba.</param>
/// <param name="Urgency">Naléhavost 1–100.</param>
/// <param name="ResourceIndex">Surovina, které se to týká; −1 = žádná.</param>
public readonly record struct GovernorAgendaItem(CityNeed Need, int Urgency, int ResourceIndex);
