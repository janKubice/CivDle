namespace CivDle.Core.Content;

/// <summary>
/// Registr biomů. Nad obecným <see cref="DefRegistry{T}"/> přidává limit počtu —
/// mapa ukládá index biomu jako <c>byte</c>.
/// </summary>
public sealed class BiomeRegistry : DefRegistry<Biome>
{
    /// <summary>Mapa ukládá index biomu jako <c>byte</c> — víc definic obsah mít nesmí.</summary>
    public const int MaxBiomes = 256;

    public BiomeRegistry(IReadOnlyList<Biome> biomes)
        : base(Check(biomes), b => b.Id, "biom")
    {
    }

    private static IReadOnlyList<Biome> Check(IReadOnlyList<Biome> biomes)
    {
        if (biomes.Count > MaxBiomes)
        {
            throw new ArgumentException($"Příliš mnoho biomů ({biomes.Count}), maximum je {MaxBiomes}.", nameof(biomes));
        }

        return biomes;
    }
}
