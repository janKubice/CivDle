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
    /// <summary>ID jazyka z <c>data/lang</c>; neznámé spadne na první dostupný.</summary>
    public string Language { get; init; } = "cs";

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
}
