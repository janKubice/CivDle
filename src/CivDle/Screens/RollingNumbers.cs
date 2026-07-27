namespace CivDle.Screens;

/// <summary>
/// Dojíždějící počítadla surovin pro HUD.
///
/// <para>Čísla se dřív nastavovala přímo, takže skákala. V idle hře je přitom
/// počítadlo samo o sobě odměna — když se hodnota plynule dotáčí nahoru, hráč
/// vidí, že město pracuje, i když se zrovna nic jiného neděje.</para>
///
/// <para>Dorovnání je exponenciální (procento zbývajícího rozdílu za sekundu),
/// takže velký skok dojede rychle a doladění je jemné. Když je rozdíl velký nebo
/// hodnota klesá (utracení surovin), skočí se rovnou — utracení má být okamžité
/// a čekat na dojezd milionu by trvalo věčnost.</para>
/// </summary>
public sealed class RollingNumbers
{
    /// <summary>
    /// Rychlost dojezdu (1/s). Vyšší číslo = svižnější dotáčení; při 8 je rozdíl
    /// po vteřině zhruba na třech promilích, takže je to plynulé a přitom rychlé.
    /// </summary>
    private const double CatchUpRate = 8.0;

    /// <summary>Nad tímhle poměrem rozdílu ke skutečné hodnotě se skočí rovnou.</summary>
    private const double SnapRatio = 0.5;

    private readonly double[] _shown;
    private readonly float[] _flash;

    public RollingNumbers(int count)
    {
        _shown = new double[count];
        _flash = new float[count];
    }

    /// <summary>Hodnota, kterou má HUD vypsat (dojíždí ke skutečné).</summary>
    public double Shown(int index) => _shown[index];

    /// <summary>Síla záblesku 0–1 po přírůstku (HUD podle ní rozsvítí čip).</summary>
    public float Flash(int index) => _flash[index];

    /// <summary>Nastaví počítadla rovnou na skutečné hodnoty (start hry, načtení savu).</summary>
    public void SnapTo(Func<int, double> actual)
    {
        for (int i = 0; i < _shown.Length; i++)
        {
            _shown[i] = actual(i);
            _flash[i] = 0f;
        }
    }

    /// <summary>Posune počítadla ke skutečným hodnotám a zháší záblesky.</summary>
    public void Update(float dt, Func<int, double> actual)
    {
        // Rámcově nezávislé na délce snímku: podíl uzavřeného rozdílu za dt.
        double catchUp = 1.0 - Math.Exp(-CatchUpRate * dt);
        for (int i = 0; i < _shown.Length; i++)
        {
            double target = actual(i);
            double difference = target - _shown[i];

            if (difference > 0 && _shown[i] > 0 && difference / Math.Max(1.0, target) > SnapRatio)
            {
                _shown[i] = target; // velký skok (odměna, načtení) — dojíždět by trvalo věčnost
            }
            else if (difference < 0)
            {
                _shown[i] = target; // utracení musí být vidět hned
            }
            else if (Math.Abs(difference) < 0.01)
            {
                _shown[i] = target;
            }
            else
            {
                _shown[i] += difference * catchUp;
                _flash[i] = 1f;
            }

            _flash[i] = Math.Max(0f, _flash[i] - dt * 3f);
        }
    }
}
