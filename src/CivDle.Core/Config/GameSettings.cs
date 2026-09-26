namespace CivDle.Core.Config;

/// <summary>Režim okna hry.</summary>
public enum WindowMode
{
    /// <summary>Klasické okno s rámečkem.</summary>
    Windowed,

    /// <summary>Celá obrazovka bez přepnutí režimu monitoru (borderless).</summary>
    Borderless,

    /// <summary>Exkluzivní celá obrazovka.</summary>
    Fullscreen,
}

/// <summary>
/// Uživatelská nastavení hry (jazyk + grafika). Neměnný record — změna vytvoří
/// kopii přes <c>with</c>; ukládá <see cref="SettingsStore"/>.
/// </summary>
public sealed record GameSettings
{
    /// <summary>
    /// ID jazyka z <c>data/lang</c>; neznámé spadne na první dostupný. Při prvním
    /// spuštění ho hra vybere podle systému (<see cref="LanguagePicker"/>);
    /// tahle výchozí hodnota platí jen pro rozbitý soubor nastavení.
    /// </summary>
    public string Language { get; init; } = LanguagePicker.Fallback;

    /// <summary>Šířka okna/rozlišení v pixelech.</summary>
    public int ResolutionWidth { get; init; } = 1280;

    /// <summary>Výška okna/rozlišení v pixelech.</summary>
    public int ResolutionHeight { get; init; } = 720;

    /// <summary>Režim okna.</summary>
    public WindowMode WindowMode { get; init; } = WindowMode.Windowed;

    /// <summary>Vertikální synchronizace.</summary>
    public bool VSync { get; init; } = true;

    /// <summary>Hlasitost zvuků 0–1 (0 = ticho).</summary>
    public float MasterVolume { get; init; } = 0.7f;

    /// <summary>
    /// Zvětšení uživatelského rozhraní (1.0 = výchozí). HUD hry je hustý a na
    /// velkém rozlišení drobný — bez tohohle je pro slabozraké nečitelný.
    /// </summary>
    public float UiScale { get; init; } = 1.0f;

    /// <summary>
    /// Omezit pohyb: vypne poletující čísla, částice a chvění obrazu. Pro hráče
    /// citlivé na pohyb (vestibulární potíže) i pro ty, komu efekty překáží ve čtení.
    /// </summary>
    public bool ReduceMotion { get; init; }

    /// <summary>
    /// Nespoléhat jen na barvu: k zelené/červené ceně přidá i značku, takže
    /// „mám / nemám" pozná i hráč s poruchou barvocitu.
    /// </summary>
    public bool ColorCues { get; init; }

    /// <summary>
    /// Jak dlouho při oddalování vydrží detaily (LOD). Stroje se liší řádově,
    /// takže volbu má mít hráč, ne jedna konstanta v kódu.
    /// </summary>
    public DetailQuality Detail { get; init; } = DetailQuality.Balanced;

    /// <summary>
    /// V jakém rozlišení se ukládají fotky a renderuje video. Nezávisí na
    /// velikosti okna — hrát se dá v okně a fotit ve 4K.
    /// </summary>
    public CaptureResolution CaptureResolution { get; init; } = CaptureResolution.Qhd1440;

    /// <summary>
    /// Kreslit na fotce dole proužek se jménem města a čísly?
    ///
    /// <para>Na sdílení je proužek to hlavní — dá obrázku smysl i pro toho, kdo
    /// hru nezná. Do traileru a na store stránku se ale nehodí: tam má být
    /// vidět hra, ne cedule s čísly.</para>
    /// </summary>
    public bool CaptureStrip { get; init; } = true;

    /// <summary>
    /// Fotit s tilt-shiftem — rozostřeným okrajem, po kterém město vypadá jako
    /// model na stole?
    ///
    /// <para>Výchozí je vypnuto: je to výrazný zásah do obrázku a kdo chce
    /// ukázat, co postavil, chce většinou vidět celé město ostře. Zapíná se
    /// na jedno kliknutí, když jde o hezkou fotku, ne o důkaz.</para>
    /// </summary>
    public bool CaptureTiltShift { get; init; }

    /// <summary>
    /// Kreslit pod budovami stín?
    ///
    /// <para>Stín dává scéně hloubku, ale je to výrazný zásah do vzhledu
    /// a ne každému sedí. Vypnutí je legitimní volba, ne degradace — proto
    /// v nastavení a ne schované za stupněm detailu.</para>
    /// </summary>
    public bool Shadows { get; init; } = true;

    /// <summary>Povolený rozsah zvětšení UI (mimo něj by se rozbilo rozvržení).</summary>
    public const float MinUiScale = 0.8f;

    /// <summary>Povolený rozsah zvětšení UI.</summary>
    public const float MaxUiScale = 1.6f;

    /// <summary>Zvětšení UI oříznuté do povoleného rozsahu (ochrana proti ručně upravenému souboru).</summary>
    public float SafeUiScale => Math.Clamp(UiScale, MinUiScale, MaxUiScale);

    /// <summary>
    /// Zvětšení UI pro dané okno.
    ///
    /// <para>Na malé obrazovce se přidává samo. Steam Deck má 1280×800, tedy
    /// méně pixelů než dnešní monitory, ale drží se sedm palců od očí —
    /// rozhraní, které je na monitoru akorát, je na něm nečitelné. Myra sice
    /// škáluje, ale podle okna, ne podle toho, jak daleko se hráč dívá.</para>
    ///
    /// <para>Hráčovo vlastní nastavení se tím <b>násobí</b>, ne přebíjí: kdo si
    /// UI zmenšil, má ho menší i na Decku — jen ne tak, aby se do toho musel
    /// trefovat.</para>
    /// </summary>
    /// <param name="windowHeight">Výška okna v pixelech.</param>
    public float UiScaleFor(int windowHeight) => SafeUiScale * HandheldBoost(windowHeight);

    /// <summary>
    /// O kolik se přidá na malé obrazovce. Práh je 900 pixelů: nad ním jsou
    /// monitory, pod ním handheldy.
    /// </summary>
    public static float HandheldBoost(int windowHeight) => windowHeight <= 900 ? 1.25f : 1f;
}
