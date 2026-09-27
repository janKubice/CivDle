namespace CivDle.Core.Sim;

// Pět základních potřeb, které guvernér řešil odjakživa. Úvaha „potřebuje to
// město?" zůstává v GovernorNeeds (testuje se samostatně); tady je jen obal,
// který jí dá naléhavost a ohodnotí kandidáty. Naléhavosti drží dosavadní
// pořadí: hlad > vyschlý vstup > služby > bydlení > práce.

/// <summary>Hlad: bez jídla se zastaví růst úplně, proto vždycky první.</summary>
internal sealed class FoodGoal : IGovernorGoal
{
    private readonly GovernorNeeds _needs;
    private readonly BuildingCapability[] _capabilities;
    private readonly GovernorRoles _roles;
    private readonly int _food;

    public FoodGoal(GovernorNeeds needs, BuildingCapability[] capabilities, GovernorRoles roles, int foodIndex)
    {
        _needs = needs;
        _capabilities = capabilities;
        _roles = roles;
        _food = foodIndex;
    }

    public CityNeed Need => CityNeed.Food;

    public GoalAssessment Assess(Simulation sim) =>
        _needs.IsHungry(sim) ? new GoalAssessment(Need, 100, _food, -1) : GoalAssessment.Idle(Need);

    /// <summary>
    /// Každá budova, která dělá jídlo. Při stavbě podle rolí má přednost ta
    /// výkonnější (rybárna před polem, skleník před obojím) — ale jen v pořadí:
    /// nejdřív se postaví to, na co je, takže hladové město nečeká na nástroje
    /// do rybárny, když má dřevo na pole.
    /// </summary>
    public int Score(Simulation sim, int defIndex, in GoalAssessment goal)
    {
        if (!_capabilities[defIndex].ProducesFood)
        {
            return 0;
        }

        return _roles.BuildsByRole ? 100 + (int)Math.Min(40, _roles.RateOf(defIndex, _food) * 20) : 100;
    }
}

/// <summary>Vyschlý vstup: výrobní řetězec stojí (pila bez dřeva). Řeší se dokrmením, ne další pilou.</summary>
internal sealed class InputsGoal : IGovernorGoal
{
    private readonly GovernorNeeds _needs;
    private readonly BuildingCapability[] _capabilities;

    public InputsGoal(GovernorNeeds needs, BuildingCapability[] capabilities)
    {
        _needs = needs;
        _capabilities = capabilities;
    }

    public CityNeed Need => CityNeed.Inputs;

    /// <summary>Surovina na výzkum spěchá míň než vyschlý vstup — výroba stojí, výzkum jen čeká.</summary>
    private const int ResearchUrgency = 58;

    public GoalAssessment Assess(Simulation sim)
    {
        int dried = _needs.DriedUpInput(sim);
        if (dried >= 0)
        {
            return new GoalAssessment(Need, 90, dried, -1);
        }

        int research = _needs.MissingResearchMaterial(sim);
        return research >= 0 ? new GoalAssessment(Need, ResearchUrgency, research, -1) : GoalAssessment.Idle(Need);
    }

    /// <summary>
    /// Budova, která chybějící surovinu vyrábí — a sama ji nepotřebuje, jinak
    /// by se postavil jen další hladový krk ve stejném řetězci.
    /// </summary>
    public int Score(Simulation sim, int defIndex, in GoalAssessment goal)
    {
        var capability = _capabilities[defIndex];
        if (goal.Resource < 0 || !capability.Outputs.Contains(goal.Resource))
        {
            return 0;
        }

        return capability.NeedsInputs.Contains(goal.Resource) ? 0 : 90;
    }
}

/// <summary>Služby: jejich nedostatek drží spokojenost, a ta škrtí růst.</summary>
internal sealed class ServicesGoal : IGovernorGoal
{
    private readonly GovernorNeeds _needs;
    private readonly BuildingCapability[] _capabilities;

    public ServicesGoal(GovernorNeeds needs, BuildingCapability[] capabilities)
    {
        _needs = needs;
        _capabilities = capabilities;
    }

    public CityNeed Need => CityNeed.Services;

    public GoalAssessment Assess(Simulation sim) =>
        _needs.LacksServices(sim) ? new GoalAssessment(Need, 60, -1, -1) : GoalAssessment.Idle(Need);

    public int Score(Simulation sim, int defIndex, in GoalAssessment goal) => _capabilities[defIndex].Services;
}

/// <summary>Bydlení: „luxusní" problém — bez něj město jen přestane růst, ale žije.</summary>
internal sealed class HousingGoal : IGovernorGoal
{
    private readonly GovernorNeeds _needs;
    private readonly BuildingCapability[] _capabilities;

    public HousingGoal(GovernorNeeds needs, BuildingCapability[] capabilities)
    {
        _needs = needs;
        _capabilities = capabilities;
    }

    public CityNeed Need => CityNeed.Housing;

    public GoalAssessment Assess(Simulation sim) =>
        _needs.NeedsHousing(sim) ? new GoalAssessment(Need, 50, -1, -1) : GoalAssessment.Idle(Need);

    public int Score(Simulation sim, int defIndex, in GoalAssessment goal) => _capabilities[defIndex].Housing;
}

/// <summary>
/// Lidé bez práce: nic se nezastaví, jen se nevyužije potenciál — proto
/// nejníž. Samotnou volbu výrobny dělá guvernér jinak než přes kandidáty
/// (podle nejprázdnějšího skladu stavebních materiálů).
/// </summary>
internal sealed class JobsGoal : IGovernorGoal
{
    private readonly GovernorNeeds _needs;

    public JobsGoal(GovernorNeeds needs) => _needs = needs;

    public CityNeed Need => CityNeed.Jobs;

    public GoalAssessment Assess(Simulation sim) =>
        _needs.HasJoblessPeople(sim) ? new GoalAssessment(Need, 20, -1, -1) : GoalAssessment.Idle(Need);

    public int Score(Simulation sim, int defIndex, in GoalAssessment goal) => 0;
}
