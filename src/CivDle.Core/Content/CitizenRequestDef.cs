using CivDle.Core.Sim;

namespace CivDle.Core.Content;

/// <summary>
/// Přání jednoho obyvatele z <c>data/citizens.json</c>: někdo konkrétní si chce
/// otevřít živnost a potřebuje k tomu materiál.
///
/// <para>Proč to ve hře je: po mapě už chodili lidé, ale byly to anonymní tečky
/// a populace bylo jedno velké číslo. Přání dává tomu číslu <b>jméno</b> — a když
/// hráč pomůže, zůstane po tom na mapě budova, kterou někdo založil. V idle hře,
/// kde se vracíš koukat, jak to roste, je rozdíl mezi „město" a „Markovým městem"
/// zásadní.</para>
///
/// <para>Je to nekonečný generátor mikroobsahu z dat, která už ve hře jsou:
/// jména se skládají ze dvou seznamů, budova i cena jsou odkazy na existující
/// obsah. Deset šablon a pár desítek jmen dá stovky různých proseb.</para>
/// </summary>
/// <param name="Id">Stabilní ID (do lokalizace).</param>
/// <param name="BuildingIndex">Kterou budovu si obyvatel chce otevřít.</param>
/// <param name="Cost">Co na to potřebuje od města.</param>
/// <param name="DurationSeconds">Jak dlouho čeká, než to vzdá.</param>
/// <param name="RequirementOrNull">Od jaké fáze hry smí prosba přijít; <c>null</c> = odjakživa.</param>
public sealed record CitizenRequestDef(
    string Id,
    int BuildingIndex,
    IReadOnlyList<ResourceAmount> Cost,
    double DurationSeconds,
    GoalCondition? RequirementOrNull = null)
{
    /// <summary>Lokalizační klíč prosby (text obsahuje jméno a budovu).</summary>
    public string TextKey => $"citizen.request.{Id}";

    /// <summary>Kolik tiků prosba vydrží.</summary>
    public int DurationTicks => (int)Math.Round(DurationSeconds * Simulation.TicksPerSecond);

    /// <summary>Podmínka, od které se prosba smí objevit (nebo <c>null</c>).</summary>
    public GoalCondition? Requirement => RequirementOrNull;
}

/// <summary>
/// Obyvatelé jako obsah: seznamy jmen a příjmení plus šablony proseb.
/// Prázdný katalog je legitimní stav (hra bez pojmenovaných obyvatel).
/// </summary>
/// <param name="FirstNames">Křestní jména.</param>
/// <param name="Surnames">Příjmení.</param>
/// <param name="Requests">Šablony proseb.</param>
/// <param name="GapSeconds">Nejkratší rozestup mezi dvěma prosbami.</param>
public sealed record CitizenCatalog(
    IReadOnlyList<string> FirstNames,
    IReadOnlyList<string> Surnames,
    DefRegistry<CitizenRequestDef> Requests,
    double GapSeconds)
{
    /// <summary>Žádní pojmenovaní obyvatelé — pro starší data i pro testy.</summary>
    public static CitizenCatalog Empty { get; } = new(
        Array.Empty<string>(),
        Array.Empty<string>(),
        new DefRegistry<CitizenRequestDef>(
            Array.Empty<CitizenRequestDef>(), r => r.Id, "prosba obyvatele", allowEmpty: true),
        0);

    /// <summary>Má smysl obyvatele vůbec oslovovat?</summary>
    public bool IsEnabled => Requests.Count > 0 && FirstNames.Count > 0 && Surnames.Count > 0;

    /// <summary>Nejkratší rozestup proseb v ticích.</summary>
    public int GapTicks => (int)Math.Round(GapSeconds * Simulation.TicksPerSecond);

    /// <summary>Složí jméno z indexů. Dvě čísla v savu místo řetězce.</summary>
    public string NameOf(int firstIndex, int surnameIndex)
    {
        if (firstIndex < 0 || firstIndex >= FirstNames.Count
            || surnameIndex < 0 || surnameIndex >= Surnames.Count)
        {
            return string.Empty;
        }

        return $"{FirstNames[firstIndex]} {Surnames[surnameIndex]}";
    }
}
