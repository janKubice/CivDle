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

    /// <summary>
    /// Násobič výroby z milníků za počet budov téhož typu (viz
    /// <c>BuildingMilestoneSystem</c>). 1.0 = typ milníky nemá nebo je ještě
    /// nedosáhl.
    /// </summary>
    public float MilestoneMult;

    /// <summary>
    /// Násobič výroby ze synergie čtvrti (viz <c>DistrictSystem</c>). 1.0 = budova
    /// není v žádné čtvrti, nebo je v ní sama.
    /// </summary>
    public float DistrictMult;

    /// <summary>
    /// Index čtvrti, do které budova patří; −1 = žádná.
    ///
    /// <para>Proč index a ne jen násobič: čtvrť má i stinnou stránku (víc kouře)
    /// a UI o ní chce mluvit jménem. Jedno číslo navíc ušetří druhý cachovaný
    /// násobič a dovolí pomalým systémům dohledat, o jakou čtvrť jde.</para>
    /// </summary>
    public int DistrictIndex;

    /// <summary>
    /// Násobič výroby ze zamoření pod budovou (viz <c>PollutionSystem</c>). 1.0 =
    /// čisto nebo budově na okolí nezáleží.
    ///
    /// <para>Cachuje se stejně jako <see cref="HaulMult"/>: znečištění je pomalá
    /// veličina a v tikové smyčce výroby se do mřížky sahat nemá.</para>
    /// </summary>
    public float PollutionMult;

    /// <summary>
    /// Kolik tiků zbývá do dokončení stavby. 0 = budova stojí a funguje.
    ///
    /// <para>Rozestavěná budova nevyrábí a nedává žádné bonusy (bydlení, pracovní
    /// místa, sklad, proud) — ty se připíšou až při dokončení. Divy světa se tím
    /// mění z drahého kliknutí na událost.</para>
    /// </summary>
    public int BuildTicksRemaining;

    /// <summary>
    /// Kam došla budova při těžbě okolí (pořadí dlaždice ve spirále kolem ní).
    ///
    /// <para>Bez tohohle by se při každém výrobním cyklu prohledávalo celé okolí
    /// od začátku. Takhle se kurzor posune, teprve až dlaždice dojde — hledání
    /// je proto amortizovaně konstantní i pro statisíce budov.</para>
    /// </summary>
    public int HarvestCursor;

    /// <summary>Došly budově v dosahu zdroje? (Render i UI to hlásí hráči.)</summary>
    public bool OutOfResources;

    /// <summary>Je budova hotová a v provozu?</summary>
    public readonly bool IsComplete => BuildTicksRemaining <= 0;
}
