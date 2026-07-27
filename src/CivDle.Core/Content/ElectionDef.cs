namespace CivDle.Core.Content;

/// <summary>
/// Co zvolený program městu přinese. Behavior-ID z dat se mapuje na tenhle
/// výčet v loaderu, takže v JSON zůstává jen řetězec a všechna logika je v kódu.
/// </summary>
public enum ElectionEffect
{
    /// <summary>Násobič výroby všech budov.</summary>
    Production,

    /// <summary>Násobič rychlosti růstu populace.</summary>
    Growth,

    /// <summary>Násobič ručního sběru.</summary>
    Harvest,

    /// <summary>Sleva na výzkum (podíl ceny).</summary>
    Research,

    /// <summary>Přičte se ke spokojenosti (0.1 = +10 p. b.).</summary>
    Happiness,
}

/// <summary>
/// Jeden kandidát ve volbách z <c>data/elections.json</c>: program a to, co
/// z něj město má po dobu volebního období.
///
/// <para>Volby existují jako pravidelné rozhodnutí, které běží na pozadí a nic
/// nezdržuje — hráč si vybere směr na pár herních dní a pak zas. Žádná volba
/// není trestem, jen jiný důraz; to sedí k relaxačnímu tónu hry.</para>
///
/// <para>Jméno a slib v jazycích pod <c>election.&lt;Id&gt;</c> / <c>.desc</c>.</para>
/// </summary>
/// <param name="Id">Stabilní ID.</param>
/// <param name="Effect">Co program dělá.</param>
/// <param name="Magnitude">Jak silně (násobič se počítá jako 1 + magnitude).</param>
public sealed record ElectionCandidateDef(string Id, ElectionEffect Effect, double Magnitude)
{
    /// <summary>Lokalizační klíč jména programu.</summary>
    public string NameKey => $"election.{Id}";

    /// <summary>Lokalizační klíč slibu.</summary>
    public string DescriptionKey => $"election.{Id}.desc";
}

/// <summary>Nastavení voleb z <c>data/elections.json</c>.</summary>
/// <param name="Candidates">Fond programů, ze kterého se sestavuje kandidátka.</param>
/// <param name="TermDays">Kolik herních dní trvá volební období.</param>
/// <param name="BallotSize">Kolik programů je na kandidátce.</param>
public sealed record ElectionConfig(
    IReadOnlyList<ElectionCandidateDef> Candidates,
    int TermDays,
    int BallotSize)
{
    /// <summary>Vypnuté volby — hra bez téhle vrstvy.</summary>
    public static ElectionConfig Disabled { get; } = new(Array.Empty<ElectionCandidateDef>(), 0, 0);

    /// <summary>Mají se volby vůbec konat?</summary>
    public bool IsEnabled => Candidates.Count > 0 && TermDays > 0 && BallotSize > 0;
}
