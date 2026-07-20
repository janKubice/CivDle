namespace CivDle.Core.Content;

/// <summary>
/// Služba překladů: drží aktuální jazyk a vrací řetězce podle klíče.
/// Chybějící klíč vrací jako <c>~klíč~</c>, aby byl ve hře okamžitě vidět
/// (sady klíčů napříč jazyky hlídá už loader, tohle je poslední záchrana).
/// UI se o změně jazyka dozví přes <see cref="LanguageChanged"/> a přestaví se.
/// </summary>
public sealed class Localization
{
    private readonly DefRegistry<LanguageDef> _languages;
    private int _currentIndex;

    /// <param name="initialLanguageId">Preferovaný jazyk (z nastavení); neznámý spadne na první v registru.</param>
    public Localization(DefRegistry<LanguageDef> languages, string? initialLanguageId = null)
    {
        _languages = languages;
        if (initialLanguageId is null || !languages.TryIndexOf(initialLanguageId, out _currentIndex))
        {
            _currentIndex = 0;
        }
    }

    /// <summary>Vyvolá se po změně jazyka — obrazovky si přestaví texty.</summary>
    public event Action? LanguageChanged;

    /// <summary>Všechny dostupné jazyky (pro nabídku v nastavení).</summary>
    public IReadOnlyList<LanguageDef> Languages => _languages.All;

    /// <summary>Aktuálně zvolený jazyk.</summary>
    public LanguageDef Current => _languages[_currentIndex];

    /// <summary>Překlad podle klíče; chybějící klíč se vrátí zvýrazněný, hra nepadá.</summary>
    public string this[string key] =>
        Current.Strings.TryGetValue(key, out var value) ? value : $"~{key}~";

    /// <summary>Překlad s dosazením argumentů (<c>string.Format</c>).</summary>
    public string Format(string key, params object[] args) => string.Format(this[key], args);

    /// <summary>Přepne jazyk; neznámé ID je programátorská chyba (nabídka jde z registru).</summary>
    public void SetLanguage(string id)
    {
        int index = _languages.IndexOf(id);
        if (index == _currentIndex)
        {
            return;
        }

        _currentIndex = index;
        LanguageChanged?.Invoke();
    }
}
