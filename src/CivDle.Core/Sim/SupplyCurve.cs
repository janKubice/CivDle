using CivDle.Core.Content;

namespace CivDle.Core.Sim;

/// <summary>
/// Jak moc zdroj dodává během dne (<see cref="SupplyTime"/>): sluneční zrcadla
/// podle výšky slunce, lapač rosy jen v noci.
///
/// <para><b>Po schodech, ne plynule.</b> Síť se přepočítává od základu a jen po
/// změně — plynulá křivka by znamenala přepočet každý tik. Osm schodů na
/// polovinu dne stačí, aby poledne bylo znát, a přepočet přijde šestnáctkrát
/// za den.</para>
///
/// <para>Čistá funkce denního času — deterministická, nic se neukládá.</para>
/// </summary>
public static class SupplyCurve
{
    /// <summary>Kolik schodů má výška slunce.</summary>
    public const int Steps = 8;

    /// <summary>Výška slunce 0–1 (0 = pod obzorem, 1 = poledne). Východ 0,25, západ 0,75.</summary>
    public static double Sun(double timeOfDay) => Math.Max(0, Math.Sin(2 * Math.PI * (timeOfDay - 0.25)));

    /// <summary>Je noc (slunce pod obzorem)?</summary>
    public static bool IsNight(double timeOfDay) => Sun(timeOfDay) <= 0;

    /// <summary>
    /// Násobič dodávky v daném čase; <paramref name="dayDim"/> ubere slunci
    /// (písečná bouře zakryje zrcadla).
    /// </summary>
    public static double Factor(SupplyTime time, double timeOfDay, double dayDim = 1.0, bool storm = false) => time switch
    {
        SupplyTime.Day => Math.Round(Sun(timeOfDay) * Steps) / Steps * dayDim,
        SupplyTime.Night => IsNight(timeOfDay) ? 1.0 : 0.0,
        SupplyTime.Storm => storm ? 1.0 : 0.0,
        _ => 1.0,
    };

    /// <summary>
    /// Jakou část času jde přes město bouřkový pás — průměr hromosvodu pro
    /// guvernéra. Hrubě: pás přejde za minutu a přichází po čtyřech až pěti.
    /// </summary>
    public const double StormShare = 0.2;

    /// <summary>
    /// Průměr za celý den — podle něj plánuje guvernér. Kdyby koukal na
    /// okamžik, stavěl by v noci zrcadlo za zrcadlem, protože „proud chybí".
    /// </summary>
    public static double Average(SupplyTime time) => time switch
    {
        SupplyTime.Day => 1.0 / Math.PI, // průměr kladné půlvlny sinu přes celý den
        SupplyTime.Night => 0.5,
        SupplyTime.Storm => StormShare,
        _ => 1.0,
    };

    /// <summary>
    /// Číslo schodu — když se změní, síť se přepočítá. Noc je jeden schod,
    /// den osm; ztlumení a bouřkový pás se přičtou, aby změna počasí přepočet
    /// vyvolala.
    /// </summary>
    public static int Phase(double timeOfDay, bool dimmed, bool storm = false) =>
        (IsNight(timeOfDay) ? -1 : (int)Math.Round(Sun(timeOfDay) * Steps)) + (dimmed ? 100 : 0) + (storm ? 1000 : 0);
}

/// <summary>
/// Vylepšení sítí světa ze Vzestupu (úsporné zavlažování, hlubší studny).
/// Proud se jimi neřídí — ten má svá vylepšení Domoviny.
/// </summary>
/// <param name="SupplyMult">Násobič výkonu zdrojů.</param>
/// <param name="DemandMult">Dělitel poptávky.</param>
/// <param name="RangeBonus">O kolik buněk dál síť dosáhne.</param>
public readonly record struct NetworkBoost(double SupplyMult, double DemandMult, int RangeBonus)
{
    /// <summary>Bez vylepšení.</summary>
    public static NetworkBoost None => new(1.0, 1.0, 0);
}

/// <summary>
/// Světlo, za kterého se síť počítá: denní čas, nebo celodenní průměr (pohled
/// guvernéra), a ztlumení slunce bouří.
/// </summary>
/// <param name="TimeOfDay">Denní čas 0–1.</param>
/// <param name="Steady">Počítat s průměrem dne, ne s okamžikem.</param>
/// <param name="DayDim">Kolik slunce projde (1 = jasno).</param>
/// <param name="Storm">Jde přes město bouřkový pás (hromosvody dodávají)?</param>
public readonly record struct NetworkLight(double TimeOfDay, bool Steady, double DayDim = 1.0, bool Storm = false)
{
    /// <summary>Světlo, na kterém nezáleží (obsah bez časovaných zdrojů).</summary>
    public static NetworkLight Noon => new(0.5, false);

    /// <summary>
    /// Násobič dodávky zdroje s daným časováním. Průměr dne bouři nezná —
    /// přejde, a guvernér nemá stavět kvůli počasí.
    /// </summary>
    public double FactorFor(SupplyTime time) =>
        Steady ? SupplyCurve.Average(time) : SupplyCurve.Factor(time, TimeOfDay, DayDim, Storm);
}
