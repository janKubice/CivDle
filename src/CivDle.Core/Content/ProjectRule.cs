namespace CivDle.Core.Content;

/// <summary>Jeden stupeň projektu: co se do něj musí vložit.</summary>
/// <param name="Cost">Suroviny, které stupeň spotřebuje.</param>
public sealed record ProjectStage(IReadOnlyList<ResourceAmount> Cost);

/// <summary>
/// Stavba, která neroste časem, ale <b>vkládáním surovin po stupních</b>
/// (Hvězdná brána, vysušení jezera a Galaktický div Souhvězdí).
///
/// <para><b>Proč ne obyčejná stavba.</b> Pozdní hra má všech surovin
/// nadbytek; stavba za pevnou cenu a čas by ho nespotřebovala a nebyla by
/// rozhodnutím. Projekt je bezedný odběr jako velké dílo — jenže má konec
/// a je vidět na mapě: je to staveniště, jehož postup je součet vkladů,
/// a fáze spritu (<c>stages</c>) ukazují, jak daleko je.</para>
///
/// <para>Postup se drží v tom, co už simulace umí — ve zbývajících tikách
/// stavby (<see cref="UnitsPerStage"/> „tiků" na stupeň). Staveniště tak
/// kreslí renderer i inspektor stejně jako každé jiné; jen čas s ním
/// nehýbe (<c>ConstructionSystem</c> projekty přeskakuje).</para>
/// </summary>
/// <param name="Stages">Stupně v pořadí stavby.</param>
/// <param name="OnComplete">
/// Behavior-ID toho, co se stane po dokončení (<see cref="KnownEffects"/>);
/// <c>null</c> = nic zvláštního, budova prostě začne fungovat.
/// </param>
/// <param name="OnStage">
/// Behavior-ID toho, co udělá <b>každý</b> dokončený stupeň
/// (<see cref="KnownStageEffects"/>); <c>null</c> = nic.
/// </param>
/// <param name="EffectRadius">Dosah efektu stupně v dlaždicích (0 = efekt ho nemá).</param>
/// <param name="EffectBiomeIndex">Biom, ve který efekt stupně mění krajinu; −1 = žádný.</param>
public sealed record ProjectRule(
    IReadOnlyList<ProjectStage> Stages,
    string? OnComplete,
    string? OnStage = null,
    int EffectRadius = 0,
    int EffectBiomeIndex = -1)
{
    /// <summary>
    /// Efekt stupně „vysuš pás břehu": vodní dlaždice v dosahu, které sousedí
    /// se souší, se změní v mokrou zem (vysušení jezera, endgame.md B3).
    /// </summary>
    public const string DrainBand = "drain_band";

    /// <summary>Efekty stupně, které kód umí.</summary>
    public static readonly IReadOnlySet<string> KnownStageEffects = new HashSet<string>(StringComparer.Ordinal)
    {
        DrainBand,
    };

    /// <summary>Kolik „tiků stavby" připadá na stupeň — jen měřítko postupu, ne čas.</summary>
    public const int UnitsPerStage = 1000;

    /// <summary>Efekt „brána otevřena": konec první kapitoly (endgame.md, bod C).</summary>
    public const string GateOpened = "gate_opened";

    /// <summary>
    /// Efekt „galaxie sjednocena": Galaktický div Souhvězdí stojí — konec druhé
    /// kapitoly a epilog celé galaxie (svety-design.md 2.7).
    /// </summary>
    public const string GalaxyUnited = "galaxy_united";

    /// <summary>Efekty dokončení, které kód umí.</summary>
    public static readonly IReadOnlySet<string> KnownEffects = new HashSet<string>(StringComparer.Ordinal)
    {
        GateOpened,
        GalaxyUnited,
    };

    /// <summary>Celková „doba stavby" v jednotkách postupu.</summary>
    public int TotalUnits => Stages.Count * UnitsPerStage;
}
