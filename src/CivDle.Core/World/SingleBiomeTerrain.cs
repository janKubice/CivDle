using CivDle.Core.Content;
using CivDle.Core.WorldGen;

namespace CivDle.Core.World;

/// <summary>
/// Terén pro pravidlo výzvy <c>singleBiome</c>: celá souš je jeden biom, jen
/// řídké čtverce („oázy") zůstanou tak, jak je vygeneroval původní terén.
///
/// <para><b>Proč oázy.</b> Čistá poušť nemá dřevo, kámen ani pole — výzva by
/// byla nevyhratelná, ne těžká. Oáza je čtverec původní krajiny (les, louka,
/// skála), takže zdroje jsou vzácné a daleko od sebe, ale jsou. Voda zůstává
/// všude: řeky a pobřeží k poušti patří a rybolov je jedna z mála jistot.</para>
///
/// <para>Dekorátor nad <see cref="ITerrain"/>, ne úprava generátoru: generátor
/// zůstane jeden a pravidlo výzvy se nemusí míchat do šumu. Která oáza kde je,
/// je čistá funkce souřadnic a seedu — nic se neukládá a po načtení savu vyjde
/// svět stejně.</para>
/// </summary>
public sealed class SingleBiomeTerrain : ITerrain
{
    private readonly ITerrain _inner;
    private readonly BiomeRegistry _biomes;
    private readonly byte _biome;
    private readonly int _chunk;
    private readonly ulong _threshold;
    private readonly ulong _salt;

    /// <param name="inner">Původní terén (voda a oázy se berou z něj).</param>
    /// <param name="biomes">Registr biomů — kvůli tomu, co je voda.</param>
    /// <param name="biomeIndex">Biom, kterým se zaplní souš.</param>
    /// <param name="chunkTiles">Velikost čtverce oázy v dlaždicích.</param>
    /// <param name="oasisShare">Jaký podíl čtverců zůstane původní krajinou (0–1).</param>
    /// <param name="seed">Seed světa — oázy jsou v každém světě jinde.</param>
    public SingleBiomeTerrain(
        ITerrain inner, BiomeRegistry biomes, int biomeIndex, int chunkTiles, double oasisShare, long seed)
    {
        if (biomeIndex < 0 || biomeIndex >= biomes.Count || biomes[biomeIndex].IsWater)
        {
            throw new ArgumentOutOfRangeException(nameof(biomeIndex), "Souš musí být suchozemský biom.");
        }

        _inner = inner;
        _biomes = biomes;
        _biome = (byte)biomeIndex;
        _chunk = Math.Max(1, chunkTiles);
        _threshold = (ulong)(Math.Clamp(oasisShare, 0.0, 1.0) * ulong.MaxValue);
        _salt = new SplitMix64(unchecked((ulong)seed ^ 0x0A515A0A515UL)).Next();
    }

    /// <inheritdoc />
    public byte BiomeAt(int x, int y)
    {
        byte original = _inner.BiomeAt(x, y);
        return _biomes[original].IsWater || IsOasis(x, y) ? original : _biome;
    }

    /// <inheritdoc />
    public float ElevationAt(int x, int y) => _inner.ElevationAt(x, y);

    /// <inheritdoc />
    public bool TryRiverFlow(int x, int y, out int dx, out int dy) => _inner.TryRiverFlow(x, y, out dx, out dy);

    /// <summary>Leží dlaždice v oáze (původní krajina)?</summary>
    public bool IsOasis(int x, int y)
    {
        long chunkX = (long)Math.Floor(x / (double)_chunk);
        long chunkY = (long)Math.Floor(y / (double)_chunk);
        ulong key = unchecked((ulong)chunkX * 0x9E3779B97F4A7C15UL ^ (ulong)chunkY * 0xC2B2AE3D27D4EB4FUL ^ _salt);
        return new SplitMix64(key).Next() < _threshold;
    }
}
