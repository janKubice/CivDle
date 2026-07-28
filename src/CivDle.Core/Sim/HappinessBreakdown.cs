namespace CivDle.Core.Sim;

/// <summary>
/// Rozpad spokojenosti na položky — proč je zrovna 62 %.
///
/// <para>Proč to existuje: spokojenost byla jedno číslo, které samo od sebe
/// kleslo, a hráč neměl jak zjistit proč. Přitom je to jediná vrstva, kde se dá
/// udělat chyba — bez vysvětlení je to nespravedlivé. Tohle je totéž číslo,
/// jen rozepsané na sčítance.</para>
///
/// <para>Struktura, ne třída: skládá se často (UI si o ni řekne každý snímek)
/// a je malá.</para>
/// </summary>
/// <param name="Base">Základ, se kterým město začíná.</param>
/// <param name="Services">Kolik přidaly služby (trhy, lázně…) podle pokrytí.</param>
/// <param name="Crowding">Kolik ubralo přelidnění (záporné číslo).</param>
/// <param name="Government">Kolik přidal nebo ubral zvolený program města.</param>
/// <param name="ServiceCoverage">Jaká část poptávky po službách je pokrytá (0–1).</param>
/// <param name="Pollution">Kolik ubral kouř nad městem (záporné číslo).</param>
public readonly record struct HappinessBreakdown(
    double Base,
    double Services,
    double Crowding,
    double Government,
    double ServiceCoverage,
    double Pollution = 0)
{
    /// <summary>Výsledná spokojenost 0–1 — součet položek oříznutý do rozsahu.</summary>
    public double Total => Math.Clamp(Base + Services + Crowding + Government + Pollution, 0.0, 1.0);

    /// <summary>Spokojenost je vypnutá (starší data) — všechno je v pořádku.</summary>
    public static HappinessBreakdown Perfect { get; } = new(1.0, 0, 0, 0, 1.0);
}
