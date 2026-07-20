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
public sealed record FaunaDef(
    string Id,
    bool[] BiomeMask,
    RgbColor Color,
    int Size,
    float Speed,
    FaunaTime Time,
    bool Glow);
