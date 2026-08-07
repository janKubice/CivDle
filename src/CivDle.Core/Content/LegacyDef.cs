using CivDle.Core.Sim;

namespace CivDle.Core.Content;

/// <summary>
/// Odkaz — <b>druhá prestižní vrstva</b> nad Vzestupem.
///
/// <para>Proč vůbec existuje: Vzestup má klesající výnos (odmocnina), takže po
/// pár desítkách běhů přestane být čím dál rychlejší a hráč se dostane do
/// stavu, kdy další Vzestup vypadá skoro stejně jako předchozí. To je moment,
/// kdy idle hry buď skončí, nebo otevřou vrstvu nad tím. Odkaz je ta vrstva:
/// smaže <b>i</b> Vzestupy a jejich upgrady a výměnou dá měnu, která zrychluje
/// samotné vzestupování — ne výrobu.</para>
///
/// <para>Klíčové je, že upgrady Odkazu míří <b>na jinou osu</b>: násobí body
/// Vzestupu a zlevňují jeho práh. Kdyby jen dávaly „ještě víc výroby", byl by
/// Odkaz jen dražší Vzestup a hráč by neměl důvod ho udělat.</para>
/// </summary>
/// <param name="Requirement">Podmínka prvního Odkazu (typicky počet Vzestupů).</param>
/// <param name="RequirementGrowth">Kolikrát je každý další Odkaz náročnější.</param>
/// <param name="PointsMetric">Metrika, ze které se počítají body Odkazu.</param>
/// <param name="PointsParam">Doplňující index metriky (surovina…), nebo −1.</param>
/// <param name="PointsDivisor">Body = (metrika ÷ dělitel) na <paramref name="PointsExponent"/>.</param>
/// <param name="PointsExponent">
/// Mocnina výnosu. Na rozdíl od Vzestupu tu dává smysl <b>víc než 1</b>: metrika
/// (počet Vzestupů) sama roste zhruba logaritmicky s časem, takže lineární výnos
/// by z Odkazu udělal krok, který se nikdy nevyplatí opakovat.
/// </param>
public sealed record LegacyConfig(
    GoalCondition Requirement,
    double RequirementGrowth,
    MetricKind PointsMetric,
    int PointsParam,
    long PointsDivisor,
    double PointsExponent)
{
    /// <summary>Prázdná (vypnutá) vrstva — hra bez <c>legacy.json</c> běží dál.</summary>
    public static LegacyConfig Disabled { get; } = new(
        new GoalCondition(MetricKind.AscensionLevel, -1, 0),
        RequirementGrowth: 1.0,
        MetricKind.AscensionLevel,
        PointsParam: -1,
        PointsDivisor: 1,
        PointsExponent: 1.0);

    /// <summary>Je vrstva v datech zapnutá? (Práh 0 = vypnuto.)</summary>
    public bool IsEnabled => Requirement.Target > 0;
}
