namespace CivDle.Core.World;

/// <summary>
/// Terén z konečné mřížky (<see cref="WorldMap"/>) — pro testy s ručně sestaveným
/// rozložením biomů. Mimo mřížku vrací biom nejbližší hrany (svět „pokračuje"
/// okrajovým biomem), takže i tenhle terén působí nekonečně.
/// </summary>
public sealed class GridTerrain : ITerrain
{
    private readonly WorldMap _map;

    public GridTerrain(WorldMap map) => _map = map;

    public byte BiomeAt(int x, int y)
    {
        int clampedX = Math.Clamp(x, 0, _map.Width - 1);
        int clampedY = Math.Clamp(y, 0, _map.Height - 1);
        return _map.BiomeAt(clampedX, clampedY);
    }
}
