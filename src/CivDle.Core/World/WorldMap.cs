namespace CivDle.Core.World;

/// <summary>
/// Datově orientovaná mapa světa: plochá pole indexovaná <c>y * Width + x</c>,
/// žádné objekty per dlaždice (tech-stack.md — cache-friendly SoA). Biom je uložen
/// jako <c>byte</c> index do registru biomů, ne string ani reference.
/// </summary>
public sealed class WorldMap
{
    public WorldMap(int width, int height)
    {
        if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
        if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));

        Width = width;
        Height = height;
        BiomeIndices = new byte[width * height];
        Elevation = new float[width * height];
        Moisture = new float[width * height];
    }

    /// <summary>Šířka mapy v dlaždicích.</summary>
    public int Width { get; }

    /// <summary>Výška mapy v dlaždicích.</summary>
    public int Height { get; }

    /// <summary>Index biomu každé dlaždice (odkaz do <c>BiomeRegistry</c>).</summary>
    public byte[] BiomeIndices { get; }

    /// <summary>Surová výška 0–1 (pod hladinou moře = voda).</summary>
    public float[] Elevation { get; }

    /// <summary>Vlhkost 0–1 — později řídí vegetaci, suroviny a dekorace.</summary>
    public float[] Moisture { get; }

    /// <summary>Převod souřadnic na index do plochých polí.</summary>
    public int Index(int x, int y) => y * Width + x;

    /// <summary>Vrací true, když souřadnice leží uvnitř mapy.</summary>
    public bool InBounds(int x, int y) => x >= 0 && x < Width && y >= 0 && y < Height;

    /// <summary>Index biomu na dlaždici.</summary>
    public byte BiomeAt(int x, int y) => BiomeIndices[Index(x, y)];
}
