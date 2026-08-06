namespace CivDle.Screens;

/// <summary>
/// Rychlost běhu simulace — pauza, normál, zrychleno.
///
/// <para>Idle hra bez ovládání času je nepříjemná ve dvou situacích naráz:
/// když hráč čeká na výrobu a nudí se, a když chce něco v klidu rozmyslet
/// a město mu mezitím utíká. Násobič jde do <c>FixedStepLoop</c>, takže se mění
/// <b>kolik času smyčce nasypeme</b>, ne délka tiku — simulace zůstává
/// deterministická a tik pořád znamená totéž.</para>
///
/// <para>Vrstva: patří k obrazovce (ovládání), ne k simulaci. Simulace o žádné
/// rychlosti neví a vědět nemá.</para>
/// </summary>
public sealed class GameSpeed
{
    /// <summary>Dostupné násobiče. Nula = pauza.</summary>
    private static readonly double[] Steps = [0.0, 1.0, 2.0, 4.0];

    private int _index = 1;

    /// <summary>Kolikrát rychleji čas běží. 0 = stojí.</summary>
    public double Multiplier => Steps[_index];

    /// <summary>Stojí čas?</summary>
    public bool IsPaused => _index == 0;

    /// <summary>Popisek do tlačítka — „II" pro pauzu, jinak „1×", „2×", „4×".</summary>
    public string Label => IsPaused ? "II" : $"{Multiplier:0}×";

    /// <summary>Přepne na další rychlost dokola.</summary>
    public void Next() => _index = (_index + 1) % Steps.Length;

    /// <summary>
    /// Pauza jako přepínač: z běhu do pauzy a zpátky na tu rychlost, u které
    /// hráč byl. Bez zapamatování by se po pauze pokaždé vracel na 1×.
    /// </summary>
    public void TogglePause()
    {
        if (_index == 0)
        {
            _index = _resumeIndex;
            return;
        }

        _resumeIndex = _index;
        _index = 0;
    }

    private int _resumeIndex = 1;

    /// <summary>Kolik času nasypat do smyčky simulace za tenhle snímek.</summary>
    public double Scale(double elapsedSeconds) => elapsedSeconds * Multiplier;
}
