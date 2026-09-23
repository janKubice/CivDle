namespace CivDle.Core.Sim;

/// <summary>
/// Na co se surovina spotřebovala. Tooltip z toho skládá odpověď na „proč mi
/// jídla ubývá" a guvernér podle toho pozná spotřebu, kterou jeho rezerva
/// nezastaví.
/// </summary>
public enum ConsumptionKind
{
    /// <summary>Vstupy výrobních receptů (pila bere dřevo).</summary>
    Recipes,

    /// <summary>Nákupy hráče i automatiky: stavby, výzkum, silnice, zakázky, události, modlitby.</summary>
    Purchases,

    /// <summary>Co snědí lidé.</summary>
    People,

    /// <summary>Palivo na topení v zimě.</summary>
    Heating,

    /// <summary>Údržba služeb a čističek.</summary>
    Upkeep,

    /// <summary>Opotřebení nástrojů při práci.</summary>
    Tools,
}

/// <summary>
/// Kolik se které suroviny za sekundu <b>vyrobí</b>, kolik <b>spotřebuje</b>
/// a kolik <b>propadne</b> do plného skladu.
///
/// <para>Proč to nejde vyčíst ze stavu skladu: hráč dosud viděl jen čistý tok
/// (rozdíl zásoby mezi dvěma vzorky). Z toho se nedá poznat rozdíl mezi
/// „nevyrábí se" a „vyrábí se a hned se to spotřebuje" — a to jsou dvě úplně
/// jiné situace s úplně jiným řešením. Navíc se zásoba zaráží o strop skladu,
/// takže při plném skladu vypadá výroba jako nula.</para>
///
/// <para><b>Propad</b> je vlastní číslo schválně. Plný sklad výrobu nezastaví,
/// přebytek mizí — je to záměr hry (žádný trest, jen motivace stavět sklady),
/// ale hráč to nemá jak zjistit. Tohle je ten ukazatel.</para>
///
/// <para>Vrstva: datová evidence uvnitř simulace. Předalokovaná pole, žádná
/// alokace v tiku, žádný slovník. Do savu nepatří — je to okno do právě
/// běžící ekonomiky, po načtení se naplní za pár tiků samo.</para>
/// </summary>
public sealed class ResourceLedger
{
    /// <summary>
    /// Jak rychle se hlášená hodnota přibližuje naměřené.
    ///
    /// <para>Vyhlazuje se, protože výroba běží v dávkách: jedna dílna dokončí
    /// cyklus v jednom tiku a pak deset tiků nic. Bez vyhlazení by ukazatel
    /// blikal mezi nulou a špičkou a nešlo by z něj číst vůbec nic.</para>
    ///
    /// <para>Číslo musí být <b>pomalejší než perioda dávek</b>, jinak vyhlazení
    /// nepomůže. Test to odhalil na hodnotě 0,12: mezi dvěma cykly (deset tiků)
    /// hodnota spadla na třetinu, takže ukazatel místo blikání aspoň dýchal —
    /// pořád ale nešlo přečíst, kolik se doopravdy vyrábí. Tady je časová
    /// konstanta zhruba tři sekundy, což přebije i pomalejší recepty.</para>
    /// </summary>
    private const double Smoothing = 0.03;

    /// <summary>Kolik druhů spotřeby se eviduje zvlášť.</summary>
    public const int KindCount = 6;

    private readonly double[] _producedTick;
    private readonly double[] _wastedTick;

    private readonly double[] _produced;
    private readonly double[] _consumed;
    private readonly double[] _wasted;

    /// <summary>Spotřeba po druzích: [druh][surovina]. Celková je jejich součet.</summary>
    private readonly double[][] _consumedByKindTick;
    private readonly double[][] _consumedByKind;

    public ResourceLedger(int resourceCount)
    {
        _producedTick = new double[resourceCount];
        _wastedTick = new double[resourceCount];
        _produced = new double[resourceCount];
        _consumed = new double[resourceCount];
        _wasted = new double[resourceCount];
        _consumedByKindTick = new double[KindCount][];
        _consumedByKind = new double[KindCount][];
        for (int k = 0; k < KindCount; k++)
        {
            _consumedByKindTick[k] = new double[resourceCount];
            _consumedByKind[k] = new double[resourceCount];
        }
    }

