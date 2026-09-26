namespace CivDle.Core.Sim;

/// <summary>
/// Dohánění offline času — <b>po krocích a s pevným stropem práce</b>.
///
/// <para><b>Po krocích:</b> dohon se dřív počítal jedním cyklem uvnitř
/// konstruktoru herní obrazovky. Po dobu výpočtu okno nepřekreslovalo ani
/// nezpracovávalo vstup, takže ho Windows označily za nereagující. Tenhle typ
/// drží stav, aby ho volající mohl posouvat po kouscích mezi snímky, ukázat
/// postup a nabídnout <see cref="Skip"/>.</para>
///
/// <para><b>Se stropem práce:</b> poctivě odtikat všechno nešlo. Dvanáct hodin
/// je 432 000 tiků (s bonusem Vzestupu až dva miliony) a tik velkého města
/// trvá milisekundy — změřeno na 24 000 budovách: pět hodin dohánění. Proto:</para>
/// <list type="number">
/// <item><b>Přesně</b> se odtiká jen tolik tiků, kolik unese rozpočet
/// (<see cref="PreciseTicksFor(int, double)"/>): čím větší město a čím
/// rychleji staví guvernér, tím méně. Krátká nepřítomnost se tak spočítá
/// celá a přesně.</item>
/// <item><b>Zbytek po úsecích odhadem.</b> Každý úsek začne krátkým přesným
/// oknem (nové budovy se rozjedou a evidence toků změří, co město teď
/// vyrábí a spotřebovává), pak se přeskočí: suroviny přibudou podle
/// změřených toků krát délka skoku (mezi nulou a stropem skladu), lidé
/// dorostou k bydlení, hodiny a staveniště se posunou (<see cref="Simulation.JumpClock"/>)
/// a guvernér odehraje svá kola za celý skok, takže město za nepřítomnosti
/// opravdu roste.</item>
/// </list>
///
/// <para>Skok se zkrátí, kdyby během něj došla surovina, která dnes ubývá
/// (pila by přestala řezat, když dojde dřevo — odhad by jinak připisoval
/// prkna z ničeho); zbytek času si vezmou další úseky. Hladové město za
/// nepřítomnosti neroste, ale ani neumírá: trvalá ztráta by byla trest za to,
/// že šel hráč spát.</para>
///
/// <para>Všechno je <b>deterministické</b>: rozpočet se odvozuje z počtu budov,
/// ne z hodin, takže stejný sav a stejná doba nepřítomnosti dají stejný
/// výsledek na každém počítači.</para>
///
/// <para>Vrstva: čistá simulace, žádné hodiny uvnitř — čas přichází parametrem,
/// takže je to testovatelné.</para>
/// </summary>
public sealed class OfflineCatchUp
{
    /// <summary>
    /// Nejvyšší počet tiků, které se dohánějí (přesně i odhadem). Strop na
    /// <b>čase</b> (12 h) sám nestačí: bonus Vzestupu počet tiků násobí.
    /// </summary>
    public const long MaxTicks = 2_000_000;

    /// <summary>
    /// Rozpočet přesného dohánění v „tik × budova" — cena tiku roste s počtem
    /// budov lineárně, takže tohle drží čas přibližně stálý. Změřeno: město
    /// o 24 000 budovách (≈ 7 ms na tik) doháněl přesně pět sekund.
    /// </summary>
    public const long WorkBudget = 20_000_000;

    /// <summary>
    /// Rozpočet staveb guvernéra během odhadu v „stavba × budova": jedna stavba
    /// ve velkém městě stojí milisekundy (hledání místa, silnice, posouzení
    /// cílů), a skoky za dvanáct hodin by jinak postavily tisíce budov za
    /// minutu výpočtu. Menší město staví za nepřítomnosti skoro jako naživo.
    /// </summary>
    public const long GovernorWorkBudget = 10_000_000;

    /// <summary>Kolik budov se k počtu přičítá: i prázdné město má pevnou cenu tiku.</summary>
    private const int BaseCost = 300;

    /// <summary>Nejméně přesných tiků (2 min) — kratší okno by neukázalo, co město dělá.</summary>
    public const long MinPreciseTicks = 1_200;

    /// <summary>Nejvíc přesných tiků (1 h) — u malého města je to pár sekund výpočtu.</summary>
    public const long MaxPreciseTicks = 36_000;

    /// <summary>
    /// Kolik stojí jedna stavba guvernéra proti jednomu tiku stejně velkého
    /// města (hledání místa, napojení silnicí, posouzení cílů). Změřeno na
    /// 25 000 budovách s guvernérem na maximu (šest staveb za tik): tik stál
    /// zhruba pětkrát víc než bez guvernéra.
    /// </summary>
    public const double GovernorBuildCost = 0.75;

