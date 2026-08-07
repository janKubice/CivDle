using CivDle.Core.Sim;

namespace CivDle.Core.Content;

/// <summary>
/// Nastavení Vzestupu (prestige) z <c>data/prestige.json</c>: kdy je Vzestup
/// dostupný a kolik bodů dá. Vzestup zresetuje éru (mapu), ale trvalé upgrady
/// a body zůstávají — progrese přes měřítko (viz progression-prestige.md).
/// </summary>
/// <param name="Requirement">Podmínka PRVNÍHO Vzestupu (metrika ≥ práh).</param>
/// <param name="PointsMetric">Metrika, ze které se počítají body Vzestupu.</param>
/// <param name="PointsParam">Doplňující index metriky bodů (surovina…), nebo −1.</param>
/// <param name="PointsDivisor">Body = hodnota metriky ÷ tenhle dělitel (celočíselně).</param>
/// <param name="RequirementGrowth">
/// Kolikrát je každý další Vzestup náročnější (1.0 = pořád stejně). Bez růstu by
/// druhý Vzestup přišel hned po prvním a celá progrese by ztratila smysl.
/// </param>
/// <param name="PointsExponent">
/// Mocnina, kterou se poměr „metrika ÷ dělitel" umocní. 1,0 = lineárně (dvakrát
/// delší běh, dvakrát víc bodů), 0,5 = odmocnina.
///
/// <para>Odmocnina je tu podstatná: při lineárním výnosu se vždycky vyplatí
/// hrát dál a rozhodnutí „resetnout teď, nebo ještě vydržet?" neexistuje —
/// přitom právě to je celý smysl prestiže. S klesajícím výnosem má každý běh
/// bod, po kterém se čekání přestane vyplácet.</para>
/// </param>
public sealed record PrestigeConfig(
    GoalCondition Requirement,
    MetricKind PointsMetric,
    int PointsParam,
    long PointsDivisor,
    double RequirementGrowth = 1.0,
    double PointsExponent = 1.0);

/// <summary>
/// Jeden trvalý upgrade za body Vzestupu. <see cref="Effect"/> je behavior-ID
/// (řetězec mapovaný v kódu na konkrétní bonus — data-driven pravidlo z CLAUDE.md),
/// <see cref="Magnitude"/> jeho síla (0.30 = +30 %). Odemyká se po prerekvizitách.
/// Jméno a popis v jazycích pod <c>prestige.&lt;Id&gt;</c> / <c>.desc</c>.
/// </summary>
/// <param name="Id">Stabilní ID (do savu i lokalizace).</param>
/// <param name="Effect">Behavior-ID efektu (production_mult, harvest_mult, …).</param>
/// <param name="Magnitude">Síla efektu (přičítá se k násobiči, 0.3 = +30 %).</param>
/// <param name="Cost">Cena první úrovně v bodech Vzestupu.</param>
/// <param name="PrerequisiteIndices">Indexy upgradů, které musí předcházet.</param>
/// <param name="MaxLevel">
/// Kolikrát jde upgrade koupit. 1 = jednorázový (staré chování).
///
/// <para>Opakovatelné upgrady jsou to, co dělá z prestiže nekonečnou osu:
/// s dvaceti jednorázovými uzly má strom pevný strop a po pár Vzestupech není
/// co kupovat.</para>
/// </param>
/// <param name="CostGrowth">Kolikrát je každá další úroveň dražší (1,15 = +15 %).</param>
public sealed record PrestigeUpgradeDef(
    string Id,
    string Effect,
    double Magnitude,
    int Cost,
    IReadOnlyList<int> PrerequisiteIndices,
    int MaxLevel = 1,
    double CostGrowth = 1.0)
{
    /// <summary>Dá se kupovat opakovaně?</summary>
    public bool IsRepeatable => MaxLevel > 1;

    /// <summary>
    /// Cena další úrovně. Roste geometricky, takže i „nekonečný" upgrade má
    /// přirozenou brzdu — desátá úroveň stojí násobně víc než první.
    /// </summary>
    public long CostAtLevel(int level) =>
        (long)Math.Round(Cost * Math.Pow(CostGrowth <= 0 ? 1.0 : CostGrowth, level));

    /// <summary>
    /// Násobič, který upgrade na dané úrovni dává. <b>Skládá se mocninou</b>,
    /// ne součtem: deset úrovní po +30 % je ×13,8, ne ×4. Přesně tenhle
    /// compounding žene čísla nahoru způsobem, kvůli kterému se žánr hraje.
    /// </summary>
    public double MultiplierAtLevel(int level) => Math.Pow(1.0 + Magnitude, level);

    /// <summary>Lokalizační klíč jména upgradu.</summary>
    public string NameKey => $"prestige.{Id}";

    /// <summary>Lokalizační klíč popisu upgradu.</summary>
    public string DescriptionKey => $"prestige.{Id}.desc";
}
