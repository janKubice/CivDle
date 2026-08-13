using CivDle.Core.Config;

namespace CivDle.Capture;

/// <summary>
/// Jak se má snímek vyrobit: jak velký, s proužkem nebo bez, s LOD nebo bez.
///
/// <para>Proč to není jen pár parametrů metody: focení má tři nezávislé volby,
/// které se různě kombinují (fotka do obchodu = velká, bez proužku, bez LOD;
/// fotka na fórum = menší, s proužkem). Pojmenovaná struktura z toho udělá
/// jedno rozhodnutí místo tří booleanů na volání, u kterých si nikdo
/// nepamatuje pořadí.</para>
/// </summary>
/// <param name="Width">Šířka výsledného obrázku v pixelech.</param>
/// <param name="Height">Výška včetně proužku, pokud se kreslí.</param>
/// <param name="WithStrip">Kreslit dole proužek se jménem města a čísly?</param>
/// <param name="FullDetail">Renderovat bez LOD, tedy se vším, co je při daném oddálení schované?</param>
public readonly record struct ShareCardOptions(int Width, int Height, bool WithStrip, bool FullDetail)
{
    /// <summary>
    /// Výška, na kterou byl proužek nakreslený. Rozměry v něm se podle ní
    /// přepočítávají, aby ve 4K nebyl proužek pruh o výšce vlasu.
    /// </summary>
    public const int ReferenceHeight = 900;

    /// <summary>Výška proužku při <see cref="ReferenceHeight"/>.</summary>
    public const int ReferenceStripHeight = 96;

    /// <summary>Rozměry daného stupně. Vždy 16:9 — sdílí se do příspěvků a na Steam.</summary>
    public static (int Width, int Height) SizeOf(CaptureResolution resolution) => resolution switch
    {
        CaptureResolution.Hd1080 => (1920, 1080),
        CaptureResolution.Uhd4K => (3840, 2160),
        _ => (2560, 1440),
    };

    /// <summary>Sestaví volby pro daný stupeň rozlišení.</summary>
    public static ShareCardOptions For(CaptureResolution resolution, bool withStrip, bool fullDetail)
    {
        var (width, height) = SizeOf(resolution);
        return new ShareCardOptions(width, height, withStrip, fullDetail);
    }

    /// <summary>Jak vysoký je proužek při tomhle rozlišení (0, když se nekreslí).</summary>
    public int StripHeight => WithStrip
        ? (int)Math.Round(ReferenceStripHeight * (Height / (double)ReferenceHeight))
        : 0;

    /// <summary>Výška samotné scény — obrázek bez proužku.</summary>
    public int SceneHeight => Math.Max(1, Height - StripHeight);

    /// <summary>
    /// Násobek, kterým se škáluje text a odsazení v proužku, aby vypadal
    /// stejně při 1080p i ve 4K.
    /// </summary>
    public float Scale => Height / (float)ReferenceHeight;

    /// <summary>
    /// Zoom, kterým se má scéna nakreslit, aby zůstal <b>stejný výřez světa</b>
    /// jako na obrazovce — jen do víc pixelů.
    ///
    /// <para>Tohle je jádro celé věci. Kdyby se zoom nepřepočítal, byla by
    /// fotka ve 4K prostě „víc světa" v témž měřítku, ne ostřejší obrázek
    /// téhož záběru — a hráč čekal to druhé.</para>
    /// </summary>
    /// <param name="sourceZoom">Zoom kamery, kterou se hráč dívá.</param>
    /// <param name="sourceViewportHeight">Výška okna hry v pixelech.</param>
    public float ZoomFor(float sourceZoom, int sourceViewportHeight) =>
        sourceZoom * (SceneHeight / (float)Math.Max(1, sourceViewportHeight));
}
