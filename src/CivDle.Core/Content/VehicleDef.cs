namespace CivDle.Core.Content;

/// <summary>
/// Vozidlo, které jezdí po hráčových silnicích — kára, povoz, náklaďák, vznášedlo.
///
/// <para>Proč je to obsah v JSON, a ne v kódu: doprava je ta nejlevnější
/// „hra žije" zpětná vazba, jakou město má, a její tvář se s érou mění.
/// Přidat do bronzové doby oslí povoz nebo do budoucnosti dron má být otázka
/// jednoho záznamu v datech, ne zásahu do renderu (data = co, kód = jak).</para>
///
/// <para>Vozidla jsou <b>kulisa</b>, ne simulace: nic nevyrábějí, nic nevozí,
/// jen zabydlují mapu u kamery (stejný princip jako fauna, viz living-map.md).
/// Proto tu není nosnost ani náklad — kdyby vozidlo něco doopravdy převáželo,
/// patřilo by do simulace, ne sem.</para>
/// </summary>
/// <param name="Id">Stabilní ID.</param>
/// <param name="Color">Barva korby (fallback, když chybí sprite).</param>
/// <param name="Width">Šířka v pixelech.</param>
/// <param name="Length">Délka v pixelech (kreslí se po směru jízdy).</param>
/// <param name="Speed">Rychlost v pixelech za sekundu.</param>
/// <param name="MinEraOrder">Od jaké éry se na silnicích objevuje.</param>
/// <param name="MaxEraOrder">Do jaké éry ještě jezdí; −1 = navždy.</param>
/// <param name="Glow">Svítí v noci? (Náklaďák má reflektory, kára ne.)</param>
public sealed record VehicleDef(
    string Id,
    RgbColor Color,
    int Width,
    int Length,
    float Speed,
    int MinEraOrder,
    int MaxEraOrder,
    bool Glow)
{
    /// <summary>
    /// Jezdí tenhle typ v dané éře?
    ///
    /// <para>Kulisa nemá vlastní jméno v jazycích (stejně jako fauna) — hráč
    /// vozidla vidí, nečte o nich.</para>
    /// </summary>
    public bool FitsEra(int eraOrder) =>
        eraOrder >= MinEraOrder && (MaxEraOrder < 0 || eraOrder <= MaxEraOrder);
}
