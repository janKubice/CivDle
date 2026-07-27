using CivDle.Core.Sim;

namespace CivDle.Core.Content;

/// <summary>
/// Jedna denní výzva z <c>data/challenges.json</c>. Na rozdíl od úkolu se neplní
/// jednou za hru, ale opakovaně — každý reálný den se z fondu vybere pár výzev
/// a hráč je splní tím, co dnes ve městě udělá.
///
/// <para>Existuje jako důvod vrátit se zítra: denní odměna se dá vyzvednout
/// a zavřít hru, výzva vyžaduje si zahrát. Text v jazycích pod
/// <c>challenge.&lt;Id&gt;</c> / <c>.desc</c>.</para>
/// </summary>
/// <param name="Id">Stabilní ID (do profilu i lokalizace).</param>
/// <param name="Condition">Co se má za den zvládnout.</param>
/// <param name="Reward">Odměna v surovinách.</param>
public sealed record ChallengeDef(
    string Id,
    GoalCondition Condition,
    IReadOnlyList<ResourceAmount> Reward)
{
    /// <summary>Lokalizační klíč jména výzvy.</summary>
    public string NameKey => $"challenge.{Id}";

    /// <summary>Lokalizační klíč popisu výzvy.</summary>
    public string DescriptionKey => $"challenge.{Id}.desc";
}

/// <summary>Nastavení denních výzev z <c>data/challenges.json</c>.</summary>
/// <param name="Challenges">Fond, ze kterého se denně vybírá.</param>
/// <param name="DailyCount">Kolik výzev je aktivních současně.</param>
public sealed record ChallengeCatalog(IReadOnlyList<ChallengeDef> Challenges, int DailyCount)
{
    /// <summary>Prázdný katalog — hra bez denních výzev.</summary>
    public static ChallengeCatalog Empty { get; } = new(Array.Empty<ChallengeDef>(), 0);

    /// <summary>Má smysl výzvy vůbec počítat?</summary>
    public bool IsEnabled => Challenges.Count > 0 && DailyCount > 0;
}