    /// <summary>
    /// Nejkratší přesná část, když guvernér staví víc, než stojí tik sám:
    /// úvodní okno a čtyři okna po šesti sekundách před skoky. Méně by už
    /// nezměřilo, co město vyrábí.
    /// </summary>
    public const long MinGovernedPreciseTicks = 360;

    /// <summary>Kolik z přesného rozpočtu jde na úvodní okno; zbytek na okna mezi skoky.</summary>
    private const double FirstWindowShare = 1.0 / 3.0;

    /// <summary>
    /// Nejkratší přesné okno před skokem. Výroba chodí po dávkách (recept trvá
    /// až šest sekund); v šesti sekundách se u velkého města fáze tisíců
    /// výroben zprůměrují, u malého je rozpočet na delší okno.
    /// </summary>
    private const long MinSettleTicks = 60;

    /// <summary>Nejvíc úseků — víc by jen rozdrobilo rozpočet na okna bez užitku.</summary>
    private const int MaxSegments = 48;

    /// <summary>Nejméně úseků, i u obřího města: jinak by se dvanáct hodin přeskočilo najednou.</summary>
    private const int MinSegments = 4;

    /// <summary>Okno, které zprůměruje aspoň dva cykly každé výrobny — pokud to rozpočet unese.</summary>
    private const long ComfortableSettleTicks = 120;

    /// <summary>Nejkratší skok (s), i když něco dochází — jinak by úseky nestačily.</summary>
    private const double MinJumpSeconds = 60;

    /// <summary>Pod tímhle podílem skladu se na dojití suroviny nehledí (už je u dna a toky to znají).</summary>
    private const double DepletionFloorShare = 0.05;

    /// <summary>Nejvíc kol guvernéra za skok a staveb v jednom kole — strop ceny odhadu.</summary>
    private const int MaxRoundsPerJump = 12;

    private const int MaxBudgetPerRound = 12;

    private enum Phase
    {
        First,
        Settle,
        Jump,
        Governor,
        Done,
    }

    private readonly Simulation _simulation;
    private readonly double[] _resourcesBefore;
    private readonly double _populationBefore;
    private readonly int _buildingsBefore;

    private readonly long _firstTicks;
    private readonly long _settleTicks;
    private readonly int _segments;

    /// <summary>Délka právě běžícího okna — zkracuje se, jak město za dohánění roste.</summary>
    private long _currentSettle;

    private Phase _phase;
    private long _left;
    private int _segmentsLeft;
    private long _jumpTicksLeft;
    private double _populationAtSettle;
    private double _populationRate;

    /// <summary>Součty čistého toku na začátku okna a průměr za okno (za sekundu).</summary>
    private readonly double[] _steadyAtSettle;
    private readonly double[] _netRate;
    private int _roundBudget;
    private bool _finished;

    /// <summary>Kolik staveb za tik guvernér zvládá (z bonusů, na začátku dohánění).</summary>
    private readonly double _governorBuildsPerTick;

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
        _steadyAtSettle = new double[resourceCount];
        _netRate = new double[resourceCount];

        double wanted = CreditedSeconds * Simulation.TicksPerSecond * simulation.Bonuses.OfflineMult;
        TotalTicks = (long)Math.Min(Math.Max(0, wanted), MaxTicks);

        _governorBuildsPerTick = simulation.GovernorBuildsPerTick;
        long precise = PreciseTicksFor(_buildingsBefore, _governorBuildsPerTick);
        if (TotalTicks <= precise)
        {
            // Vejde se celé: přesně, tik po tiku, jako dřív.
            _firstTicks = TotalTicks;
            TotalWork = TotalTicks;
        }
        else
        {
            _firstTicks = (long)(precise * FirstWindowShare);
            long between = precise - _firstTicks;
            _segments = (int)Math.Clamp(between / ComfortableSettleTicks, MinSegments, MaxSegments);
            _settleTicks = Math.Max(MinSettleTicks, between / _segments);

            // Kolik času zbude na skoky, se ví až za pochodu: okna se zkracují,
            // jak město roste (viz BeginSegment). Plánuje se s tím, co je teď.
            _jumpTicksLeft = Math.Max(0, TotalTicks - _firstTicks - _segments * _settleTicks);
            IsEstimated = true;

            // Práce, ne čas: skok je jeden krok a kola guvernéra jsou kroky
            // navíc — ukazatel tak jde rovnoměrně, ne po skocích.
            long roundsPerJump = RoundsFor(_jumpTicksLeft / _segments);
            TotalWork = _firstTicks + _segments * (_settleTicks + 1 + roundsPerJump);
        }

