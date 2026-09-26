namespace CivDle.Core.Content;

/// <summary>Jeden stupeň projektu: co se do něj musí vložit.</summary>
/// <param name="Cost">Suroviny, které stupeň spotřebuje.</param>
public sealed record ProjectStage(IReadOnlyList<ResourceAmount> Cost);

/// <summary>
/// Stavba, která neroste časem, ale <b>vkládáním surovin po stupních</b>
/// (Hvězdná brána, později vysušení jezera a Galaktický div).
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
public sealed record ProjectRule(IReadOnlyList<ProjectStage> Stages, string? OnComplete)
{
    /// <summary>Kolik „tiků stavby" připadá na stupeň — jen měřítko postupu, ne čas.</summary>
    public const int UnitsPerStage = 1000;

    /// <summary>Efekt „brána otevřena": konec první kapitoly (endgame.md, bod C).</summary>
    public const string GateOpened = "gate_opened";

    /// <summary>Efekty dokončení, které kód umí.</summary>
    public static readonly IReadOnlySet<string> KnownEffects = new HashSet<string>(StringComparer.Ordinal)
    {
        GateOpened,
    };

    /// <summary>Celková „doba stavby" v jednotkách postupu.</summary>
    public int TotalUnits => Stages.Count * UnitsPerStage;
}
