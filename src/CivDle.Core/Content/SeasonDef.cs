namespace CivDle.Core.Content;

/// <summary>
/// Jedno roční období z <c>data/seasons.json</c>.
///
/// <para>Proč to v hře je: idle hra, kde je každá minuta stejná jako předchozí,
/// se po hodině přestane dívat. Období dává čtyřtaktní rytmus — na jaře se roste,
/// v létě sklízí, na podzim sbírá do zásoby, v zimě přežívá z toho, co se
/// nastřádalo. Hráč najednou musí myslet dopředu, ne jen víc stavět.</para>
///
/// <para>Data = co (kolik ubere zima jídlu, kolik přidá podzim sběru), kód = jak
/// (kdy období nastane, jak se násobiče použijí). Žádná logika v JSON.</para>
/// </summary>
/// <param name="Id">Stabilní ID; jméno pro hráče je v jazycích pod <c>season.&lt;Id&gt;</c>.</param>
/// <param name="TintColor">Nádech scény během období (vizuál, ne mechanika).</param>
/// <param name="TintAlpha">Síla nádechu 0–1. 0 = období scénu nebarví.</param>
/// <param name="FoodProductionMult">Násobič výroby jídla (zima podvazuje pole).</param>
/// <param name="HarvestMult">Násobič ručního sběru (podzim je čas sbírat).</param>
/// <param name="GrowthMult">Násobič růstu populace (jaro).</param>
/// <param name="FuelPerPersonPerSecond">Kolik paliva spotřebuje jeden obyvatel na topení.</param>
/// <param name="ColdGrowthMult">Násobič růstu, když palivo na topení došlo (zima bez dřeva).</param>
public sealed record SeasonDef(
    string Id,
    RgbColor TintColor,
    double TintAlpha,
    double FoodProductionMult,
    double HarvestMult,
    double GrowthMult,
    double FuelPerPersonPerSecond,
    double ColdGrowthMult)
{
    /// <summary>Lokalizační klíč jména období.</summary>
    public string NameKey => $"season.{Id}";

    /// <summary>Lokalizační klíč krátkého popisu („pole nesou míň, topí se").</summary>
    public string DescriptionKey => $"season.{Id}.desc";

    /// <summary>Topí se v tomhle období?</summary>
    public bool NeedsHeating => FuelPerPersonPerSecond > 0;
}

/// <summary>
/// Kalendář z <c>data/seasons.json</c>: pořadí období a jak dlouho každé trvá.
///
/// <para>Období je čistá funkce čísla dne — nic se neukládá a nic se nemůže
/// rozejít se savem, stejně jako u počasí.</para>
/// </summary>
/// <param name="Seasons">Období v pořadí, v jakém se střídají.</param>
/// <param name="DaysPerSeason">Kolik herních dní trvá jedno období.</param>
/// <param name="FuelResourceIndex">Čím se topí; −1 = topení vypnuté.</param>
public sealed record SeasonCalendar(
    IReadOnlyList<SeasonDef> Seasons,
    int DaysPerSeason,
    int FuelResourceIndex)
{
    /// <summary>Hra bez ročních období (výchozí pro starší data).</summary>
    public static SeasonCalendar Disabled { get; } = new(Array.Empty<SeasonDef>(), 0, -1);

    /// <summary>Má smysl období vůbec počítat?</summary>
    public bool IsEnabled => Seasons.Count > 0 && DaysPerSeason > 0;

    /// <summary>Kolik herních dní trvá celý rok.</summary>
    public int DaysPerYear => Seasons.Count * DaysPerSeason;

    /// <summary>
    /// Které období panuje daný den (první den hry = 1). Modulo přes celý rok —
    /// žádný stav, žádné ukládání.
    /// </summary>
    public int IndexForDay(long dayNumber)
    {
        if (!IsEnabled)
        {
            return -1;
        }

        long day = Math.Max(0, dayNumber - 1);
        return (int)(day / DaysPerSeason % Seasons.Count);
    }
}
