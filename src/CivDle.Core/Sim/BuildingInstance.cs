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

    /// <summary>
    /// Násobič výroby z biomu, na kterém budova stojí (ekonomická identita biomů).
    /// Cachuje se při položení/načtení — jinak by se terén vzorkoval každý tik
    /// pro každou budovu (hot path, CLAUDE.md výkon). Neukládá se: odvodí se z terénu.
    /// </summary>
    public float BiomeMult;

    /// <summary>
    /// Násobič výroby z okolí (pila u lesa, lom u hor — viz <c>AdjacencyRule</c>).
    /// Stejně jako <see cref="BiomeMult"/> se cachuje při položení: okolí se
    /// prohlédne jednou, ne každý tik. 1.0 = budova bez pravidla nebo bez okolí.
    /// </summary>
    public float AdjacencyMult;

    /// <summary>
    /// Násobič výroby ze vzdálenosti k nejbližšímu skladu (viz <c>HaulSystem</c>).
    /// Na rozdíl od <see cref="BiomeMult"/> se může měnit i bez zásahu do budovy —
    /// stačí, že hráč postaví sklad opodál. Přepočet je proto rozložený do ticků,
    /// ne v hot path.
    /// </summary>
    public float HaulMult;
}
