namespace CivDle.Core.Content;

/// <summary>
/// Guvernér jako plánovač cílů — prahy, podle kterých pozná, co město chybí
/// nad rámec jídla, bydlení a služeb.
///
/// <para><b>Proč je to v datech:</b> „kdy postavit sklad" nebo „jak dlouho smí
/// trvat další výzkum" jsou ladicí čísla, ne logika. Kód říká <em>jak</em> cíl
/// poznat a pokrýt, data <em>kdy</em> (CLAUDE.md).</para>
///
/// <para><b>Bez bloku v datech</b> se guvernér chová jako dřív: staví jen budovy
/// se značkou <c>autoBuild</c> a řeší jen pět základních potřeb. Starší data
/// i mody tak nic nepoznají.</para>
/// </summary>
/// <param name="BuildsByRole">
/// Smí guvernér stavět každou budovu, která plní roli (sklad, školka, přístav,
/// elektrárna…), nejen ty se značkou <c>autoBuild</c>? Značka pak znamená jen
/// přednost. Divy, megastavby, podmořské stavby a terraformace zůstávají hráči.
/// </param>
/// <param name="Storage">Kdy stavět sklady.</param>
/// <param name="Knowledge">Kdy stavět knihovny a školy (věda na výzkum).</param>
/// <param name="Faith">Kdy stavět svatyně a chrámy (víra na modlitby).</param>
/// <param name="Landscape">Kdy vracet krajinu (lesní školky u vytěžených dřevorubců a lomů).</param>
/// <param name="Power">Kdy stavět elektrárny.</param>
public sealed record GovernorConfig(
    bool BuildsByRole,
    StorageGoalConfig Storage,
    SupplyGoalConfig Knowledge,
    SupplyGoalConfig Faith,
    LandscapeGoalConfig Landscape,
    PowerGoalConfig Power)
{
    /// <summary>Dosavadní guvernér: jen <c>autoBuild</c> a pět základních potřeb.</summary>
    public static GovernorConfig Classic { get; } = new(
        false, StorageGoalConfig.Off, SupplyGoalConfig.Off, SupplyGoalConfig.Off,
        LandscapeGoalConfig.Off, PowerGoalConfig.Off);
}

/// <summary>Kdy guvernér staví sklady.</summary>
/// <param name="FullShare">
/// Od jakého naplnění (0–1) je sklad „plný": výroba se o něj zastavuje nebo
/// přetéká. 0 = cíl vypnutý.
/// </param>
public sealed record StorageGoalConfig(double FullShare)
{
    /// <summary>Vypnuto.</summary>
    public static StorageGoalConfig Off { get; } = new(0);

    /// <summary>Hlídá guvernér sklady?</summary>
    public bool IsEnabled => FullShare > 0;
}

/// <summary>
/// Stálý přísun suroviny, kterou hráč utrácí sám (věda za výzkum, víra za
/// modlitby) — guvernér ji nespotřebovává, jen zajistí, aby tekla.
/// </summary>
/// <param name="ResourceIndex">Která surovina; −1 = cíl vypnutý.</param>
/// <param name="MinPopulation">
/// Od kolika obyvatel. Vesnice o dvou domech potřebuje prkna na domy, ne
/// knihovnu, která je sní.
/// </param>
/// <param name="TargetSeconds">
/// Na nejbližší věc, za kterou se surovina utrácí, se nemá čekat déle. Když
/// by se čekalo, přibude budova, která surovinu vyrábí.
/// </param>
public sealed record SupplyGoalConfig(int ResourceIndex, double MinPopulation, double TargetSeconds)
{
    /// <summary>Vypnuto.</summary>
    public static SupplyGoalConfig Off { get; } = new(-1, 0, 0);

    /// <summary>Hlídá guvernér přísun?</summary>
    public bool IsEnabled => ResourceIndex >= 0 && TargetSeconds > 0;
}

/// <summary>Kdy guvernér vrací krajinu.</summary>
/// <param name="MinNodes">
/// Pod kolik uzlů v dosahu těžební budovy se okolí bere jako vytěžené a má
/// k ní přibýt budova, která krajinu obnovuje. 0 = cíl vypnutý.
/// </param>
public sealed record LandscapeGoalConfig(int MinNodes)
{
    /// <summary>Vypnuto.</summary>
    public static LandscapeGoalConfig Off { get; } = new(0);

    /// <summary>Hlídá guvernér krajinu?</summary>
    public bool IsEnabled => MinNodes > 0;
}

/// <summary>Kdy guvernér staví elektrárny.</summary>
/// <param name="MinCoverage">
/// Pod jaké pokrytí proudem (0–1) u budovy, která ho potřebuje, přibude
/// elektrárna. 0 = cíl vypnutý.
/// </param>
public sealed record PowerGoalConfig(double MinCoverage)
{
    /// <summary>Vypnuto.</summary>
    public static PowerGoalConfig Off { get; } = new(0);

    /// <summary>Hlídá guvernér proud?</summary>
    public bool IsEnabled => MinCoverage > 0;
}
