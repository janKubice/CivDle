namespace CivDle.Core.Content;

/// <summary>
/// Politika růstu z <c>data/policies.json</c> (automatizace, stupeň 4 dle
/// living-city.md). Globální pravidlo, které hráč zapíná/vypíná; moduluje
/// chování auto-stavby a plnění zón — „hráč nastavuje principy, civilizace se
/// řídí sama".
///
/// <para><see cref="Effect"/> je behavior-ID (data = co, kód = jak): mapuje se
/// v <see cref="Sim.Simulation"/> na odvozené parametry růstu (viz
/// RecomputePolicyEffects). <see cref="Magnitude"/> je volitelná síla efektu.
/// Neznámý efekt se při přepočtu tiše ignoruje — data smí předběhnout kód.</para>
/// Jméno/popis v jazycích pod <c>policy.&lt;Id&gt;</c> a <c>policy.&lt;Id&gt;.desc</c>.
/// </summary>
/// <param name="Id">Stabilní ID.</param>
/// <param name="Effect">Behavior-ID efektu (např. „build_pace", „housing_density").</param>
/// <param name="Magnitude">Síla efektu (význam závisí na efektu; 0 = neškálované).</param>
public sealed record GrowthPolicyDef(string Id, string Effect, double Magnitude)
{
    /// <summary>Lokalizační klíč jména politiky.</summary>
    public string NameKey => $"policy.{Id}";

    /// <summary>Lokalizační klíč popisu politiky.</summary>
    public string DescriptionKey => $"policy.{Id}.desc";
}
