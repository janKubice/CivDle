namespace CivDle.Core.Content;

/// <summary>
/// Jedna modlitba z <c>data/faith.json</c>.
///
/// <para>Modlitba není kouzlo se zaručeným účinkem: hráč si volí <b>sílu</b>,
/// a čím víc chce, tím dráž to je a tím menší je šance, že to vyjde. To je celá
/// mechanika — rozhodnutí mezi jistou drobností a drahým hazardem. Bez rizika by
/// z víry byl jen další zdroj s tlačítkem „vyplatit".</para>
///
/// <para>Účinek je behavior-ID (data = co, kód = jak), stejně jako u technologií.
/// Neznámý účinek se tiše ignoruje — data smí předběhnout kód.</para>
/// </summary>
/// <param name="Id">Stabilní ID (lokalizace pod <c>prayer.&lt;Id&gt;</c>).</param>
/// <param name="Effect">Behavior-ID účinku.</param>
/// <param name="BaseCost">Cena ve víře při síle 1.</param>
/// <param name="BaseChance">Šance na vyslyšení při síle 1 (0–1).</param>
/// <param name="ChanceFalloff">O kolik klesne šance s každým dalším stupněm síly.</param>
/// <param name="Magnitude">Síla účinku při stupni 1 (význam podle účinku).</param>
/// <param name="RadiusTiles">Dosah kolem cíle v dlaždicích (0 = na celé město).</param>
public sealed record PrayerDef(
    string Id,
    string Effect,
    int BaseCost,
    double BaseChance,
    double ChanceFalloff,
    double Magnitude,
    int RadiusTiles)
{
    /// <summary>Lokalizační klíč jména.</summary>
    public string NameKey => $"prayer.{Id}";

    /// <summary>Lokalizační klíč popisu.</summary>
    public string DescriptionKey => $"prayer.{Id}.desc";

    /// <summary>Míří modlitba na místo na mapě (jinak platí pro celé město)?</summary>
    public bool NeedsTarget => RadiusTiles > 0;

    /// <summary>
    /// Cena při dané síle. Roste rychleji než lineárně, aby velká modlitba byla
    /// opravdu velká investice, ne jen několikrát ta malá.
    /// </summary>
    public int CostAt(int strength) => (int)Math.Round(BaseCost * Math.Pow(strength, 1.6));

    /// <summary>
    /// Šance na vyslyšení při dané síle. Nikdy neklesne pod 5 % — i beznadějná
    /// modlitba musí mít jiskřičku, jinak by to bylo jen zahození surovin.
    /// </summary>
    public double ChanceAt(int strength) =>
        Math.Clamp(BaseChance - ChanceFalloff * (strength - 1), 0.05, 1.0);

    /// <summary>Síla účinku při daném stupni.</summary>
    public double MagnitudeAt(int strength) => Magnitude * strength;
}

/// <summary>
/// Katalog víry: modlitby a surovina, kterou se platí. Prázdný katalog = víra
/// je v datech vypnutá a hra o ní nikde nemluví.
/// </summary>
public sealed class FaithCatalog
{
    public FaithCatalog(int faithResourceIndex, DefRegistry<PrayerDef> prayers)
    {
        FaithResourceIndex = faithResourceIndex;
        Prayers = prayers;
    }

    /// <summary>Surovina, kterou se modlitby platí; −1 = víra vypnutá.</summary>
    public int FaithResourceIndex { get; }

    /// <summary>Dostupné modlitby.</summary>
    public DefRegistry<PrayerDef> Prayers { get; }

    /// <summary>Je víra v datech zapnutá?</summary>
    public bool IsEnabled => FaithResourceIndex >= 0 && Prayers.Count > 0;

    /// <summary>Vypnutá víra — pro testy a data bez faith.json.</summary>
    public static FaithCatalog Empty { get; } =
        new(-1, new DefRegistry<PrayerDef>(Array.Empty<PrayerDef>(), p => p.Id, "modlitba", allowEmpty: true));
}
