namespace CivDle.Core.Content;

/// <summary>
/// Atmosféra světa (svety-design.md 5.1): světlo během dne, celkový nádech,
/// částice ve vzduchu, noční obloha a akcent rozhraní. Každý svět má poznat
/// hráč <b>podle světla dřív než podle budov</b> — a to je data, ne kód.
///
/// <para>Render profil jen čte. Domovina má profil, který přesně odpovídá
/// dřívějším konstantám v <c>DayNightCycle</c> (regresní test to hlídá),
/// takže první kapitola vypadá stejně jako dřív.</para>
/// </summary>
/// <param name="Id">ID profilu (<c>home</c>, <c>dune</c>…).</param>
/// <param name="Morning">Barva ranního světla (kotva gradingu).</param>
/// <param name="Noon">Barva poledního světla.</param>
/// <param name="Evening">Barva večerního světla.</param>
/// <param name="MorningAlpha">Síla ranního nádechu (0–0,5).</param>
/// <param name="EveningAlpha">Síla večerního nádechu (0–0,5).</param>
/// <param name="Tint">Celkový nádech světa (násobí světlo celý den).</param>
/// <param name="TintStrength">Síla nádechu (0 = žádný, 1 = plná barva).</param>
/// <param name="Particles">Co poletuje vzduchem (<see cref="ParticleKinds"/>).</param>
/// <param name="ParticleDensity">Hustota částic (0–1).</param>
/// <param name="Aurora">Polární záře v noci.</param>
/// <param name="Moons">Kolik měsíců je v noci vidět (0–4).</param>
/// <param name="Ring">Prstenec přes oblohu.</param>
/// <param name="SecondSun">Druhé slunce (barva); <c>null</c> = jedno.</param>
/// <param name="HudAccent">Akcent rozhraní světa.</param>
public sealed record AtmosphereProfile(
    string Id,
    RgbColor Morning,
    RgbColor Noon,
    RgbColor Evening,
    double MorningAlpha,
    double EveningAlpha,
    RgbColor Tint,
    double TintStrength,
    string Particles,
    double ParticleDensity,
    bool Aurora,
    int Moons,
    bool Ring,
    RgbColor? SecondSun,
    RgbColor HudAccent)
{
    /// <summary>Známé druhy částic — render je umí nakreslit, loader jiné odmítne.</summary>
    public static IReadOnlySet<string> ParticleKinds { get; } =
        new HashSet<string>(StringComparer.Ordinal) { "none", "sand", "snow", "ash", "spores", "pollen", "mist" };

    /// <summary>
    /// Vzhled Domoviny přesně podle dřívějších konstant — obsah bez
    /// <c>atmospheres.json</c> (starší data, mody, testy) vypadá jako dřív.
    /// </summary>
    public static AtmosphereProfile Home { get; } = new(
        "home",
        new RgbColor(255, 196, 128), new RgbColor(255, 252, 240), new RgbColor(126, 118, 210),
        0.10, 0.12,
        new RgbColor(255, 255, 255), 0,
        "none", 0,
        false, 0, false, null,
        new RgbColor(96, 196, 220));

    /// <summary>Poletuje něco vzduchem?</summary>
    public bool HasParticles => Particles != "none" && ParticleDensity > 0;
}
