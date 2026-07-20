namespace CivDle.Core.World;

/// <summary>
/// Balení souřadnic dlaždice (x, y) do jednoho <c>long</c> klíče pro řídké mapy
/// (occupancy, silnice) na nekonečné mapě. Podporuje i záporné souřadnice
/// (round-trip přes <c>uint</c>).
/// </summary>
public static class TileKey
{
    /// <summary>Zabalí (x, y) do klíče.</summary>
    public static long Pack(int x, int y) => ((long)(uint)x) | ((long)(uint)y << 32);

    /// <summary>Vytáhne x z klíče.</summary>
    public static int X(long key) => (int)(uint)(key & 0xFFFFFFFF);

    /// <summary>Vytáhne y z klíče.</summary>
    public static int Y(long key) => (int)(uint)(key >> 32);
}
