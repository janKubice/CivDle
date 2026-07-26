namespace CivDle.Core.Content;

/// <summary>
/// Landmark z <c>data/landmarks.json</c> (living-map.md §4): vzácný výrazný prvek,
/// co láme monotónnost nekonečné mapy a dává hráči orientační bod — gejzír, kaňon,
/// prastarý strom.
///
/// <para>Část landmarků je zároveň <b>surovinový uzel</b> (§3: „jelen v lese = maso"):
/// stádo, hejno ryb nebo bobulový háj dají při kliknutí víc než okolní biom. Tím
/// nejsou jen kulisa, ale i důvod někam zajít.</para>
///
/// Výskyt je čistá funkce pozice (hash), takže se nic negeneruje dopředu ani neukládá.
/// Jméno v jazycích pod <c>landmark.&lt;Id&gt;</c>.
/// </summary>
/// <param name="Id">Stabilní ID.</param>
/// <param name="BiomeMask">Ve kterých biomech se landmark vyskytuje.</param>
/// <param name="MapColor">Barva pro vykreslení.</param>
/// <param name="Size">Velikost kresby v pixelech (v rámci dlaždice).</param>
/// <param name="Rarity">Vyskytuje se zhruba na každé N-té vhodné dlaždici (větší = vzácnější).</param>
/// <param name="ClickYield">Co dá při ručním sběru; <c>null</c> = jen ozdoba.</param>
public sealed record LandmarkDef(
    string Id,
    IReadOnlyList<bool> BiomeMask,
    RgbColor MapColor,
    int Size,
    int Rarity,
    ClickYield? ClickYield)
{
    /// <summary>Lokalizační klíč jména landmarku.</summary>
    public string NameKey => $"landmark.{Id}";

    /// <summary>Dá se z landmarku něco sbírat (surovinový uzel), nebo je to jen ozdoba?</summary>
    public bool IsHarvestable => ClickYield is not null;

    /// <summary>Vyskytuje se v daném biomu?</summary>
    public bool AppliesTo(int biomeIndex) =>
        biomeIndex >= 0 && biomeIndex < BiomeMask.Count && BiomeMask[biomeIndex];
}