    /// <summary>Kolik surovin evidence sleduje.</summary>
    public int Count => _produced.Length;

    /// <summary>Zapíše vyrobené množství (to, co se opravdu vešlo do skladu).</summary>
    public void RecordProduced(int resourceIndex, double amount)
    {
        if (amount > 0)
        {
            _producedTick[resourceIndex] += amount;
        }
    }

    /// <summary>Zapíše spotřebu vstupů výrobního receptu.</summary>
    public void RecordConsumed(int resourceIndex, double amount) =>
        RecordConsumed(resourceIndex, amount, ConsumptionKind.Recipes);

    /// <summary>
    /// Zapíše spotřebu s důvodem. Každý odběr ze skladu sem musí dojít — jinak
    /// tooltip lže: u jídla ukazoval „spotřeba 0", zatímco ho lidé jedli
    /// a zásoba padala k nule.
    /// </summary>
    public void RecordConsumed(int resourceIndex, double amount, ConsumptionKind kind)
    {
        if (amount > 0)
        {
            _consumedByKindTick[(int)kind][resourceIndex] += amount;
        }
    }

    /// <summary>Zapíše, co se vyrobilo, ale nevešlo do skladu.</summary>
    public void RecordWasted(int resourceIndex, double amount)
    {
        if (amount > 0)
        {
            _wastedTick[resourceIndex] += amount;
        }
    }

    /// <summary>
    /// Uzavře tik: přepočte nasbírané množství na sekundu a přiblíží k němu
    /// hlášenou hodnotu. Volá se právě jednou za tik, na jeho konci.
    /// </summary>
    public void EndTick(double ticksPerSecond)
    {
        for (int i = 0; i < _produced.Length; i++)
        {
            Roll(_produced, _producedTick, i, ticksPerSecond);
            Roll(_wasted, _wastedTick, i, ticksPerSecond);

            double total = 0;
            for (int k = 0; k < KindCount; k++)
            {
                Roll(_consumedByKind[k], _consumedByKindTick[k], i, ticksPerSecond);
                total += _consumedByKind[k][i];
            }

            _consumed[i] = total;
        }
    }

    /// <summary>Kolik se suroviny vyrobí za sekundu.</summary>
    public double ProducedPerSecond(int resourceIndex) => _produced[resourceIndex];

    /// <summary>Kolik se jí za sekundu spotřebuje (všemi způsoby dohromady).</summary>
    public double ConsumedPerSecond(int resourceIndex) => _consumed[resourceIndex];

    /// <summary>Kolik se jí za sekundu spotřebuje daným způsobem.</summary>
    public double ConsumedPerSecond(int resourceIndex, ConsumptionKind kind) =>
        _consumedByKind[(int)kind][resourceIndex];


    /// <summary>Kolik jí za sekundu propadne do plného skladu.</summary>
    public double WastedPerSecond(int resourceIndex) => _wasted[resourceIndex];

    /// <summary>Čistý tok: co přibývá (kladné) nebo ubývá (záporné).</summary>
    public double NetPerSecond(int resourceIndex) =>
        _produced[resourceIndex] - _consumed[resourceIndex];

    /// <summary>Vyprázdní evidenci — po Vzestupu je předchozí ekonomika nezajímavá.</summary>
    public void Reset()
    {
        Array.Clear(_producedTick);
        Array.Clear(_wastedTick);
        Array.Clear(_produced);
        Array.Clear(_consumed);
        Array.Clear(_wasted);
        for (int k = 0; k < KindCount; k++)
        {
            Array.Clear(_consumedByKindTick[k]);
            Array.Clear(_consumedByKind[k]);
        }
    }

    private static void Roll(double[] reported, double[] tick, int index, double ticksPerSecond)
    {
        double measured = tick[index] * ticksPerSecond;
        reported[index] += (measured - reported[index]) * Smoothing;
        tick[index] = 0;
    }
}
