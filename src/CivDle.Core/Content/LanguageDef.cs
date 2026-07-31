namespace CivDle.Core.Content;

/// <summary>
/// Jeden jazyk z <c>data/lang/*.json</c>: identifikátor, nativní název pro menu
/// („Čeština", „English") a slovník řetězců.
///
/// <para><b>Základní jazyk</b> (první v abecedě souborů) musí být úplný — loader
/// hlídá, že pokrývá jména veškerého obsahu. Ostatní jazyky smí být <b>částečné</b>:
/// co v nich chybí, se vezme ze základního.</para>
///
/// <para>Proč zrovna takhle: dřív musel nový jazyk přinést všech ~1200 klíčů,
/// jinak hra vůbec nenaběhla. To znamenalo, že překlad buď někdo dotáhl do
/// posledního tooltipu, nebo nevznikl vůbec — a taky že každý nový řetězec ve hře
/// rozbil všechny hotové překlady. S návratem k základnímu jazyku jde jazyk přidat
/// po kusech a rozpracovaný překlad nikomu nic nerozbije.</para>
/// </summary>
public sealed record LanguageDef(
    string Id,
    string NativeName,
    IReadOnlyDictionary<string, string> Strings)
{
    /// <summary>
    /// Kolik klíčů základního jazyka tenhle jazyk pokrývá (0–1). Menu z toho
    /// umí napsat „částečný překlad" a nepřekvapit hráče anglickými větami.
    /// </summary>
    public double Coverage { get; init; } = 1.0;

    /// <summary>Je překlad úplný?</summary>
    public bool IsComplete => Coverage >= 0.999;
}
