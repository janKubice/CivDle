namespace CivDle.Core.Content;

/// <summary>
/// Jedno, co UFO při návštěvě provede. <see cref="Behavior"/> je behavior-ID
/// (data = co, kód = jak) — mapuje se v <see cref="Sim.UfoSystem"/> na konkrétní
/// zásah do světa. Neznámé chování se tiše přeskočí, aby data směla předběhnout kód.
///
/// <para>Jméno a hláška do zpráv jsou v jazycích pod <c>ufo.&lt;Id&gt;</c>.</para>
/// </summary>
/// <param name="Id">Stabilní ID (do savu i lokalizace).</param>
/// <param name="Behavior">Behavior-ID zásahu (abduct, demolish, plant, terraform, gift).</param>
/// <param name="Weight">Váha v losu — čím vyšší, tím častější.</param>
/// <param name="Magnitude">Síla zásahu (počet unesených lidí, velikost daru…).</param>
public sealed record UfoActionDef(string Id, string Behavior, double Weight, double Magnitude)
{
    /// <summary>Lokalizační klíč hlášky o tom, co UFO provedlo.</summary>
    public string MessageKey => $"ufo.{Id}";
}

/// <summary>
/// Nastavení návštěv UFO z <c>data/ufo.json</c> (living-map.md — mapa má občas
/// dělat něco sama od sebe). Návštěva je vzácná událost: v pravidelném okně padne
/// los a při úspěchu UFO přiletí, něco provede a zase zmizí.
/// </summary>
/// <param name="WindowSeconds">Jak často se losuje, jestli UFO přiletí.</param>
/// <param name="Chance">Pravděpodobnost návštěvy v jednom okně (0–1).</param>
/// <param name="VisitSeconds">Jak dlouho je UFO vidět nad mapou.</param>
/// <param name="Radius">V jakém okruhu od města UFO přistává (dlaždice).</param>
/// <param name="Actions">Co UFO umí; váhy se losují dohromady.</param>
public sealed record UfoConfig(
    double WindowSeconds,
    double Chance,
    double VisitSeconds,
    int Radius,
    IReadOnlyList<UfoActionDef> Actions)
{
    /// <summary>Prázdné nastavení — UFO ve hře není (volitelný obsah).</summary>
    public static UfoConfig Disabled { get; } = new(600, 0, 0, 0, Array.Empty<UfoActionDef>());

    /// <summary>Má smysl UFO vůbec simulovat?</summary>
    public bool IsEnabled => Chance > 0 && Actions.Count > 0;
}