        _segmentsLeft = _segments;
        _left = _firstTicks;
        _phase = _firstTicks > 0 ? Phase.First : _segments > 0 ? Phase.Settle : Phase.Done;
        if (_phase == Phase.Settle)
        {
            BeginSegment();
        }
    }

    /// <summary>Kolik reálného času uběhlo od uložení.</summary>
    public long ElapsedSeconds { get; }

    /// <summary>Kolik z toho se započítalo (po stropu).</summary>
    public long CreditedSeconds { get; }

    /// <summary>Kolik herních tiků je celkem potřeba dohnat (přesně i odhadem).</summary>
    public long TotalTicks { get; }

    /// <summary>Kolik herních tiků je hotových (odtikaných i přeskočených).</summary>
    public long DoneTicks { get; private set; }

    /// <summary>Kolik kroků práce dohon zabere (přesné tiky, skoky, kola guvernéra).</summary>
    public long TotalWork { get; }

    /// <summary>Kolik kroků práce je hotových.</summary>
    public long DoneWork { get; private set; }

    /// <summary>Počítá se část nepřítomnosti odhadem (dlouhá nepřítomnost, velké město)?</summary>
    public bool IsEstimated { get; }

    /// <summary>Postup 0–1 pro ukazatel (podle práce, ne času — skoky by ho rozházely).</summary>
    public double Progress => IsDone ? 1.0
        : TotalWork <= 0 ? 1.0
        : Math.Clamp(DoneWork / (double)TotalWork, 0.0, 1.0);

    /// <summary>Přerušil hráč dohon tlačítkem?</summary>
    public bool WasSkipped { get; private set; }

    /// <summary>Je hotovo (dopočítáno, nebo přeskočeno)?</summary>
    public bool IsDone => WasSkipped || _phase == Phase.Done;

    /// <summary>
    /// Kolik přesných tiků unese rozpočet u města o <paramref name="buildings"/>
    /// budovách. Deterministické: z počtu budov, ne z měření času.
    /// </summary>
    public static long PreciseTicksFor(int buildings) =>
        Math.Clamp(WorkBudget / (Math.Max(0, buildings) + BaseCost), MinPreciseTicks, MaxPreciseTicks);

    /// <summary>
    /// Totéž, když guvernér staví <paramref name="governorBuildsPerTick"/>
    /// budov za tik.
    ///
    /// <para><b>Proč:</b> rozpočet počítal tik jako práci úměrnou počtu budov.
    /// Guvernér s vylepšeními ale staví i šest budov za tik a každá stavba
    /// stojí zhruba tolik co tik sám — přesných 1 200 tiků u velkého města
    /// pak trvalo přes minutu místo pár sekund. Dokud guvernér stojí méně
    /// než tik (<see cref="GovernorBuildCost"/> × tempo ≤ 1), platí rozpočet
    /// jako dřív; nad tím se přesná část úměrně zkrátí, nejvýš na
    /// <see cref="MinGovernedPreciseTicks"/>. Zbytek času dopočítají skoky.</para>
    /// </summary>
    public static long PreciseTicksFor(int buildings, double governorBuildsPerTick)
    {
        long ticks = PreciseTicksFor(buildings);
        double governor = GovernorBuildCost * Math.Max(0, governorBuildsPerTick);
        return governor <= 1 ? ticks : Math.Max(Math.Min(ticks, MinGovernedPreciseTicks), (long)(ticks / governor));
    }

    /// <summary>
    /// Udělá až <paramref name="steps"/> kroků práce: přesný tik, skok, nebo
    /// kolo guvernéra. Volající si řídí počet podle času na snímek; jeden krok
    /// je vždycky krátký, takže okno nezamrzne.
    /// </summary>
    public void Advance(long steps)
    {
        for (long i = 0; i < steps && !IsDone; i++)
        {
            Step();
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

        // Započítaný čas se krátí podle toho, kolik se opravdu dohnalo —
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

    private void Step()
    {
        DoneWork++;
        switch (_phase)
        {
            case Phase.First:
                Tick();
                if (--_left <= 0)
                {
                    if (_segmentsLeft > 0)
                    {
                        BeginSegment();
                    }
                    else
                    {
                        _phase = Phase.Done;
                    }
                }

                break;

            case Phase.Settle:
                Tick();
                if (--_left <= 0)
                {
                    MeasureWindow();
                    _phase = Phase.Jump;
                }

                break;

            case Phase.Jump:
                Jump();
                break;

            case Phase.Governor:
                _simulation.RunGovernorRound(_roundBudget);
                if (--_left <= 0)
                {
                    EndSegment();
                }

                break;
        }
    }

    private void Tick()
    {
        _simulation.Tick();
        DoneTicks++;
    }

    private void BeginSegment()
    {
        // Okno se měří podle dnešní velikosti města, ne podle té při uložení:
        // guvernér za dohánění postaví i tisíce budov a tik se úměrně prodraží.
        // Kratší okno vrátí svůj zbytek do skoků, takže čas sedí dál.
        long budget = (PreciseTicksFor(_simulation.Buildings.Length, _governorBuildsPerTick) - _firstTicks)
            / Math.Max(1, _segments);
        _currentSettle = Math.Clamp(budget, MinSettleTicks, _settleTicks);
        _jumpTicksLeft += _settleTicks - _currentSettle;

        _phase = Phase.Settle;
        _left = _currentSettle;
        _populationAtSettle = _simulation.Population;
        var ledger = _simulation.Ledger;
        for (int r = 0; r < _steadyAtSettle.Length; r++)
        {
            _steadyAtSettle[r] = ledger.SteadyTotal(r);
        }
    }

    /// <summary>Průměrné toky a přírůstek lidí za právě doběhlé přesné okno.</summary>
    private void MeasureWindow()
    {
        double seconds = _currentSettle / Simulation.TicksPerSecond;
        _populationRate = (_simulation.Population - _populationAtSettle) / seconds;
        var ledger = _simulation.Ledger;
        for (int r = 0; r < _netRate.Length; r++)
        {
            _netRate[r] = (ledger.SteadyTotal(r) - _steadyAtSettle[r]) / seconds;
        }
    }

    private void EndSegment()
    {
        if (--_segmentsLeft > 0)
        {
            BeginSegment();
        }
        else
        {
            _phase = Phase.Done;
        }
    }

    /// <summary>
    /// Skok: suroviny podle změřených toků, lidé k bydlení, hodiny dopředu,
    /// pak kola guvernéra za celý skok.
    /// </summary>
    private void Jump()
    {
        bool last = _segmentsLeft <= 1;
        long planned = last ? _jumpTicksLeft : _jumpTicksLeft / _segmentsLeft;
        double seconds = planned / Simulation.TicksPerSecond;
        if (!last)
        {
            seconds = Math.Min(seconds, Math.Max(Math.Min(seconds, MinJumpSeconds), SecondsUntilSomethingRunsOut()));
        }

        long ticks = Math.Min(_jumpTicksLeft, (long)Math.Round(seconds * Simulation.TicksPerSecond));
        seconds = ticks / Simulation.TicksPerSecond;
        CreditEconomy(seconds);
        _simulation.JumpClock(ticks);
        _jumpTicksLeft -= ticks;
        DoneTicks += ticks;

        // Guvernér za celý skok: kol je méně, ale v každém staví za víc
        // intervalů najednou — stejná práce za zlomek režie. Staveb je nejvýš
        // tolik, kolik unese rozpočet pro dnešní velikost města.
        long intervals = ticks / Math.Max(1, _simulation.AutoBuildInterval);
        long affordable = GovernorWorkBudget / (_simulation.Buildings.Length + BaseCost) / Math.Max(1, _segments);
        long wanted = Math.Min(intervals * Math.Max(1, _simulation.AutoBuildBudget), Math.Max(1, affordable));
        int rounds = (int)Math.Min(RoundsFor(ticks), (wanted + MaxBudgetPerRound - 1) / MaxBudgetPerRound);
        rounds = Math.Max(rounds, Math.Min(RoundsFor(ticks), 1));
        if (rounds <= 0)
        {
            EndSegment();
            return;
        }

        _roundBudget = (int)Math.Clamp((wanted + rounds - 1) / rounds, 1, MaxBudgetPerRound);
        _left = rounds;
        _phase = Phase.Governor;
    }

    private int RoundsFor(long ticks) =>
        (int)Math.Clamp(ticks / Math.Max(1, _simulation.AutoBuildInterval), 0, MaxRoundsPerJump);

    /// <summary>
    /// Za kolik sekund dojde první surovina, které dnes ubývá (a ještě není u dna).
    /// Po jejím dojití by se zastavily výrobny, které z ní vyrábějí, a odhad by
    /// připisoval výrobek z ničeho — skok proto skončí dřív.
    /// </summary>
    private double SecondsUntilSomethingRunsOut()
    {
        double earliest = double.MaxValue;
        for (int r = 0; r < _netRate.Length; r++)
        {
            double net = _netRate[r];
            double stock = _simulation.GetResource(r);
            if (net < 0 && stock > _simulation.GetStorageCap(r) * DepletionFloorShare)
            {
                earliest = Math.Min(earliest, stock / -net);
            }
        }

        return earliest;
    }

    private void CreditEconomy(double seconds)
    {
        for (int r = 0; r < _netRate.Length; r++)
        {
            _simulation.CreditEstimated(r, _netRate[r] * seconds);
        }

        // Hladové město neroste — ale ani neumírá (viz popis třídy).
        int food = _simulation.ContentRef.Gameplay.FoodResourceIndex;
        bool fed = food < 0 || _netRate[food] >= 0 || _simulation.GetResource(food) > 0;
        if (fed && _populationRate > 0)
        {
            _simulation.GrowPopulationEstimated(_populationRate * seconds);
        }
    }
}
