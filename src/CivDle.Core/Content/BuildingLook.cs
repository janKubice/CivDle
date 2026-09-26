namespace CivDle.Core.Content;

/// <summary>
/// Jak budova vypadá — popsané daty, nakreslené kódem.
///
/// <para><b>Proč:</b> sprity se kreslí v kódu (<c>SpriteLibrary</c>) a každá
/// budova měla vlastní ručně psanou kresbu. Pro pozdní hru a nové světy
/// přibývá přes sto budov; psát každou zvlášť by bylo pomalé a nejednotné.
/// Vzhled se proto skládá z <b>tvaru</b> (věž, kopule, kůly, balon…),
/// <b>barev</b> a <b>prvků</b> (okna, komín, světla, rostliny…). Data říkají
/// „co" (jaký tvar a barvy), render „jak" (malíř tvarů v
/// <c>CivDle.Rendering.Sprites.LookPainter</c>) — v datech žádná kreslicí
/// logika.</para>
///
/// <para>Tvary a prvky jsou pevný katalog (<see cref="KnownShapes"/>,
/// <see cref="KnownFeatures"/>); loader odmítne neznámé jméno hned při startu
/// a test v UI ověří, že malíř umí každé z nich.</para>
/// </summary>
/// <param name="Shape">Tvar z <see cref="KnownShapes"/>.</param>
/// <param name="Wall">Hlavní barva (zdi, trup, kmen).</param>
/// <param name="Roof">Druhá barva (střecha, kopule, koruna).</param>
/// <param name="Accent">Doplňková barva (dveře, lemy, rostliny, voda).</param>
/// <param name="Glow">Barva světel a svítících částí; <c>null</c> = teplé žluté světlo.</param>
/// <param name="Features">Prvky z <see cref="KnownFeatures"/>, v pořadí kreslení.</param>
public sealed record BuildingLook(
    string Shape,
    RgbColor Wall,
    RgbColor Roof,
    RgbColor Accent,
    RgbColor? Glow,
    IReadOnlyList<string> Features)
{
    /// <summary>Tvary, které malíř umí.</summary>
    public static readonly IReadOnlySet<string> KnownShapes = new HashSet<string>(StringComparer.Ordinal)
    {
        "hut", "house", "tower", "dome", "hall", "workshop", "tanks", "pit", "field", "grove",
        "stilts", "raft", "balloon", "mast", "column", "obelisk", "arch", "crystal", "bulb", "tree",
        "wall", "channel", "pool", "platform", "vortex", "ring", "mirrors", "pier", "rig", "pods",
    };

    /// <summary>Prvky, které malíř umí přikreslit k tvaru.</summary>
    public static readonly IReadOnlySet<string> KnownFeatures = new HashSet<string>(StringComparer.Ordinal)
    {
        "windows", "chimney", "antenna", "flag", "lanterns", "glow", "plants", "snow", "pipes",
        "sails", "solar", "crystals", "stripes", "rings", "flat_roof", "dome_roof", "thatch_roof",
        "spire_top", "steam", "water", "sand", "arches", "tendrils", "rotor", "spark", "ropes",
    };

    /// <summary>Má vzhled daný prvek?</summary>
    public bool Has(string feature) => Features.Contains(feature, StringComparer.Ordinal);
}
