namespace CivDle.Core.Sim;

/// <summary>
/// Dohánění offline času <b>po dávkách</b>.
///
/// <para>Proč to vzniklo: dohon se počítal jedním cyklem uvnitř konstruktoru
/// herní obrazovky. Dvanáct hodin je 432 000 tiků — a s bonusem Vzestupu
/// klidně mnohonásobek. Po dobu výpočtu okno nepřekreslovalo ani nezpracovávalo
/// vstup, takže Windows hru označily za nereagující: hráč viděl doběhnutý
/// ukazatel, klikl a hra spadla. Přitom se „jen" počítalo.</para>
///
/// <para>Tenhle typ drží stav dohonu, aby ho volající mohl posouvat po
/// kouscích mezi snímky, ukázat postup a nabídnout <see cref="Skip"/>.
/// Přeskočení nic nedopočítává ani nedodává: hráč si nechá to, co se do té
/// chvíle spočítalo. Slibovat víc by znamenalo lhát o číslech.</para>
///
/// <para>Vrstva: čistá simulace, žádné hodiny uvnitř — čas přichází parametrem,
/// takže je to testovatelné.</para>
/// </summary>
public sealed class OfflineCatchUp
{
    /// <summary>
    /// Nejvyšší počet tiků, které se dohánějí. Strop na <b>čase</b> (12 h) sám
    /// nestačí: bonus Vzestupu počet tiků násobí, takže vymaxovaná hra by
    /// doháněla desítky milionů tiků a hráč by čekal minuty. Tohle je pojistka
    /// pro trpělivost, ne pro balanc.
    /// </summary>
    public const long MaxTicks = 2_000_000;

    private readonly Simulation _simulation;
    private readonly double[] _resourcesBefore;
    private readonly double _populationBefore;
    private readonly int _buildingsBefore;

    private bool _finished;

    /// <param name="savedAtUtc">Čas uložení hry.</param>
    /// <param name="nowUtc">Teď (předává volající — simulace hodiny nezná).</param>
    public OfflineCatchUp(Simulation simulation, DateTime savedAtUtc, DateTime nowUtc)
    {
        _simulation = simulation;

        ElapsedSeconds = Math.Max(0, (long)(nowUtc - savedAtUtc).TotalSeconds);
        CreditedSeconds = Math.Min(ElapsedSeconds, OfflineProgress.MaxCreditedSeconds);

        int resourceCount = simulation.ResourceCount;
        _resourcesBefore = new double[resourceCount];
        for (int i = 0; i < resourceCount; i++)
        {
            _resourcesBefore[i] = simulation.GetResource(i);
        }

        _populationBefore = simulation.Population;
        _buildingsBefore = simulation.Buildings.Length;

        double wanted = CreditedSeconds * Simulation.TicksPerSecond * simulation.Bonuses.OfflineMult;
        TotalTicks = (long)Math.Min(Math.Max(0, wanted), MaxTicks);
    }

    /// <summary>Kolik reálného času uběhlo od uložení.</summary>
    public long ElapsedSeconds { get; }

    /// <summary>Kolik z toho se započítalo (po stropu).</summary>
    public long CreditedSeconds { get; }

    /// <summary>Kolik tiků je celkem potřeba dohnat.</summary>
    public long TotalTicks { get; }

    /// <summary>Kolik tiků je hotových.</summary>
    public long DoneTicks { get; private set; }

    /// <summary>Postup 0–1 pro ukazatel.</summary>
    public double Progress => TotalTicks <= 0 ? 1.0 : Math.Clamp(DoneTicks / (double)TotalTicks, 0.0, 1.0);

    /// <summary>Přerušil hráč dohon tlačítkem?</summary>
    public bool WasSkipped { get; private set; }

    /// <summary>Je hotovo (dopočítáno, nebo přeskočeno)?</summary>
    public bool IsDone => WasSkipped || DoneTicks >= TotalTicks;

    /// <summary>Odtiká další dávku. Volající si řídí velikost podle času na snímek.</summary>
    public void Advance(long ticks)
    {
        long remaining = Math.Min(ticks, TotalTicks - DoneTicks);
        for (long i = 0; i < remaining && !WasSkipped; i++)
        {
            _simulation.Tick();
            DoneTicks++;
        }
    }

    /// <summary>
    /// Hráč nechce čekat. Co se spočítalo, to platí; zbytek se zahodí — žádné
    /// dopočítávání „od oka", protože souhrn má sedět na to, co se opravdu stalo.
    /// </summary>
    public void Skip() => WasSkipped = true;

    /// <summary>
    /// Uzavře dohon a vrátí souhrn pro uvítací obrazovku. Volá se jednou;
    /// další volání vrací totéž bez dalších zásahů do simulace.
    /// </summary>
    public OfflineSummary Finish()
    {
        if (!_finished)
        {
            _finished = true;
            _simulation.ClearNotifications(); // žádná záplava toastů po přihlášení
        }

        var gains = new double[_resourcesBefore.Length];
        for (int i = 0; i < gains.Length; i++)
        {
            gains[i] = Math.Max(0, _simulation.GetResource(i) - _resourcesBefore[i]);
        }

        // Započítaný čas se krátí podle toho, kolik se opravdu odtikalo —
        // po přeskočení by původní číslo hráči slibovalo víc, než dostal.
        long credited = TotalTicks <= 0
            ? CreditedSeconds
            : (long)(CreditedSeconds * (DoneTicks / (double)TotalTicks));

        return new OfflineSummary(
            ElapsedSeconds,
            credited,
            gains,
            Math.Max(0, _simulation.Population - _populationBefore),
            Math.Max(0, _simulation.Buildings.Length - _buildingsBefore));
    }
}
