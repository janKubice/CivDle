namespace CivDle.Core.Sim;

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

    private readonly double[] _producedTick;
    private readonly double[] _consumedTick;
    private readonly double[] _wastedTick;

    private readonly double[] _produced;
    private readonly double[] _consumed;
    private readonly double[] _wasted;

    public ResourceLedger(int resourceCount)
    {
        _producedTick = new double[resourceCount];
        _consumedTick = new double[resourceCount];
        _wastedTick = new double[resourceCount];
        _produced = new double[resourceCount];
        _consumed = new double[resourceCount];
        _wasted = new double[resourceCount];
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

    /// <summary>Zapíše spotřebu — vstupy receptů i zaplacené stavby a výzkumy.</summary>
    public void RecordConsumed(int resourceIndex, double amount)
    {
        if (amount > 0)
        {
            _consumedTick[resourceIndex] += amount;
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
            Roll(_consumed, _consumedTick, i, ticksPerSecond);
            Roll(_wasted, _wastedTick, i, ticksPerSecond);
        }
    }

    /// <summary>Kolik se suroviny vyrobí za sekundu.</summary>
    public double ProducedPerSecond(int resourceIndex) => _produced[resourceIndex];

    /// <summary>Kolik se jí za sekundu spotřebuje.</summary>
    public double ConsumedPerSecond(int resourceIndex) => _consumed[resourceIndex];

    /// <summary>Kolik jí za sekundu propadne do plného skladu.</summary>
    public double WastedPerSecond(int resourceIndex) => _wasted[resourceIndex];

    /// <summary>Čistý tok: co přibývá (kladné) nebo ubývá (záporné).</summary>
    public double NetPerSecond(int resourceIndex) =>
        _produced[resourceIndex] - _consumed[resourceIndex];

    /// <summary>Vyprázdní evidenci — po Vzestupu je předchozí ekonomika nezajímavá.</summary>
    public void Reset()
    {
        Array.Clear(_producedTick);
        Array.Clear(_consumedTick);
        Array.Clear(_wastedTick);
        Array.Clear(_produced);
        Array.Clear(_consumed);
        Array.Clear(_wasted);
    }

    private static void Roll(double[] reported, double[] tick, int index, double ticksPerSecond)
    {
        double measured = tick[index] * ticksPerSecond;
        reported[index] += (measured - reported[index]) * Smoothing;
        tick[index] = 0;
    }
}
