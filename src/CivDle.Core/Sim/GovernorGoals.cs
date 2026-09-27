using CivDle.Core.Content;

namespace CivDle.Core.Sim;

/// <summary>
/// Plánovač guvernéra: všechny cíle, seřazené podle toho, jak moc je město
/// zrovna potřebuje.
///
/// <para><b>Proč místo pevného pořadí:</b> dřív byl seznam potřeb daný kódem
/// a končil prací pro nezaměstnané. Sklady, věda, obnova krajiny ani proud
/// v něm nebyly, a přidat je znamenalo přepisovat rozhodování. Teď každý cíl
/// řekne svou naléhavost (<see cref="GoalAssessment.Urgency"/>) a plánovač
/// je jen seřadí. Nový cíl = nová malá třída.</para>
///
/// <para>Stavět se pak zkouší shora: když nejnaléhavější cíl teď nejde pokrýt
/// (šetří se na něj), přijde na řadu další — z přebytku nad rezervou. Tak
/// dostane knihovna šanci i ve městě, které pořád potřebuje domy.</para>
///
/// <para>Vrstva: čistá úvaha nad simulací; nic neukládá (plán se každé kolo
/// počítá znovu ze stavu hry), takže načtená hra pokračuje stejně.</para>
/// </summary>
internal sealed class GovernorGoals
{
    /// <summary>Kolik cílů může být najednou (velikost bufferu pro <see cref="AssessAll"/>).</summary>
    public const int MaxGoals = 15;

    private readonly IGovernorGoal[] _goals;
    private readonly IGovernorGoal?[] _byNeed;

    public GovernorGoals(
        GameContent content, GovernorNeeds needs, BuildingCapability[] capabilities,
        GovernorRoles roles, GovernorMemory memory)
    {
        _goals = new IGovernorGoal[]
        {
            new FoodGoal(needs, capabilities, roles, content.Gameplay.FoodResourceIndex),
            new InputsGoal(needs, capabilities),
            new PowerGoal(content, roles),
            new NetworkGoal(content, roles),
            new ProtectionGoal(content, roles),
            new LavaGoal(content, roles),
            new PlatformGoal(content),
            new StorageGoal(content, roles, memory),
            new ServicesGoal(needs, capabilities),
            new HousingGoal(needs, capabilities),
            new LandscapeGoal(content, roles),
            new KnowledgeGoal(content, roles),
            new FaithGoal(content, roles),
            new JobsGoal(needs),
        };

        _byNeed = new IGovernorGoal?[Enum.GetValues<CityNeed>().Length];
        foreach (var goal in _goals)
        {
            _byNeed[(int)goal.Need] = goal;
        }
    }

    /// <summary>Cíl, který hlídá danou potřebu.</summary>
    public IGovernorGoal For(CityNeed need) =>
        _byNeed[(int)need] ?? throw new ArgumentOutOfRangeException(nameof(need), need, "žádný cíl");

    /// <summary>
    /// Aktivní cíle od nejnaléhavějšího. Při shodě rozhoduje pořadí
    /// v <see cref="_goals"/> — deterministicky, bez náhody.
    /// </summary>
    /// <returns>Kolik cílů je aktivních.</returns>
    public int AssessAll(Simulation sim, Span<GoalAssessment> into)
    {
        int count = 0;
        foreach (var goal in _goals)
        {
            var assessment = goal.Assess(sim);
            if (!assessment.IsActive)
            {
                continue;
            }

            // Insertion sort: deset položek jednou za kolo — levnější než
            // cokoli, co alokuje, a stabilní.
            int at = count++;
            while (at > 0 && into[at - 1].Urgency < assessment.Urgency)
            {
                into[at] = into[at - 1];
                at--;
            }

            into[at] = assessment;
        }

        return count;
    }
}

/// <summary>
/// Co si guvernér pamatuje z minulého kola pro úvahy v tom dalším. Neukládá se:
/// po načtení se to zjistí znovu během jednoho kola.
/// </summary>
internal sealed class GovernorMemory
{
    /// <summary>
    /// Surovina, na kterou se v minulém kole nedalo našetřit, protože cena
    /// chtěné stavby přerostla sklad; −1 = žádná. Podle ní staví sklady
    /// (<see cref="StorageGoal"/>).
    /// </summary>
    public int BeyondStorage { get; set; } = -1;
}
