using System.Globalization;
using System.Text;

namespace CivDle.Screens;

/// <summary>
/// Rejstřík pro vyhledávání v dlouhém seznamu položek (strom výzkumu, stavební
/// katalog).
///
/// <para>Proč vlastní typ a ne prosté <c>Contains</c>: hra běží v pěti jazycích
/// a <b>hráč nepíše diakritiku</b>. „drevo" musí najít „Dřevo", „muhle" musí
/// najít „Mühle", „elektrarna" musí najít „Elektrárna". Bez odstranění
/// diakritiky by hledání sloužilo jen tomu, kdo trefí přesně — což u sto
/// čtyřiceti technologií nikdo nedělá.</para>
///
/// <para>Text se normalizuje <b>jednou při stavbě</b> rejstříku, ne při každém
/// stisku klávesy. Porovnání pak stojí jedno <c>Contains</c> a čtení z pole je
/// O(1) — což je potřeba, protože se na shodu ptá vykreslování šedesátkrát za
/// vteřinu pro každý uzel.</para>
///
/// <para>Vrstva: čistý text. Žádné UI, žádná simulace — jde otestovat bez okna.</para>
/// </summary>
public sealed class SearchIndex
{
    private readonly string[] _haystacks;
    private readonly bool[] _matches;

    /// <param name="entries">
    /// Pro každou položku jeden text, ve kterém se hledá. Klidně slepený
    /// z názvu, popisu a ID — čím víc, tím víc cest, jak věc najít.
    /// </param>
    public SearchIndex(IReadOnlyList<string> entries)
    {
        _haystacks = new string[entries.Count];
        _matches = new bool[entries.Count];

        for (int i = 0; i < entries.Count; i++)
        {
            _haystacks[i] = Normalize(entries[i]);
            _matches[i] = true;
        }

        MatchCount = entries.Count;
    }

    /// <summary>Kolik položek rejstřík zná.</summary>
    public int Count => _haystacks.Length;

    /// <summary>Filtruje se právě teď? (Prázdný dotaz = ne, všechno projde.)</summary>
    public bool IsFiltering { get; private set; }

    /// <summary>Kolik položek dotazu vyhovuje.</summary>
    public int MatchCount { get; private set; }

    /// <summary>Vyhovuje položka? Bez dotazu vyhovuje všechno.</summary>
    public bool IsMatch(int index) => _matches[index];

    /// <summary>První vyhovující položka, nebo −1. Slouží k „skoč na nález".</summary>
    public int FirstMatch()
    {
        for (int i = 0; i < _matches.Length; i++)
        {
            if (_matches[i])
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>
    /// Nastaví dotaz a přepočítá shody. Volá se při psaní, ne při kreslení.
    ///
    /// <para>Samé mezery se berou jako prázdný dotaz: hráč, kterému ujede
    /// mezerník, nesmí dostat prázdný strom.</para>
    /// </summary>
    public void Search(string? query)
    {
        string needle = Normalize(query ?? string.Empty);
        IsFiltering = needle.Length > 0;

        if (!IsFiltering)
        {
            Array.Fill(_matches, true);
            MatchCount = _matches.Length;
            return;
        }

        int found = 0;
        for (int i = 0; i < _haystacks.Length; i++)
        {
            _matches[i] = _haystacks[i].Contains(needle, StringComparison.Ordinal);
            if (_matches[i])
            {
                found++;
            }
        }

        MatchCount = found;
    }

    /// <summary>
    /// Text na malá písmena a bez diakritiky.
    ///
    /// <para>Rozklad do <see cref="NormalizationForm.FormD"/> oddělí háček od
    /// písmene jako samostatný znak a ty se pak zahodí — funguje to stejně na
    /// češtinu, němčinu, polštinu i španělštinu, aniž by kdokoli psal tabulku
    /// náhrad. Malá písmena se dělají <b>invariantně</b>: turecké „i" by jinak
    /// hráči s tureckým systémem rozbilo hledání ve zbytku hry.</para>
    /// </summary>
    public static string Normalize(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        string decomposed = text.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);

        foreach (char c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(c);
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }
}
