namespace CivDle.Core.Content;

/// <summary>Kdy se tvor objevuje (living-map.md: život podle času).</summary>
public enum FaunaTime
{
    /// <summary>Jen ve dne.</summary>
    Day,

    /// <summary>Jen v noci (světlušky, sovy).</summary>
    Night,

    /// <summary>Kdykoli.</summary>
    Any,
}

/// <summary>
/// Ambientní fauna z <c>data/fauna.json</c> — kulisa, ne simulace: tvorové
/// existují jen u kamery (LOD), render je spawnuje a hýbe jimi sám.
/// </summary>
/// <param name="Id">Stabilní ID.</param>
/// <param name="BiomeMask">Biomy, kde se tvor vyskytuje.</param>
/// <param name="Color">Barva tvora (MVP vizuál).</param>
/// <param name="Size">Velikost v pixelech dlaždice.</param>
/// <param name="Speed">Rychlost pohybu ve world pixelech za sekundu.</param>
/// <param name="Time">Denní doba výskytu.</param>
/// <param name="Glow">Svítí (světlušky) — kreslí se s pulzující jasnou barvou.</param>
/// <param name="Herd">
/// Kolik kusů se objeví pohromadě. Srnec sám uprostřed pláně je tečka; stádo,
/// které se táhne přes louku, je výjev. 1 = samotář (liška, sova).
/// </param>
/// <param name="Shy">
/// Utíká tenhle druh před lidmi? Plachost je nejlevnější způsob, jak dát
/// zvířeti <b>reakci</b> — a bez reakce nevypadá živě nic, ať se hýbe jakkoli.
/// Vrabec ne, srnec ano.
/// </param>
/// <param name="Predator">
/// Loví tenhle druh ostatní? Plachá zvířata před ním utíkají stejně jako
/// před člověkem.
///
/// <para>Je to ta nejlevnější věc, která ze savany udělá ekosystém místo
/// zoo: stádo gazel, které se dá na útěk před lvem, vypráví příběh, aniž by
/// se cokoli simulovalo. Nikdo nikoho nechytá — jde o <b>reakci</b>, ne
/// o predaci.</para>
/// </param>
public sealed record FaunaDef(
    string Id,
    bool[] BiomeMask,
    RgbColor Color,
    int Size,
    float Speed,
    FaunaTime Time,
    bool Glow,
    int Herd = 1,
    bool Shy = false,
    bool Predator = false);
