using CivDle.Core.Sim;

namespace CivDle.Capture;

/// <summary>
/// Přepínač „hra si nedělá starosti": nevyčerpatelné sklady a guvernér na plný
/// plyn, kdykoli a jak dlouho je potřeba.
///
/// <para>Proč to vzniklo: ladicí menu umí jednorázové páky — dosypat suroviny,
/// nakopnout stavbu na minutu. Na testování pozdní hry to stačí, na <b>natáčení
/// traileru ne</b>: uprostřed záběru dojdou prkna, tempo se propadne a záběr je
/// k zahození. Kameraman potřebuje stav, ne jednorázovou akci.</para>
///
/// <para>Klíčové rozhodnutí: <b>tohle není nový stav v simulaci.</b> Je to
/// jenom smyčka, která z vrstvy nad simulací dokola volá tytéž ladicí háky,
/// jaké má ladicí menu. Simulace se tím nemění, nic se neukládá do savu
/// a determinismus zůstane, jak byl — což je přesně to, co chceme: cheat je
/// vlastnost <em>relace</em>, ne rozehrané hry. Po načtení savu je zase pryč.</para>
///
/// <para>Vrstva: nástroj nad simulací (stejně jako <see cref="ShareCard"/>).
/// Volá se jednou za snímek.</para>
/// </summary>
public sealed class CheatMode
{
    /// <summary>Jak často se sklady dosypávají. Každý snímek by to byla zbytečná práce.</summary>
    private const double RefillSeconds = 0.25;

    /// <summary>
    /// Jak moc se zrychlí auto-stavba. Stonásobek je hodně i na trailer, ale
    /// pod ním není na záběru růst vidět jako růst.
    /// </summary>
    public const double GovernorMultiplier = 100.0;

    /// <summary>
    /// Na jak dlouho se zrychlení nasazuje. Krátce a pořád dokola: kdyby to
    /// bylo na hodiny, zůstalo by zrychlení viset i po vypnutí přepínače.
    /// </summary>
    private const double BoostSeconds = 3.0;

    private double _sinceRefill = RefillSeconds; // ať první snímek hned dosype

    /// <summary>Nevyčerpatelné suroviny.</summary>
    public bool Resources { get; private set; }

    /// <summary>Guvernér staví a vylepšuje na maximum.</summary>
    public bool Governor { get; private set; }

    /// <summary>Je zapnuté aspoň něco? (Pro cedulku v rohu.)</summary>
    public bool Enabled => Resources || Governor;

    /// <summary>Přepne nevyčerpatelné suroviny.</summary>
    public void ToggleResources() => Resources = !Resources;

    /// <summary>Přepne guvernéra na plný plyn.</summary>
    public void ToggleGovernor() => Governor = !Governor;

    /// <summary>
    /// Zapne nebo vypne obojí naráz. Když je zapnuté cokoli, vypne se všechno —
    /// jeden přepínač na natáčení, ne hádanka, co je zrovna aktivní.
    /// </summary>
    public void ToggleAll()
    {
        bool turnOn = !Enabled;
        Resources = turnOn;
        Governor = turnOn;
    }

    /// <summary>Vypne všechno (po dotočení záběru).</summary>
    public void Disable()
    {
        Resources = false;
        Governor = false;
    }

    /// <summary>
    /// Udrží zapnuté cheaty naživu. Volá se jednou za snímek herním časem,
    /// takže se v pauze nic nedosypává — pauza má být pauza.
    /// </summary>
    public void Apply(Simulation simulation, double dt)
    {
        if (Governor && !simulation.DebugBuildBoostActive)
        {
            // Nasazuje se znovu, až když to předchozí dojede. Jinak by se
            // zrychlení pořád restartovalo a nikdy by nedoběhlo do konce.
            simulation.DebugBoostAutoBuild(GovernorMultiplier, BoostSeconds);
        }

        if (!Resources)
        {
            return;
        }

        _sinceRefill += dt;
        if (_sinceRefill < RefillSeconds)
        {
            return;
        }

        _sinceRefill = 0;
        simulation.DebugFillStorages();
    }
}
