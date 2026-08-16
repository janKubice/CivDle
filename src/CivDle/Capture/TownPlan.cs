namespace CivDle.Capture;

/// <summary>Čím se blok zastaví — z toho plyne, které budovy do něj patří.</summary>
internal enum BlockRole
{
    /// <summary>Náměstí uprostřed: skoro prázdné, s pomníkem a lavičkami.</summary>
    Plaza,

    /// <summary>Centrum: obchody, škola, chrám, měšťanské domy.</summary>
    Core,

    /// <summary>Obytná čtvrť.</summary>
    Residential,

    /// <summary>Okraj: dílny, sklady, pole.</summary>
    Outskirts,

    /// <summary>Park — blok, který se schválně nezastaví.</summary>
    Green,
}

/// <summary>
/// Jedna parcela: kde je, jak je velká a co se na ni hodí.
///
/// <para>Kandidátů je víc a jsou v pořadí preference. Plán neví nic o terénu,
/// takže se může trefit do biomu, kam daná budova nesmí — a v takovém případě
/// je lepší postavit druhou volbu než nechat v ulici díru.</para>
/// </summary>
internal readonly record struct TownLot(int X, int Y, int Width, int Height, IReadOnlyList<string> Candidates);

/// <summary>
/// Půdorys ukázkového městečka: kudy vedou ulice a co stojí na parcelách.
///
/// <para>Souřadnice jsou <b>lokální</b> (0 … Size−1). Kam se městečko posadí na
/// mapě, řeší až <see cref="ShowcaseTown"/> — plán sám je jen tvar, takže se dá
/// ověřit bez simulace i bez grafiky.</para>
/// </summary>
internal sealed class TownPlan
{
    public TownPlan(int size, IReadOnlyList<(int X, int Y)> roads, IReadOnlyList<TownLot> lots)
    {
        Size = size;
        Roads = roads;
        Lots = lots;
    }

    /// <summary>Strana čtvercového výřezu v dlaždicích.</summary>
    public int Size { get; }

    /// <summary>Dlaždice s ulicí.</summary>
    public IReadOnlyList<(int X, int Y)> Roads { get; }

    /// <summary>Parcely k zastavění.</summary>
    public IReadOnlyList<TownLot> Lots { get; }
}
