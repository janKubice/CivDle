namespace CivDle.Core.World;

/// <summary>Terén s jediným biomem všude — pro testy a jednoduchá pozadí.</summary>
public sealed class UniformTerrain : ITerrain
{
    private readonly byte _biomeIndex;

    public UniformTerrain(int biomeIndex) => _biomeIndex = (byte)biomeIndex;

    public byte BiomeAt(int x, int y) => _biomeIndex;
}
