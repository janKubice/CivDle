namespace CivDle.Core.Content;

/// <summary>
/// Registr biomů: pole definic + převod string ID → int index.
/// String se hledá jen jednou při načítání; simulace i mapa pracují výhradně
/// s indexy (na mapě jako <c>byte</c>, proto limit 256 biomů).
/// </summary>
public sealed class BiomeRegistry
{
    /// <summary>Mapa ukládá index biomu jako <c>byte</c> — víc definic obsah mít nesmí.</summary>
    public const int MaxBiomes = 256;

    private readonly Biome[] _biomes;
    private readonly Dictionary<string, int> _idToIndex;

    public BiomeRegistry(IReadOnlyList<Biome> biomes)
    {
        if (biomes.Count == 0)
        {
            throw new ArgumentException("Registr biomů nesmí být prázdný.", nameof(biomes));
        }

        if (biomes.Count > MaxBiomes)
        {
            throw new ArgumentException($"Příliš mnoho biomů ({biomes.Count}), maximum je {MaxBiomes}.", nameof(biomes));
        }

        _biomes = biomes.ToArray();
        _idToIndex = new Dictionary<string, int>(_biomes.Length);
        for (int i = 0; i < _biomes.Length; i++)
        {
            if (!_idToIndex.TryAdd(_biomes[i].Id, i))
            {
                throw new ArgumentException($"Duplicitní ID biomu '{_biomes[i].Id}'.", nameof(biomes));
            }
        }
    }

    /// <summary>Počet definovaných biomů.</summary>
    public int Count => _biomes.Length;

    /// <summary>Definice podle indexu — hot path generátoru i renderu.</summary>
    public Biome this[int index] => _biomes[index];

    /// <summary>Všechny definice v pořadí souboru (pořadí = priorita výběru při generování).</summary>
    public IReadOnlyList<Biome> All => _biomes;

    /// <summary>Převod ID → index; neznámé ID je chyba dat, proto výjimka.</summary>
    public int IndexOf(string id) =>
        _idToIndex.TryGetValue(id, out var index)
            ? index
            : throw new KeyNotFoundException($"Neznámý biom '{id}'.");

    /// <summary>Bezpečný převod ID → index.</summary>
    public bool TryIndexOf(string id, out int index) => _idToIndex.TryGetValue(id, out index);
}
