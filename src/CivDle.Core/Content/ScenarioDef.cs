namespace CivDle.Core.Content;

/// <summary>
/// Zvláštní pravidlo scénáře — něco, co se nedá napsat jako jiné číslo.
///
/// <para>Behavior-ID, ne <c>if</c> ve scénáři (viz data-driven-content.md):
/// data řeknou <b>které</b> pravidlo platí, kód ví <b>co</b> to znamená.
/// Kdyby si podmínku nesla data, byla by v JSON logika — a tu tenhle projekt
/// nemá.</para>
/// </summary>
public enum ScenarioRule
{
    /// <summary>Guvernér nestaví. Všechno je na hráči.</summary>
    NoAutoBuild,

    /// <summary>Vzestup je zakázaný — scénář je jeden běh, ne nekonečná spirála.</summary>
    NoAscension,
}

/// <summary>
/// Přebití herních čísel pro scénář. Co je <c>null</c>, zůstane, jak je
/// v <c>gameplay.json</c>.
///
/// <para>Záměrně <b>malá</b> a vyjmenovaná sada. „Scénář smí přepsat cokoli"
/// zní lákavě, ale znamená to, že se každá změna v gameplay.json musí ověřit
/// proti každému scénáři — a že se scénářem dá hra rozbít způsobem, který
/// nikdo nečekal.</para>
/// </summary>
/// <param name="StartingPopulation">S kolika lidmi se začíná.</param>
/// <param name="BaseHousingCapacity">Kolik lidí uživí základní tábor.</param>
/// <param name="PopulationGrowthPerSecond">Jak rychle přibývají lidé.</param>
/// <param name="FoodPerPersonPerSecond">Kolik jídla spotřebuje jeden člověk.</param>
public sealed record GameplayOverride(
    double? StartingPopulation = null,
    int? BaseHousingCapacity = null,
    double? PopulationGrowthPerSecond = null,
    double? FoodPerPersonPerSecond = null)
{
    /// <summary>Nic se nemění.</summary>
    public static GameplayOverride None { get; } = new();

    /// <summary>Mění tenhle scénář vůbec něco?</summary>
    public bool IsEmpty =>
        StartingPopulation is null && BaseHousingCapacity is null
        && PopulationGrowthPerSecond is null && FoodPerPersonPerSecond is null;

    /// <summary>Vyrobí nastavení hry pro scénář. Původní zůstane nedotčené.</summary>
    public GameplayConfig Apply(GameplayConfig config) => config with
    {
        StartingPopulation = StartingPopulation ?? config.StartingPopulation,
        BaseHousingCapacity = BaseHousingCapacity ?? config.BaseHousingCapacity,
        PopulationGrowthPerSecond = PopulationGrowthPerSecond ?? config.PopulationGrowthPerSecond,
        FoodPerPersonPerSecond = FoodPerPersonPerSecond ?? config.FoodPerPersonPerSecond,
    };
}

/// <summary>
/// Jeden scénář: svět s pevným seedem, jinými pravidly a <b>koncem</b>.
///
/// <para>Proč to ve hře je: idle jádro nikdy nekončí, a to je jeho síla i
/// jeho mez. Scénář je ta druhá polovina — hodina, která má zadání, výhru
/// a prohru, a po které se dá říct „hotovo". Bez ní hra nemá co nabídnout
/// hráči, který nechce stavět donekonečna.</para>
///
/// <para>Cíl i prohra jdou přes tentýž <see cref="Sim.GoalCondition"/> jako
/// úkoly a achievementy — druhý systém cílů by znamenal dvě místa, kde se
/// počítá totéž, a dřív nebo později dvě různé odpovědi.</para>
/// </summary>
/// <param name="Id">Identifikátor do dat i do lokalizace (<c>scenario.&lt;id&gt;</c>).</param>
/// <param name="Seed">Pevný seed světa — scénář musí být pro všechny stejný.</param>
/// <param name="PresetIndex">Který terénní preset; −1 = výchozí.</param>
/// <param name="Gameplay">Přebití herních čísel.</param>
/// <param name="StartingResources">Co má hráč na začátku navíc.</param>
/// <param name="Goal">Kdy je scénář vyhraný (metrika ≥ práh).</param>
/// <param name="FailBelow">
/// Kdy je scénář prohraný (metrika ≤ práh); <c>null</c> = prohrát nejde.
/// Směr je v názvu schválně: <see cref="Sim.GoalCondition"/> sám o sobě
/// znamená „≥" a tichý obrat významu by byl past.
/// </param>
/// <param name="TimeLimitSeconds">Časový limit v sekundách herního času; 0 = žádný.</param>
/// <param name="Rules">Zvláštní pravidla navíc.</param>
public sealed record ScenarioDef(
    string Id,
    long Seed,
    int PresetIndex,
    GameplayOverride Gameplay,
    IReadOnlyList<ResourceAmount> StartingResources,
    Sim.GoalCondition Goal,
    Sim.GoalCondition? FailBelow,
    double TimeLimitSeconds,
    IReadOnlyList<ScenarioRule> Rules)
{
    /// <summary>Lokalizační klíč jména.</summary>
    public string NameKey => $"scenario.{Id}";

    /// <summary>Lokalizační klíč popisu (zadání pro hráče).</summary>
    public string DescriptionKey => $"scenario.{Id}.desc";

    /// <summary>Má scénář časový limit?</summary>
    public bool HasTimeLimit => TimeLimitSeconds > 0;

    /// <summary>Platí ve scénáři tohle zvláštní pravidlo?</summary>
    public bool Has(ScenarioRule rule)
    {
        for (int i = 0; i < Rules.Count; i++)
        {
            if (Rules[i] == rule)
            {
                return true;
            }
        }

        return false;
    }
}

/// <summary>Scénáře z <c>data/scenarios.json</c>. Prázdné = režim se nenabízí.</summary>
/// <param name="Scenarios">Scénáře v pořadí z dat.</param>
public sealed record ScenarioCatalog(IReadOnlyList<ScenarioDef> Scenarios)
{
    /// <summary>Hra bez scénářů.</summary>
    public static ScenarioCatalog Empty { get; } = new(Array.Empty<ScenarioDef>());

    /// <summary>Dá se scénář vůbec vybrat?</summary>
    public bool IsEnabled => Scenarios.Count > 0;

    public int Count => Scenarios.Count;

    public ScenarioDef this[int index] => Scenarios[index];

    /// <summary>Index podle ID, nebo −1.</summary>
    public int IndexOf(string id)
    {
        for (int i = 0; i < Scenarios.Count; i++)
        {
            if (string.Equals(Scenarios[i].Id, id, StringComparison.Ordinal))
            {
                return i;
            }
        }

        return -1;
    }
}
