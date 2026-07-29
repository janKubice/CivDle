namespace CivDle.Core.Content;

/// <summary>
/// Jeden soused z <c>data/neighbours.json</c>: cizí sídlo, se kterým město
/// obchoduje.
///
/// <para>Proč to ve hře je: karavany už jezdily po hráčových silnicích, ale byly
/// anonymní — přijela, zaplatila, zmizela. Se sousedy má obchod <b>dlouhý
/// horizont</b>: Kamenná zátoka se vrací, pamatuje si, kolikrát jsi ji doprovodil,
/// a čím je vztah lepší, tím líp platí. Zároveň to dává smysl silnicím vedoucím
/// k okraji mapy — vedou někam.</para>
/// </summary>
/// <param name="Id">Stabilní ID (do savu i lokalizace).</param>
/// <param name="MapColor">Barva sousedova praporu (karavana, seznam).</param>
public sealed record NeighbourDef(string Id, RgbColor MapColor)
{
    /// <summary>Lokalizační klíč jména souseda.</summary>
    public string NameKey => $"neighbour.{Id}";
}

/// <summary>
/// Sousedé a pravidla vztahu s nimi. Prázdný katalog je legitimní stav —
/// karavany pak zůstanou anonymní jako dřív.
/// </summary>
/// <param name="Neighbours">Kdo všechno kolem města žije.</param>
/// <param name="TradesPerLevel">Kolik dokončených obchodů posune vztah o stupeň.</param>
/// <param name="BonusPerLevel">O kolik lepší výplatu dá každý stupeň vztahu.</param>
/// <param name="MaxLevel">Strop vztahu — po něm už se jen obchoduje.</param>
public sealed record NeighbourCatalog(
    DefRegistry<NeighbourDef> Neighbours,
    int TradesPerLevel,
    double BonusPerLevel,
    int MaxLevel)
{
    /// <summary>Žádní sousedé — pro starší data i pro testy.</summary>
    public static NeighbourCatalog Empty { get; } = new(
        new DefRegistry<NeighbourDef>(Array.Empty<NeighbourDef>(), n => n.Id, "soused", allowEmpty: true),
        1, 0, 0);

    /// <summary>Má smysl vztahy vůbec vést?</summary>
    public bool IsEnabled => Neighbours.Count > 0;

    /// <summary>Stupeň vztahu po daném počtu obchodů (0 = cizinci).</summary>
    public int LevelFor(long trades) =>
        TradesPerLevel <= 0 ? 0 : (int)Math.Min(MaxLevel, trades / TradesPerLevel);

    /// <summary>
    /// Násobič výplaty podle stupně vztahu. Vztah nikdy neubírá — nejhorší, co se
    /// může stát, je, že soused platí základ (soft pressure jako všude jinde).
    /// </summary>
    public double PayoutMultiplier(long trades) => 1.0 + LevelFor(trades) * BonusPerLevel;
}
