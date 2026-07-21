using CivDle.Core.Sim;

namespace CivDle.Core.Content;

/// <summary>
/// Definice achievementu z <c>data/achievements.json</c>. Odemkne se jednou
/// (napříč hrami/érami), když metrika dosáhne prahu — bez odměny, jen záznam
/// a toast. Stabilní <see cref="Id"/> je klíč i pro budoucí napojení na Steam.
/// Jméno a popis v jazycích pod <c>achievement.&lt;Id&gt;</c> / <c>.desc</c>.
/// </summary>
/// <param name="Id">Stabilní ID (profil + budoucí Steam mapping).</param>
/// <param name="Condition">Podmínka odemčení (metrika ≥ práh).</param>
/// <param name="Hidden">Skrytý (dokud není odemčený, ukáže se jen „???").</param>
public sealed record AchievementDef(string Id, GoalCondition Condition, bool Hidden)
{
    /// <summary>Lokalizační klíč jména achievementu.</summary>
    public string NameKey => $"achievement.{Id}";

    /// <summary>Lokalizační klíč popisu achievementu.</summary>
    public string DescriptionKey => $"achievement.{Id}.desc";
}
