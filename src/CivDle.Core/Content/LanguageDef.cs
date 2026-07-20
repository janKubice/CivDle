namespace CivDle.Core.Content;

/// <summary>
/// Jeden jazyk z <c>data/lang/*.json</c>: identifikátor, nativní název pro menu
/// („Čeština", „English") a slovník všech řetězců hry. Loader validuje, že všechny
/// jazyky mají shodnou sadu klíčů a že pokrývají jména veškerého obsahu.
/// </summary>
public sealed record LanguageDef(
    string Id,
    string NativeName,
    IReadOnlyDictionary<string, string> Strings);
