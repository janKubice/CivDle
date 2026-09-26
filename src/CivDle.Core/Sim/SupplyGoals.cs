using CivDle.Core.Content;

namespace CivDle.Core.Sim;

/// <summary>
/// Stálý přísun suroviny, kterou utrácí hráč, ne město — věda za výzkum,
/// víra za modlitby.
///
/// <para><b>Proč to chybělo:</b> knihovnu a svatyni guvernér znal jen jako
/// „službu za 3 body", stejně jako park. Postavil tedy nejlevnější park a vědu
/// ani víru neřešil — hráč, který nechal město guvernérovi, se k výzkumu
/// nedostal nikdy.</para>
///
/// <para>Cíl se ptá: <em>jak dlouho by se čekalo na další věc, za kterou se
/// surovina utrácí?</em> Když by to trvalo déle než
/// <see cref="SupplyGoalConfig.TargetSeconds"/>, přibude budova, která ji
/// vyrábí. Když surovinu nevyrábí nikdo, je to naléhavější.</para>
///
/// <para>Co cíl <b>nedělá</b>: nestaví další knihovnu vedle stojící (bez
/// prken, bez lidí) — to je úzké hrdlo o patro níž a řeší ho dokrmení
/// řetězce. A nestaví, když se cena do skladu nevejde; to je práce pro
/// sklady (<see cref="StorageGoal"/>).</para>
/// </summary>
internal abstract class SupplyGoal : IGovernorGoal
{
    /// <summary>Nad takovým naplněním skladu surovina stačí, ať teče jakkoli.</summary>
    private const double FullEnough = 0.9;

    /// <summary>Pod tímhle přítokem (za sekundu) surovinu nikdo nevyrábí.</summary>
    private const double NoFlowBelow = 0.001;

    private readonly SupplyGoalConfig _config;
    private readonly GovernorRoles _roles;
    private readonly int _noSourceUrgency;
    private readonly int _slowUrgency;

    protected SupplyGoal(SupplyGoalConfig config, GovernorRoles roles, int noSourceUrgency, int slowUrgency)
    {
        _config = config;
        _roles = roles;
        _noSourceUrgency = noSourceUrgency;
        _slowUrgency = slowUrgency;
    }

    public abstract CityNeed Need { get; }

    /// <summary>Kolik suroviny stojí další věc, za kterou se utrácí; 0 = není za co.</summary>
    protected abstract double NextSpend(Simulation sim);

    public GoalAssessment Assess(Simulation sim)
    {
        int resource = _config.ResourceIndex;
        if (!_config.IsEnabled || sim.Population < _config.MinPopulation)
        {
            return GoalAssessment.Idle(Need);
        }

        double have = sim.GetResource(resource);
        double cap = sim.GetStorageCap(resource);
        double next = NextSpend(sim);
        if (next <= have || next > cap || have >= cap * FullEnough)
        {
            return GoalAssessment.Idle(Need);
        }

        // Stojící výrobna říká „chybí mi vstup nebo lidi", ne „chybí druhá".
        switch (Producers(sim, resource))
        {
            case ProducerState.SomeStalled:
                return GoalAssessment.Idle(Need);
            case ProducerState.None:
                return new GoalAssessment(Need, _noSourceUrgency, resource, -1);
        }

        double rate = sim.Ledger.ProducedPerSecond(resource);
        bool slow = rate <= NoFlowBelow || (next - have) / rate > _config.TargetSeconds;
        return slow ? new GoalAssessment(Need, _slowUrgency, resource, -1) : GoalAssessment.Idle(Need);
    }

    /// <summary>Čím víc suroviny za sekundu, tím líp (škola před knihovnou, když je odemčená).</summary>
    public int Score(Simulation sim, int defIndex, in GoalAssessment goal)
    {
        double rate = _roles.RateOf(defIndex, _config.ResourceIndex);
        return rate <= 0 ? 0 : Math.Max(1, (int)Math.Min(1000, rate * 100));
    }

    private enum ProducerState
    {
        None,
        AllWorking,
        SomeStalled,
    }

    private ProducerState Producers(Simulation sim, int resource)
    {
        var buildings = sim.Buildings;
        bool any = false;
        for (int i = 0; i < buildings.Length; i++)
        {
            if (_roles.RateOf(buildings[i].DefIndex, resource) <= 0)
            {
                continue;
            }

            any = true;
            if (!buildings[i].IsComplete || buildings[i].Stall != BuildingStall.None)
            {
                return ProducerState.SomeStalled;
            }
        }

        return any ? ProducerState.AllWorking : ProducerState.None;
    }
}

/// <summary>Věda na výzkum: knihovny, školy, univerzity.</summary>
internal sealed class KnowledgeGoal : SupplyGoal
{
    private readonly int _resource;

    public KnowledgeGoal(GameContent content, GovernorRoles roles)
        : base(content.Gameplay.Governor.Knowledge, roles, noSourceUrgency: 55, slowUrgency: 35)
    {
        _resource = content.Gameplay.Governor.Knowledge.ResourceIndex;
    }

    public override CityNeed Need => CityNeed.Knowledge;

    /// <summary>Cena nejlevnější technologie, na kterou má město předpoklady.</summary>
    protected override double NextSpend(Simulation sim)
    {
        int tech = sim.CheapestOpenTech(_resource);
        return tech < 0 ? 0 : sim.ResearchCostOf(tech, _resource);
    }
}

/// <summary>Víra na modlitby: svatyně, chrámy, kláštery.</summary>
internal sealed class FaithGoal : SupplyGoal
{
    private readonly FaithCatalog _faith;

    public FaithGoal(GameContent content, GovernorRoles roles)
        : base(content.Gameplay.Governor.Faith, roles, noSourceUrgency: 45, slowUrgency: 25)
    {
        _faith = content.Faith;
    }

    public override CityNeed Need => CityNeed.Faith;

    /// <summary>Cena nejlevnější modlitby v nejslabší síle — to, na co hráč sáhne první.</summary>
    protected override double NextSpend(Simulation sim)
    {
        double cheapest = 0;
        for (int i = 0; i < _faith.Prayers.Count; i++)
        {
            int cost = _faith.Prayers[i].CostAt(1);
            if (cheapest <= 0 || cost < cheapest)
            {
                cheapest = cost;
            }
        }

        return cheapest;
    }
}
