namespace CivDle.Core.Sim;

/// <summary>
/// Kam má guvernér tlačit: do velikosti, nebo do kvality.
///
/// <para>Pořadí je stupnice, ne libovolný výčet — UI ho prochází zleva doprava
/// jako táhlo.</para>
/// </summary>
public enum GovernorFocus
{
    /// <summary>Jen růst. Guvernér staví a nevylepšuje vůbec.</summary>
    Growth,

    /// <summary>Spíš růst: staví dvakrát tolik, co vylepšuje.</summary>
    MostlyGrowth,

    /// <summary>Vyvážený plán — výchozí a dosavadní chování.</summary>
    Balanced,

    /// <summary>Spíš kvalita: vylepšuje dvakrát tolik, co staví.</summary>
    MostlyQuality,

    /// <summary>Jen kvalita. Guvernér vylepšuje a město nerozšiřuje.</summary>
    Quality,
}

/// <summary>
/// Plán guvernéra: <b>kam</b> tlačit (velikost vs. kvalita) a <b>co</b> smí
/// stavět.
///
/// <para>Proč to vzniklo: guvernér uměl jen „co smí vylepšovat" a jinak si
/// dělal, co chtěl. Hráč, který chtěl kompaktní vylepšené město, neměl jak mu
/// říct, ať přestane zabírat krajinu — a hráč, který chtěl expandovat, ho
/// nemohl přimět nechat vylepšování být. Automatizace bez kontroly není
/// pomocník, ale spolubydlící, který přestavuje byt podle svého.</para>
///
/// <para><b>Zaměření</b> rozděluje rozpočet mezi stavbu a vylepšování. Váhy
/// jsou schválně postavené tak, že <see cref="GovernorFocus.Balanced"/> se
/// chová přesně jako dosavadní hra — kdo si nic nenastaví, nepozná rozdíl;
/// kdo si vybere stranu, dostane jí dvojnásobek, ne polovinu té druhé. Volba
/// tedy něco přidává, nic nebere.</para>
///
/// <para><b>Kategorie</b> se drží jako seznam <em>zakázaných</em>, ne
/// povolených. Kdyby to byl seznam povolených, každá nová budova z aktualizace
/// nebo z modu by byla tiše zakázaná a hráč by marně hledal, proč se nestaví.</para>
///
/// <para>Vrstva: stav simulace (ukládá se do savu), ale čistě rozhodovací —
/// žádná geometrie ani entity. Proto normální třída, ne struktura v poli.</para>
/// </summary>
public sealed class GovernorPlan
{
    private readonly HashSet<string> _blockedCategories = new(StringComparer.Ordinal);

    /// <summary>Kam guvernér tlačí. Výchozí = dosavadní chování.</summary>
    public GovernorFocus Focus { get; private set; } = GovernorFocus.Balanced;

    /// <summary>Příkaz hráče: přenastaví zaměření.</summary>
    public void SetFocus(GovernorFocus focus) => Focus = focus;

    /// <summary>
    /// Násobič rozpočtu na <b>stavbu</b>. Vyvážený plán = 1,0, tedy přesně to,
    /// co hra dělala předtím.
    /// </summary>
    public double GrowthWeight => Focus switch
    {
        GovernorFocus.Growth => 2.0,
        GovernorFocus.MostlyGrowth => 1.5,
        GovernorFocus.MostlyQuality => 0.5,
        GovernorFocus.Quality => 0.0,
        _ => 1.0,
    };

    /// <summary>Násobič rozpočtu na <b>vylepšování</b>. Zrcadlí <see cref="GrowthWeight"/>.</summary>
    public double QualityWeight => Focus switch
    {
        GovernorFocus.Growth => 0.0,
        GovernorFocus.MostlyGrowth => 0.5,
        GovernorFocus.MostlyQuality => 1.5,
        GovernorFocus.Quality => 2.0,
        _ => 1.0,
    };

    /// <summary>Rozšiřuje guvernér vůbec město? (Na krajní volbě ne — a UI to musí říct.)</summary>
    public bool BuildsAtAll => GrowthWeight > 0;

    /// <summary>Vylepšuje guvernér vůbec? (Na krajní volbě ne.)</summary>
    public bool UpgradesAtAll => QualityWeight > 0;

    /// <summary>Smí guvernér stavět budovy téhle kategorie?</summary>
    public bool AllowsCategory(string category) => !_blockedCategories.Contains(category);

    /// <summary>Příkaz hráče: povolí nebo zakáže kategorii.</summary>
    public void SetCategoryAllowed(string category, bool allowed)
    {
        if (allowed)
        {
            _blockedCategories.Remove(category);
        }
        else
        {
            _blockedCategories.Add(category);
        }
    }

    /// <summary>Zakázané kategorie — pro save a pro UI.</summary>
    public IReadOnlyCollection<string> BlockedCategories => _blockedCategories;

    /// <summary>Obnoví plán ze savu.</summary>
    internal void Restore(GovernorFocus focus, IEnumerable<string> blocked)
    {
        Focus = focus;
        _blockedCategories.Clear();
        foreach (string category in blocked)
        {
            _blockedCategories.Add(category);
        }
    }
}
