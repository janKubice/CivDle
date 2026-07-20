namespace CivDle.Core.Content;

/// <summary>
/// Obecný registr definic: pole položek + převod string ID → int index.
/// String ID se hledá jen při načítání a v UI; simulace pracuje s indexy
/// (viz data-driven-content.md — definice vs. instance).
/// </summary>
/// <typeparam name="T">Typ definice (record načtený z JSON).</typeparam>
public class DefRegistry<T> where T : class
{
    private readonly T[] _items;
    private readonly Dictionary<string, int> _idToIndex;
    private readonly string _kind;

    /// <param name="kind">Lidský název druhu definice do chybových hlášek („biom", „surovina"…).</param>
    /// <param name="allowEmpty">Povolí prázdný registr (volitelný obsah, např. tech tree).</param>
    public DefRegistry(IReadOnlyList<T> items, Func<T, string> idSelector, string kind, bool allowEmpty = false)
    {
        _kind = kind;
        if (items.Count == 0 && !allowEmpty)
        {
            throw new ArgumentException($"Registr ({kind}) nesmí být prázdný.", nameof(items));
        }

        _items = items.ToArray();
        _idToIndex = new Dictionary<string, int>(_items.Length);
        for (int i = 0; i < _items.Length; i++)
        {
            if (!_idToIndex.TryAdd(idSelector(_items[i]), i))
            {
                throw new ArgumentException($"Duplicitní ID ({kind}): '{idSelector(_items[i])}'.", nameof(items));
            }
        }
    }

    /// <summary>Počet definic.</summary>
    public int Count => _items.Length;

    /// <summary>Definice podle indexu — hot path simulace i renderu.</summary>
    public T this[int index] => _items[index];

    /// <summary>Všechny definice v pořadí souboru.</summary>
    public IReadOnlyList<T> All => _items;

    /// <summary>Převod ID → index; neznámé ID je chyba dat, proto výjimka.</summary>
    public int IndexOf(string id) =>
        _idToIndex.TryGetValue(id, out var index)
            ? index
            : throw new KeyNotFoundException($"Neznámé ID ({_kind}): '{id}'.");

    /// <summary>Bezpečný převod ID → index.</summary>
    public bool TryIndexOf(string id, out int index) => _idToIndex.TryGetValue(id, out index);
}
