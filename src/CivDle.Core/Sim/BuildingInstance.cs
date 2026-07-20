namespace CivDle.Core.Sim;

/// <summary>
/// Instance postavené budovy — malá struktura v plochém poli simulace
/// (data-oriented, viz CLAUDE.md). Na definici odkazuje indexem, ne referencí.
/// </summary>
public struct BuildingInstance
{
    /// <summary>Index definice v registru budov.</summary>
    public int DefIndex;

    /// <summary>Levý horní roh v dlaždicích.</summary>
    public int X;

    /// <summary>Levý horní roh v dlaždicích.</summary>
    public int Y;

    /// <summary>
    /// Postup aktuálního výrobního cyklu v „efektivních ticích" (tik × obsazenost).
    /// Po dosažení <c>Recipe.TimeTicks</c> proběhne výroba.
    /// </summary>
    public float Progress;
}
